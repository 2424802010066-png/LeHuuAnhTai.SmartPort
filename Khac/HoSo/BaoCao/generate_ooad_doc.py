# -*- coding: utf-8 -*-
"""
Script tao file Word Bao cao Thuc hanh Phan tich Thiet ke Huong doi tuong (OOAD)
Tich hop day du 5 So do khoa hoc (Architecture, Use Case, Activity, Class Diagram, Physical ERD)
De tai: He thong Quan ly va Dieu hanh Cang bien Thong minh (SmartPort)
Sinh vien: Le Huu Anh Tai - MSSV: 2424802010066 - Dai hoc Thu Dau Mot
"""

import os
import docx
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import parse_xml
from docx.oxml.ns import nsdecls

def set_cell_background(cell, fill_hex):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = parse_xml(f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>')
    tcPr.append(shd)

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = parse_xml(f'<w:tcMar {nsdecls("w")}><w:top w:w="{top}" w:type="dxa"/><w:bottom w:w="{bottom}" w:type="dxa"/><w:left w:w="{left}" w:type="dxa"/><w:right w:w="{right}" w:type="dxa"/></w:tcMar>')
    tcPr.append(tcMar)

def set_table_borders(table, color="D3D3D3"):
    tblPr = table._tbl.tblPr
    borders = parse_xml(
        f'<w:tblBorders {nsdecls("w")}>'
        f'<w:top w:val="single" w:sz="4" w:space="0" w:color="{color}"/>'
        f'<w:bottom w:val="single" w:sz="4" w:space="0" w:color="{color}"/>'
        f'<w:insideH w:val="single" w:sz="4" w:space="0" w:color="{color}"/>'
        f'<w:insideV w:val="single" w:sz="4" w:space="0" w:color="{color}"/>'
        f'<w:left w:val="none"/>'
        f'<w:right w:val="none"/>'
        f'</w:tblBorders>'
    )
    tblPr.append(borders)

def build_word_doc():
    doc = Document()
    
    # Page Setup: A4, standard thesis margins: Left 3cm, Right 2cm, Top 2cm, Bottom 2cm
    for section in doc.sections:
        section.top_margin = Inches(0.79)     # ~2.0 cm
        section.bottom_margin = Inches(0.79)  # ~2.0 cm
        section.left_margin = Inches(1.18)    # ~3.0 cm
        section.right_margin = Inches(0.79)   # ~2.0 cm
        section.page_width = Inches(8.27)     # A4
        section.page_height = Inches(11.69)
        
    # Default Normal style
    normal_style = doc.styles['Normal']
    normal_style.font.name = 'Times New Roman'
    normal_style.font.size = Pt(13)
    normal_style.font.color.rgb = RGBColor(30, 30, 30)
    normal_style.paragraph_format.line_spacing = 1.3
    normal_style.paragraph_format.space_after = Pt(6)

    # Colors
    NAVY = RGBColor(10, 47, 87)       # #0A2F57
    ORANGE = RGBColor(220, 80, 20)
    GRAY = RGBColor(100, 100, 100)
    DARK_BLUE = "0A2F57"
    LIGHT_BG = "F4F6F9"
    CALLOUT_BG = "EBF3FA"

    img_base_dir = r"C:\Users\ASUS\Downloads\SmartPort\BaoCao\img-doc"

    # Helpers
    def add_p(text, align=WD_ALIGN_PARAGRAPH.JUSTIFY, bold=False, italic=False, size=13, color=None, space_after=6, space_before=0, first_indent=0.3):
        p = doc.add_paragraph()
        p.alignment = align
        p.paragraph_format.space_after = Pt(space_after)
        p.paragraph_format.space_before = Pt(space_before)
        if first_indent > 0 and align in [WD_ALIGN_PARAGRAPH.JUSTIFY, WD_ALIGN_PARAGRAPH.LEFT]:
            p.paragraph_format.first_line_indent = Inches(first_indent)
        run = p.add_run(text)
        run.bold = bold
        run.italic = italic
        run.font.name = 'Times New Roman'
        run.font.size = Pt(size)
        if color:
            run.font.color.rgb = color
        return p

    def add_h1(text):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.space_before = Pt(16)
        p.paragraph_format.space_after = Pt(8)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.bold = True
        run.font.name = 'Times New Roman'
        run.font.size = Pt(15)
        run.font.color.rgb = NAVY
        return p

    def add_h2(text):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.space_before = Pt(12)
        p.paragraph_format.space_after = Pt(6)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.bold = True
        run.font.name = 'Times New Roman'
        run.font.size = Pt(13.5)
        run.font.color.rgb = NAVY
        return p

    def add_h3(text):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.space_before = Pt(8)
        p.paragraph_format.space_after = Pt(4)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.bold = True
        run.italic = True
        run.font.name = 'Times New Roman'
        run.font.size = Pt(13)
        run.font.color.rgb = RGBColor(40, 40, 40)
        return p

    def add_bullet(bold_prefix, text):
        p = doc.add_paragraph(style='List Bullet')
        p.paragraph_format.space_after = Pt(4)
        p.paragraph_format.line_spacing = 1.25
        run1 = p.add_run(bold_prefix)
        run1.bold = True
        run1.font.name = 'Times New Roman'
        run1.font.size = Pt(13)
        run2 = p.add_run(text)
        run2.font.name = 'Times New Roman'
        run2.font.size = Pt(13)
        return p

    def add_callout(text, italic=True):
        tbl = doc.add_table(rows=1, cols=1)
        tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
        cell = tbl.cell(0, 0)
        set_cell_background(cell, CALLOUT_BG)
        set_cell_margins(cell, top=140, bottom=140, left=200, right=180)
        
        tcPr = cell._tc.get_or_add_tcPr()
        borders = parse_xml(
            f'<w:tcBorders {nsdecls("w")}>'
            f'<w:left w:val="single" w:sz="24" w:space="0" w:color="{DARK_BLUE}"/>'
            f'<w:top w:val="none"/>'
            f'<w:right w:val="none"/>'
            f'<w:bottom w:val="none"/>'
            f'</w:tcBorders>'
        )
        tcPr.append(borders)
        
        p = cell.paragraphs[0]
        p.paragraph_format.space_before = Pt(2)
        p.paragraph_format.space_after = Pt(2)
        p.paragraph_format.line_spacing = 1.3
        run = p.add_run(text)
        run.italic = italic
        run.font.name = 'Times New Roman'
        run.font.size = Pt(12.5)
        run.font.color.rgb = RGBColor(20, 35, 60)
        
        doc.add_paragraph().paragraph_format.space_after = Pt(4)

    def add_diagram(filename, caption, width_inches=6.2):
        file_path = os.path.join(img_base_dir, filename)
        if not os.path.exists(file_path):
            file_path = os.path.join(r"C:\Users\ASUS\Downloads\SmartPort\BaoCao\diagrams", filename)
            
        if os.path.exists(file_path):
            p_img = doc.add_paragraph()
            p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p_img.paragraph_format.space_before = Pt(10)
            p_img.paragraph_format.space_after = Pt(4)
            p_img.paragraph_format.keep_with_next = True
            run_img = p_img.add_run()
            run_img.add_picture(file_path, width=Inches(width_inches))
            
            p_cap = doc.add_paragraph()
            p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p_cap.paragraph_format.space_before = Pt(2)
            p_cap.paragraph_format.space_after = Pt(12)
            run_cap = p_cap.add_run(caption)
            run_cap.bold = True
            run_cap.italic = True
            run_cap.font.name = 'Times New Roman'
            run_cap.font.size = Pt(11)
            run_cap.font.color.rgb = NAVY
        else:
            print(f"Warning: Diagram image not found at {file_path}")

    # =============================================================
    # 1. TRANG BÌA (COVER PAGE)
    # =============================================================
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(10)
    p.paragraph_format.space_after = Pt(2)
    r = p.add_run("ỦY BAN NHÂN DÂN TỈNH BÌNH DƯƠNG\n")
    r.font.name = 'Times New Roman'; r.font.size = Pt(12); r.bold = True
    r2 = p.add_run("TRƯỜNG ĐẠI HỌC THỦ DẦU MỘT\n")
    r2.font.name = 'Times New Roman'; r2.font.size = Pt(13); r2.bold = True; r2.font.color.rgb = NAVY
    r3 = p.add_run("KHOA CÔNG NGHỆ THÔNG TIN")
    r3.font.name = 'Times New Roman'; r3.font.size = Pt(12); r3.bold = True

    p_line = doc.add_paragraph()
    p_line.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_line.paragraph_format.space_after = Pt(18)
    r_line = p_line.add_run("─────── *** ───────")
    r_line.font.name = 'Times New Roman'; r_line.font.size = Pt(11); r_line.font.color.rgb = GRAY

    logo_path = os.path.join(img_base_dir, "logo-tdmu.png")
    if os.path.exists(logo_path):
        p_logo = doc.add_paragraph()
        p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_logo.paragraph_format.space_after = Pt(18)
        p_logo.add_run().add_picture(logo_path, width=Inches(1.5))

    p_report = doc.add_paragraph()
    p_report.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_report.paragraph_format.space_before = Pt(12)
    p_report.paragraph_format.space_after = Pt(8)
    r_rep = p_report.add_run("BÁO CÁO THỰC HÀNH\nPHÂN TÍCH VÀ THIẾT KẾ HƯỚNG ĐỐI TƯỢNG (OOAD)")
    r_rep.font.name = 'Times New Roman'; r_rep.font.size = Pt(16); r_rep.bold = True; r_rep.font.color.rgb = NAVY

    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_title.paragraph_format.space_before = Pt(15)
    p_title.paragraph_format.space_after = Pt(25)
    r_dt = p_title.add_run("ĐỀ TÀI:\n")
    r_dt.font.name = 'Times New Roman'; r_dt.font.size = Pt(13); r_dt.bold = True
    r_tname = p_title.add_run("HỆ THỐNG QUẢN LÝ VÀ ĐIỀU HÀNH\nCẢNG BIỂN THÔNG MINH (SMARTPORT)")
    r_tname.font.name = 'Times New Roman'; r_tname.font.size = Pt(17); r_tname.bold = True; r_tname.font.color.rgb = ORANGE

    info_tbl = doc.add_table(rows=3, cols=2)
    info_tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    info_tbl.autofit = False
    
    col_widths = [Inches(2.5), Inches(3.5)]
    for row in info_tbl.rows:
        for idx, width in enumerate(col_widths):
            row.cells[idx].width = width

    info_data = [
        ("Sinh viên thực hiện:", "LÊ HỮU ANH TÀI"),
        ("Mã số sinh viên (MSSV):", "2424802010066"),
        ("Ngành đào tạo:", "Công nghệ Thông tin / Kỹ thuật Phần mềm")
    ]
    for idx, (label, val) in enumerate(info_data):
        c0 = info_tbl.cell(idx, 0); c1 = info_tbl.cell(idx, 1)
        p0 = c0.paragraphs[0]; p0.paragraph_format.space_after = Pt(4); p0.paragraph_format.line_spacing = 1.2
        r0 = p0.add_run(label); r0.bold = True; r0.font.size = Pt(12.5)
        p1 = c1.paragraphs[0]; p1.paragraph_format.space_after = Pt(4); p1.paragraph_format.line_spacing = 1.2
        r1 = p1.add_run(val); r1.bold = (idx < 2); r1.font.size = Pt(12.5)
        if idx == 0:
            r1.font.color.rgb = NAVY

    p_bot = doc.add_paragraph()
    p_bot.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_bot.paragraph_format.space_before = Pt(55)
    p_bot.paragraph_format.space_after = Pt(0)
    r_bot = p_bot.add_run("BÌNH DƯƠNG — THÁNG 09/2026")
    r_bot.font.name = 'Times New Roman'; r_bot.font.size = Pt(12); r_bot.bold = True

    doc.add_page_break()

    # =============================================================
    # 2. LỜI MỞ ĐẦU & TỔNG QUAN
    # =============================================================
    add_h1("LỜI NÓI ĐẦU VÀ CAM ĐOAN DỮ LIỆU")
    
    add_callout(
        "Tài liệu này được rút ra trực tiếp từ mã nguồn đang vận hành (SmartPort.Net10) "
        "và cơ sở dữ liệu thực tế SmartPortDB, hoàn toàn không phải là mô hình giả định. "
        "Mọi con số trong báo cáo (40 lớp thực thể, 99 chức năng nghiệp vụ, 3 vai trò người dùng, "
        "các chu trình trạng thái và quy tắc ràng buộc) đều tương ứng chính xác 1-1 với mã nguồn "
        "và hệ thống 325 bài kiểm thử tự động của đồ án.",
        italic=True
    )

    add_p(
        "Báo cáo này được biên soạn phục vụ cho học phần Thực hành Phân tích và Thiết kế Hướng đối tượng (OOAD). "
        "Mục tiêu cốt lõi là áp dụng phương pháp luận hướng đối tượng để mô hình hóa toàn diện hệ thống điều hành "
        "cảng cạn (ICD) và bãi container (Depot), giải quyết triệt để bài toán rời rạc dữ liệu và tự động hóa điều hành logistics."
    )

    # =============================================================
    # CHƯƠNG 1: MÔ TẢ HỆ THỐNG
    # =============================================================
    add_h1("1. MÔ TẢ HỆ THỐNG")

    add_h2("1.1. Bối cảnh bài toán")
    add_p(
        "Tại Việt Nam, các depot và cảng cạn (ICD) hiện nay phần lớn vẫn đang vận hành theo một mô thức chung: "
        "dữ liệu bị chia cắt thành từng 'ốc đảo' cục bộ. Cụ thể:"
    )
    add_bullet("Bảo vệ tại chốt cổng: ", "Ghi chép biển số xe và giờ ra vào bằng sổ tay thủ công.")
    add_bullet("Trạm cân điện tử: ", "In phiếu cân giấy và kẹp vào hồ sơ chứng từ riêng lẻ.")
    add_bullet("Bộ phận kế toán / thương vụ: ", "Mở bảng tính Excel riêng để đối soát và tự tính toán phí lưu bãi.")
    add_bullet("Hãng tàu đối tác: ", "Yêu cầu báo cáo tình trạng container qua email, nhân viên cảng phải gõ tay từng dòng.")

    add_p(
        "Hệ quả thực tế đo lường được là: khi chủ hàng thắc mắc 'container của tôi đang nằm ở vị trí nào, "
        "đang phát sinh bao nhiêu tiền nợ lưu bãi?', nhân viên không thể trả lời ngay mà phải lục tìm ở ba nơi khác nhau. "
        "Nghiêm trọng hơn, tiền phí lưu bãi thường xuyên bị 'treo ngoài bãi' mà không ai nắm được con số chính xác, "
        "bởi vì số tiền chỉ hiển thị tạm tính trên màn hình chứ không được chốt thành phiếu thu chính thức có giá trị pháp lý."
    )
    add_p(
        "Mặc dù các hệ thống TOS quốc tế giải quyết được bài toán này, nhưng chi phí bản quyền và yêu cầu đầu tư hạ tầng "
        "vượt xa khả năng chi trả của các doanh nghiệp logistics vừa và nhỏ (SMEs) tại Việt Nam."
    )

    add_h2("1.2. Định vị sản phẩm")
    add_callout(
        "SmartPort là giải pháp phần mềm điều hành depot/ICD dành riêng cho doanh nghiệp vừa và nhỏ Việt Nam "
        "— tận dụng hạ tầng camera sẵn có, và ứng dụng AI không chỉ dừng lại ở mức 'nhìn nhận diện' "
        "mà có khả năng 'tự hành động' hỗ trợ vận hành.",
        italic=False
    )

    p_t1 = add_p("Bảng 1.1: Ba điểm tựa công nghệ giải quyết ba nỗi đau vận hành", align=WD_ALIGN_PARAGRAPH.CENTER, bold=True, size=11.5)
    t1 = doc.add_table(rows=4, cols=2)
    t1.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_borders(t1)
    
    headers = ["Điểm tựa giải pháp SmartPort", "Vấn đề thực tế được giải quyết triệt để"]
    for i, h in enumerate(headers):
        c = t1.cell(0, i)
        set_cell_background(c, DARK_BLUE)
        set_cell_margins(c, top=120, bottom=120, left=150, right=150)
        p = c.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(h); r.bold = True; r.font.color.rgb = RGBColor(255, 255, 255); r.font.size = Pt(11.5)

    rows_data1 = [
        ("Liên kết dữ liệu toàn hệ thống — Xây dựng một dòng thời gian (Timeline 360°) duy nhất cho mỗi container", "Chấm dứt hoàn toàn tình trạng 'ốc đảo dữ liệu' giữa các phòng ban"),
        ("Đồng hồ lưu bãi thời gian thực → Chốt kỳ thành phiếu thu có mã VietQR động", "Thu hồi dòng tiền, không để tiền cước lưu bãi bị treo vô định ngoài bãi"),
        ("Agent tự hành động — Tự động lập phiếu, sinh bản tin EDI, có kèm lý do minh bạch và nút hoàn tác", "Xóa bỏ công việc lặp đi lặp lại hàng ngày, loại trừ sai sót do nhập liệu tay")
    ]
    for r_idx, (col0, col1) in enumerate(rows_data1, start=1):
        c0 = t1.cell(r_idx, 0); c1 = t1.cell(r_idx, 1)
        if r_idx % 2 == 1:
            set_cell_background(c0, LIGHT_BG); set_cell_background(c1, LIGHT_BG)
        set_cell_margins(c0, top=100, bottom=100, left=140, right=140)
        set_cell_margins(c1, top=100, bottom=100, left=140, right=140)
        p0 = c0.paragraphs[0]; r0 = p0.add_run(col0); r0.font.size = Pt(11.5); r0.bold = True
        p1 = c1.paragraphs[0]; r1 = p1.add_run(col1); r1.font.size = Pt(11.5)

    doc.add_paragraph().paragraph_format.space_after = Pt(4)

    add_h2("1.3. Đặc trưng kỹ thuật nổi bật")
    add_bullet("Trí tuệ nhân tạo suy luận ngay trên trình duyệt (In-browser AI): ", 
               "Các mô hình học sâu như YOLO26 (nhận diện vật thể), CCT-S v2 (nhận dạng ký tự quang học OCR), "
               "MediaPipe (phát hiện khung xương, cảnh báo tài xế) chạy trực tiếp thông qua ONNX Runtime Web + WebGPU "
               "ngay tại thiết bị của người dùng. Hệ quả mang lại giá trị cao: không đòi hỏi máy chủ GPU đắt tiền, "
               "không gửi dữ liệu video nhạy cảm ra ngoài đám mây, và hệ thống duy trì hoạt động bình thường ngay cả khi mất kết nối Internet.")
    
    add_bullet("Agent hành động dựa trên luật nghiệp vụ (Rule-based Agent): ", 
               "Đây là quyết định thiết kế có chủ đích. Do Agent can thiệp trực tiếp vào tài chính của khách hàng "
               "(lập phiếu thu tiền) và tự gửi bản tin đối soát tới hãng tàu quốc tế, mỗi hành vi của Agent phải hoàn toàn xác định, "
               "minh bạch và giải trình được trước kiểm toán độc lập, thay vì sử dụng các mô hình ngôn ngữ lớn (LLM) có xác suất ảo giác.")

    add_h2("1.4. Nền tảng và Công nghệ triển khai")
    
    p_t2 = add_p("Bảng 1.2: Bảng phân công công nghệ nền tảng hệ thống SmartPort", align=WD_ALIGN_PARAGRAPH.CENTER, bold=True, size=11.5)
    t2 = doc.add_table(rows=8, cols=2)
    t2.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_borders(t2)

    headers2 = ["Thành phần kiến trúc", "Công nghệ và Phiên bản sử dụng"]
    for i, h in enumerate(headers2):
        c = t2.cell(0, i)
        set_cell_background(c, DARK_BLUE)
        set_cell_margins(c, top=120, bottom=120, left=150, right=150)
        p = c.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(h); r.bold = True; r.font.color.rgb = RGBColor(255, 255, 255); r.font.size = Pt(11.5)

    tech_data = [
        ("Ứng dụng Web chính (Core Backend)", "ASP.NET Core MVC trên nền tảng .NET 10 hiện đại nhất"),
        ("Tầng truy cập dữ liệu (ORM)", "Entity Framework Core 10 (Phương pháp Code-First, Data Annotations)"),
        ("Hệ quản trị cơ sở dữ liệu (DBMS)", "Microsoft SQL Server — Database SmartPortDB với 40 bảng thực thể"),
        ("Giao diện người dùng (Frontend)", "Razor View Engine kết hợp Bootstrap 5.3, Bootstrap Icons và Chart.js"),
        ("AI phía trình duyệt (Client-side AI)", "ONNX Runtime Web (WebGPU), TensorFlow.js, MediaPipe, Tesseract.js"),
        ("Ứng dụng di động bổ trợ (Mobile App)", ".NET MAUI đa nền tảng (Biên dịch trực tiếp file SmartPort-App-Android.apk)"),
        ("Kiểm thử chất lượng phần mềm (Testing)", "Bộ 325 ca kiểm thử tự động, khẳng định trực tiếp trên số dòng và số tiền trong CSDL")
    ]
    for r_idx, (col0, col1) in enumerate(tech_data, start=1):
        c0 = t2.cell(r_idx, 0); c1 = t2.cell(r_idx, 1)
        if r_idx % 2 == 1:
            set_cell_background(c0, LIGHT_BG); set_cell_background(c1, LIGHT_BG)
        set_cell_margins(c0, top=100, bottom=100, left=140, right=140)
        set_cell_margins(c1, top=100, bottom=100, left=140, right=140)
        p0 = c0.paragraphs[0]; r0 = p0.add_run(col0); r0.font.size = Pt(11.5); r0.bold = True
        p1 = c1.paragraphs[0]; r1 = p1.add_run(col1); r1.font.size = Pt(11.5)

    doc.add_paragraph().paragraph_format.space_after = Pt(6)

    # =============================================================
    # CHƯƠNG 2: PHẠM VI HỆ THỐNG
    # =============================================================
    add_h1("2. PHẠM VI HỆ THỐNG")

    add_h2("2.1. Phạm vi nghiệp vụ — Mô hình Gate-to-Gate khép kín")
    add_p(
        "SmartPort bao quát trọn vẹn toàn bộ vòng đời của một lượt giao nhận hàng hóa, "
        "từ thời điểm khách hàng lập lệnh đặt dịch vụ trực tuyến đến khi phương tiện hoàn tất thủ tục rời cổng. "
        "Quy trình được chuẩn hóa thành 12 giai đoạn nghiệp vụ rõ ràng:"
    )

    p_t3 = add_p("Bảng 2.1: Bảng 12 giai đoạn trong mô hình quản lý Gate-to-Gate", align=WD_ALIGN_PARAGRAPH.CENTER, bold=True, size=11.5)
    t3 = doc.add_table(rows=13, cols=3)
    t3.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_borders(t3)

    headers3 = ["Giai đoạn", "Tên quy trình nghiệp vụ", "Tác nhân chính tham gia"]
    for i, h in enumerate(headers3):
        c = t3.cell(0, i)
        set_cell_background(c, DARK_BLUE)
        set_cell_margins(c, top=120, bottom=120, left=120, right=120)
        p = c.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(h); r.bold = True; r.font.color.rgb = RGBColor(255, 255, 255); r.font.size = Pt(11)

    stages_data = [
        ("Giai đoạn 1", "Khách hàng đăng ký tài khoản doanh nghiệp và đặt dịch vụ ePort trực tuyến", "Khách hàng (Customer)"),
        ("Giai đoạn 2", "Thanh toán cước dịch vụ trước (Chuyển khoản / VietQR động)", "Khách hàng · Nhân viên thu ngân"),
        ("Giai đoạn 3", "Tạo và phát hành Lệnh giao nhận điện tử (eGate Pass kèm số lệnh + PIN)", "Nhân viên điều độ cảng"),
        ("Giai đoạn 4", "Kế hoạch điều độ tàu, chuyến tàu và thuật toán gợi ý phân bổ cầu bến", "Nhân viên điều độ cảng"),
        ("Giai đoạn 5", "Kiểm soát cổng vào (Gate-In): Nhận dạng biển số 3 tầng, tự đối chiếu lệnh", "Hệ thống tự động · Nhân viên cổng"),
        ("Giai đoạn 6", "Trạm cân điện tử lần 1 (Cân tổng tải trọng phục vụ xác thực VGM)", "Nhân viên trạm cân"),
        ("Giai đoạn 7", "Hạ bãi container: Thuật toán AI gợi ý tọa độ Block–Bay–Row–Tier tối ưu", "Nhân viên điều độ bãi (Yard)"),
        ("Giai đoạn 8", "Giám sát an ninh bãi 24/7: Nhận diện lửa, người ngã, đột nhập, lái xe mất tập trung", "Hệ thống AI giám sát tự động"),
        ("Giai đoạn 9", "Hồ sơ container 360°: Hợp nhất lịch sử, nhiệt độ cont lạnh, phiếu sửa chữa EOR", "Điều độ viên · Giao nhận"),
        ("Giai đoạn 10", "Chứng từ giao nhận: Tự động phát hành phiếu EIR song ngữ và ghi sổ kho", "Nhân viên giao nhận thương vụ"),
        ("Giai đoạn 11", "Kiểm soát cổng ra (Gate-Out): Cân lần 2, kiểm tra nợ phí, khép vòng đời lệnh", "Hệ thống tự động · Nhân viên cổng"),
        ("Giai đoạn 12", "Hậu kiểm, đối soát số đếm xe chống gian lận và xuất báo cáo quản trị", "Quản trị viên hệ thống (Admin)")
    ]
    for r_idx, (col0, col1, col2) in enumerate(stages_data, start=1):
        c0 = t3.cell(r_idx, 0); c1 = t3.cell(r_idx, 1); c2 = t3.cell(r_idx, 2)
        if r_idx % 2 == 1:
            set_cell_background(c0, LIGHT_BG); set_cell_background(c1, LIGHT_BG); set_cell_background(c2, LIGHT_BG)
        set_cell_margins(c0, top=80, bottom=80, left=100, right=100)
        set_cell_margins(c1, top=80, bottom=80, left=100, right=100)
        set_cell_margins(c2, top=80, bottom=80, left=100, right=100)
        c0.paragraphs[0].add_run(col0).font.size = Pt(11); c0.paragraphs[0].runs[0].bold = True
        c1.paragraphs[0].add_run(col1).font.size = Pt(11)
        c2.paragraphs[0].add_run(col2).font.size = Pt(11); c2.paragraphs[0].runs[0].italic = True

    doc.add_paragraph().paragraph_format.space_after = Pt(6)

    # Activity Diagram insertion
    add_h2("2.2. Sơ đồ Hoạt động (Activity Diagram) — Quy trình Đặt và Duyệt Dịch vụ ePort")
    add_p(
        "Sơ đồ Activity (biểu đồ hoạt động) theo chuẩn UML mô tả chi tiết tiến trình thực thi của một luồng nghiệp vụ "
        "theo trục thời gian và các điều kiện rẽ nhánh logic. "
        "Hình 2.1 dưới đây minh họa quy trình từ thời điểm Khách hàng xác nhận tạo đơn đặt dịch vụ tại cổng ePort, "
        "hệ thống tự động tính cước, nhân viên điều độ phê duyệt hoặc từ chối đơn hàng, và xuất hóa đơn điện tử tự động:"
    )
    add_diagram("d3-activity.png", "Hình 2.1: Sơ đồ Activity Diagram — Quy trình đặt và duyệt dịch vụ tại cổng ePort", width_inches=6.0)

    add_p(
        "Giải thích các bước trong sơ đồ Activity:\n"
        "1. Khách hàng chọn dịch vụ (nâng/hạ cont, cắm điện lạnh, vệ sinh...) và nhập số lượng container.\n"
        "2. Hệ thống kiểm tra dữ liệu hợp lệ và tự động tính tiền theo biểu giá niêm yết trong CSDL.\n"
        "3. Khách hàng xác nhận đơn đặt hàng (Booking) và chọn phương thức thanh toán VietQR.\n"
        "4. Nhân viên khai thác kiểm tra tính khả thi tại bãi và quyết định Duyệt hoặc Từ chối đơn.\n"
        "5. Nếu được Duyệt, hệ thống tự động sinh Lệnh giao nhận điện tử và phát hành hóa đơn kế toán."
    )

    add_h2("2.3. Ranh giới hệ thống")
    
    add_h3("2.3.1. Nằm TRONG phạm vi (In-Scope)")
    add_bullet("Quản lý tài nguyên cảng: ", "Toàn bộ thông tin về tàu biển, hãng tàu, chuyến tàu, cầu bến, bãi container và tài khoản khách hàng.")
    add_bullet("Cổng dịch vụ trực tuyến ePort: ", "Đặt lịch dịch vụ, thanh toán không dùng tiền mặt VietQR/NAPAS 247, nhận hóa đơn qua thư điện tử.")
    add_bullet("Lệnh giao nhận điện tử (eGate Pass): ", "Bảo mật bằng cơ chế cặp Số lệnh + Mã PIN bí mật cho tài xế.")
    add_bullet("Cổng thông minh (Smart Gate): ", "Camera OCR đọc biển số xe, mã số container, đối chiếu lệnh hợp lệ, điều phối dock làm việc.")
    add_bullet("Trạm cân điện tử: ", "Cân 2 lần (lúc vào và lúc ra) tự động tính khối lượng tịnh hàng hóa và cấp phiếu VGM theo công ước SOLAS.")
    add_bullet("Quản lý sơ đồ bãi: ", "Bản đồ nhiệt và thuật toán AI gợi ý vị trí hạ bãi tránh đảo chuyển vỏ cont.")
    add_bullet("Giám sát AI 24/7: ", "Phát hiện lửa, người ngã, xâm nhập vùng cấm, giám sát an toàn tài xế.")
    add_bullet("Quản lý dòng tiền: ", "Đồng hồ lưu bãi thời gian thực, chốt kỳ tính phí thành phiếu thu có mã QR, theo dõi công nợ.")
    add_bullet("Trao đổi dữ liệu điện tử (EDI): ", "Tạo và gửi các bản tin chuẩn UN/EDIFACT D:95B (CODECO, COPARN, COARRI) cho hãng tàu.")
    add_bullet("Chống gian lận đếm xe: ", "Cảm biến vòng từ gửi xung độc lập, đối soát phát hiện sai lệch số xe.")
    add_bullet("Agent tự hành động: ", "Tự động hóa tác vụ định kỳ có ghi vết nhật ký và cơ chế hoàn tác dữ liệu.")
    add_bullet("Giao diện lập trình mở (REST API): ", "Cung cấp phân hệ `/api/v1/` kết nối các phần mềm bên ngoài.")

    add_h3("2.3.2. Nằm NGOÀI phạm vi (Out-of-Scope)")
    
    p_t4 = add_p("Bảng 2.2: Các phân hệ nằm ngoài phạm vi đề tài và lý do giải trình", align=WD_ALIGN_PARAGRAPH.CENTER, bold=True, size=11.5)
    t4 = doc.add_table(rows=6, cols=2)
    t4.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_borders(t4)

    headers4 = ["Nghiệp vụ không thuộc phạm vi", "Lý do và Giải pháp liên kết"]
    for i, h in enumerate(headers4):
        c = t4.cell(0, i)
        set_cell_background(c, DARK_BLUE)
        set_cell_margins(c, top=120, bottom=120, left=150, right=150)
        p = c.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(h); r.bold = True; r.font.color.rgb = RGBColor(255, 255, 255); r.font.size = Pt(11.5)

    out_data = [
        ("Kế toán tài chính, sổ cái, quyết toán thuế", "SmartPort xuất dữ liệu sang phần mềm MISA chuyên dụng, không làm lại phần mềm kế toán."),
        ("Điều khiển cơ khí thiết bị cẩu bãi / cẩu bờ", "Thuộc hệ thống điều khiển công nghiệp tự động hóa SCADA/PLC riêng của hãng cẩu."),
        ("Khai báo thủ tục hải quan trực tiếp (VNACCS/VCIS)", "Nghiệp vụ cơ quan nhà nước, cảng chỉ đối chiếu trạng thái thông quan qua kết nối dữ liệu."),
        ("Quản lý nhân sự nội bộ, chấm công, tiền lương", "Không thuộc trọng tâm điều hành chuỗi cung ứng container cảng biển."),
        ("Hoạch định và điều hướng đội xe container đường bộ", "Thuộc phạm vi của phần mềm quản lý vận tải đường bộ (TMS).")
    ]
    for r_idx, (col0, col1) in enumerate(out_data, start=1):
        c0 = t4.cell(r_idx, 0); c1 = t4.cell(r_idx, 1)
        if r_idx % 2 == 1:
            set_cell_background(c0, LIGHT_BG); set_cell_background(c1, LIGHT_BG)
        set_cell_margins(c0, top=100, bottom=100, left=140, right=140)
        set_cell_margins(c1, top=100, bottom=100, left=140, right=140)
        p0 = c0.paragraphs[0]; r0 = p0.add_run(col0); r0.font.size = Pt(11.5); r0.bold = True
        p1 = c1.paragraphs[0]; r1 = p1.add_run(col1); r1.font.size = Pt(11.5)

    doc.add_paragraph().paragraph_format.space_after = Pt(4)

    add_h2("2.4. Ràng buộc và Giả định")
    add_p("Hệ thống được thiết kế dựa trên các ràng buộc kỹ thuật và giả định môi trường chặt chẽ:")
    add_bullet("Ràng buộc thiết bị: ", "Phần mềm phải vận hành trơn tru trên hệ thống camera IP có sẵn của doanh nghiệp cảng, không bắt buộc chủ đầu tư thay mới phần cứng.")
    add_bullet("Ràng buộc tính toán AI: ", "Các thuật toán suy luận phải chạy được trên trình duyệt người dùng để giải phóng máy chủ khỏi yêu cầu GPU.")
    add_bullet("Ràng buộc chuẩn hóa quốc tế: ", "Bản tin EDI phải tuân thủ nghiêm ngặt chuẩn UN/EDIFACT D:95B; Mã định danh container phải thỏa mãn thuật toán chữ số kiểm tra ISO 6346.")
    add_bullet("Ràng buộc kiểm toán chứng từ: ", "Mọi hóa đơn, phiếu thu, chứng từ EIR đã được cấp số thứ tự thì tuyệt đối không được phép xóa khỏi cơ sở dữ liệu — chỉ được chuyển trạng thái sang 'Hủy' để phục vụ đối soát thanh tra.")
    add_bullet("Giả định kết nối: ", "Kết nối mạng Internet chỉ bắt buộc khi xác thực thanh toán ngân hàng và truyền tin EDI; các phân hệ kiểm soát cổng và vận hành nội bộ vẫn duy trì bình thường khi mất mạng.")

    # =============================================================
    # CHƯƠNG 3: ĐỐI TƯỢNG HỆ THỐNG
    # =============================================================
    add_h1("3. ĐỐI TƯỢNG HỆ THỐNG")

    add_h2("3.1. Đối tượng sử dụng (Actors)")

    add_h3("3.1.1. Tác nhân là con người")
    add_p(
        "Hệ thống cài đặt 3 vai trò phân quyền kỹ thuật chính trong bảng dữ liệu: "
        "Admin (Quản trị), NhanVien (Nhân viên), KhachHang (Khách hàng). "
        "Trong đó vai trò NhanVien được chia nhỏ theo chức năng nhiệm vụ thực tế:"
    )

    p_t5 = add_p("Bảng 3.1: Danh sách các tác nhân người dùng và mối quan tâm nghiệp vụ", align=WD_ALIGN_PARAGRAPH.CENTER, bold=True, size=11.5)
    t5 = doc.add_table(rows=10, cols=3)
    t5.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_borders(t5)

    headers5 = ["Tác nhân (Actor)", "Vai trò kỹ thuật", "Mối quan tâm và Nghiệp vụ phụ trách"]
    for i, h in enumerate(headers5):
        c = t5.cell(0, i)
        set_cell_background(c, DARK_BLUE)
        set_cell_margins(c, top=120, bottom=120, left=120, right=120)
        p = c.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(h); r.bold = True; r.font.color.rgb = RGBColor(255, 255, 255); r.font.size = Pt(11)

    actors_human = [
        ("Quản trị viên hệ thống", "Admin", "Cấu hình hệ thống, phân quyền tài khoản, biểu phí, khóa API, cấu hình Agent, nhật ký thanh tra."),
        ("Nhân viên điều độ cảng", "NhanVien", "Duyệt lệnh giao nhận, lập kế hoạch tàu, phân bổ cầu bến, sơ đồ bãi, vị trí hạ container."),
        ("Nhân viên giao nhận", "NhanVien", "Quản lý hồ sơ container 360°, phát hành chứng từ EIR, quản lý bản tin EDI, lập phiếu sửa chữa EOR."),
        ("Bảo vệ / Nhân viên trực cổng", "NhanVien", "Kiểm soát cổng vào–ra, đối chiếu lệnh, thu phí cổng, quét CCCD/GPLX tài xế."),
        ("Nhân viên trạm cân", "NhanVien", "Thực hiện cân xe 2 lần, xác nhận khối lượng tịnh, cấp số liệu VGM theo chuẩn SOLAS."),
        ("Nhân viên thu ngân / Kế toán", "NhanVien", "Đối soát thanh toán trực tuyến, quản lý phiếu thu lưu bãi, theo dõi công nợ, xuất file sang MISA."),
        ("Khách hàng doanh nghiệp", "KhachHang", "Tạo đơn đặt dịch vụ trực tuyến, thanh toán VietQR, tra cứu container, theo dõi công nợ của công ty."),
        ("Tài xế xe container", "Không cần tài khoản", "Tra cứu thông tin lệnh giao nhận bằng cặp Số lệnh + Mã PIN, thực hiện check-in trực tuyến trước khi đến cổng."),
        ("Khách vãng lai", "Không cần tài khoản", "Xem thông tin cảng, biểu giá dịch vụ niêm yết, tra cứu lịch tàu cập cảng, hỏi đáp với PortBot.")
    ]
    for r_idx, (col0, col1, col2) in enumerate(actors_human, start=1):
        c0 = t5.cell(r_idx, 0); c1 = t5.cell(r_idx, 1); c2 = t5.cell(r_idx, 2)
        if r_idx % 2 == 1:
            set_cell_background(c0, LIGHT_BG); set_cell_background(c1, LIGHT_BG); set_cell_background(c2, LIGHT_BG)
        set_cell_margins(c0, top=80, bottom=80, left=100, right=100)
        set_cell_margins(c1, top=80, bottom=80, left=100, right=100)
        set_cell_margins(c2, top=80, bottom=80, left=100, right=100)
        c0.paragraphs[0].add_run(col0).font.size = Pt(11); c0.paragraphs[0].runs[0].bold = True
        c1.paragraphs[0].add_run(col1).font.size = Pt(11); c1.paragraphs[0].runs[0].italic = True
        c2.paragraphs[0].add_run(col2).font.size = Pt(11)

    add_callout(
        "Ghi chú thiết kế quan trọng: Hệ thống cố ý thiết kế để tài xế KHÔNG CẦN TẠO TÀI KHOẢN. "
        "Trong thực tế logistics, việc bắt tài xế xe tải phải tải app, đăng ký tài khoản và ghi nhớ mật khẩu "
        "là rào cản lớn nhất gây tắc nghẽn cổng cảng. Cơ chế tra cứu bằng cặp Số lệnh + Mã PIN duy nhất vừa đảm bảo "
        "tính bảo mật, vừa xóa bỏ hoàn toàn rào cản triển khai thực địa.",
        italic=True
    )

    add_h3("3.1.2. Tác nhân là hệ thống ngoài (External Systems)")
    add_bullet("Hệ thống Camera IP: ", "Cung cấp luồng dữ liệu hình ảnh trực tiếp phục vụ AI nhận diện tại cổng và các khu vực bãi.")
    add_bullet("Cảm biến vòng từ đếm xe độc lập: ", "Gửi các xung đếm xe vật lý độc lập qua API để hệ thống tự động đối soát chống tiêu cực gian lận vé.")
    add_bullet("Hệ thống máy chủ Hãng tàu: ", "Tiếp nhận bản tin CODECO/COARRI và gửi bản tin COPARN qua kết nối EDI tự động.")
    add_bullet("Cổng thanh toán VietQR / NAPAS 247: ", "Sinh mã QR động theo từng giao dịch và phản hồi thông báo thanh toán thành công (Webhook).")
    add_bullet("Phần mềm kế toán MISA: ", "Tiếp nhận các file xuất hóa đơn, chứng từ doanh thu để hạch toán vào sổ cái doanh nghiệp.")
    add_bullet("Ứng dụng của bên thứ ba (Third-party Apps): ", "Truy xuất và tích hợp dữ liệu thông qua hệ thống REST API `/api/v1/`.")

    add_h3("3.1.3. Tác nhân đặc biệt — Agent tự động (Autonomous Agent)")
    add_p(
        "Trong lý thuyết hướng đối tượng, đây là một điểm sáng tạo đột phá: "
        "Agent tự động được mô hình hóa như một Tác nhân chủ động (Active Actor) khởi xướng ca sử dụng, "
        "chứ không phải một đoạn mã hàm thụ động chờ người dùng gọi. Cụ thể:"
    )
    add_p(
        "Agent được cấu hình tự đánh thức theo chu kỳ (mỗi 30 phút một lần), tự quét dữ liệu cơ sở dữ liệu, "
        "tự đánh giá luật nghiệp vụ và chủ động thực thi các ca sử dụng nội bộ của hệ thống. "
        "Người quản trị có thể phân quyền cho Agent theo 3 chế độ độc lập cho từng kỹ năng: Tắt (Disabled), "
        "Xin phép (Proposal) và Tự làm (Autonomous)."
    )

    # Class Diagram insertion
    add_h2("3.2. Sơ đồ Lớp (Class Diagram) — Cấu trúc các lớp Model và Quan hệ liên kết")
    add_p(
        "Sơ đồ lớp (Class Diagram) theo chuẩn UML mô tả cấu trúc tĩnh của hệ thống: các lớp đối tượng "
        "(tương ứng các lớp Model trong Entity Framework Core), các thuộc tính (attributes), phương thức (operations) "
        "và quan hệ liên kết (association) kèm bội số (multiplicity) giữa chúng. "
        "Hình 3.1 dưới đây trình bày sơ đồ lớp tổng thể của các thực thể nghiệp vụ cốt lõi trong SmartPort:"
    )
    add_diagram("d4-class.png", "Hình 3.1: Sơ đồ Class Diagram — Các lớp Model chính và quan hệ liên kết trong SmartPort", width_inches=6.3)

    add_p(
        "Phân tích các quan hệ hướng đối tượng chính trên biểu đồ lớp:\n"
        "• Quan hệ 1—n giữa HangTau và Tau: Một hãng tàu sở hữu hoặc khai thác nhiều con tàu.\n"
        "• Quan hệ 1—n giữa Tau và ChuyenTau: Một con tàu thực hiện nhiều chuyến cập cảng theo thời gian.\n"
        "• Quan hệ 1—n giữa Ben và ChuyenTau: Một cầu bến tiếp nhận nhiều chuyến tàu cập neo đậu.\n"
        "• Quan hệ 1—n giữa KhachHang và DatDichVu: Khách hàng tạo nhiều đơn đặt dịch vụ cảng.\n"
        "• Quan hệ Composition (1—n) giữa DatDichVu và DatDichVuChiTiet: Chi tiết đơn hàng không thể tồn tại độc lập nếu đơn hàng gốc bị hủy.\n"
        "• Quan hệ trung tâm xoay quanh Container: Container liên kết với ChuyenTau (chuyến nhập/chuyến xuất), "
        "KhachHang (chủ hàng), và là đối tượng tham chiếu của LenhGiaoNhan và PhieuLuuBai."
    )

    # Physical Database Model (ERD) insertion
    add_h2("3.3. Sơ đồ Mô hình Dữ liệu Vật lý (Physical ERD)")
    add_p(
        "Mô hình vật lý (Physical Data Model) mô tả chi tiết cấu trúc lưu trữ thực tế trên hệ quản trị CSDL SQL Server: "
        "tên bảng, tên cột, kiểu dữ liệu cụ thể, khóa chính (PK), khóa ngoại (FK) và các ràng buộc toàn vẹn. "
        "Cơ sở dữ liệu SmartPortDB thể hiện đầy đủ quan hệ giữa các bảng nghiệp vụ, như được minh họa ở Hình 3.2:"
    )
    add_diagram("d5-er.png", "Hình 3.2: Sơ đồ Mô hình Dữ liệu Vật lý (ERD) cơ sở dữ liệu SmartPortDB", width_inches=6.3)

    add_p(
        "Toàn vẹn dữ liệu được bảo đảm ở nhiều lớp:\n"
        "1. Ràng buộc khóa ngoại (FOREIGN KEY) ngăn chặn triệt để tình trạng dữ liệu mồ côi (orphaned data).\n"
        "2. Ràng buộc duy nhất (UNIQUE) trên các trường nghiệp vụ: TenDangNhap, SoCont (theo chuẩn ISO 6346), MaSoBooking.\n"
        "3. Quy tắc ON DELETE CASCADE áp dụng cho bảng DatDichVuChiTiet để tự động dọn dẹp các dòng con khi đơn hàng gốc bị xóa.\n"
        "4. Entity Framework Core tự động tham số hóa mọi câu lệnh LINQ, loại bỏ 100% nguy cơ tấn công SQL Injection."
    )

    add_h3("3.3.1. Danh mục 8 Gói thực thể (Packages)")
    packages_data = [
        ("Gói 1: Đối tác & Tài khoản (Accounts & Partners)", "KhachHang, TaiKhoan, HangTau, TaiXe, KhuonMat"),
        ("Gói 2: Tàu & Cầu bến (Vessels & Berths)", "Tau, Ben, ChuyenTau"),
        ("Gói 3: Hàng hóa & Mặt bãi (Cargo & Yard)", "Container, NhietDoLog, PhieuSuaChua, KiemDem"),
        ("Gói 4: Dịch vụ & Dòng tiền (Services & Finance)", "DichVu, DatDichVu, DatDichVuChiTiet, ThanhToan, PhieuLuuBai, ChungTu, SanLuongThang"),
        ("Gói 5: Cổng & Vận hành (Gate & Operations)", "LenhGiaoNhan, XeRaVao, PhieuCan, BieuPhiCong, TheXe, DemXeCong, XungDemXe"),
        ("Gói 6: Trí tuệ nhân tạo & An ninh (AI & Security)", "NhanDienAI, NhanDienChiTiet, SuCoAnNinh, CameraIP, MediaAI, PhanHoiAI"),
        ("Gói 7: Tác nhân tự hành (Autonomous Agent)", "CauHinhAgent, NhatKyAgent"),
        ("Gói 8: Tích hợp & Hệ thống (Integration & System)", "BanTinEDI, ApiKey, NhatKy, TinTuc, LienHe, CauHinhAnh")
    ]
    for p_name, p_classes in packages_data:
        add_bullet(p_name + ": ", p_classes)

    # =============================================================
    # CHƯƠNG 4: CHỨC NĂNG HỆ THỐNG
    # =============================================================
    add_h1("4. CHỨC NĂNG HỆ THỐNG")

    add_h2("4.1. Sơ đồ Use Case Tổng quát của Hệ thống SmartPort")
    add_p(
        "Sơ đồ Use Case (biểu đồ trường hợp sử dụng) theo chuẩn UML mô tả trực quan các chức năng "
        "mà từng nhóm tác nhân (Actor) có thể tương tác với hệ thống. "
        "Hình 4.1 trình bày sơ đồ Use Case tổng quát của SmartPort với 3 tác nhân người dùng chính "
        "(Khách hàng, Nhân viên và Quản trị viên) cùng hệ thống AI hỗ trợ:"
    )
    add_diagram("d2-usecase.png", "Hình 4.1: Sơ đồ Use Case tổng quát hệ thống SmartPort", width_inches=6.3)

    add_p(
        "Phân tích chi tiết các ca sử dụng theo phân quyền trên sơ đồ Use Case:\n"
        "1. Tác nhân Khách hàng (ePort Customer): Thực hiện các Use Case trực tuyến gồm Đăng nhập, Đăng ký tài khoản, "
        "Tra cứu container (tích hợp OCR quét ảnh vỏ cont), Xem lịch tàu cập bến, Đặt dịch vụ cảng trực tuyến, "
        "Xem lịch sử đơn và hóa đơn điện tử, Tương tác với trợ lý PortBot.\n"
        "2. Tác nhân Nhân viên (Port Operator): Đăng nhập hệ thống, Theo dõi Dashboard chỉ số cảng, Điều độ chuyến tàu, "
        "Phân bổ cầu bến bằng AI, Giám sát Camera AI, Quản lý container & sơ đồ bãi, Duyệt đơn đặt dịch vụ, Xuất hóa đơn Excel/PDF.\n"
        "3. Tác nhân Quản trị viên (Admin): Kế thừa toàn bộ quyền của Nhân viên, đồng thời độc quyền quản lý tài khoản người dùng, "
        "phân quyền vai trò, cấu hình hệ thống, thiết lập khóa API và quản lý cấu hình Agent tự động."
    )

    add_h2("4.2. Bảng Tổng hợp 99 Chức năng theo 14 Nhóm nghiệp vụ")
    add_p(
        "Toàn bộ 99 chức năng của hệ thống được đánh số kiểm soát nghiêm ngặt, phân bố theo tiến trình nghiệp vụ thực tế:"
    )

    p_t8 = add_p("Bảng 4.1: Bảng tổng hợp số lượng chức năng theo 14 nhóm nghiệp vụ", align=WD_ALIGN_PARAGRAPH.CENTER, bold=True, size=11.5)
    t8 = doc.add_table(rows=15, cols=2)
    t8.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_borders(t8)

    headers8 = ["Nhóm chức năng nghiệp vụ", "Số lượng Chức năng (CN)"]
    for i, h in enumerate(headers8):
        c = t8.cell(0, i)
        set_cell_background(c, DARK_BLUE)
        set_cell_margins(c, top=120, bottom=120, left=150, right=150)
        p = c.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(h); r.bold = True; r.font.color.rgb = RGBColor(255, 255, 255); r.font.size = Pt(11.5)

    fn_groups = [
        ("GĐ 1–2 · Phân hệ Khách hàng & Đặt dịch vụ trực tuyến", "13 chức năng"),
        ("GĐ 5–6 · Phân hệ Cổng vào thông minh & Trạm cân điện tử", "11 chức năng"),
        ("Agent tự hành động (Tác nhân AI tự thực thi có giải trình)", "10 chức năng"),
        ("GĐ 8 · Giám sát thông minh AI 24/7 (Lửa, người ngã, tài xế)", "9 chức năng"),
        ("Quản trị Dòng tiền (Đồng hồ lưu bãi, phiếu thu, công nợ)", "8 chức năng"),
        ("Nền tảng hệ thống (Quản trị tài khoản, phân quyền, cấu hình)", "7 chức năng"),
        ("GĐ 9–10 · Hồ sơ container 360° & Phát hành chứng từ điện tử", "7 chức năng"),
        ("An ninh cảng & Cơ chế chống gian lận đếm xe độc lập", "7 chức năng"),
        ("Liên kết dữ liệu (Tra cứu toàn cục Ctrl+K, Timeline lịch sử)", "6 chức năng"),
        ("GĐ 3–4 · Lệnh giao nhận điện tử & Điều độ chuyến tàu cập bến", "6 chức năng"),
        ("GĐ 11–12 · Kiểm soát Cổng ra & Báo cáo hậu kiểm điều hành", "6 chức năng"),
        ("GĐ 7 · Quản lý kho bãi & Thuật toán gợi ý vị trí hạ bãi", "5 chức năng"),
        ("Kết nối Hãng tàu quốc tế qua chuẩn EDI (CODECO, COPARN, COARRI)", "4 chức năng"),
        ("TỔNG CỘNG TOÀN HỆ THỐNG", "99 CHỨC NĂNG")
    ]
    for r_idx, (col0, col1) in enumerate(fn_groups, start=1):
        c0 = t8.cell(r_idx, 0); c1 = t8.cell(r_idx, 1)
        is_total = (r_idx == len(fn_groups))
        if is_total:
            set_cell_background(c0, "D0E1FD"); set_cell_background(c1, "D0E1FD")
        elif r_idx % 2 == 1:
            set_cell_background(c0, LIGHT_BG); set_cell_background(c1, LIGHT_BG)
        set_cell_margins(c0, top=80, bottom=80, left=140, right=140)
        set_cell_margins(c1, top=80, bottom=80, left=140, right=140)
        p0 = c0.paragraphs[0]; r0 = p0.add_run(col0); r0.font.size = Pt(11.5); r0.bold = is_total or ("Agent" in col0)
        p1 = c1.paragraphs[0]; p1.alignment = WD_ALIGN_PARAGRAPH.RIGHT
        r1 = p1.add_run(col1); r1.font.size = Pt(11.5); r1.bold = is_total or ("Agent" in col0)
        if is_total:
            r0.font.color.rgb = NAVY; r1.font.color.rgb = NAVY

    doc.add_paragraph().paragraph_format.space_after = Pt(4)

    add_h2("4.3. Yêu cầu phi chức năng (Non-Functional Requirements)")
    
    p_t9 = add_p("Bảng 4.2: Các yêu cầu phi chức năng đảm bảo chất lượng hệ thống", align=WD_ALIGN_PARAGRAPH.CENTER, bold=True, size=11.5)
    t9 = doc.add_table(rows=8, cols=2)
    t9.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_borders(t9)

    headers9 = ["Tiêu chuẩn chất lượng", "Yêu cầu kỹ thuật chi tiết"]
    for i, h in enumerate(headers9):
        c = t9.cell(0, i)
        set_cell_background(c, DARK_BLUE)
        set_cell_margins(c, top=120, bottom=120, left=150, right=150)
        p = c.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(h); r.bold = True; r.font.color.rgb = RGBColor(255, 255, 255); r.font.size = Pt(11.5)

    nfr_data = [
        ("Hiệu năng (Performance)", "Các mô hình AI suy luận trực tiếp trên trình duyệt máy khách (WebGPU), không phụ thuộc vào tài nguyên GPU máy chủ."),
        ("Độ sẵn sàng (Availability)", "Phân hệ kiểm soát chốt cổng hoạt động liên tục ngay cả khi gặp sự cố mất kết nối mạng Internet."),
        ("Bảo mật (Security)", "Mật khẩu băm chuẩn PBKDF2/SHA-256; Phân quyền chặt chẽ theo vai trò; Khóa API giới hạn phạm vi đọc/ghi theo từng khách hàng; Cơ chế chống giả mạo biểu mẫu Anti-Forgery Token."),
        ("Khả năng kiểm toán (Auditability)", "Mọi hành động đều được lưu vết trong bảng nhật ký kèm định danh người thực hiện; Chứng từ kế toán đã đánh số không được phép xóa."),
        ("Trải nghiệm người dùng (UX)", "Giao diện màn hình bảo vệ tại chốt cổng tối giản chỉ với 2 nút thao tác chính; Hỗ trợ phím tắt tra cứu nhanh toàn cục (Ctrl+K); Không đòi hỏi đào tạo phức tạp."),
        ("Chuẩn hóa tích hợp (Integration)", "Tuân thủ chặt chẽ chuẩn điện tử UN/EDIFACT D:95B; Quy chuẩn mã container ISO 6346; Cổng thanh toán quốc gia VietQR/NAPAS 247."),
        ("Kiểm thử khẳng định (Testing)", "Hệ sinh thái 325 bài kiểm thử tự động, khẳng định trực tiếp trên số dòng và số dư tiền tệ trong CSDL SQL Server, không kiểm tra dựa trên chuỗi văn bản tạm thời.")
    ]
    for r_idx, (col0, col1) in enumerate(nfr_data, start=1):
        c0 = t9.cell(r_idx, 0); c1 = t9.cell(r_idx, 1)
        if r_idx % 2 == 1:
            set_cell_background(c0, LIGHT_BG); set_cell_background(c1, LIGHT_BG)
        set_cell_margins(c0, top=90, bottom=90, left=140, right=140)
        set_cell_margins(c1, top=90, bottom=90, left=140, right=140)
        p0 = c0.paragraphs[0]; r0 = p0.add_run(col0); r0.font.size = Pt(11.5); r0.bold = True
        p1 = c1.paragraphs[0]; r1 = p1.add_run(col1); r1.font.size = Pt(11.5)

    doc.add_paragraph().paragraph_format.space_after = Pt(6)

    # =============================================================
    # CHƯƠNG 5: KIẾN TRÚC PHÂN TẦNG VÀ NGUYÊN TẮC THIẾT KẾ
    # =============================================================
    add_h1("5. KIẾN TRÚC PHÂN TẦNG VÀ NGUYÊN TẮC THIẾT KẾ")

    add_h2("5.1. Sơ đồ Kiến trúc phân tầng và Luồng dữ liệu hệ thống")
    add_p(
        "Hệ thống SmartPort được tổ chức theo kiến trúc phân tầng đa lớp (Multi-tier Architecture) "
        "kết hợp kiến trúc hướng dịch vụ (Service-Oriented). "
        "Hình 5.1 dưới đây thể hiện cấu trúc phân tầng chi tiết từ tầng giao diện Presentation, "
        "tầng điều khiển Controller, tầng dịch vụ nghiệp vụ Business Services, tầng truy xuất dữ liệu ORM "
        "đến hệ quản trị cơ sở dữ liệu SQL Server:"
    )
    add_diagram("d1-kientruc.png", "Hình 5.1: Sơ đồ Kiến trúc phân tầng và Luồng dữ liệu hệ thống SmartPort", width_inches=6.2)

    add_p(
        "Phân tích cấu trúc 5 tầng trên sơ đồ kiến trúc:\n"
        "1. Tầng Giao diện (Presentation Layer): Razor Views + Bootstrap 5.3 + JavaScript. Phân định rõ ràng Khu vực Admin (34 Controller) và Cổng thông tin công khai (16 Controller).\n"
        "2. Tầng Điều khiển (Controller Layer): Tiếp nhận yêu cầu HTTP, kiểm tra quyền hạn (Role-based Authorization), điều hướng luồng xử lý và gọi các Service nghiệp vụ tương ứng.\n"
        "3. Tầng Dịch vụ Nghiệp vụ (Business Service Layer): Chứa 20 lớp nghiệp vụ chuyên biệt (LuuBaiService, EdiService, YardService, AgentTuDongService...). Đóng gói 100% logic tính cước, kiểm tra an toàn và thuật toán phân bổ.\n"
        "4. Tầng Truy xuất Dữ liệu (Data Access Layer): Entity Framework Core 10 quản lý 40 DbSet ánh xạ vào 40 bảng thực thể trong SmartPortDB.\n"
        "5. Tầng Cơ sở Dữ liệu (Database Layer): Hệ quản trị CSDL Microsoft SQL Server bảo đảm tính toàn vẹn và độ bền vững giao dịch (ACID)."
    )

    add_h2("5.2. Ba nguyên tắc thiết kế hướng đối tượng cốt lõi")
    add_bullet("Nguyên tắc 1: Một nguồn sự thật duy nhất (Single Source of Truth): ", 
               "Mọi phép tính toán liên quan đến chi phí lưu bãi của container đều bắt buộc phải đi qua một điểm duy nhất là `LuuBaiService`. "
               "Màn hình hiển thị hồ sơ container, bảng kê công nợ doanh nghiệp và tác nhân Agent tự động đều gọi chung một phương thức tính toán. "
               "Điều này triệt tiêu hoàn toàn rủi ro xuất hiện các con số sai lệch nhau giữa các phòng ban.")
    
    add_bullet("Nguyên tắc 2: Tầng dịch vụ hoàn toàn độc lập với giao thức HTTP: ", 
               "Các lớp dịch vụ nghiệp vụ (`LuuBaiService`, `EdiService`, `AgentTuDongService`) được thiết kế đóng gói thuần túy, "
               "chỉ nhận tham số dữ liệu và đối tượng DbContext mà không phụ thuộc vào `HttpContext`. "
               "Nhờ vậy, tác nhân Agent khi chạy ngầm dưới nền (Background Job) có thể tái sử dụng 100% các hàm nghiệp vụ "
               "mà con người thao tác trên giao diện web, không phải viết lại mã nguồn lần thứ hai.")

    add_bullet("Nguyên tắc 3: Nhật ký kiểm toán là một thành phần thiết kế gốc: ", 
               "Lớp `NhatKyAgent` được thiết kế ngay từ đầu với mục tiêu minh bạch hóa. Bản ghi nhật ký lưu trữ lý do hành động "
               "bằng câu văn hoàn chỉnh mà con người có thể đọc hiểu được, đồng thời lưu tên bảng (`BangThamChieu`) và khóa chính (`MaThamChieu`) "
               "của bản ghi mà Agent đã tự tạo. Thiết kế này giúp người dùng có thể tra cứu minh bạch và bấm nút 'Hoàn tác' tức thì khi cần.")

    # =============================================================
    # CHƯƠNG 6: KẾT LUẬN
    # =============================================================
    add_h1("6. KẾT LUẬN")
    add_p(
        "Bản phân tích và thiết kế hướng đối tượng của Đề tài Hệ thống Quản lý và Điều hành Cảng biển Thông minh (SmartPort) "
        "đã hoàn thành việc chuẩn hóa toàn diện 40 lớp thực thể nghiệp vụ chia thành 8 gói, phục vụ 9 nhóm tác nhân người dùng, "
        "6 hệ thống bên ngoài và 1 tác nhân tự động (Agent). Toàn bộ 99 chức năng nghiệp vụ được tổ chức chặt chẽ trên nền tảng kiến trúc 5 tầng, "
        "được trực quan hóa bằng đầy đủ 5 sơ đồ khoa học chuẩn mực (Kiến trúc, Use Case, Activity, Class Diagram và Physical ERD)."
    )
    add_p(
        "Điểm đột phá lớn nhất về mặt phương pháp luận hướng đối tượng của đề tài nằm ở việc mô hình hóa Agent tự động "
        "như một Tác nhân chủ động (Active Actor) có khả năng tự khởi xướng các ca sử dụng. "
        "Đồng thời, thiết kế đã trả lời xuất sắc 3 câu hỏi sống còn trong quản trị hệ thống phần mềm doanh nghiệp:\n"
        "1. Agent được phép làm gì? — Được kiểm soát minh bạch thông qua mô hình 3 chế độ độc lập cho từng kỹ năng.\n"
        "2. Agent đã làm những gì và vì sao lại làm? — Được giải trình rõ ràng qua nhật ký lưu vết câu chữ tự nhiên mà con người hiểu được.\n"
        "3. Khi Agent xử lý nhầm thì khắc phục thế nào? — Được giải quyết triệt để nhờ cơ chế liên kết tham chiếu đa hình phục vụ nút hoàn tác (Undo)."
    )
    add_p(
        "Với nền tảng phân tích vững chắc, các biểu đồ thiết kế trực quan và hệ thống mã nguồn đã được kiểm thử trên 325 test cases thực tế, "
        "báo cáo cung cấp đầy đủ luận cứ khoa học và thực tiễn để phục vụ học phần Thực hành Phân tích và Thiết kế Hướng đối tượng."
    )

    out_dir = r"C:\Users\ASUS\Downloads\SmartPort\BaoCao"
    out_docx = os.path.join(out_dir, "BaoCao_OOAD_SmartPort_LeHuuAnhTai_2424802010066.docx")
    doc.save(out_docx)
    print(f"SUCCESS: Saved Word document with 5 diagrams at: {out_docx}")

if __name__ == "__main__":
    build_word_doc()
