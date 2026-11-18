# -*- coding: utf-8 -*-
"""
DUNG / CAP NHAT CSDL RIENG CHO TUNG DICH VU (database-per-service) tu CSDL nguon (mac dinh SmartPortDB_BaoCao, CHI DOC).
Theo dich-vu.json, moi dich vu co CSDL "SmartPortMS_<Ten>" gom:
  - bang SO HUU   : y nhu nguon (khoa tu tang IDENTITY, khoa chinh, gia tri mac dinh, chi muc UNIQUE) + du lieu
  - bang BAN SAO  : cung cot nhung KHONG tu tang (khoa lay tu dich vu so huu, cap nhat qua su kien RabbitMQ) + du lieu
  - bang CUC BO   : NhatKy, SuKienOutbox + SuKienDaXuLy (hop thu di / da xu ly), YeuCauDaXuLy (chong gui lap),
                    BanSaoPhienBan (chong ban sao cu) + bangCucBoRieng cua tung dich vu (vd cong: LuotXeNgoaiTuyen) — rong
KHONG tao khoa ngoai giua cac bang: khoa ngoai cheo dich vu khong ton tai trong microservices (toan ven "cuoi cung
nhat quan" qua su kien). Chi dung / xoa CSDL ten bat dau "SmartPortMS_" — khong dung toi CSDL khac.

HET "PHUC HOI GIA": truoc day thieu CSDL nao la AM THAM dung lai tu du lieu mau -> he thong len xanh nhung du lieu la cua
ngay seed. Nay:
  - CHUA co CSDL SmartPortMS_* nao (may moi)      -> dung lan dau (thong bao ro). Neu may DA TUNG sao luu -> DUNG.
  - Co mot phan, THIEU mot so                    -> DUNG, ma thoat 2, bao do: phuc hoi tu ban sao luu hoac --tao-moi.
  - Du CSDL                                      -> chi bo sung bang moi (script Database/Add_*.sql idempotent).

Chay:  python dung_csdl_dich_vu.py                 (nhu tren)
       python dung_csdl_dich_vu.py --tao-moi       (dung MOI tu du lieu mau cac CSDL dang thieu — mat du lieu cu cua chung)
       python dung_csdl_dich_vu.py --lam-lai       (xoa + tao lai TOAN BO CSDL SmartPortMS_* tu du lieu mau)
       python dung_csdl_dich_vu.py --chi cong bai  (chi cac dich vu nay)
Ma thoat: 0 = xong, 2 = thieu CSDL (dung co chu dich, khong sua gi), khac = loi.
"""
import argparse
import json
import os
import re
import sys
import time

import pyodbc

GOC = os.path.dirname(os.path.abspath(__file__))
TIEN_TO = "SmartPortMS_"
MAY_CHU = r".\MSSQLSERVER01"
MA_THIEU_CSDL = 2
# Bang cuc bo co tu truoc (lay luoc do tu CSDL nguon); bang cuc bo MOI (theo hop dong) tao bang script Add_<Bang>.sql
BANG_CUC_BO_CU = {"NhatKy", "SuKienOutbox", "SuKienDaXuLy"}

DO, VANG, XANH, HET = "\033[91m", "\033[93m", "\033[92m", "\033[0m"
_co_mau = False


def bat_mau():
    """Bat mau ANSI tren console Windows (khong co thi in chu tron)"""
    global _co_mau
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass
    if sys.stdout.isatty():
        if os.name == "nt":
            os.system("")          # bat che do VT100 cua console
        _co_mau = True


def _driver_odbc():
    """ODBC Driver 17 hoac 18 (may moi cai SQL Server 2022 thuong chi co 18). Driver 18 mac dinh ma hoa -> tin chung chi
    cuc bo (chi noi may chu SQL trong may / mang noi bo)."""
    co = set(pyodbc.drivers())
    for ten in ("ODBC Driver 17 for SQL Server", "ODBC Driver 18 for SQL Server"):
        if ten in co:
            return "DRIVER={%s};" % ten + ("TrustServerCertificate=yes;" if "18" in ten else "")
    raise SystemExit("Thieu ODBC Driver 17/18 for SQL Server: cai tu https://learn.microsoft.com/sql/connect/odbc/download-odbc-driver-for-sql-server")


DRIVER_ODBC = _driver_odbc()


def in_mau(mau, noi_dung):
    print(mau + noi_dung + HET if _co_mau else noi_dung, flush=True)


def ket_noi(csdl="master"):
    return pyodbc.connect(DRIVER_ODBC + "SERVER=%s;DATABASE=%s;Trusted_Connection=yes;" % (MAY_CHU, csdl),
                          autocommit=True)


def kieu_cot(c):
    t = c.ten_kieu
    if t in ("varchar", "char", "varbinary", "binary"):
        return "%s(%s)" % (t, "max" if c.max_length == -1 else c.max_length)
    if t in ("nvarchar", "nchar"):
        return "%s(%s)" % (t, "max" if c.max_length == -1 else c.max_length // 2)
    if t in ("decimal", "numeric"):
        return "%s(%d, %d)" % (t, c.precision, c.scale)
    if t in ("datetime2", "time", "datetimeoffset"):
        return "%s(%d)" % (t, c.scale)
    return t


def lay_luoc_do(cn_nguon, bang):
    cur = cn_nguon.cursor()
    cot = cur.execute("""
SELECT c.name, t.name AS ten_kieu, c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity,
       CAST(ic.seed_value AS bigint) seed, CAST(ic.increment_value AS bigint) inc, dc.definition AS mac_dinh,
       cc.definition AS cong_thuc, c.column_id
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
WHERE c.object_id = OBJECT_ID(?) ORDER BY c.column_id""", "dbo." + bang).fetchall()
    if not cot:
        raise RuntimeError("CSDL nguon khong co bang dbo.%s" % bang)
    chi_muc = cur.execute("""
SELECT i.name, i.is_primary_key, i.is_unique, i.type_desc, i.filter_definition,
       STRING_AGG(QUOTENAME(col.name) + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END, ', ')
           WITHIN GROUP (ORDER BY ic.key_ordinal) AS cot
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(?) AND (i.is_primary_key = 1 OR i.is_unique = 1 OR i.type_desc = 'NONCLUSTERED')
GROUP BY i.name, i.is_primary_key, i.is_unique, i.type_desc, i.filter_definition""", "dbo." + bang).fetchall()
    return cot, chi_muc


def tao_bang(cn_dich, bang, cot, chi_muc, giu_identity, so_huu):
    dong = []
    for c in cot:
        if c.cong_thuc:
            dong.append("[%s] AS %s" % (c.name, c.cong_thuc))
            continue
        s = "[%s] %s" % (c.name, kieu_cot(c))
        if c.is_identity and giu_identity:
            s += " IDENTITY(%d, %d)" % (c.seed or 1, c.inc or 1)
        s += " NULL" if c.is_nullable else " NOT NULL"
        if c.mac_dinh and so_huu:
            s += " DEFAULT %s" % c.mac_dinh
        dong.append(s)
    pk = [i for i in chi_muc if i.is_primary_key]
    if pk:
        dong.append("CONSTRAINT [PK_%s] PRIMARY KEY (%s)" % (bang, pk[0].cot))
    cn_dich.cursor().execute("CREATE TABLE dbo.[%s] (\n  %s\n)" % (bang, ",\n  ".join(dong)))
    for i in chi_muc:
        if i.is_primary_key:
            continue
        # chi muc UNIQUE giu o bang so huu (rang buoc nghiep vu); ban sao giu chi muc thuong de truy van nhanh
        loai = "UNIQUE NONCLUSTERED" if (i.is_unique and so_huu) else "NONCLUSTERED"
        loc = (" WHERE " + i.filter_definition) if i.filter_definition else ""
        try:
            cn_dich.cursor().execute("CREATE %s INDEX [%s] ON dbo.[%s] (%s)%s" % (loai, i.name, bang, i.cot, loc))
        except pyodbc.Error as e:
            print("    (bo qua chi muc %s: %s)" % (i.name, str(e)[:100]))


def chep_du_lieu(cn_dich, csdl_nguon, bang, cot, giu_identity):
    ten_cot = ", ".join("[%s]" % c.name for c in cot if not c.cong_thuc)
    co_identity = giu_identity and any(c.is_identity for c in cot)
    sql = "INSERT INTO dbo.[%s] (%s) SELECT %s FROM [%s].dbo.[%s]" % (bang, ten_cot, ten_cot, csdl_nguon, bang)
    if co_identity:
        sql = "SET IDENTITY_INSERT dbo.[%s] ON; %s; SET IDENTITY_INSERT dbo.[%s] OFF;" % (bang, sql, bang)
    cur = cn_dich.cursor()
    cur.execute("SET NOCOUNT ON; " + sql)
    while cur.nextset():
        pass
    n = cn_dich.cursor().execute("SELECT COUNT(*) FROM dbo.[%s]" % bang).fetchone()[0]
    if co_identity:
        # khoa tu tang tiep tuc tu gia tri hien tai cua nguon (khong cap trung khoa da phat)
        cur_nguon = cn_dich.cursor().execute("SELECT IDENT_CURRENT('[%s].dbo.[%s]')" % (csdl_nguon, bang)).fetchone()[0]
        if cur_nguon is not None:
            cn_dich.cursor().execute("DBCC CHECKIDENT ('dbo.[%s]', RESEED, %d) WITH NO_INFOMSGS" % (bang, int(cur_nguon)))
    return n


# ------------------------------------------------------------------ script Database/Add_*.sql
def tim_script(bang):
    """Uu tien Database/Add_<Bang>.sql; khong co thi tim script co 'CREATE TABLE dbo.<bang>'"""
    thu_muc = os.path.join(GOC, "..", "Database")
    dung_ten = os.path.join(thu_muc, "Add_%s.sql" % bang)
    if os.path.isfile(dung_ten):
        return dung_ten
    for f in sorted(os.listdir(thu_muc)):
        if not f.startswith("Add_") or not f.endswith(".sql"):
            continue
        noi_dung = open(os.path.join(thu_muc, f), encoding="utf-8-sig", errors="replace").read()
        if ("CREATE TABLE dbo.%s " % bang in noi_dung or "CREATE TABLE dbo.%s(" % bang in noi_dung
                or "CREATE TABLE [dbo].[%s]" % bang in noi_dung):
            return os.path.join(thu_muc, f)
    return None


def bo_chu_thich(noi_dung):
    noi_dung = re.sub(r"/\*.*?\*/", " ", noi_dung, flags=re.S)
    return re.sub(r"--[^\n]*", " ", noi_dung)


def kiem_an_toan(noi_dung, ten):
    """Script cap nhat chay tren CSDL DANG CO DU LIEU: cam xoa CSDL / bang, cam doi CSDL (USE)"""
    sach = bo_chu_thich(noi_dung).upper()
    for mau, ly_do in ((r"\bDROP\s+DATABASE\b", "DROP DATABASE"), (r"\bDROP\s+TABLE\b", "DROP TABLE"),
                       (r"\bTRUNCATE\s+TABLE\b", "TRUNCATE TABLE"), (r"(^|;)\s*USE\s", "USE <CSDL>")):
        if re.search(mau, sach, flags=re.M):
            raise RuntimeError("Script %s co %s - khong chay." % (os.path.basename(ten), ly_do))


def chi_ddl(noi_dung):
    """Script chi tao bang / chi muc (khong INSERT/UPDATE/DELETE/MERGE) -> chay lai moi lan khoi dong van an toan"""
    return not re.search(r"(^|;)\s*(INSERT|UPDATE|DELETE|MERGE)\b", bo_chu_thich(noi_dung), flags=re.I | re.M)


def chay_script(cn_dich, duong_dan):
    noi_dung = open(duong_dan, encoding="utf-8-sig", errors="replace").read()
    kiem_an_toan(noi_dung, duong_dan)
    cur = cn_dich.cursor()
    for lo in [x for x in re.split(r"^\s*GO\s*$", noi_dung, flags=re.M | re.I) if x.strip()]:
        cur.execute(lo)
        while cur.nextset():
            pass


def chay_script_tao_bang(cn_dich, bang):
    """Bang SO HUU / BAN SAO chua co o CSDL nguon: tao bang script (BAT BUOC co script)"""
    sc = tim_script(bang)
    if not sc:
        raise RuntimeError("Khong co script nao tao bang " + bang)
    chay_script(cn_dich, sc)
    print("    (bang %s chua co o CSDL nguon -> tao bang script %s)" % (bang, os.path.basename(sc)))


def co_bang(cn_dich, bang):
    return cn_dich.cursor().execute("SELECT OBJECT_ID(?, 'U')", "dbo." + bang).fetchone()[0] is not None


def dam_bao_bang_cuc_bo(cn_dich, ten_dv, bang):
    """
    Bang cuc bo bang script idempotent: chua co -> chay script; da co va script CHI DDL -> chay lai (bo sung chi muc moi).
    Thieu script / script loi -> canh bao VANG, bo qua: dich vu tu suy giam (vd [ChongGuiLap] chay nhu khong co khoa).
    Tra: "tao" | "cap-nhat" | None (khong lam gi) | "thieu-script" | "loi"
    """
    da_co = co_bang(cn_dich, bang)
    sc = tim_script(bang)
    if not sc:
        if not da_co:
            in_mau(VANG, "    ! %s: chưa có script Database/Add_%s.sql — bỏ qua bảng %s (tính năng liên quan tự tắt)."
                   % (ten_dv, bang, bang))
            return "thieu-script"
        return None
    try:
        if da_co:
            noi_dung = open(sc, encoding="utf-8-sig", errors="replace").read()
            if bang in BANG_CUC_BO_CU or not chi_ddl(noi_dung):
                return None                 # script co ghi du lieu: chi chay khi bang chua co (tranh chen trung)
        chay_script(cn_dich, sc)
        return "cap-nhat" if da_co else "tao"
    except Exception as e:                  # noqa: BLE001 — loi bat ky cua script khong duoc chan khoi dong
        in_mau(VANG, "    ! %s: script %s lỗi — bỏ qua bảng %s: %s" % (ten_dv, os.path.basename(sc), bang, str(e)[:200]))
        return "loi"


def bang_cuc_bo_cua(dm, dv):
    ds = list(dm.get("bangCucBo") or []) + ["SuKienOutbox", "SuKienDaXuLy"] + list(dv.get("bangCucBoRieng") or [])
    ra = []
    for b in ds:
        if b not in ra:
            ra.append(b)
    return ra


# ------------------------------------------------------------------ dung / cap nhat
def dung_moi(dm, dv, csdl_nguon, xoa_cu):
    """Tao CSDL tu du lieu mau (lan dau / --tao-moi / --lam-lai)"""
    ten_csdl = dv["coSoDuLieu"]
    assert ten_csdl.startswith(TIEN_TO), "chi dung CSDL ten bat dau " + TIEN_TO
    cn = ket_noi()
    co = cn.cursor().execute("SELECT DB_ID(?)", ten_csdl).fetchone()[0] is not None
    if co:
        if not xoa_cu:
            raise RuntimeError("CSDL %s da co — khong ghi de (dung --lam-lai neu that su muon xoa)." % ten_csdl)
        c = cn.cursor()
        c.execute("ALTER DATABASE [%s] SET SINGLE_USER WITH ROLLBACK IMMEDIATE" % ten_csdl)
        c.execute("DROP DATABASE [%s]" % ten_csdl)
    c = cn.cursor()
    c.execute("CREATE DATABASE [%s]" % ten_csdl)
    c.execute("ALTER DATABASE [%s] SET RECOVERY SIMPLE" % ten_csdl)
    cn.close()

    t0 = time.time()
    cn_nguon = ket_noi(csdl_nguon)
    cn_dich = ket_noi(ten_csdl)
    so_huu = dv.get("soHuu") or []
    ban_sao = dv.get("banSao") or []
    tong, thieu = {}, []
    for bang in so_huu:
        try:
            cot, cm = lay_luoc_do(cn_nguon, bang)
        except RuntimeError:
            thieu.append(bang)              # CSDL nguon chua chay script them bang nay -> tao tu script, khong sua nguon
            continue
        tao_bang(cn_dich, bang, cot, cm, giu_identity=True, so_huu=True)
        tong[bang] = chep_du_lieu(cn_dich, csdl_nguon, bang, cot, giu_identity=True)
    for bang in ban_sao:
        try:
            cot, cm = lay_luoc_do(cn_nguon, bang)
        except RuntimeError:
            thieu.append(bang)              # ban sao cua bang moi (CSDL nguon chua co): tao rong tu script, du lieu den qua su kien
            continue
        tao_bang(cn_dich, bang, cot, cm, giu_identity=False, so_huu=False)
        tong[bang] = chep_du_lieu(cn_dich, csdl_nguon, bang, cot, giu_identity=False)
    for bang in thieu:
        chay_script_tao_bang(cn_dich, bang)
    for bang in bang_cuc_bo_cua(dm, dv):
        if bang in BANG_CUC_BO_CU:
            if co_bang(cn_dich, bang):
                continue                    # script cua bang khac da tao kem (vd Add_SuKienOutbox.sql tao ca 2 bang)
            try:
                cot, cm = lay_luoc_do(cn_nguon, bang)
                tao_bang(cn_dich, bang, cot, cm, giu_identity=True, so_huu=True)       # rong: moi dich vu tu ghi
                continue
            except RuntimeError:
                pass                        # nguon chua co -> tao bang script
        dam_bao_bang_cuc_bo(cn_dich, dv["ten"], bang)
    print("+ %-11s %-24s %2d sở hữu, %2d bản sao, %d dòng | %.1fs" % (dv["ten"], ten_csdl, len(so_huu), len(ban_sao),
                                                                       sum(tong.values()), time.time() - t0))


def cap_nhat_da_co(dm, dv):
    """CSDL da co (GIU du lieu): bo sung bang so huu moi + bang cuc bo bang script idempotent"""
    ten_csdl = dv["coSoDuLieu"]
    cn_dich = ket_noi(ten_csdl)
    thieu = [b for b in (dv.get("soHuu") or []) if not co_bang(cn_dich, b)]
    for bang in thieu:
        chay_script_tao_bang(cn_dich, bang)
    cuc_bo = []
    for bang in bang_cuc_bo_cua(dm, dv):
        kq = dam_bao_bang_cuc_bo(cn_dich, dv["ten"], bang)
        if kq in ("tao", "cap-nhat"):
            cuc_bo.append("%s (%s)" % (bang, "tạo" if kq == "tao" else "cập nhật"))
    cn_dich.close()
    them = []
    if thieu:
        them.append("bổ sung bảng: " + ", ".join(thieu))
    if cuc_bo:
        them.append("bảng cục bộ: " + ", ".join(cuc_bo))
    print("= %-11s %s đã có%s" % (dv["ten"], ten_csdl, (" — " + "; ".join(them)) if them else ""))


def dau_vet_sao_luu(cn):
    """May nay DA TUNG sao luu CSDL microservices? (msdb hoac thu muc MicroServices/sao-luu co ban sao)"""
    dau = []
    try:
        ds = cn.cursor().execute("SELECT DISTINCT database_name FROM msdb.dbo.backupset "
                                 "WHERE database_name LIKE 'SmartPortMS[_]%'").fetchall()
        if ds:
            dau.append("msdb ghi nhận đã sao lưu " + ", ".join(sorted(r[0] for r in ds)))
    except pyodbc.Error:
        pass
    thu_muc = os.path.join(GOC, "sao-luu")
    if os.path.isdir(thu_muc) and any(os.scandir(thu_muc)):
        dau.append("có bản sao lưu trong " + thu_muc)
    return "; ".join(dau) or None


def bao_thieu(ds_thieu, ly_do=None):
    in_mau(DO, "")
    for dv in ds_thieu:
        in_mau(DO, "THIẾU CSDL %s (dịch vụ %s)." % (dv["coSoDuLieu"], dv["ten"]))
    if ly_do:
        in_mau(DO, ly_do)
    in_mau(DO, "KHÔNG tự dựng lại từ dữ liệu mẫu (sẽ thành \"phục hồi giả\": hệ thống lên xanh nhưng dữ liệu là của ngày seed).")
    in_mau(DO, "-> Phục hồi từ bản sao lưu:  pwsh MicroServices\\phuc_hoi.ps1 -Ban <thư mục sao lưu>   (hoặc SMARTPORT.bat phuc-hoi)")
    in_mau(DO, "-> Hoặc CHẮC CHẮN không cần dữ liệu cũ (vd lần dựng đầu bị dừng giữa chừng):  python MicroServices\\dung_csdl_dich_vu.py --tao-moi")


def main():
    bat_mau()
    ap = argparse.ArgumentParser(description="Dựng / cập nhật CSDL riêng từng dịch vụ SmartPort (SmartPortMS_*)")
    ap.add_argument("--lam-lai", action="store_true", help="XÓA và dựng lại TOÀN BỘ CSDL đã chọn từ dữ liệu mẫu")
    ap.add_argument("--tao-moi", action="store_true", help="dựng MỚI từ dữ liệu mẫu các CSDL đang thiếu (không phục hồi)")
    ap.add_argument("--chi", nargs="*", help="chỉ các dịch vụ này (tên trong dich-vu.json)")
    ap.add_argument("--nguon", help="CSDL nguồn dữ liệu mẫu (mặc định coSoDuLieuNguon trong dich-vu.json)")
    a = ap.parse_args()
    dm = json.load(open(os.path.join(GOC, "..", "dich-vu.json"), encoding="utf-8"))
    csdl_nguon = a.nguon or dm.get("coSoDuLieuNguon") or "SmartPortDB_BaoCao"

    tat_ca = [dv for dv in dm["dichVu"] if dv.get("nenTang", "dotnet") == "dotnet" and dv.get("coSoDuLieu")]
    chon = [dv for dv in tat_ca if not a.chi or dv["ten"] in a.chi]
    cn = ket_noi()
    co = {dv["ten"]: cn.cursor().execute("SELECT DB_ID(?)", dv["coSoDuLieu"]).fetchone()[0] is not None for dv in tat_ca}

    if a.lam_lai:
        in_mau(VANG, "--lam-lai: XÓA và dựng lại %d CSDL từ dữ liệu mẫu %s." % (len(chon), csdl_nguon))
        for dv in chon:
            dung_moi(dm, dv, csdl_nguon, xoa_cu=True)
        return 0

    thieu = [dv for dv in chon if not co[dv["ten"]]]
    if thieu:
        if a.tao_moi:
            in_mau(VANG, "--tao-moi: dựng MỚI từ dữ liệu mẫu %s các CSDL đang thiếu: %s (dữ liệu cũ của chúng KHÔNG được phục hồi)."
                   % (csdl_nguon, ", ".join(dv["coSoDuLieu"] for dv in thieu)))
        elif not any(co.values()):
            dau_vet = dau_vet_sao_luu(cn)
            if dau_vet:
                bao_thieu(thieu, "Chưa có CSDL SmartPortMS_* nào nhưng máy ĐÃ TỪNG sao lưu (%s) — có thể đã mất dữ liệu thật." % dau_vet)
                return MA_THIEU_CSDL
            in_mau(XANH, "Chưa có CSDL microservices nào — DỰNG LẦN ĐẦU %d CSDL từ dữ liệu mẫu %s (chỉ đọc nguồn)."
                   % (len(thieu), csdl_nguon))
        else:
            bao_thieu(thieu)
            return MA_THIEU_CSDL
        for dv in thieu:
            dung_moi(dm, dv, csdl_nguon, xoa_cu=False)

    print("CSDL nguồn dữ liệu mẫu (chỉ đọc): %s" % csdl_nguon)
    for dv in chon:
        if co[dv["ten"]]:
            cap_nhat_da_co(dm, dv)
    return 0


if __name__ == "__main__":
    sys.exit(main())
