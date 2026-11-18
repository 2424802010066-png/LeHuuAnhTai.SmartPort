"""
DIEN TAP SU CO tren he thong SmartPort DANG CHAY (chi may nay: localhost + SQL Server .\\MSSQLSERVER01, CSDL SmartPortMS_*).
Chung minh 3 dieu thay hoi: "mat mang / mat thanh phan thi he thong con chay khong, co mat du lieu khong".

  A. RabbitMQ chet  -> dich vu "suy-giam" (HTTP 200), cong VAN ghi luot xe, su kien nam cho trong Outbox;
                       bat lai RabbitMQ -> Outbox tu gui bu, ban sao o dich vu Bai nhan duoc luot xe (khong mat su kien).
  B. Dich vu Cong chet -> gateway tra JSON tieng Viet (khong trang trang); giam_sat.ps1 tu chay lai trong ~1 phut.
  C. Bam lai khi Wi-Fi chap chon -> cung Idempotency-Key gui 2 lan chi ra 1 luot xe.
  D. RabbitMQ chet khi giam_sat.ps1 dang chay -> giam sat tu "docker start", dich vu ve lai "tot".

Dung:  python MicroServices/dien_tap_su_co.py [--bo-qua-rabbit] [--bo-qua-giam-sat] [--chi A,B,C,D]
Ma thoat: 0 = dat het, 1 = co buoc LOI, 2 = he thong chua chay / thieu tai khoan kiem thu.
KHONG in mat khau; tai khoan lay tu MicroServices/.bimat/tai-khoan-kiem-thu.json (bo kiem thu tao).
"""
import json
import os
import random
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

try:
    import pyodbc
except ImportError:
    print("Thieu pyodbc: pip install pyodbc")
    sys.exit(2)

GOC = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.dirname(GOC)
GATEWAY = "http://127.0.0.1:5100"
SQL = r".\MSSQLSERVER01"
CONTAINER = "smartport-rabbitmq"

dat, loi = 0, 0


def kiem(ten, ok, chi_tiet=""):
    global dat, loi
    if ok:
        dat += 1
        print(f"  DAT  {ten}")
    else:
        loi += 1
        print(f"  LOI  {ten}  -> {chi_tiet}")
    return ok


def goi(phuong_thuc, url, than=None, token=None, khoa=None, cho=10):
    """Tra (ma HTTP, dict/str). Loi mang -> (0, thong bao)"""
    du_lieu = json.dumps(than).encode("utf-8") if than is not None else None
    yc = urllib.request.Request(url, data=du_lieu, method=phuong_thuc)
    if du_lieu is not None:
        yc.add_header("Content-Type", "application/json")
    if token:
        yc.add_header("Authorization", "Bearer " + token)
    if khoa:
        yc.add_header("Idempotency-Key", khoa)
    try:
        with urllib.request.urlopen(yc, timeout=cho) as r:
            noi_dung = r.read().decode("utf-8", "replace")
            ma = r.status
    except urllib.error.HTTPError as e:
        noi_dung = e.read().decode("utf-8", "replace")
        ma = e.code
    except Exception as e:  # tu choi ket noi, het gio
        return 0, str(e)
    try:
        return ma, json.loads(noi_dung)
    except ValueError:
        return ma, noi_dung


def cho_den(dieu_kien, toi_da_giay, buoc=2.0):
    """Lap toi khi dieu_kien() tra gia tri truthy hoac het gio. Tra (gia tri, so giay da cho)"""
    bat_dau = time.time()
    while True:
        try:
            v = dieu_kien()
        except Exception:
            v = None
        if v:
            return v, time.time() - bat_dau
        if time.time() - bat_dau > toi_da_giay:
            return None, time.time() - bat_dau
        time.sleep(buoc)


def _driver_odbc():
    """ODBC Driver 17 hoac 18 (may moi cai SQL Server 2022 thuong chi co 18). Driver 18 mac dinh ma hoa -> tin chung chi
    cuc bo (chi noi may chu SQL trong may / mang noi bo)."""
    co = set(pyodbc.drivers())
    for ten in ("ODBC Driver 17 for SQL Server", "ODBC Driver 18 for SQL Server"):
        if ten in co:
            return "DRIVER={%s};" % ten + ("TrustServerCertificate=yes;" if "18" in ten else "")
    raise SystemExit("Thieu ODBC Driver 17/18 for SQL Server: cai tu https://learn.microsoft.com/sql/connect/odbc/download-odbc-driver-for-sql-server")


DRIVER_ODBC = _driver_odbc()


def doc_so(csdl, sql, *tham_so):
    cn = pyodbc.connect(DRIVER_ODBC + f"SERVER={SQL};DATABASE={csdl};Trusted_Connection=yes;", timeout=5)
    try:
        hang = cn.cursor().execute(sql, *tham_so).fetchone()
        return hang[0] if hang else None
    finally:
        cn.close()


def suc_khoe_dich_vu(cong):
    ma, kq = goi("GET", f"http://127.0.0.1:{cong}/suc-khoe", cho=4)
    return ma, (kq.get("tinhTrang") if isinstance(kq, dict) else None)


def docker(*doi_so, cho=120):
    r = subprocess.run(["docker", *doi_so], capture_output=True, text=True, timeout=cho)
    return r.returncode, (r.stdout + r.stderr).strip()


def dang_nhap():
    tep = os.path.join(GOC, ".bimat", "tai-khoan-kiem-thu.json")
    if not os.path.exists(tep):
        print("Chua co tai khoan kiem thu - chay 'SMARTPORT.bat kiem-thu' (nhom microservices) truoc.")
        sys.exit(2)
    with open(tep, encoding="utf-8") as f:
        tk = json.load(f)
    than = {"tenDangNhap": tk.get("tenDangNhap", "kiemthu.ms"), "matKhau": tk.get("matKhau")}
    for lan in range(2):
        ma, kq = goi("POST", GATEWAY + "/api/auth/dang-nhap", than)
        if ma == 429 and lan == 0:
            print("  (dang nhap bi gioi han toc do - cho 61 giay)")
            time.sleep(61)
            continue
        break
    token = kq.get("token") if isinstance(kq, dict) else None
    kiem("Dang nhap qua gateway bang tai khoan kiem thu", token is not None, f"HTTP {ma}")
    return token


def bien_so():
    return "51C" + "".join(random.choice("0123456789") for _ in range(5))


def ghi_luot_xe(token, khoa, bien):
    return goi("POST", GATEWAY + "/api/cong/luot-xe",
               {"bienSo": bien, "huongDi": "Vào", "cong": "A1", "loaiXe": "Xe tải thùng", "nguon": "DienTap"},
               token=token, khoa=khoa, cho=20)


# ------------------------------------------------------------------------------------------------ A. RabbitMQ
def dien_tap_rabbit(token):
    print("\n== A. RabbitMQ CHET giua ca lam viec ==")
    ma, ra = docker("stop", CONTAINER)
    if not kiem("Tat container RabbitMQ (docker stop)", ma == 0, ra):
        return
    try:
        tt, giay = cho_den(lambda: suc_khoe_dich_vu(5105)[1] == "suy-giam", 60)
        ma_sk, _ = suc_khoe_dich_vu(5105)
        kiem(f"Dich vu Cong chuyen 'suy-giam' (HTTP {ma_sk}, khong phai 503) sau {giay:.0f} s", tt and ma_sk == 200, f"HTTP {ma_sk}")
        ma_gw, kq_gw = goi("GET", GATEWAY + "/suc-khoe")
        kiem("Gateway van tra loi /suc-khoe (200) khi mat RabbitMQ", ma_gw == 200, f"HTTP {ma_gw}")

        bien = bien_so()
        ma, kq = ghi_luot_xe(token, str(uuid.uuid4()), bien)
        ma_xv = kq.get("maXV") if isinstance(kq, dict) else None
        if not kiem(f"Cong VAN ghi luot xe {bien} khi mat RabbitMQ (HTTP {ma})", ma in (200, 201) and ma_xv, str(kq)[:200]):
            return
        cho_gui = doc_so("SmartPortMS_Cong",
                         "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE Loai = 'cong.xe-da-qua-cong' AND MaTuongQuan = ? AND TrangThai = 0",
                         f"xe:{ma_xv}")
        kiem("Su kien cong.xe-da-qua-cong NAM CHO trong Outbox (khong mat, khong loi)", cho_gui == 1, f"so dong cho = {cho_gui}")
        ban_sao = doc_so("SmartPortMS_Bai", "SELECT COUNT(*) FROM dbo.XeRaVao WHERE MaXV = ?", ma_xv)
        kiem("Ban sao o dich vu Bai CHUA co luot xe (dung: chua co bus)", ban_sao == 0, f"so dong = {ban_sao}")
    finally:
        ma, ra = docker("start", CONTAINER)
        kiem("Bat lai RabbitMQ (docker start)", ma == 0, ra)

    _, giay = cho_den(lambda: docker("inspect", "-f", "{{.State.Health.Status}}", CONTAINER, cho=10)[1] == "healthy", 120, 3)
    print(f"  (RabbitMQ healthy sau {giay:.0f} s)")
    da_gui, giay = cho_den(lambda: doc_so("SmartPortMS_Cong",
                                           "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE Loai = 'cong.xe-da-qua-cong' AND MaTuongQuan = ? AND TrangThai = 1",
                                           f"xe:{ma_xv}") == 1, 120)
    kiem(f"Outbox tu GUI BU su kien sau khi RabbitMQ song lai ({giay:.0f} s)", da_gui, "van nam cho sau 120 s")
    co, giay = cho_den(lambda: doc_so("SmartPortMS_Bai", "SELECT COUNT(*) FROM dbo.XeRaVao WHERE MaXV = ?", ma_xv) == 1, 60)
    kiem(f"Dich vu Bai nhan duoc luot xe qua ban sao ({giay:.0f} s) -> khong mat du lieu", co, "chua thay sau 60 s")
    tot, giay = cho_den(lambda: suc_khoe_dich_vu(5105)[1] == "tốt", 60)
    kiem(f"Dich vu Cong ve lai 'tốt' ({giay:.0f} s)", tot, suc_khoe_dich_vu(5105))


# ------------------------------------------------------------------------------------------------ B. dich vu chet
def pid_dich_vu(ten):
    try:
        with open(os.path.join(GOC, "pid", ten + ".pid"), encoding="utf-8") as f:
            return int(f.read().strip())
    except (OSError, ValueError):
        return None


def giam_sat_dang_chay():
    tep = os.path.join(GOC, "pid", "giam-sat.khoa")
    if not os.path.exists(tep):
        return False
    try:
        with open(tep, encoding="utf-8") as f:
            pid = int(f.read().strip())
    except (OSError, ValueError):
        return False
    r = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"], capture_output=True, text=True)
    return "pwsh" in r.stdout.lower()


def dien_tap_dich_vu_chet(token):
    print("\n== B. Dich vu Cong CHET dot ngot (giam sat tu chay lai) ==")
    tu_bat = False
    if not giam_sat_dang_chay():
        subprocess.Popen(["pwsh", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", os.path.join(GOC, "giam_sat.ps1")],
                         # cua so console rieng nhu "SMARTPORT.bat giam-sat" (pwsh khong co console thi thoat ngay)
                         creationflags=getattr(subprocess, "CREATE_NEW_CONSOLE", 0))
        tu_bat = True
        ok, _ = cho_den(giam_sat_dang_chay, 30, 1)
        if not kiem("Bat giam_sat.ps1 (chay nen)", ok, "khong thay tep pid/giam-sat.khoa"):
            return
    pid_cu = pid_dich_vu("cong")
    if not kiem("Tim thay tien trinh dich vu Cong", pid_cu is not None, "khong co pid/cong.pid"):
        return
    subprocess.run(["taskkill", "/PID", str(pid_cu), "/T", "/F"], capture_output=True)
    _, giay = cho_den(lambda: suc_khoe_dich_vu(5105)[0] == 0, 15, 0.5)

    ma, kq = goi("GET", GATEWAY + "/api/cong/goi-ngoai-tuyen?cong=A1", token=token)
    kiem(f"Gateway tra JSON tieng Viet khi Cong chet (HTTP {ma}, khong trang trang)",
         ma in (502, 503, 504) and isinstance(kq, dict) and kq.get("thongBao") and kq.get("nguon") == "gateway",
         str(kq)[:200])
    ma_kh, _ = goi("GET", GATEWAY + "/api/auth/toi", token=token)
    kiem(f"Dich vu khac (Dinh danh) van phuc vu khi Cong chet (HTTP {ma_kh})", ma_kh in (200, 404), f"HTTP {ma_kh}")

    tot, giay = cho_den(lambda: suc_khoe_dich_vu(5105)[1] == "tốt", 150, 3)
    pid_moi = pid_dich_vu("cong")
    kiem(f"giam_sat.ps1 tu chay lai Cong sau {giay:.0f} s (pid {pid_cu} -> {pid_moi})", tot and pid_moi and pid_moi != pid_cu,
         "chua len sau 150 s - xem MicroServices/nhat-ky/giam-sat.log")
    if tot:
        ma, kq = goi("GET", GATEWAY + "/api/cong/goi-ngoai-tuyen?cong=A1", token=token)
        kiem(f"Gateway tu dua Cong tro lai tuyen (HTTP {ma})", ma == 200, str(kq)[:160])
    if tu_bat:
        print("  (giam_sat.ps1 van chay nen de bao ve he thong; 'SMARTPORT.bat tat' se tat no truoc)")


# ------------------------------------------------------------------------------------------------ D. giam sat bat lai RabbitMQ
def dien_tap_giam_sat_rabbit():
    print("\n== D. RabbitMQ chet khi GIAM SAT dang chay -> tu bat lai, khong can nguoi ==")
    if not giam_sat_dang_chay():
        kiem("giam_sat.ps1 dang chay (can buoc B truoc)", False, "khong thay giam sat")
        return
    tep_log = os.path.join(GOC, "nhat-ky", "giam-sat.log")
    vi_tri = os.path.getsize(tep_log) if os.path.exists(tep_log) else 0
    ma, ra = docker("stop", CONTAINER)
    if not kiem("Tat container RabbitMQ (docker stop)", ma == 0, ra):
        return
    song, giay = cho_den(lambda: docker("inspect", "-f", "{{.State.Status}}|{{.State.Health.Status}}", CONTAINER, cho=10)[1] == "running|healthy", 150, 3)
    moi = ""
    if os.path.exists(tep_log):
        with open(tep_log, "rb") as f:
            f.seek(vi_tri)
            moi = f.read().decode("utf-8", "replace")
    kiem("Giam sat ghi nhan va tu chay 'docker start smartport-rabbitmq'", "docker start smartport-rabbitmq" in moi, moi[-300:])
    kiem(f"RabbitMQ tu song lai (healthy) sau {giay:.0f} s", song, docker("inspect", "-f", "{{.State.Status}}", CONTAINER, cho=10)[1])
    tot, giay = cho_den(lambda: suc_khoe_dich_vu(5105)[1] == "tốt", 60)
    kiem(f"Dich vu Cong ve lai 'tốt' ({giay:.0f} s)", tot, suc_khoe_dich_vu(5105))
    if not song:
        docker("start", CONTAINER)


# ------------------------------------------------------------------------------------------------ C. bam lai
def dien_tap_bam_lai(token):
    print("\n== C. Bao ve bam 'Ghi' 2 lan khi Wi-Fi chap chon ==")
    khoa, bien = str(uuid.uuid4()), bien_so()
    ma1, kq1 = ghi_luot_xe(token, khoa, bien)
    ma2, kq2 = ghi_luot_xe(token, khoa, bien)
    xv1 = kq1.get("maXV") if isinstance(kq1, dict) else None
    xv2 = kq2.get("maXV") if isinstance(kq2, dict) else None
    kiem(f"Lan 1 ghi luot (HTTP {ma1}), lan 2 tra lai dung ket qua cu (HTTP {ma2}, cung maXV {xv1})",
         ma1 == 201 and ma2 == 200 and xv1 and xv1 == xv2, f"{ma1}/{ma2} {xv1}/{xv2}")
    so = doc_so("SmartPortMS_Cong", "SELECT COUNT(*) FROM dbo.XeRaVao WHERE BienSo = ? AND ThoiGian >= DATEADD(MINUTE, -5, GETDATE())", bien)
    kiem("CSDL Cong chi co 1 luot xe cho bien so nay", so == 1, f"so luot = {so}")


def main():
    print("DIEN TAP SU CO - SmartPort (he thong dang chay tren may nay)")
    ma, kq = goi("GET", GATEWAY + "/suc-khoe", cho=5)
    if ma != 200:
        print(f"He thong chua chay (gateway HTTP {ma}) - chay 'SMARTPORT.bat chay' truoc.")
        sys.exit(2)
    token = dang_nhap()
    if not token:
        sys.exit(2)
    # --chi A,D : chi chay cac phan nay (mac dinh C, A, B, D theo thu tu)
    chi = None
    if "--chi" in sys.argv and sys.argv.index("--chi") + 1 < len(sys.argv):
        chi = {x.strip().upper() for x in sys.argv[sys.argv.index("--chi") + 1].split(",")}
    co = lambda phan: chi is None or phan in chi
    bo_rabbit, bo_giam_sat = "--bo-qua-rabbit" in sys.argv, "--bo-qua-giam-sat" in sys.argv
    if co("C"):
        dien_tap_bam_lai(token)
    if co("A") and not bo_rabbit:
        dien_tap_rabbit(token)
    if co("B") and not bo_giam_sat:
        dien_tap_dich_vu_chet(token)
    if co("D") and not bo_rabbit and not bo_giam_sat:
        dien_tap_giam_sat_rabbit()
    print(f"\nKET QUA DIEN TAP: {dat}/{dat + loi} DAT")
    sys.exit(0 if loi == 0 else 1)


if __name__ == "__main__":
    main()
