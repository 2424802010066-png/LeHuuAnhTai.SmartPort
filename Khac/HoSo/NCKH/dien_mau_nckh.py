# -*- coding: utf-8 -*-
"""
Do noi dung de tai SmartPort vao CHINH FILE MAU cua Vien Cong nghe so.

Vi sao khong tu dung lai bang python-docx: trang bia cua mau co khung vien xanh
(8 v:rect), LOGO truong (anh nhung), va 4 textbox — dung lai bang tay khong bao gio
khop. Script nay copy file mau roi DIEN noi dung vao, giu nguyen 100% trang bia,
checkbox Wingdings, footer danh so trang, font va le cua Vien.

Mau goc: Khac/HoSo/NCKH/_Mau_goc_Vien.docx
Chay:    python dien_mau_nckh.py
"""
import copy
import os
import shutil

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Cm, Pt
from docx.text.paragraph import Paragraph

HERE = os.path.dirname(os.path.abspath(__file__))
MAU = os.path.join(HERE, "_Mau_goc_Vien.docx")
OUT = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_LeHuuAnhTai_2424802010066.docx")

FONT = "Times New Roman"
SZ = 12  # mau dung 12pt cho than bai

def _copy_co_thu_lai(src, dst, lan=6, cho=2.0):
    """Windows hay khoa file .docx (preview handler, Search indexer) — thu lai vai lan."""
    import time
    for i in range(lan):
        try:
            shutil.copyfile(src, dst)
            return dst
        except PermissionError:
            if i == lan - 1:
                alt = dst.replace(".docx", "_BAN-MOI.docx")
                shutil.copyfile(src, alt)
                print("CANH BAO: %s dang bi khoa -> ghi ra %s" % (os.path.basename(dst),
                                                                  os.path.basename(alt)))
                return alt
            time.sleep(cho)


OUT = _copy_co_thu_lai(MAU, OUT)
doc = Document(OUT)
BODY = doc.element.body

# QUAN TRONG: giu tham chieu 3 bang cua mau NGAY BAY GIO.
# Sau nay script con chen them bang ket qua thuc nghiem vao giua tai lieu, luc do
# chi so trong doc.tables bi DICH — doc.tables[1] se tro thanh bang moi chu khong
# con la bang tien do. Bam theo chi so se dien nham bang.
T_THANHVIEN, T_TIENDO, T_KY = doc.tables[0], doc.tables[1], doc.tables[2]

# =====================================================================
# TIEN ICH
# =====================================================================
def _style_run(r, bold=False, italic=False, size=SZ):
    r.font.name = FONT
    r.font.size = Pt(size)
    r.bold = bold
    r.italic = italic
    rf = r._element.get_or_add_rPr().get_or_add_rFonts()
    rf.set(qn("w:eastAsia"), FONT)
    rf.set(qn("w:cs"), FONT)


def _add_runs(p, text, bold=False, italic=False, size=SZ):
    """Ho tro **in dam** giua chuoi."""
    for i, seg in enumerate(text.split("**")):
        if not seg:
            continue
        _style_run(p.add_run(seg), bold or (i % 2 == 1), italic, size)


def thay_w_t(old, new):
    """Thay chuoi trong MOI the <w:t> cua tai lieu, ke ca ben trong textbox."""
    n = 0
    for t in BODY.iter(qn("w:t")):
        if t.text and old in t.text:
            t.text = t.text.replace(old, new)
            n += 1
    return n


def tim(prefix):
    for p in doc.paragraphs:
        if p.text.strip().startswith(prefix):
            return p
    raise KeyError("Khong tim thay doan bat dau bang: " + prefix)


def dat_text(p, text, bold=False, italic=False, size=SZ):
    """Xoa het run cu cua doan roi ghi text moi (giu nguyen dinh dang doan)."""
    for r in list(p.runs):
        r._element.getparent().remove(r._element)
    _add_runs(p, text, bold, italic, size)
    return p


def chen_sau(anchor, text="", bold=False, italic=False, size=SZ,
             align="just", first_indent=0.8, left=0.0, after=4):
    """Tao doan moi ngay sau `anchor`, ke thua dinh dang doan cua anchor."""
    new_p = copy.deepcopy(anchor._p)
    for child in list(new_p):
        if child.tag != qn("w:pPr"):
            new_p.remove(child)
    anchor._p.addnext(new_p)
    p = Paragraph(new_p, anchor._parent)
    pf = p.paragraph_format
    pf.line_spacing = 1.3
    pf.space_after = Pt(after)
    pf.space_before = Pt(0)
    pf.first_line_indent = Cm(first_indent)
    pf.left_indent = Cm(left)
    p.alignment = {"just": WD_ALIGN_PARAGRAPH.JUSTIFY, "c": WD_ALIGN_PARAGRAPH.CENTER,
                   "l": WD_ALIGN_PARAGRAPH.LEFT}[align]
    if text:
        _add_runs(p, text, bold, italic, size)
    return p


def khoi(anchor, dong_list):
    """Chen lien tiep nhieu doan sau anchor. Moi phan tu: str hoac (str, kwargs)."""
    cur = anchor
    for d in dong_list:
        if isinstance(d, tuple):
            cur = chen_sau(cur, d[0], **d[1])
        else:
            cur = chen_sau(cur, d)
    return cur


def gach_dau_dong(text):
    return (text, dict(first_indent=0.0, left=1.0, align="just", after=3))


def tieu_de_phu(text):
    return (text, dict(bold=True, italic=True, first_indent=0.5, after=3))


def tick_o(para, thu_tu):
    """Doi o vuong Wingdings F06F (trong) -> Wingdings 2 F052 (da tick)."""
    syms = [s for s in para._p.iter(qn("w:sym"))]
    s = syms[thu_tu]
    s.set(qn("w:font"), "Wingdings 2")
    s.set(qn("w:char"), "F052")


def set_o(cell, text, bold=False, align="l", size=SZ, italic=False):
    cell.text = ""
    p = cell.paragraphs[0]
    p.paragraph_format.line_spacing = 1.15
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.space_before = Pt(2)
    p.alignment = {"l": WD_ALIGN_PARAGRAPH.LEFT, "c": WD_ALIGN_PARAGRAPH.CENTER,
                   "just": WD_ALIGN_PARAGRAPH.JUSTIFY}[align]
    _add_runs(p, text, bold, italic, size)


# =====================================================================
# 1. TRANG BIA + TIEU DE  (thay cho giu nguyen khung vien, logo, textbox)
# =====================================================================
TEN_DE_TAI = ("NGHIÊN CỨU MÔ HÌNH HỌC SÂU TRIỂN KHAI TẠI BIÊN CHO DỰ BÁO SẢN LƯỢNG "
              "CONTAINER VÀ TỰ ĐỘNG HÓA CỔNG CẢNG")

thay_w_t("TÊN ĐỀ TÀI", TEN_DE_TAI)

# Textbox "TÊN ĐỀ TÀI" cua mau cao dung 1 dong (cy=514350 EMU ~ 1,43cm) va chu 20pt.
# Ten de tai that dai hon nhieu -> phai ha co chu va noi chieu cao, neu khong Word CAT bot chu.
A_NS = "http://schemas.openxmlformats.org/drawingml/2006/main"


def noi_khung_ten_de_tai(moc, co_chu_half_pt=32, cao_emu=1600000):
    for t in BODY.iter(qn("w:t")):
        if not (t.text and moc in t.text):
            continue
        node = t
        while node is not None:
            if node.tag.split("}")[-1] in ("AlternateContent", "drawing", "pict"):
                break
            node = node.getparent()
        if node is None:
            continue
        for the in ("w:sz", "w:szCs"):
            for e in node.iter(qn(the)):
                e.set(qn("w:val"), str(co_chu_half_pt))
        # Phai sua CA HAI: <a:ext> (kich thuoc shape) VA <wp:extent> (o chua anh/shape
        # trong dong van ban). Chi sua a:ext thi Word van CLIP theo wp:extent.
        for e in node.iter():
            if e.tag.split("}")[-1] in ("ext", "extent") and e.get("cy"):
                e.set("cy", str(cao_emu))
        # Cho shape tu gian theo text de khong bao gio cat chu
        for bp in node.iter():
            if bp.tag.split("}")[-1] == "bodyPr":
                for con in list(bp):
                    if con.tag.split("}")[-1] in ("spAutoFit", "noAutofit"):
                        bp.remove(con)
                bp.append(bp.makeelement("{%s}spAutoFit" % A_NS, {}))
        # ban du phong VML: sua height trong thuoc tinh style
        for e in node.iter():
            stl = e.get("style")
            if stl and "height:" in stl:
                import re as _re
                e.set("style", _re.sub(r"height:[\d.]+pt",
                                       "height:%.1fpt" % (cao_emu / 12700.0), stl))


noi_khung_ten_de_tai(TEN_DE_TAI[:18])
thay_w_t("Nguyễn Văn A (Nhóm trưởng)", "Lê Hữu Anh Tài (Nhóm trưởng)")
thay_w_t("TS/ThS.", "TS. Huỳnh Nguyễn Thành Luân")
thay_w_t("20…-20…", "2026-2027")          # bia 1
thay_w_t("201…-201…", "2026-2027")        # bia 2
thay_w_t("năm 201…", "năm 2026")          # dong ky Lanh dao Vien
thay_w_t("Thành phố Hồ Chí Minh, ngày       tháng       năm",
         "Thành phố Hồ Chí Minh, ngày       tháng       năm 2026")

# =====================================================================
# MUC 1 — Ten de tai
# =====================================================================
p1 = tim("1. Tên đề tài:")
chen_sau(p1, "Nghiên cứu mô hình học sâu triển khai tại biên cho dự báo sản lượng container "
             "và tự động hóa cổng cảng — thực nghiệm trên dữ liệu cảng biển Việt Nam.")

# =====================================================================
# MUC 2, 3 — tick o vuong
# =====================================================================
tick_o(tim("2. Loại hình nghiên cứu"), 1)   # o thu 2 = Ung dung
# Muc 3: mau da tick san "Khoa hoc Ky thuat va Cong nghe" -> giu nguyen

# =====================================================================
# MUC 4 — Thoi gian
# =====================================================================
p4 = tim("4. Thời gian thực hiện")
dat_text(p4, "4. Thời gian thực hiện:  06 tháng", bold=True)
p4_sau = Paragraph(p4._p.getnext(), p4._parent)
dat_text(p4_sau, "            Từ tháng   10   năm  2026          đến  tháng  03     năm 2027")

# =====================================================================
# MUC 5 — Don vi quan ly
# =====================================================================
p5 = tim("Đơn vị: Viện Công nghệ số")
dat_text(p5, "Đơn vị: Viện Công nghệ số                  Chương trình đào tạo: Công nghệ thông tin")

# =====================================================================
# MUC 6 — Giao vien huong dan
# =====================================================================
dat_text(tim("Họ và tên:"),
         "Họ và tên: Huỳnh Nguyễn Thành Luân                              Học vị: Tiến sĩ")
dat_text(tim("Di động:"),
         "Di động: ………………………                    E-mail: luanhnt@tdmu.edu.vn")

# =====================================================================
# MUC 7 — Nhom sinh vien
# =====================================================================
t_tv = T_THANHVIEN
set_o(t_tv.rows[1].cells[1], "Lê Hữu Anh Tài")
set_o(t_tv.rows[1].cells[2], "2424802010066", align="c")
set_o(t_tv.rows[1].cells[3], "……………", align="c")

for nhan, gt in (("Họ và tên:", "Họ và tên: Lê Hữu Anh Tài"),
                 ("SĐT:", "SĐT: 0387 562 579"),
                 ("Email:", "Email: lehuuanhtai2020tkh@gmail.com")):
    for p in doc.paragraphs:
        if p.text.strip() == nhan:
            dat_text(p, gt)
            break

# =====================================================================
# MUC 8 — Tinh cap thiet
# =====================================================================
khoi(tim("8. Tính cấp thiết"), [
    "Container hóa là trụ cột của chuỗi cung ứng Việt Nam. Theo số liệu thống kê của Cục Hàng hải và "
    "Đường thủy Việt Nam mà nhóm nghiên cứu đã thu thập và làm sạch (66 tháng, từ 01/2021 đến 06/2026), "
    "sản lượng container thông qua hệ thống cảng biển cả nước năm 2024 đạt 30.571 nghìn TEU, duy trì đà "
    "tăng trưởng hai chữ số ở nhiều thời điểm. Vùng kinh tế trọng điểm phía Nam là địa bàn tập trung dày "
    "đặc các cảng cạn (ICD) và depot container phục vụ các khu công nghiệp.",

    "Áp lực lớn nhất của các điểm thông quan này không nằm ở cầu bến mà nằm ở cổng ra vào. Kinh nghiệm "
    "của Tân cảng Cát Lái cho thấy khi áp dụng check-in điện tử bắt buộc trước 2 giờ, thời gian xe chờ "
    "tại cổng giảm từ 13 phút xuống còn 6 phút — cải thiện có giá trị kinh tế và môi trường rõ rệt, bởi "
    "mỗi phút xe đầu kéo nổ máy chờ đều là nhiên liệu và phát thải. Tuy nhiên, các giải pháp tạo ra kết "
    "quả đó (hệ sinh thái hơn 30 phần mềm, cổng tự động nhận dạng biển số, hạ tầng máy chủ AI chuyên "
    "dụng) đòi hỏi nguồn lực mà chỉ những tập đoàn khai thác cảng lớn mới đáp ứng được.",

    "Phần lớn ICD, depot và kho bãi vừa và nhỏ **không đủ điều kiện triển khai AI theo mô hình tập "
    "trung**: chi phí máy chủ GPU, chi phí đường truyền tải ảnh camera liên tục lên đám mây, và đặc biệt "
    "là thiếu nhân lực vận hành hạ tầng AI. Từ đó hình thành hai khoảng trống nghiên cứu.",

    "**Thứ nhất, dự báo sản lượng container Việt Nam chưa được mô hình hóa đúng với đặc thù mùa vụ bản "
    "địa.** Chuỗi sản lượng chịu chi phối mạnh bởi Tết Nguyên đán — hiệu ứng mùa vụ theo lịch Âm, trượt "
    "giữa tháng 1 và tháng 2 dương lịch qua các năm, nên các mô hình mùa vụ chu kỳ cố định 12 tháng "
    "không nắm bắt được. Trong thực nghiệm sơ bộ của nhóm (mục 10), mô hình mạng nơ-ron nhiều lớp (MLP) "
    "chỉ cải thiện khoảng 14,2% MAE so với dự báo ngây thơ (naive), và suy giảm nghiêm trọng khi dự báo "
    "nhiều bước: sai số MAPE tăng từ 9,06% (dự báo 1 bước) lên 20,35% (dự báo cuộn 18 tháng). Với công "
    "tác lập kế hoạch bãi và nhân lực — vốn cần tầm nhìn 3–6 tháng — mức sai số này là không dùng được.",

    "**Thứ hai, học sâu tại biên (edge/on-device) cho nghiệp vụ cảng chưa được đánh giá định lượng.** "
    "Các nghiên cứu về nhận dạng biển số và mã container hiện nay hầu hết giả định có GPU máy chủ. Trong "
    "khi đó, hạ tầng suy luận ngay trên trình duyệt (ONNX Runtime Web, WebAssembly, WebGPU) đã đủ chín "
    "để chạy các mô hình nhẹ, mang lại ba lợi ích phù hợp với doanh nghiệp vừa và nhỏ: không cần máy chủ "
    "AI, dữ liệu ảnh không rời khỏi thiết bị, và vẫn hoạt động khi mất kết nối. Tuy vậy, chưa có công bố "
    "nào đánh giá độ chính xác — độ trễ của hướng tiếp cận này trên bài toán cổng cảng với điều kiện ảnh "
    "thực địa Việt Nam.",

    "Do đó, nghiên cứu một bộ mô hình học sâu nhẹ, triển khai được tại biên cho hai bài toán nêu trên là "
    "hướng tiếp cận thiết thực và cấp thiết, vừa góp phần thu hẹp khoảng cách công nghệ cho doanh nghiệp "
    "logistics vừa và nhỏ, vừa bổ sung bằng chứng khoa học còn thiếu cho bối cảnh Việt Nam.",
])

# =====================================================================
# MUC 9 — Muc tieu
# =====================================================================
khoi(tim("9. Mục tiêu đề tài"), [
    tieu_de_phu("Mục tiêu tổng quát:"),
    "Nghiên cứu, đề xuất và đánh giá thực nghiệm bộ mô hình học sâu nhẹ, triển khai được tại biên nhằm "
    "giải quyết hai bài toán lõi của cảng cạn Việt Nam: dự báo sản lượng container ngắn hạn và nhận dạng "
    "tự động phương tiện/container tại cổng.",
    tieu_de_phu("Mục tiêu cụ thể:"),
    gach_dau_dong("- Xây dựng bộ dữ liệu nghiên cứu: chuỗi sản lượng container Việt Nam ≥ 66 tháng có "
                  "kiểm tra nhất quán, và bộ ảnh cổng thực địa ≥ 1.000 ảnh đã gán nhãn."),
    gach_dau_dong("- Đề xuất mô hình dự báo dựa trên mô hình không gian trạng thái chọn lọc (Selective "
                  "SSM) kết hợp đặc trưng lịch Âm, mục tiêu **giảm ≥ 15% MAE** so với mô hình tốt nhất "
                  "hiện có của nhóm (MLP, MAE = 259,8 nghìn TEU) và **giảm ≥ 25% MAPE** ở kịch bản dự "
                  "báo cuộn nhiều bước."),
    gach_dau_dong("- Xây dựng pipeline nhận dạng hai tầng chạy hoàn toàn trên trình duyệt, đạt độ chính "
                  "xác cấp ký tự **≥ 95%** và độ trễ **≤ 1,5 giây/ảnh** trên máy tính văn phòng phổ "
                  "thông không GPU rời."),
    gach_dau_dong("- So sánh hiệu suất các mô hình theo MAE, RMSE, MAPE (bài toán dự báo) và độ chính "
                  "xác cấp ký tự, độ trễ suy luận (bài toán nhận dạng)."),
    gach_dau_dong("- Tích hợp kết quả vào hệ thống SmartPort, đo hiệu quả vận hành và đề xuất hướng "
                  "cải thiện."),
])

# =====================================================================
# MUC 10 — Tong quan
# =====================================================================
p10 = tim("10. Tổng quan tình hình nghiên cứu")
cur = khoi(p10, [
    tieu_de_phu("a) Dự báo chuỗi thời gian bằng mô hình không gian trạng thái chọn lọc"),
    "Năm 2023, Gu và Dao [1] giới thiệu Mamba — kiến trúc không gian trạng thái (State Space Model) với "
    "cơ chế chọn lọc: các tham số của phương trình trạng thái được điều biến theo từng đầu vào, cho phép "
    "mô hình “nhớ” hoặc “quên” có chủ đích. Mamba đạt chất lượng ngang hoặc vượt Transformer nhưng độ "
    "phức tạp chỉ tuyến tính O(n) thay vì bậc hai, nên phù hợp với thiết bị hạn chế tài nguyên. Hướng "
    "này nhanh chóng được mở rộng: MambaTS [2] cải tiến cơ chế quét cho dự báo dài hạn; ST-Mamba [3] và "
    "ST-MambaSync [4] áp dụng cho dự báo luồng giao thông, cho thấy khả năng nắm bắt phụ thuộc dài hạn "
    "mà không bị làm trơn quá mức. Pessoa và cộng sự [5] bổ sung định lượng độ bất định cho dự báo bằng "
    "Mamba, ghi nhận sai số trung bình khoảng 8% trên các bộ chuẩn.",

    tieu_de_phu("b) Dự báo và tối ưu vận hành cổng cảng"),
    "Sun và cộng sự [6] chứng minh hệ thống đặt lịch xe giảm đáng kể thời gian quay vòng xe tại cảng. "
    "Li và cộng sự [7] đề xuất mô hình học sâu tổ hợp dự báo lượng xe đến cảng theo khung thời gian linh "
    "hoạt từ 1 đến 24 giờ. Min và cộng sự [8] sử dụng học chuyển giao để dự báo thời gian quay vòng xe "
    "tại các cảng thiếu dữ liệu — tình huống rất gần với bối cảnh ICD Việt Nam. Chhetri và cộng sự [9] "
    "mô hình hóa thời gian lưu bãi container bằng các thuật toán học máy.",

    tieu_de_phu("c) Nhận dạng tự động tại cổng và suy luận tại biên"),
    "Hướng nhận dạng mã container đang chuyển dịch sang kiến trúc hai tầng phát hiện kết hợp đọc chữ: "
    "YOLO kết hợp TrOCR cho mã container theo chuẩn ISO 6346 [10]; hoặc tích hợp khử nhiễu sâu để giám "
    "sát phương tiện trong cảng thông minh [11]. Về mô hình đọc ký tự nhẹ, Compact Convolutional "
    "Transformer [12] cho phép Transformer hoạt động hiệu quả trên tập dữ liệu nhỏ — nền tảng của mô "
    "hình CCT-S v2 chuyên đọc biển số. Về triển khai, nghiên cứu năm 2024 [13] chỉ ra suy luận trong "
    "trình duyệt giúp phản hồi nhanh hơn và bảo vệ riêng tư tốt hơn nhờ loại bỏ vòng truyền lên đám mây; "
    "các tiến bộ về WebAssembly và WebGPU [14] tiếp tục thu hẹp khoảng cách hiệu năng.",

    tieu_de_phu("d) Tình hình trong nước"),
    "Tại Việt Nam, chuyển đổi số cảng biển chủ yếu diễn ra ở dạng triển khai công nghiệp, ít công bố "
    "khoa học. Tổng công ty Tân cảng Sài Gòn đã vận hành hệ sinh thái eSNP với hơn 30 phần mềm, cổng tự "
    "động nhận dạng biển số độ chính xác khoảng 99%; giai đoạn 2021–2025 công bố tiết kiệm gần 600 tỷ "
    "đồng. Tuy nhiên đây là các hệ thống thương mại đóng, không công bố phương pháp, tập dữ liệu hay chỉ "
    "số đánh giá — nên không thể tái lập hoặc kế thừa trong nghiên cứu. Về công bố học thuật, nhóm "
    "nghiên cứu Trường Đại học Thủ Dầu Một đã có kinh nghiệm đưa đề tài ứng dụng AI ra hội nghị quốc tế "
    "[15].",

    tieu_de_phu("e) Kết quả nghiên cứu sơ bộ của nhóm"),
    "Nhóm đã thực hiện một vòng thực nghiệm đối chứng trên bộ dữ liệu 66 tháng: chia tập theo thời gian "
    "(36 tháng huấn luyện / 12 tháng kiểm định / 18 tháng kiểm tra), lặp lại với 20 hạt giống ngẫu nhiên "
    "và báo cáo trung bình kèm độ lệch chuẩn. Kết quả trên tập kiểm tra (MAE, RMSE: nghìn TEU):",
])

# Bang ket qua thuc nghiem — chen ngay sau doan tren
bang_kq = doc.add_table(rows=1, cols=4)
bang_kq.style = "Table Grid"
hdr = ["Mô hình", "MAE", "RMSE", "MAPE (%)"]
for i, h in enumerate(hdr):
    set_o(bang_kq.rows[0].cells[i], h, bold=True, align="c")
for row in [
    ["Naive — lấy giá trị tháng trước", "302,9", "354,1", "10,65"],
    ["Naive theo mùa — cùng kỳ năm trước", "335,3", "388,0", "11,47"],
    ["Hồi quy tuyến tính theo thời gian", "321,5", "384,8", "10,56"],
    ["Hồi quy tuyến tính trên 3 độ trễ — AR(3)", "286,6", "331,8", "9,64"],
    ["**MLP (3 độ trễ) — tốt nhất hiện tại**", "**259,8 ± 29,6**", "**331,2**", "**9,06**"],
    ["MLP (3 độ trễ + trễ 12 + đặc trưng Tết)", "270,7 ± 71,3", "329,4", "9,33"],
    ["Dự báo cuộn 18 tháng — MLP (3 độ trễ)", "606,1", "782,7", "**20,35**"],
    ["Dự báo cuộn 18 tháng — MLP + đặc trưng Tết", "432,0", "506,9", "**14,69**"],
]:
    cells = bang_kq.add_row().cells
    for i, v in enumerate(row):
        set_o(cells[i], v, align=("l" if i == 0 else "c"))
for r in bang_kq.rows:
    for i, w in enumerate([7.4, 2.9, 2.6, 2.9]):
        r.cells[i].width = Cm(w)
# Di chuyen bang tu cuoi body len ngay sau doan dan `cur`, roi chen tiep 4 doan ket luan
cur._p.addnext(bang_kq._tbl)
node = bang_kq._tbl
for txt, kw in [
    ("Ba kết luận rút ra, định hình hướng nghiên cứu của đề tài:", dict(first_indent=0.8, after=3)),
    ("- Mức cải thiện hiện tại còn khiêm tốn: mô hình học sâu tốt nhất chỉ giảm 14,2% MAE so với dự báo "
     "naive, chưa đủ thuyết phục để đưa vào quyết định vận hành. Cần kiến trúc chuyên cho chuỗi thời "
     "gian thay vì mạng nơ-ron tổng quát.", dict(first_indent=0.0, left=1.0, after=3)),
    ("- Dự báo nhiều bước là điểm gãy: MAPE tăng gấp hơn hai lần khi chuyển từ dự báo 1 bước sang dự báo "
     "cuộn. Đây chính là bài toán mà cơ chế trạng thái ẩn của SSM được kỳ vọng xử lý tốt hơn.",
     dict(first_indent=0.0, left=1.0, after=3)),
    ("- Đặc trưng Tết có giá trị nhưng chưa được khai thác đúng: thêm đặc trưng Tết làm xấu đi dự báo 1 "
     "bước (MAE 259,8 → 270,7) nhưng cải thiện mạnh dự báo cuộn (MAPE 20,35% → 14,69%). Tín hiệu mùa vụ "
     "Âm lịch là thật và đáng giá, nhưng cách mã hóa nhị phân thô sơ hiện tại chưa phù hợp. **Đây là "
     "đóng góp khoa học mang tính bản địa rõ nét nhất của đề tài.**",
     dict(first_indent=0.0, left=1.0, after=3)),
]:
    tmp = chen_sau(cur, txt, **kw)
    node.addnext(tmp._p)
    node = tmp._p

# =====================================================================
# MUC 11 — Doi tuong, pham vi, cach tiep can, phuong phap
# =====================================================================
khoi(tim("11. Đối tượng, phạm vi nghiên cứu"), [
    tieu_de_phu("Đối tượng nghiên cứu:"),
    gach_dau_dong("- Các mô hình học sâu cho chuỗi thời gian: mô hình không gian trạng thái chọn lọc "
                  "(Selective SSM / Mamba), mạng hồi tiếp (LSTM, GRU), Transformer và các mô hình đối "
                  "chứng thống kê."),
    gach_dau_dong("- Các mô hình thị giác máy tính nhẹ cho phát hiện đối tượng và nhận dạng ký tự quang "
                  "học trong điều kiện triển khai tại biên."),
    gach_dau_dong("- Quy trình nghiệp vụ cổng cảng cạn: tiếp nhận xe, đối chiếu lệnh giao nhận, ghi nhận "
                  "container ra/vào bãi."),

    tieu_de_phu("Phạm vi nghiên cứu:"),
    gach_dau_dong("- Dữ liệu dự báo: chuỗi sản lượng container thông qua cảng biển Việt Nam theo tháng, "
                  "kỳ 01/2021 – 06/2026 (66 tháng), nguồn Cục Hàng hải và Đường thủy Việt Nam [16]."),
    gach_dau_dong("- Dữ liệu thị giác: ảnh chụp thực địa tại cổng ICD/depot, bổ sung ảnh từ hệ thống "
                  "SmartPort do nhóm đang vận hành."),
    gach_dau_dong("- Mô hình phải chạy được ở chế độ suy luận trên trình duyệt của máy tính văn phòng "
                  "phổ thông (CPU, không GPU rời). Giai đoạn huấn luyện vẫn thực hiện ngoại tuyến."),
    gach_dau_dong("- Đề tài **không đi sâu** vào phân bổ cầu bến tối ưu, lập kế hoạch xếp dỡ tàu hay "
                  "điều độ cẩu."),

    tieu_de_phu("Cách tiếp cận:"),
    "Tiếp cận theo hướng nghiên cứu thực nghiệm có đối chứng, gắn với hệ thống thật. Thay vì xây dựng "
    "mô hình trên dữ liệu mô phỏng, đề tài sử dụng dữ liệu thống kê chính thức của cơ quan quản lý nhà "
    "nước và dữ liệu vận hành từ hệ thống SmartPort, rồi đưa mô hình quay ngược trở lại hệ thống để đo "
    "hiệu quả thực tế. Nguyên tắc xuyên suốt: mọi cải tiến phải được chứng minh bằng chỉ số định lượng "
    "so với đường cơ sở công bố công khai.",

    tieu_de_phu("Phương pháp nghiên cứu:"),
    gach_dau_dong("- **Phương pháp nghiên cứu tài liệu:** tổng hợp, phân tích các công bố quốc tế giai "
                  "đoạn 2022–2026 về SSM/Mamba, dự báo vận hành cảng, nhận dạng tại cổng và suy luận "
                  "tại biên."),
    gach_dau_dong("- **Phương pháp thu thập và xử lý dữ liệu:** thu thập báo cáo thống kê tháng; xử lý "
                  "theo bộ quy tắc làm sạch đã thiết lập (lấy hiệu lũy kế thay vì số ước thực hiện, ưu "
                  "tiên bản công bố muộn nhất đã hiệu chỉnh, kiểm tra nhất quán tổng xuất – nhập – nội "
                  "địa); gán nhãn ảnh theo chuẩn ISO 6346."),
    gach_dau_dong("- **Phương pháp mô phỏng – thực nghiệm:** huấn luyện mô hình bằng PyTorch, triển khai "
                  "suy luận bằng ONNX Runtime Web; tách tập theo thời gian để tránh rò rỉ dữ liệu tương "
                  "lai, lặp nhiều hạt giống."),
    gach_dau_dong("- **Phương pháp phân tích – so sánh:** đối chứng với đầy đủ đường cơ sở (naive, naive "
                  "theo mùa, ARIMA/SARIMA, AR, MLP, LSTM, GRU, Transformer); nghiên cứu loại trừ "
                  "(ablation) để tách riêng đóng góp của từng thành phần đề xuất."),
    gach_dau_dong("- **Phương pháp đo hiệu năng triển khai:** đo độ trễ suy luận, dung lượng mô hình, "
                  "mức tiêu thụ bộ nhớ; lặp phép đo nhiều lần và loại bỏ lần chạy khởi động."),
])

# =====================================================================
# MUC 12.1 — Noi dung nghien cuu (de cuong chi tiet)
# =====================================================================
p121 = tim("12.1. Nội dung nghiên cứu")
cur = p121
for ch, subs in [
    ("CHƯƠNG 1: GIỚI THIỆU TỔNG QUAN",
     ["1.1. Lý do chọn đề tài", "1.2. Tính cấp thiết", "1.3. Mục tiêu và nhiệm vụ nghiên cứu",
      "1.4. Đối tượng và phạm vi nghiên cứu", "1.5. Phương pháp tiếp cận"]),
    ("CHƯƠNG 2: CƠ SỞ LÝ THUYẾT",
     ["2.1. Bài toán dự báo chuỗi thời gian trong khai thác cảng",
      "2.2. Mô hình không gian trạng thái và cơ chế chọn lọc (Mamba)",
      "2.3. Mô hình phát hiện đối tượng và nhận dạng ký tự quang học nhẹ",
      "2.4. Suy luận học sâu tại biên: ONNX Runtime Web, WebAssembly, WebGPU"]),
    ("CHƯƠNG 3: XỬ LÝ DỮ LIỆU",
     ["3.1. Chuỗi sản lượng container Việt Nam: nguồn và quy tắc làm sạch",
      "3.2. Thiết kế đặc trưng lịch Âm (hiệu ứng Tết Nguyên đán)",
      "3.3. Bộ ảnh cổng thực địa: thu thập, gán nhãn, kiểm định chất lượng"]),
    ("CHƯƠNG 4: MÔ HÌNH ĐỀ XUẤT",
     ["4.1. Kiến trúc SSM chọn lọc có nhận biết lịch Âm",
      "4.2. Cơ chế điều biến bước chọn lọc theo vector đặc trưng lịch",
      "4.3. Pipeline nhận dạng hai tầng và cơ chế đối chiếu chéo hai engine",
      "4.4. Hậu xử lý theo chuẩn: biểu thức chính quy biển số và số kiểm tra ISO 6346"]),
    ("CHƯƠNG 5: THỰC NGHIỆM VÀ ĐÁNH GIÁ",
     ["5.1. Thiết lập thực nghiệm và các đường cơ sở đối chứng",
      "5.2. Kết quả dự báo: MAE, RMSE, MAPE ở kịch bản một bước và nhiều bước",
      "5.3. Kết quả nhận dạng: độ chính xác cấp ký tự và độ trễ suy luận",
      "5.4. Nghiên cứu loại trừ (ablation study)",
      "5.5. Tích hợp vào hệ thống SmartPort và đo hiệu quả vận hành"]),
    ("CHƯƠNG 6: KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN",
     ["6.1. Tổng kết kết quả đạt được", "6.2. Hạn chế của đề tài", "6.3. Hướng phát triển"]),
]:
    cur = chen_sau(cur, ch, bold=True, first_indent=0.5, align="l", after=2)
    for s in subs:
        cur = chen_sau(cur, s, first_indent=0.0, left=1.2, align="l", after=1)
    cur = chen_sau(cur, "", after=2)

# =====================================================================
# MUC 12.2 — Bang tien do
# =====================================================================
t_td = T_TIENDO
tien_do = [
    ("Tháng 10/2026",
     "Khảo sát, tổng quan tài liệu; chuẩn hóa khung đánh giá và chốt các đường cơ sở đối chứng",
     "Báo cáo tổng quan; danh mục tài liệu tham khảo"),
    ("Tháng 11/2026",
     "Hoàn thiện bộ dữ liệu chuỗi sản lượng; thu thập và gán nhãn bộ ảnh cổng thực địa",
     "Bộ dữ liệu kèm tài liệu mô tả và quy tắc làm sạch"),
    ("Tháng 12/2026",
     "Cài đặt mô hình SSM chọn lọc có nhận biết lịch Âm; thiết kế đặc trưng lịch; huấn luyện, tinh chỉnh",
     "Mô hình dự báo hoàn chỉnh; mã nguồn thực nghiệm"),
    ("Tháng 01/2027",
     "Thực nghiệm đối chứng đầy đủ với các đường cơ sở; nghiên cứu loại trừ (ablation)",
     "Bảng kết quả đối chứng; báo cáo phân tích"),
    ("Tháng 02/2027",
     "Tinh chỉnh pipeline nhận dạng hai tầng, đo độ chính xác và độ trễ; tích hợp vào hệ thống SmartPort",
     "Mô hình đã tinh chỉnh; bản tích hợp chạy được"),
    ("Tháng 03/2027",
     "Viết bài báo khoa học; hoàn thiện báo cáo tổng kết và hồ sơ nghiệm thu",
     "Bản thảo bài báo; báo cáo tổng kết đề tài"),
]
mau_row = t_td.rows[1]._tr
for i, (tg, nd, sp) in enumerate(tien_do):
    tr = mau_row if i == 0 else copy.deepcopy(mau_row)
    if i:
        t_td._tbl.append(tr)
    from docx.table import _Row
    row = _Row(tr, t_td)
    set_o(row.cells[0], tg)
    set_o(row.cells[1], nd)
    set_o(row.cells[2], sp)
    set_o(row.cells[3], "Lê Hữu Anh Tài")

# =====================================================================
# MUC 13 — San pham va kha nang ung dung
# =====================================================================
khoi(tim("13. Sản phẩm và khả năng ứng dụng"), [
    tieu_de_phu("Sản phẩm đạt được:"),
    gach_dau_dong("- Mô hình dự báo sản lượng container dựa trên SSM chọn lọc kết hợp đặc trưng lịch Âm, "
                  "kèm mã nguồn thực nghiệm và bảng kết quả đối chứng."),
    gach_dau_dong("- Pipeline nhận dạng biển số xe và mã container chạy hoàn toàn trên trình duyệt, kèm "
                  "báo cáo đo độ chính xác và độ trễ."),
    gach_dau_dong("- Bộ dữ liệu nghiên cứu: chuỗi sản lượng ≥ 66 tháng có kiểm tra nhất quán và bộ ảnh "
                  "cổng ≥ 1.000 ảnh đã gán nhãn, kèm tài liệu mô tả cho phép tái lập."),
    gach_dau_dong("- Mô-đun tích hợp trên hệ thống SmartPort kèm hướng dẫn sử dụng."),
    gach_dau_dong("- 01 bài báo khoa học gửi hội nghị/tạp chí chuyên ngành công nghệ thông tin và 01 báo "
                  "cáo tổng kết đề tài theo quy định."),

    tieu_de_phu("Khả năng ứng dụng:"),
    "Kết quả đề tài có thể triển khai trực tiếp tại các cảng cạn (ICD), depot container và doanh nghiệp "
    "kho vận vừa và nhỏ: dự báo sản lượng hỗ trợ lập kế hoạch bãi và nhân lực; nhận dạng tự động tại "
    "cổng giúp rút ngắn thời gian xe chờ, giảm nhiên liệu tiêu hao và khí thải. Do mô hình chạy trên "
    "thiết bị sẵn có, chi phí triển khai gần như chỉ còn chi phí phần mềm, hạ thấp rào cản công nghệ cho "
    "nhóm doanh nghiệp vừa và nhỏ; việc dữ liệu ảnh không rời khỏi thiết bị cũng giải quyết e ngại về "
    "bảo mật thông tin khách hàng. Ngoài ra, hướng tiếp cận đặc trưng lịch Âm có thể áp dụng cho nhiều "
    "chuỗi dữ liệu kinh tế Việt Nam khác chịu ảnh hưởng của Tết Nguyên đán. Đề tài cũng là học liệu minh "
    "họa cho các học phần Trí tuệ nhân tạo, Học máy và Hệ thống thông tin tại Viện Công nghệ số.",
])

# =====================================================================
# MUC 14 — Tai lieu tham khao
# =====================================================================
REFS = [
    "A. Gu and T. Dao, “Mamba: Linear-Time Sequence Modeling with Selective State Spaces,” arXiv preprint arXiv:2312.00752, 2023.",
    "X. Cai et al., “MambaTS: Improved Selective State Space Models for Long-term Time Series Forecasting,” in Advances in Neural Information Processing Systems (NeurIPS), 2024.",
    "Z. Shao et al., “ST-Mamba: Spatial-Temporal Selective State Space Model for Traffic Flow Prediction,” arXiv preprint arXiv:2404.13257, 2024.",
    "Z. Shao et al., “ST-MambaSync: Complement the power of Mamba and Transformer for time series forecasting,” Information Fusion, 2025.",
    "P. Pessoa et al., “Mamba time series forecasting with uncertainty quantification,” arXiv preprint arXiv:2503.10873, 2025.",
    "S. Sun et al., “Reducing external container trucks’ turnaround time in ports,” Computers & Industrial Engineering, 2022.",
    "N. Li et al., “Prediction of delivery truck arrivals at container terminals,” Maritime Economics & Logistics, vol. 26, no. 4, 2024.",
    "Y. Min et al., “Freight Truck Turnaround Time Prediction at Container Ports Using Transfer Learning,” Journal of Marine Science and Engineering, vol. 14, no. 8, art. 727, 2026.",
    "P. Chhetri et al., “Container dwell time predictive modelling: an application of machine learning,” Maritime Policy & Management, 2025.",
    "“Automated Container Code Recognition with YOLOv7 and TrOCR,” Preprints.org, 202508.0232, 2025.",
    "“Advanced vehicle monitoring in smart port utilizing deep denoising techniques,” Ain Shams Engineering Journal, 2025.",
    "A. Hassani et al., “Escaping the Big Data Paradigm with Compact Transformers,” arXiv preprint arXiv:2104.05704, 2021.",
    "“Empowering In-Browser Deep Learning Inference on Edge Devices,” arXiv preprint, 2024.",
    "Chrome Developers, “WebAssembly and WebGPU enhancements for faster Web AI,” 2024. Địa chỉ: https://developer.chrome.com",
    "V. Nguyen, L. N. T. Huynh, and T. Vo, “Optimizing Customer Feedback Analysis with BERT-Based Sentiment Classification: A Case Study of Toyota Dong Sai Gon,” in Future Data and Security Engineering (FDSE 2024), CCIS vol. 2309, Springer, 2024, pp. 269–281.",
    "Cục Hàng hải và Đường thủy Việt Nam, “Thống kê sản lượng hàng hóa thông qua cảng biển,” 2021–2026. Địa chỉ: https://vimawa.gov.vn/vi/thong-ke",
]
cur = tim("14. Tài liệu tham khảo")
for i, r in enumerate(REFS, 1):
    cur = chen_sau(cur, "[%d]  %s" % (i, r), first_indent=-1.0, left=1.6, after=3)

# =====================================================================
# MUC 15 — Kinh phi
# =====================================================================
dat_text(tim("Kinh phí thực hiện (đồng):"),
         "Kinh phí thực hiện (đồng): 4.950.000 đồng", bold=True)
dat_text(tim("Bằng chữ:"), "Bằng chữ: Bốn triệu chín trăm năm mươi nghìn đồng", bold=True)

# =====================================================================
# KY TEN — dien ten vao bang ky
# =====================================================================
t_ky = T_KY
# Noi cot 0, thu hep cot giua — neu khong, "Huỳnh Nguyễn Thành Luân" bi ngat 2 dong.
# Phai sua <w:tblGrid> (luoi cot that su) chu khong chi cell.width, va giu nguyen tong
# be rong 9062 dxa de bang khong tran ra ngoai le.
_grid_moi = (4200, 1015, 3847)
_grid = t_ky._tbl.find(qn("w:tblGrid"))
for _gc, _w in zip(_grid.findall(qn("w:gridCol")), _grid_moi):
    _gc.set(qn("w:w"), str(_w))
for r in t_ky.rows:
    for i, w in enumerate(_grid_moi):
        if i < len(r.cells):
            tcW = r.cells[i]._tc.get_or_add_tcPr().find(qn("w:tcW"))
            if tcW is not None:
                tcW.set(qn("w:w"), str(w))
                tcW.set(qn("w:type"), "dxa")

o_gv = t_ky.rows[0].cells[0]
o_sv = t_ky.rows[0].cells[2]
for o, ten, truoc in ((o_gv, "Huỳnh Nguyễn Thành Luân", 36), (o_sv, "Lê Hữu Anh Tài", 24)):
    p = o.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(truoc)
    p.paragraph_format.space_after = Pt(0)
    _style_run(p.add_run(ten), bold=True)

doc.save(OUT)
print("DA LUU:", OUT)
