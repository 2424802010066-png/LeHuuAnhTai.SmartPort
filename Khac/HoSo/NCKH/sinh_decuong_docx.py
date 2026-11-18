# -*- coding: utf-8 -*-
"""
Sinh THUYET MINH DE CUONG NCKH SINH VIEN (.docx) — de tai SmartPort.

BAM DUNG mau chinh thuc "Mau Thuyet minh de cuong NCKH (bao gom trang bia).docx"
cua Vien Cong nghe so (doc tu Google Drive cua thay ngay 21/09/2026), doi chieu them
de cuong mau da duoc duyet nam 2025-2026 (nhom Nguyen Tan Sang, cung GVHD TS. Huynh
Nguyen Thanh Luan).

Cau truc bat buoc: 2 trang bia + 15 muc + ky 3 ben.
Sua noi dung o day roi chay lai file nay, DUNG sua tay trong Word.
"""
import os
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

FONT = "Times New Roman"
SZ = 13
OUT = r"C:\Users\ASUS\Downloads\SmartPort\Khac\HoSo\NCKH\ThuyetMinhDeCuong_NCKH_LeHuuAnhTai_2424802010066.docx"

doc = Document()

for s in doc.sections:
    s.page_width, s.page_height = Cm(21), Cm(29.7)
    s.top_margin, s.bottom_margin = Cm(2.0), Cm(2.0)
    s.left_margin, s.right_margin = Cm(3), Cm(2)

# ---- Danh so trang o footer (canh giua) ----
def _danh_so_trang(section):
    p = section.footer.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run()
    r.font.name = FONT
    r.font.size = Pt(12)
    for el, attrs, txt in (("w:fldChar", {"w:fldCharType": "begin"}, None),
                           ("w:instrText", {"xml:space": "preserve"}, " PAGE "),
                           ("w:fldChar", {"w:fldCharType": "end"}, None)):
        e = OxmlElement(el)
        for k, v in attrs.items():
            e.set(qn(k), v)
        if txt:
            e.text = txt
        r._r.append(e)


for s in doc.sections:
    _danh_so_trang(s)

st = doc.styles["Normal"]
st.font.name = FONT
st.font.size = Pt(SZ)
st.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
st.paragraph_format.line_spacing = 1.4
st.paragraph_format.space_after = Pt(6)
st.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY


def add_runs(p, text, bold=False, italic=False, size=SZ):
    for i, seg in enumerate(text.split("**")):
        if not seg:
            continue
        r = p.add_run(seg)
        r.font.name = FONT
        r.font.size = Pt(size)
        r._element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
        r.bold = bold or (i % 2 == 1)
        r.italic = italic


def P(text="", bold=False, italic=False, align="just", size=SZ, after=6, indent=None, before=0):
    p = doc.add_paragraph()
    p.paragraph_format.line_spacing = 1.4
    p.paragraph_format.space_after = Pt(after)
    p.paragraph_format.space_before = Pt(before)
    p.alignment = {"just": WD_ALIGN_PARAGRAPH.JUSTIFY, "c": WD_ALIGN_PARAGRAPH.CENTER,
                   "l": WD_ALIGN_PARAGRAPH.LEFT, "r": WD_ALIGN_PARAGRAPH.RIGHT}[align]
    if indent is not None:
        p.paragraph_format.first_line_indent = Cm(indent)
    if text:
        add_runs(p, text, bold, italic, size)
    return p


def MUC(so, ten):
    """Tieu de muc danh so 1..15 — in dam, khong dung Heading style de khoi lo font theme."""
    return P("%s. %s" % (so, ten), bold=True, align="l", after=6, before=10)


def SUB(text):
    return P(text, bold=True, italic=True, align="l", after=4, before=6)


def BULLET(text, size=SZ, lv=0):
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.line_spacing = 1.4
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.left_indent = Cm(1 + 0.6 * lv)
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    add_runs(p, text, size=size)
    return p


def CHUONG(text, bold=True, indent=0.0, after=2):
    p = doc.add_paragraph()
    p.paragraph_format.line_spacing = 1.3
    p.paragraph_format.space_after = Pt(after)
    p.paragraph_format.left_indent = Cm(indent)
    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    add_runs(p, text, bold=bold, size=SZ)
    return p


def set_cell(cell, text, bold=False, align="l", size=12, italic=False):
    cell.text = ""
    p = cell.paragraphs[0]
    p.paragraph_format.line_spacing = 1.15
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.space_before = Pt(2)
    p.alignment = {"l": WD_ALIGN_PARAGRAPH.LEFT, "c": WD_ALIGN_PARAGRAPH.CENTER,
                   "r": WD_ALIGN_PARAGRAPH.RIGHT, "just": WD_ALIGN_PARAGRAPH.JUSTIFY}[align]
    add_runs(p, text, bold=bold, italic=italic, size=size)


def TABLE(headers, rows, widths=None, aligns=None, fill="D9E2F3", size=12):
    t = doc.add_table(rows=1, cols=len(headers))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, h in enumerate(headers):
        set_cell(t.rows[0].cells[i], h, bold=True, align="c", size=size)
        sh = OxmlElement("w:shd")
        sh.set(qn("w:fill"), fill)
        t.rows[0].cells[i]._tc.get_or_add_tcPr().append(sh)
    for row in rows:
        cells = t.add_row().cells
        for i, v in enumerate(row):
            set_cell(cells[i], str(v), align=(aligns[i] if aligns else "l"), size=size)
    if widths:
        for r in t.rows:
            for i, w in enumerate(widths):
                r.cells[i].width = Cm(w)
    P(after=2)
    return t


# =====================================================================
# TRANG BIA 1
# =====================================================================
P("TRƯỜNG ĐẠI HỌC THỦ DẦU MỘT", bold=True, align="c", after=0)
P("VIỆN CÔNG NGHỆ SỐ", bold=True, align="c", after=30)
P()
P("THUYẾT MINH ĐỀ CƯƠNG", bold=True, align="c", size=16, after=2)
P("ĐỀ TÀI NGHIÊN CỨU KHOA HỌC CỦA SINH VIÊN", bold=True, align="c", size=16, after=2)
P("NĂM HỌC 2026 - 2027", bold=True, align="c", size=14, after=34)
P()
P("TÊN ĐỀ TÀI", bold=True, align="c", after=6)
P("NGHIÊN CỨU MÔ HÌNH HỌC SÂU TRIỂN KHAI TẠI BIÊN CHO DỰ BÁO SẢN LƯỢNG CONTAINER "
  "VÀ TỰ ĐỘNG HÓA CỔNG CẢNG", bold=True, align="c", size=15, after=40)
P()
P()
P("SV/ Nhóm SV:    1. Lê Hữu Anh Tài (Nhóm trưởng)", align="l", after=2, indent=2.0)
P("GVHD:    TS. Huỳnh Nguyễn Thành Luân", align="l", after=40, indent=2.0)
P()
P()
P("Thành phố Hồ Chí Minh, ngày       tháng       năm 2026", align="c")
doc.add_page_break()

# =====================================================================
# TRANG BIA 2
# =====================================================================
P("UBND THÀNH PHỐ HỒ CHÍ MINH", bold=True, align="c", after=0)
P("TRƯỜNG ĐẠI HỌC THỦ DẦU MỘT", bold=True, align="c", after=26)
P()
P("THUYẾT MINH ĐỀ TÀI", bold=True, align="c", size=16, after=2)
P("NGHIÊN CỨU KHOA HỌC SINH VIÊN", bold=True, align="c", size=16, after=4)
P("Năm học 2026 - 2027", italic=True, align="c", size=14, after=20)

# ---------------------------- MUC 1 ----------------------------
MUC(1, "Tên đề tài:")
P("Nghiên cứu mô hình học sâu triển khai tại biên cho dự báo sản lượng container và tự động hóa "
  "cổng cảng — thực nghiệm trên dữ liệu cảng biển Việt Nam.", indent=0.8)

# ---------------------------- MUC 2 ----------------------------
MUC(2, "Loại hình nghiên cứu:")
P("☐  Cơ bản                    ☑  Ứng dụng                    ☐  Triển khai", align="l", indent=0.8)

# ---------------------------- MUC 3 ----------------------------
MUC(3, "Lĩnh vực nghiên cứu:")
P("☐  Khoa học Xã hội và Nhân văn                ☑  Khoa học Kỹ thuật và Công nghệ", align="l", indent=0.8, after=2)
P("☐  Kinh tế                                                        ☐  Khoa học Tự nhiên", align="l", indent=0.8, after=2)
P("☐  Khoa học Giáo dục", align="l", indent=0.8)

# ---------------------------- MUC 4 ----------------------------
MUC(4, "Thời gian thực hiện:  06 tháng")
P("Từ tháng  10  năm  2026  đến tháng  03  năm  2027", align="l", indent=1.5)

# ---------------------------- MUC 5 ----------------------------
MUC(5, "Đơn vị quản lý về chuyên môn:")
P("Đơn vị: Viện Công nghệ số                    Khoa/Chương trình: Công nghệ thông tin",
  align="l", indent=0.8)

# ---------------------------- MUC 6 ----------------------------
MUC(6, "Giảng viên hướng dẫn:")
P("Họ và tên: Huỳnh Nguyễn Thành Luân                                    Học vị: Tiến sĩ",
  align="l", indent=0.8, after=3)
P("Đơn vị công tác (Khoa, Phòng): Viện Công nghệ số", align="l", indent=0.8, after=3)
P("Địa chỉ nhà riêng: ……………………………………………………………", align="l", indent=0.8, after=3)
P("Điện thoại nhà riêng: ………………………", align="l", indent=0.8, after=3)
P("Di động: ………………………                    E-mail: luanhnt@tdmu.edu.vn", align="l", indent=0.8)

# ---------------------------- MUC 7 ----------------------------
MUC(7, "Sinh viên/Nhóm sinh viên thực hiện đề tài:")
P("Các thành viên tham gia đề tài (không quá 05 sinh viên):", align="l", indent=0.8, after=4)
TABLE(["TT", "Họ và tên", "MSSV", "Lớp", "Ghi chú"],
      [["1", "Lê Hữu Anh Tài", "2424802010066", "……………", "Nhóm trưởng"],
       ["2", "", "", "", ""],
       ["3", "", "", "", ""]],
      widths=[1.0, 5.0, 3.6, 3.0, 3.4], aligns=["c", "l", "c", "c", "c"])
P("Thông tin SV chịu trách nhiệm chính:", bold=True, align="l", indent=0.8, after=3)
P("Họ và tên: Lê Hữu Anh Tài", align="l", indent=0.8, after=3)
P("SĐT: 0387 562 579", align="l", indent=0.8, after=3)
P("Email: lehuuanhtai2020tkh@gmail.com", align="l", indent=0.8)

# ---------------------------- MUC 8 ----------------------------
MUC(8, "Tính cấp thiết của đề tài:")
P("Container hóa là trụ cột của chuỗi cung ứng Việt Nam. Theo số liệu thống kê của Cục Hàng hải và "
  "Đường thủy Việt Nam mà nhóm nghiên cứu đã thu thập và làm sạch (66 tháng, từ 01/2021 đến 06/2026), "
  "sản lượng container thông qua hệ thống cảng biển cả nước năm 2024 đạt 30.571 nghìn TEU, duy trì đà "
  "tăng trưởng hai chữ số ở nhiều thời điểm. Vùng kinh tế trọng điểm phía Nam là địa bàn tập trung dày "
  "đặc các cảng cạn (ICD) và depot container phục vụ các khu công nghiệp.", indent=0.8)
P("Áp lực lớn nhất của các điểm thông quan này không nằm ở cầu bến mà nằm ở cổng ra vào. Kinh nghiệm của "
  "Tân cảng Cát Lái cho thấy khi áp dụng check-in điện tử bắt buộc trước 2 giờ, thời gian xe chờ tại cổng "
  "giảm từ 13 phút xuống còn 6 phút — một cải thiện có giá trị kinh tế và môi trường rõ rệt, bởi mỗi phút "
  "xe đầu kéo nổ máy chờ đều là nhiên liệu và phát thải. Tuy nhiên, các giải pháp tạo ra kết quả đó (hệ "
  "sinh thái hơn 30 phần mềm, cổng tự động nhận dạng biển số, hạ tầng máy chủ AI chuyên dụng) đòi hỏi "
  "nguồn lực mà chỉ những tập đoàn khai thác cảng lớn mới đáp ứng được.", indent=0.8)
P("Phần lớn ICD, depot và kho bãi vừa và nhỏ **không đủ điều kiện triển khai AI theo mô hình tập trung**: "
  "chi phí máy chủ GPU, chi phí đường truyền tải ảnh camera liên tục lên đám mây, và đặc biệt là thiếu "
  "nhân lực vận hành hạ tầng AI. Từ đó hình thành hai khoảng trống nghiên cứu mà đề tài hướng tới.", indent=0.8)
P("**Thứ nhất, dự báo sản lượng container Việt Nam chưa được mô hình hóa đúng với đặc thù mùa vụ bản địa.** "
  "Chuỗi sản lượng chịu chi phối mạnh bởi Tết Nguyên đán — hiệu ứng mùa vụ theo lịch Âm, trượt giữa tháng 1 "
  "và tháng 2 dương lịch qua các năm, nên các mô hình mùa vụ chu kỳ cố định 12 tháng không nắm bắt được. "
  "Trong thực nghiệm sơ bộ của nhóm (trình bày tại mục 10), mô hình mạng nơ-ron nhiều lớp (MLP) chỉ cải "
  "thiện khoảng 14,2% MAE so với dự báo ngây thơ (naive), và suy giảm nghiêm trọng khi dự báo nhiều bước: "
  "sai số MAPE tăng từ 9,06% (dự báo 1 bước) lên 20,35% (dự báo cuộn 18 tháng). Với công tác lập kế hoạch "
  "bãi và nhân lực — vốn cần tầm nhìn 3–6 tháng — mức sai số này là không dùng được.", indent=0.8)
P("**Thứ hai, học sâu tại biên (edge/on-device) cho nghiệp vụ cảng chưa được đánh giá định lượng.** Các "
  "nghiên cứu về nhận dạng biển số và mã container hiện nay hầu hết giả định có GPU máy chủ. Trong khi đó, "
  "hạ tầng suy luận ngay trên trình duyệt (ONNX Runtime Web, WebAssembly, WebGPU) đã đủ chín để chạy các "
  "mô hình nhẹ, mang lại ba lợi ích phù hợp với doanh nghiệp vừa và nhỏ: không cần máy chủ AI, dữ liệu ảnh "
  "không rời khỏi thiết bị, và vẫn hoạt động khi mất kết nối. Tuy vậy, chưa có công bố nào đánh giá độ "
  "chính xác — độ trễ của hướng tiếp cận này trên bài toán cổng cảng với điều kiện ảnh thực địa Việt Nam.",
  indent=0.8)
P("Do đó, việc nghiên cứu một bộ mô hình học sâu nhẹ, triển khai được tại biên cho hai bài toán nêu trên "
  "là hướng tiếp cận thiết thực và cấp thiết, vừa góp phần thu hẹp khoảng cách công nghệ cho doanh nghiệp "
  "logistics vừa và nhỏ, vừa bổ sung bằng chứng khoa học còn thiếu cho bối cảnh Việt Nam.", indent=0.8)

# ---------------------------- MUC 9 ----------------------------
MUC(9, "Mục tiêu đề tài:")
SUB("Mục tiêu tổng quát:")
P("Nghiên cứu, đề xuất và đánh giá thực nghiệm bộ mô hình học sâu nhẹ, triển khai được tại biên nhằm giải "
  "quyết hai bài toán lõi của cảng cạn Việt Nam: dự báo sản lượng container ngắn hạn và nhận dạng tự động "
  "phương tiện/container tại cổng.", indent=0.8)
SUB("Mục tiêu cụ thể:")
BULLET("Xây dựng bộ dữ liệu nghiên cứu: chuỗi sản lượng container Việt Nam ≥ 66 tháng có kiểm tra nhất "
       "quán, và bộ ảnh cổng thực địa ≥ 1.000 ảnh đã gán nhãn biển số / mã container.")
BULLET("Đề xuất mô hình dự báo dựa trên mô hình không gian trạng thái chọn lọc (Selective SSM) kết hợp "
       "đặc trưng lịch Âm, mục tiêu **giảm ≥ 15% MAE** so với mô hình tốt nhất hiện có của nhóm "
       "(MLP, MAE = 259,8 nghìn TEU) và **giảm ≥ 25% MAPE** ở kịch bản dự báo cuộn nhiều bước.")
BULLET("Xây dựng pipeline nhận dạng hai tầng chạy hoàn toàn trên trình duyệt, đạt độ chính xác cấp ký tự "
       "**≥ 95%** và độ trễ **≤ 1,5 giây/ảnh** trên máy tính văn phòng phổ thông không GPU rời.")
BULLET("So sánh hiệu suất các mô hình theo các chỉ số MAE, RMSE, MAPE (bài toán dự báo) và độ chính xác "
       "cấp ký tự, độ trễ suy luận (bài toán nhận dạng).")
BULLET("Tích hợp kết quả vào hệ thống SmartPort và đo hiệu quả vận hành; đề xuất hướng cải thiện và khả "
       "năng ứng dụng thực tế.")

# ---------------------------- MUC 10 ----------------------------
MUC(10, "Tổng quan tình hình nghiên cứu liên quan đến đề tài:")
SUB("a) Dự báo chuỗi thời gian bằng mô hình không gian trạng thái chọn lọc")
P("Năm 2023, Gu và Dao [1] giới thiệu Mamba — kiến trúc không gian trạng thái (State Space Model) với cơ "
  "chế chọn lọc: các tham số của phương trình trạng thái được điều biến theo từng đầu vào, cho phép mô hình "
  "“nhớ” hoặc “quên” có chủ đích. Mamba đạt chất lượng ngang hoặc vượt Transformer nhưng độ phức tạp chỉ "
  "tuyến tính O(n) thay vì bậc hai, nên phù hợp với thiết bị hạn chế tài nguyên. Hướng này nhanh chóng được "
  "mở rộng sang dự báo chuỗi thời gian và giao thông: MambaTS [2] cải tiến cơ chế quét cho dự báo dài hạn; "
  "ST-Mamba [3] và ST-MambaSync [4] áp dụng cho dự báo luồng giao thông, cho thấy khả năng nắm bắt phụ "
  "thuộc dài hạn mà không bị làm trơn quá mức. Pessoa và cộng sự [5] bổ sung định lượng độ bất định cho dự "
  "báo bằng Mamba, ghi nhận sai số trung bình khoảng 8% trên các bộ chuẩn.", indent=0.8)
SUB("b) Dự báo và tối ưu vận hành cổng cảng")
P("Sun và cộng sự [6] chứng minh hệ thống đặt lịch xe giảm đáng kể thời gian quay vòng xe tại cảng. Li và "
  "cộng sự [7] đề xuất mô hình học sâu tổ hợp dự báo lượng xe đến cảng theo khung thời gian linh hoạt từ 1 "
  "đến 24 giờ. Min và cộng sự [8] sử dụng học chuyển giao để dự báo thời gian quay vòng xe tại các cảng "
  "thiếu dữ liệu — tình huống rất gần với bối cảnh ICD Việt Nam. Chhetri và cộng sự [9] mô hình hóa thời "
  "gian lưu bãi container bằng các thuật toán học máy.", indent=0.8)
SUB("c) Nhận dạng tự động tại cổng và suy luận tại biên")
P("Hướng nhận dạng mã container đang chuyển dịch sang kiến trúc hai tầng phát hiện kết hợp đọc chữ: YOLO "
  "kết hợp TrOCR cho mã container theo chuẩn ISO 6346 [10]; hoặc tích hợp khử nhiễu sâu để giám sát phương "
  "tiện trong cảng thông minh [11]. Về mô hình đọc ký tự nhẹ, Compact Convolutional Transformer [12] cho "
  "phép Transformer hoạt động hiệu quả trên tập dữ liệu nhỏ — nền tảng của mô hình CCT-S v2 chuyên đọc biển "
  "số. Về triển khai, nghiên cứu năm 2024 [13] chỉ ra suy luận trong trình duyệt giúp phản hồi nhanh hơn và "
  "bảo vệ riêng tư tốt hơn nhờ loại bỏ vòng truyền lên đám mây; các tiến bộ về WebAssembly và WebGPU [14] "
  "tiếp tục thu hẹp khoảng cách hiệu năng so với suy luận phía máy chủ.", indent=0.8)
SUB("d) Tình hình trong nước")
P("Tại Việt Nam, chuyển đổi số cảng biển chủ yếu diễn ra ở dạng triển khai công nghiệp, ít công bố khoa "
  "học. Tổng công ty Tân cảng Sài Gòn đã vận hành hệ sinh thái eSNP với hơn 30 phần mềm, cổng tự động nhận "
  "dạng biển số độ chính xác khoảng 99%; giai đoạn 2021–2025 công bố tiết kiệm gần 600 tỷ đồng. Tuy nhiên "
  "đây là các hệ thống thương mại đóng, không công bố phương pháp, tập dữ liệu hay chỉ số đánh giá — nên "
  "không thể tái lập hoặc kế thừa trong nghiên cứu. Về công bố học thuật, nhóm nghiên cứu Trường Đại học "
  "Thủ Dầu Một đã có kinh nghiệm đưa đề tài ứng dụng AI ra hội nghị quốc tế [15].", indent=0.8)
SUB("e) Kết quả nghiên cứu sơ bộ của nhóm")
P("Nhóm đã thực hiện một vòng thực nghiệm đối chứng trên bộ dữ liệu 66 tháng: chia tập theo thời gian "
  "(36 tháng huấn luyện / 12 tháng kiểm định / 18 tháng kiểm tra), lặp lại với 20 hạt giống ngẫu nhiên và "
  "báo cáo trung bình kèm độ lệch chuẩn.", indent=0.8)
P("Bảng 1. Kết quả đối chứng trên tập kiểm tra 18 tháng (MAE, RMSE: nghìn TEU)",
  italic=True, align="c", size=12, after=4)
TABLE(["Mô hình", "MAE", "RMSE", "MAPE (%)"],
      [["Naive — lấy giá trị tháng trước", "302,9", "354,1", "10,65"],
       ["Naive theo mùa — cùng kỳ năm trước", "335,3", "388,0", "11,47"],
       ["Hồi quy tuyến tính theo thời gian", "321,5", "384,8", "10,56"],
       ["Hồi quy tuyến tính trên 3 độ trễ — AR(3)", "286,6", "331,8", "9,64"],
       ["**MLP (3 độ trễ) — tốt nhất hiện tại**", "**259,8 ± 29,6**", "**331,2**", "**9,06**"],
       ["MLP (3 độ trễ + trễ 12 + đặc trưng Tết)", "270,7 ± 71,3", "329,4", "9,33"],
       ["*Dự báo cuộn 18 tháng — MLP (3 độ trễ)*", "606,1", "782,7", "**20,35**"],
       ["*Dự báo cuộn 18 tháng — MLP + đặc trưng Tết*", "432,0", "506,9", "**14,69**"]],
      widths=[7.6, 2.9, 2.6, 2.9], aligns=["l", "c", "c", "c"])
P("Ba kết luận rút ra, định hình hướng nghiên cứu của đề tài:", indent=0.8, after=3)
BULLET("Mức cải thiện hiện tại còn khiêm tốn — mô hình học sâu tốt nhất chỉ giảm 14,2% MAE so với dự báo "
       "naive, chưa đủ thuyết phục để đưa vào quyết định vận hành. Cần kiến trúc chuyên cho chuỗi thời "
       "gian thay vì mạng nơ-ron tổng quát.")
BULLET("Dự báo nhiều bước là điểm gãy — MAPE tăng gấp hơn hai lần khi chuyển từ dự báo 1 bước sang dự báo "
       "cuộn. Đây chính là bài toán mà cơ chế trạng thái ẩn của SSM được kỳ vọng xử lý tốt hơn.")
BULLET("Đặc trưng Tết có giá trị nhưng chưa được khai thác đúng — việc thêm đặc trưng Tết làm xấu đi dự "
       "báo 1 bước (MAE 259,8 → 270,7) nhưng cải thiện mạnh dự báo cuộn (MAPE 20,35% → 14,69%). Tín hiệu "
       "mùa vụ Âm lịch là thật và đáng giá, nhưng cách mã hóa nhị phân thô sơ hiện tại chưa phù hợp. "
       "**Đây là đóng góp khoa học mang tính bản địa rõ nét nhất của đề tài.**")

# ---------------------------- MUC 11 ----------------------------
MUC(11, "Đối tượng, phạm vi nghiên cứu, cách tiếp cận và phương pháp nghiên cứu:")
SUB("Đối tượng nghiên cứu:")
BULLET("Các mô hình học sâu cho chuỗi thời gian: mô hình không gian trạng thái chọn lọc (Selective SSM / "
       "Mamba), mạng hồi tiếp (LSTM, GRU), Transformer và các mô hình đối chứng thống kê.")
BULLET("Các mô hình thị giác máy tính nhẹ cho phát hiện đối tượng và nhận dạng ký tự quang học trong điều "
       "kiện triển khai tại biên.")
BULLET("Quy trình nghiệp vụ cổng cảng cạn: tiếp nhận xe, đối chiếu lệnh giao nhận, ghi nhận container "
       "ra/vào bãi.")
SUB("Phạm vi nghiên cứu:")
BULLET("Dữ liệu dự báo: chuỗi sản lượng container thông qua cảng biển Việt Nam theo tháng, kỳ 01/2021 – "
       "06/2026 (66 tháng), nguồn Cục Hàng hải và Đường thủy Việt Nam [16].")
BULLET("Dữ liệu thị giác: ảnh chụp thực địa tại cổng ICD/depot, bổ sung ảnh từ hệ thống SmartPort do nhóm "
       "đang vận hành.")
BULLET("Mô hình phải chạy được ở chế độ suy luận trên trình duyệt của máy tính văn phòng phổ thông (CPU, "
       "không GPU rời). Giai đoạn huấn luyện vẫn thực hiện ngoại tuyến — đây là ranh giới cần nêu rõ.")
BULLET("Đề tài giới hạn ở hai bài toán nêu trên; **không đi sâu** vào phân bổ cầu bến tối ưu, lập kế hoạch "
       "xếp dỡ tàu hay điều độ cẩu.")
SUB("Cách tiếp cận:")
P("Tiếp cận theo hướng nghiên cứu thực nghiệm có đối chứng, gắn với hệ thống thật. Thay vì xây dựng mô "
  "hình trên dữ liệu mô phỏng, đề tài sử dụng dữ liệu thống kê chính thức của cơ quan quản lý nhà nước và "
  "dữ liệu vận hành từ hệ thống SmartPort, rồi đưa mô hình quay ngược trở lại hệ thống để đo hiệu quả thực "
  "tế. Nguyên tắc xuyên suốt: mọi cải tiến phải được chứng minh bằng chỉ số định lượng so với đường cơ sở "
  "công bố công khai.", indent=0.8)
SUB("Phương pháp nghiên cứu:")
BULLET("**Phương pháp nghiên cứu tài liệu:** tổng hợp, phân tích các công bố quốc tế giai đoạn 2022–2026 "
       "về SSM/Mamba, dự báo vận hành cảng, nhận dạng tại cổng và suy luận tại biên.")
BULLET("**Phương pháp thu thập và xử lý dữ liệu:** thu thập báo cáo thống kê tháng; xử lý theo bộ quy tắc "
       "làm sạch đã thiết lập (lấy hiệu lũy kế thay vì số ước thực hiện, ưu tiên bản công bố muộn nhất đã "
       "hiệu chỉnh, kiểm tra nhất quán tổng xuất – nhập – nội địa); gán nhãn ảnh theo chuẩn ISO 6346.")
BULLET("**Phương pháp mô phỏng – thực nghiệm:** huấn luyện mô hình bằng PyTorch, triển khai suy luận bằng "
       "ONNX Runtime Web; tách tập theo thời gian để tránh rò rỉ dữ liệu tương lai, lặp nhiều hạt giống.")
BULLET("**Phương pháp phân tích – so sánh:** đối chứng với đầy đủ đường cơ sở (naive, naive theo mùa, "
       "ARIMA/SARIMA, AR, MLP, LSTM, GRU, Transformer); nghiên cứu loại trừ (ablation) để tách riêng đóng "
       "góp của từng thành phần đề xuất, tránh kết luận sai khi cải thiện chỉ do tăng số tham số.")
BULLET("**Phương pháp đo hiệu năng triển khai:** đo độ trễ suy luận, dung lượng mô hình, mức tiêu thụ bộ "
       "nhớ; lặp phép đo nhiều lần và loại bỏ lần chạy khởi động.")

# ---------------------------- MUC 12 ----------------------------
MUC(12, "Nội dung nghiên cứu và tiến độ thực hiện:")
SUB("12.1. Nội dung nghiên cứu (trình bày dưới dạng đề cương nghiên cứu chi tiết)")
for ch, subs in [
    ("CHƯƠNG 1: GIỚI THIỆU TỔNG QUAN",
     ["1.1. Lý do chọn đề tài", "1.2. Tính cấp thiết",
      "1.3. Mục tiêu và nhiệm vụ nghiên cứu", "1.4. Đối tượng và phạm vi nghiên cứu",
      "1.5. Phương pháp tiếp cận"]),
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
    CHUONG(ch, bold=True, indent=0.8, after=2)
    for s in subs:
        CHUONG(s, bold=False, indent=1.5, after=1)
    P(after=2)

SUB("12.2. Tiến độ thực hiện")
TABLE(["Thời gian", "Các nội dung, công việc thực hiện", "Sản phẩm", "Người thực hiện"],
      [["Tháng 10/2026",
        "Khảo sát, tổng quan tài liệu; chuẩn hóa khung đánh giá và chốt các đường cơ sở đối chứng",
        "Báo cáo tổng quan; danh mục tài liệu tham khảo", "Lê Hữu Anh Tài"],
       ["Tháng 11/2026",
        "Hoàn thiện bộ dữ liệu chuỗi sản lượng; thu thập và gán nhãn bộ ảnh cổng thực địa",
        "Bộ dữ liệu kèm tài liệu mô tả và quy tắc làm sạch", "Lê Hữu Anh Tài"],
       ["Tháng 12/2026",
        "Cài đặt mô hình SSM chọn lọc có nhận biết lịch Âm; thiết kế đặc trưng lịch; huấn luyện và tinh chỉnh",
        "Mô hình dự báo hoàn chỉnh; mã nguồn thực nghiệm", "Lê Hữu Anh Tài"],
       ["Tháng 01/2027",
        "Thực nghiệm đối chứng đầy đủ với các đường cơ sở; nghiên cứu loại trừ (ablation)",
        "Bảng kết quả đối chứng; báo cáo phân tích", "Lê Hữu Anh Tài"],
       ["Tháng 02/2027",
        "Tinh chỉnh pipeline nhận dạng hai tầng, đo độ chính xác và độ trễ; tích hợp vào hệ thống SmartPort và đo hiệu quả vận hành",
        "Mô hình đã tinh chỉnh; bản tích hợp chạy được", "Lê Hữu Anh Tài"],
       ["Tháng 03/2027",
        "Viết bài báo khoa học; hoàn thiện báo cáo tổng kết và hồ sơ nghiệm thu",
        "Bản thảo bài báo; báo cáo tổng kết đề tài", "Lê Hữu Anh Tài"]],
      widths=[3.4, 6.4, 4.0, 2.6], aligns=["l", "l", "l", "l"])

# ---------------------------- MUC 13 ----------------------------
MUC(13, "Sản phẩm và khả năng ứng dụng:")
SUB("Sản phẩm đạt được:")
BULLET("Mô hình dự báo sản lượng container dựa trên SSM chọn lọc kết hợp đặc trưng lịch Âm, kèm mã nguồn "
       "thực nghiệm và bảng kết quả đối chứng.")
BULLET("Pipeline nhận dạng biển số xe và mã container chạy hoàn toàn trên trình duyệt, kèm báo cáo đo độ "
       "chính xác và độ trễ.")
BULLET("Bộ dữ liệu nghiên cứu: chuỗi sản lượng ≥ 66 tháng có kiểm tra nhất quán và bộ ảnh cổng ≥ 1.000 ảnh "
       "đã gán nhãn, kèm tài liệu mô tả cho phép tái lập.")
BULLET("Mô-đun tích hợp trên hệ thống SmartPort kèm hướng dẫn sử dụng.")
BULLET("01 bài báo khoa học gửi hội nghị/tạp chí chuyên ngành công nghệ thông tin và 01 báo cáo tổng kết "
       "đề tài theo quy định.")
SUB("Khả năng ứng dụng:")
P("Kết quả đề tài có thể triển khai trực tiếp tại các cảng cạn (ICD), depot container và doanh nghiệp kho "
  "vận vừa và nhỏ: dự báo sản lượng hỗ trợ lập kế hoạch bãi và nhân lực; nhận dạng tự động tại cổng giúp "
  "rút ngắn thời gian xe chờ, giảm nhiên liệu tiêu hao và khí thải. Do mô hình chạy trên thiết bị sẵn có, "
  "chi phí triển khai gần như chỉ còn chi phí phần mềm, hạ thấp rào cản công nghệ cho nhóm doanh nghiệp "
  "vừa và nhỏ; việc dữ liệu ảnh không rời khỏi thiết bị cũng giải quyết e ngại về bảo mật thông tin khách "
  "hàng. Ngoài ra, hướng tiếp cận đặc trưng lịch Âm có thể áp dụng cho nhiều chuỗi dữ liệu kinh tế Việt Nam "
  "khác chịu ảnh hưởng của Tết Nguyên đán. Đề tài cũng là học liệu minh họa cho các học phần Trí tuệ nhân "
  "tạo, Học máy và Hệ thống thông tin tại Viện Công nghệ số.", indent=0.8)

# ---------------------------- MUC 14 ----------------------------
MUC(14, "Tài liệu tham khảo")
refs = [
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
    "Chrome Developers, “WebAssembly and WebGPU enhancements for faster Web AI,” 2024. [Trực tuyến]. Địa chỉ: https://developer.chrome.com",
    "V. Nguyen, L. N. T. Huynh, and T. Vo, “Optimizing Customer Feedback Analysis with BERT-Based Sentiment Classification: A Case Study of Toyota Dong Sai Gon,” in Future Data and Security Engineering (FDSE 2024), CCIS vol. 2309, Springer, 2024, pp. 269–281.",
    "Cục Hàng hải và Đường thủy Việt Nam, “Thống kê sản lượng hàng hóa thông qua cảng biển,” 2021–2026. [Trực tuyến]. Địa chỉ: https://vimawa.gov.vn/vi/thong-ke",
]
for i, r in enumerate(refs, 1):
    p = doc.add_paragraph()
    p.paragraph_format.line_spacing = 1.3
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.left_indent = Cm(1.6)
    p.paragraph_format.first_line_indent = Cm(-1.0)
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    add_runs(p, "[%d]  %s" % (i, r), size=12)

# ---------------------------- MUC 15 ----------------------------
MUC(15, "Kinh phí thực hiện đề tài:")
P("Kinh phí thực hiện (đồng): **4.950.000 đồng**", align="l", indent=0.8, after=3)
P("Bằng chữ: **Bốn triệu chín trăm năm mươi nghìn đồng**", align="l", indent=0.8, after=6)
P("Dự toán chi tiết:", italic=True, align="l", indent=0.8, after=4)
TABLE(["TT", "Nội dung chi", "Diễn giải", "Thành tiền (đồng)"],
      [["1", "Chi công lao động chuyên môn", "Thù lao thực hiện các nội dung nghiên cứu", "2.000.000"],
       ["2", "Chi thu thập và xử lý số liệu",
        "Khảo sát thực địa tại ICD/depot; công gán nhãn bộ ảnh ≥ 1.000 ảnh", "1.200.000"],
       ["3", "Chi thuê dịch vụ kỹ thuật",
        "Thuê tài nguyên tính toán đám mây phục vụ huấn luyện mô hình (theo giờ)", "800.000"],
       ["4", "Chi công bố khoa học", "Lệ phí gửi bài; in ấn tài liệu trình bày hội nghị", "450.000"],
       ["5", "Chi văn phòng phẩm, in ấn", "In đề cương, báo cáo tiến độ, báo cáo tổng kết", "500.000"],
       ["", "**TỔNG CỘNG**", "", "**4.950.000**"]],
      widths=[1.0, 4.4, 7.6, 3.0], aligns=["c", "l", "l", "r"])

# =====================================================================
# KY TEN — 3 BEN
# =====================================================================
P(after=10)
t1 = doc.add_table(rows=2, cols=2)
t1.alignment = WD_TABLE_ALIGNMENT.CENTER
set_cell(t1.rows[0].cells[0], "Giảng viên hướng dẫn đề tài\n(Ký, ghi rõ họ tên)", bold=True, align="c", size=13)
set_cell(t1.rows[0].cells[1],
         "Ngày …… tháng …… năm 20……\nSinh viên chịu trách nhiệm chính\n(Ký, ghi rõ họ tên)",
         bold=True, align="c", size=13)
set_cell(t1.rows[1].cells[0], "\n\n\nHuỳnh Nguyễn Thành Luân", bold=True, align="c", size=13)
set_cell(t1.rows[1].cells[1], "\n\n\nLê Hữu Anh Tài", bold=True, align="c", size=13)
for r in t1.rows:
    for c in r.cells:
        c.width = Cm(8)

P(after=14)
P("Thành phố Hồ Chí Minh, ngày …… tháng …… năm 20……", align="c", after=2)
P("TRƯỞNG ĐƠN VỊ", bold=True, align="c", after=2)
P("(Ký, ghi rõ họ tên)", italic=True, align="c", size=12)

doc.save(OUT)
print("DA LUU:", OUT)
