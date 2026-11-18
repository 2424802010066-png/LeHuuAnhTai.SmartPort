# -*- coding: utf-8 -*-
"""
Dung lai de cuong theo KHUON TRINH BAY cua de cuong Nguyen Tan Sang 2026-2027
(Downloads/NguyenTanSang.docx - de cuong da duoc thay Luan huong dan).

Dac diem khuon mau Sang (do tu file goc, 30/09/2026):
  - Than bai: gian dong 1,5; thut dong dau 1,11 cm; can deu; khong cach doan.
  - Muc 1: "1. Ten de tai: <ten>" tren cung mot dong, in dam.
  - Muc 9: nhan dam "Muc tieu tong quat" / "Muc tieu cu the" + gach dau dong "-", KHONG bang.
  - Muc 10: van xuoi lien mach, KHONG tieu muc a) b) c); "Tai lieu tham khao" in dam,
    danh muc IEEE viet tat, gian dong don, ten tap chi / hoi nghi in nghieng, khong DOI.
  - Muc 11: nhan dam + gach dau dong; phuong phap dang "Nhan: noi dung".
  - Muc 12.2: thoi gian dang "Tu thang 11-2026 den thang 12-2026", 5 giai doan,
    cot nguoi thuc hien ghi ca hai thanh vien.
  - Muc 13: "San pham dat duoc:" (gach dau dong) + "Kha nang ung dung:" (mot doan).
  - Muc 14: chi co tong kinh phi + bang chu, KHONG bang du toan.
  - Bia: dien thang 9.

Noi dung khoa hoc giu nguyen cac dinh chinh cua phieu nhan xet (mod 10, mo hinh nen,
chap nhan sai / tu choi, baseline, U/J/Z + BIC, tinh moi), chi viet gon lai.

Chay lai bao nhieu lan cung duoc: luon dung tu ban goc *_TRUOC_NHANXET.docx.
"""
import copy
import os
import re
import shutil
import time

from docx import Document
from docx.enum.table import WD_ALIGN_VERTICAL, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Emu, Pt
from docx.text.paragraph import Paragraph
from docx.text.run import Run

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")
GOC = FILE.replace(".docx", "_TRUOC_NHANXET.docx")
BAN_PHIEU = FILE.replace(".docx", "_BAN_PHIEU_NHANXET.docx")

TEN = ("Giải mã có ràng buộc số kiểm tra ISO 6346 nhằm nâng cao độ chính xác nhận dạng "
       "mã container trong điều kiện ảnh suy giảm chất lượng")
TEN_CU_HOA = ("NÂNG CAO ĐỘ CHÍNH XÁC MÔ HÌNH HỌC SÂU NHẬN DẠNG MÃ CONTAINER BẰNG GIẢI MÃ CÓ "
              "RÀNG BUỘC ISO 6346")

# =====================================================================================
# NOI DUNG. Danh dau: **dam**  __nghieng__  ^tren^  ~duoi~  {KHOA} = trich dan
# Moi phan tu: (kieu, noi_dung); kieu: body | nhan | gach
# =====================================================================================

MUC_8 = [
    ("body",
     "Vận tải container là phương thức chủ đạo trong chuỗi cung ứng hàng hóa; riêng tại Việt "
     "Nam, sản lượng container thông qua cảng biển năm 2025 đạt khoảng 34,5 triệu TEU {VIMAWA}. "
     "Cổng ra vào của cảng biển, cảng cạn hoặc depot container là điểm nghẽn cố hữu của quy trình "
     "khai thác. Nghiên cứu tình huống tại cảng DCT Gdańsk ghi nhận thời gian trễ điển hình tại "
     "cổng vào từ bốn đến năm phút cho mỗi lượt xe; ngay cả khi cổng đã được tự động hóa bằng hệ "
     "thống nhận dạng ký tự quang học (Optical Character Recognition, OCR), khoảng 3–5% lượt "
     "nhận dạng thất bại do mã số bị hư hại và phải chuyển sang hiệu chỉnh thủ công, với thời "
     "gian hiệu chỉnh ước tính trung bình 75 giây mỗi trường hợp {MOSZYK}. Nghiên cứu về hệ "
     "thống OCR tại cảng Tangier Med cho biết tỷ lệ lỗi nhận dạng có thể dao động từ 10% đến 30% "
     "trong các điều kiện bất lợi {GARMOUCH}. Ở quy mô khai thác nêu trên, dù chỉ một tỷ lệ nhỏ "
     "lượt nhận dạng thất bại cũng tạo ra khối lượng xử lý thủ công đáng kể. Nguyên nhân chủ yếu "
     "đến từ đặc thù của đối tượng: mã container được in trên vách thép gợn sóng nên ký tự bị "
     "biến dạng và đứt nét, bề mặt vỏ thường bị ăn mòn, trầy xước hoặc bám bẩn, và mã được in "
     "theo cả phương ngang lẫn phương dọc."),
    ("body",
     "Chuẩn ISO 6346 quy định mã định danh container gồm mười một ký tự: ba chữ cái mã chủ sở "
     "hữu, một chữ cái ký hiệu loại thiết bị (U, J hoặc Z), sáu chữ số sê-ri và một chữ số kiểm "
     "tra {ISO6346}. Gọi v(c~i~) là giá trị quy đổi của ký tự thứ i (i = 0, 1, …, 9; chữ số giữ "
     "nguyên giá trị, chữ cái nhận các giá trị từ 10 đến 38 và bỏ qua 11, 22, 33), số kiểm tra "
     "được xác định bởi d = (S mod 11) mod 10, trong đó S = Σ v(c~i~)·2^i^. Ràng buộc này là một "
     "nguồn tri thức tiên nghiệm có giá trị, song trong các công bố đã khảo sát, nó chỉ được dùng "
     "ở khâu hậu kiểm: chuỗi ký tự được giải mã độc lập tại từng vị trí theo nguyên tắc cực đại "
     "hóa xác suất cục bộ (argmax), chẳng hạn giải mã tham lam CTC trong {HLABISA}, sau đó số kiểm "
     "tra chỉ được dùng để xác nhận tính hợp lệ của kết quả {SANTOS}. Cách làm này loại bỏ phân "
     "phối xác suất do mô hình sinh ra: khi "
     "một ký tự bị suy giảm chất lượng, ứng viên đúng có thể chỉ xếp hạng thứ hai và bị bỏ qua, "
     "còn chuỗi không thỏa số kiểm tra bị loại toàn bộ và chuyển sang xử lý thủ công."),
    ("body",
     "Mặt khác, số kiểm tra không bảo đảm phát hiện mọi sai sót. Tại bốn vị trí chữ cái, hai "
     "mươi sáu chữ cái chỉ phân bố vào mười lớp thặng dư modulo 11, tạo thành các nhóm chữ cái "
     "đồng dư không phân biệt được như (A, K, U) hoặc (B, L, V). Tại các vị trí chữ số, bước ánh "
     "xạ phần dư 10 thành chữ số 0 gây ra hiện tượng va chạm: hai mã MKSU511193 (phần dư 10) và "
     "MKSU311193 (phần dư 0) chỉ khác nhau một chữ số nhưng đều có số kiểm tra 0. Ngoài ra, khi "
     "nhiều ký tự bị đọc sai ngẫu nhiên, chuỗi sai vẫn thỏa mãn số kiểm tra với xác suất xấp xỉ "
     "1/11, và xấp xỉ 2/11 nếu số kiểm tra bằng 0; do đó, một bộ giải mã luôn trả về chuỗi hợp lệ "
     "có thể ghi nhận nhầm sang mã của một container khác. Tại cổng cảng, sai sót này dẫn tới "
     "giao nhận nhầm container, nên nguy hiểm hơn việc từ chối kết quả và chuyển cho nhân viên "
     "kiểm tra."),
    ("body",
     "Do đó, đề tài tập trung nghiên cứu phương pháp giải mã tích hợp trực tiếp ràng buộc số kiểm "
     "tra ISO 6346 vào quá trình suy luận, kết hợp quy tắc từ chối theo độ tin cậy, đồng thời "
     "định lượng giới hạn của chính ràng buộc này. Đề tài được thực hiện trên nền SmartPort, hệ "
     "thống điều hành depot và cảng cạn do sinh viên chủ nhiệm đề tài xây dựng, có phân hệ cổng "
     "nhận dạng mã container bằng mô hình học sâu."),
]

MUC_9 = [
    ("nhan", "Mục tiêu tổng quát"),
    ("body",
     "Xây dựng và đánh giá phương pháp giải mã có ràng buộc số kiểm tra ISO 6346 nhằm nâng cao "
     "độ chính xác nhận dạng mã container trong điều kiện ảnh suy giảm chất lượng, đồng thời kiểm "
     "soát tỷ lệ chấp nhận sai và định lượng giới hạn của ràng buộc này."),
    ("nhan", "Mục tiêu cụ thể"),
    ("gach",
     "Xây dựng bộ dữ liệu kiểm thử tối thiểu 1.200 ảnh mã container, phân thành bốn tầng theo "
     "loại điều kiện chụp (thuận lợi, suy giảm bề mặt, suy giảm chiếu sáng, suy giảm hình học), "
     "mỗi tầng tối thiểu 300 ảnh và tách biệt hoàn toàn với dữ liệu huấn luyện."),
    ("gach",
     "Xây dựng phương pháp giải mã có ràng buộc số kiểm tra cho hai dạng mô hình nhận dạng (phân "
     "loại theo vị trí và CRNN – CTC), xử lý đầy đủ trường hợp phần dư 10, kèm quy tắc từ chối "
     "theo xác suất hậu nghiệm."),
    ("gach",
     "Đánh giá phương pháp so với các đường cơ sở theo độ chính xác, tỷ lệ chấp nhận sai và tỷ lệ "
     "từ chối; kiểm định giả thuyết: so với đường cơ sở mạnh nhất, phương pháp đề xuất nâng độ "
     "chính xác cấp chuỗi trên các tầng ảnh suy giảm mà không làm tăng tỷ lệ chấp nhận sai tại "
     "cùng tỷ lệ từ chối."),
    ("gach",
     "Định lượng giới hạn của số kiểm tra tại các nhóm chữ cái đồng dư và trường hợp va chạm phần "
     "dư, đồng thời đánh giá hai cơ chế bổ sung: tiên nghiệm ký tự thứ tư thuộc tập {U, J, Z} và "
     "từ điển mã chủ sở hữu đăng ký với BIC."),
    ("gach", "Hoàn thành và gửi bản thảo một bài báo khoa học trình bày kết quả nghiên cứu."),
]

MUC_10 = [
    ("body",
     "Nhận dạng mã container là bài toán nhận dạng văn bản trong cảnh thực với điều kiện đặc "
     "thù. Zhao và cộng sự {ZHAO} cải tiến khung phát hiện đối tượng DETR cho ký tự trên vỏ "
     "container. Liang và cộng sự {LIANG} đề xuất kiến trúc Transformer thị giác – ngôn ngữ tách "
     "chiều cho bài toán phát hiện và nhận dạng đồng thời ký hiệu container công nghiệp. Hlabisa "
     "và cộng sự {HLABISA} kết hợp mạng định vị trên nền ResNet và UNet với mạng nhận dạng lai "
     "tích chập – hồi tiếp huấn luyện bằng hàm mất mát CTC, đạt độ chính xác 97,93% ở tốc độ 64,11 "
     "khung hình mỗi giây. Santos và cộng sự {SANTOS} kết hợp YOLOv7 với TrOCR và dùng số kiểm "
     "tra để xác nhận mã sau khi nhận dạng, còn Ta và cộng sự {TA} đề xuất mạng CRNN tăng cường "
     "chú ý đa phân giải kết hợp khử nhiễu cho môi trường cảng thông minh. Các công trình này tập "
     "trung cải tiến kiến trúc mạng; số kiểm tra ISO 6346 chủ "
     "yếu được nhắc tới ở vai trò mô tả cấu trúc mã hoặc xác nhận kết quả, chưa được đưa vào quá "
     "trình giải mã, và rủi ro chấp nhận một mã hợp lệ nhưng sai chưa được đánh giá trong các "
     "công bố đã khảo sát."),
    ("body",
     "Việc đưa tri thức tiên nghiệm vào quá trình giải mã chuỗi là hướng nghiên cứu đã được xác "
     "lập. Trong nhận dạng tiếng nói, Miao và cộng sự {MIAO} biểu diễn từ điển và mô hình ngôn "
     "ngữ dưới dạng bộ chuyển đổi trạng thái hữu hạn có trọng số (Weighted Finite-State "
     "Transducer, WFST) và kết hợp với đầu ra CTC, qua đó giải mã có ràng buộc bằng tìm kiếm "
     "Viterbi trên tích của lưới xác suất và automat. Hokamp và Liu {HOKAMP} đề xuất thuật toán "
     "tìm kiếm chùm theo lưới, cho phép áp đặt ràng buộc từ vựng trong quá trình sinh chuỗi của "
     "mô hình tự hồi quy. Đối với các chuỗi định danh khác có số kiểm tra, tổng quan của Gayer và "
     "cộng sự {GAYER} về nhận dạng vùng đọc máy (Machine-Readable Zone, MRZ) trên giấy tờ tùy "
     "thân chỉ ra rằng các mô hình nhận dạng và mô hình ngôn ngữ chuyên biệt cho loại chuỗi này "
     "vẫn chưa được nghiên cứu đầy đủ."),
    ("body",
     "Xét về bản chất thuật toán, tập các mã thỏa mãn số kiểm tra ISO 6346 là một ngôn ngữ chính "
     "quy, được nhận biết bởi automat hữu hạn có 11 trạng thái ứng với 11 lớp thặng dư modulo 11. "
     "Giải mã có ràng buộc số kiểm tra vì vậy là trường hợp riêng của tìm kiếm Viterbi trên tích "
     "lưới xác suất và automat, cùng họ với giải mã WFST {MIAO}. Đề tài không xem thuật toán này "
     "là đóng góp mới; đóng góp được đặt vào việc vận dụng cho bài toán mã container, đánh giá "
     "thực nghiệm có đối chứng và phân tích giới hạn của chính ràng buộc."),
    ("body",
     "Tính đúng đắn của phép giải mã phụ thuộc vào dạng đầu ra của mô hình nhận dạng. Với mô hình "
     "phân loại theo vị trí cố định, trong đó mỗi vị trí ký tự có một đầu phân loại riêng theo "
     "cách tiếp cận của Goodfellow và cộng sự {GOODFELLOW}, xác suất của chuỗi là tích xác suất "
     "tại các vị trí và quy hoạch động cho nghiệm tối ưu chính xác. Với mô hình CRNN {SHI} huấn "
     "luyện bằng hàm mất mát phân loại thời gian kết nối (Connectionist Temporal Classification, "
     "CTC) {GRAVES}, ràng buộc phải được áp lên lưới căn chỉnh có ký hiệu trống (blank) thay vì "
     "lên mười một vị trí cố định, và phép giải mã cho đường căn chỉnh tốt nhất thỏa ràng buộc "
     "theo xấp xỉ Viterbi như trong giải mã WFST {MIAO}. Mô hình tự hồi quy như TrOCR {LI_TROCR} "
     "không phù hợp, vì xác "
     "suất tại mỗi vị trí phụ thuộc vào các ký tự đã sinh trước đó nên quy hoạch động theo vị trí "
     "độc lập không bảo đảm nghiệm tối ưu."),
    ("body",
     "Do phép giải mã và quyết định chấp nhận kết quả đều dựa trực tiếp vào xác suất đầu ra, chất "
     "lượng hiệu chỉnh của xác suất có vai trò quyết định. Guo và cộng sự {GUO} chỉ ra rằng mạng "
     "nơ-ron sâu hiện đại thường đưa ra xác suất quá tự tin và đề xuất phép hiệu chỉnh nhiệt độ "
     "(temperature scaling). Lý thuyết đánh đổi giữa sai sót và từ chối của Chow {CHOW} cùng khung "
     "phân loại có chọn lọc của Geifman và El-Yaniv {GEIFMAN} là cơ sở để xây dựng quy tắc từ chối "
     "theo xác suất hậu nghiệm của chuỗi được chọn."),
    ("body",
     "Các nghiên cứu đã khảo sát cho thấy độ chính xác nhận dạng mã container đã được cải thiện "
     "đáng kể nhờ cải tiến kiến trúc mạng, trong khi tri thức đại số của chuẩn ISO 6346 chưa được "
     "khai thác trong khâu giải mã. Đề tài sẽ khảo sát việc tích hợp ràng buộc này vào quá trình "
     "giải mã, kết hợp với quy tắc từ chối, và định lượng giới hạn của nó tại các nhóm chữ cái "
     "đồng dư và trường hợp va chạm phần dư, qua đó cung cấp bằng chứng thực nghiệm cho việc áp "
     "dụng trong vận hành cổng cảng."),
]

MUC_11 = [
    ("nhan", "Đối tượng nghiên cứu:"),
    ("gach", "Phép giải mã chuỗi ký tự dưới ràng buộc số kiểm tra ISO 6346 trên đầu ra của mô "
             "hình nhận dạng mã container."),
    ("gach", "Quy tắc chấp nhận hoặc từ chối kết quả theo xác suất hậu nghiệm của chuỗi được "
             "giải mã."),
    ("gach", "Giới hạn của số kiểm tra tại các nhóm chữ cái đồng dư và trường hợp va chạm phần dư "
             "0 và 10."),
    ("nhan", "Phạm vi nghiên cứu:"),
    ("body",
     "Đề tài tập trung vào khâu giải mã chuỗi ký tự và quyết định chấp nhận kết quả, không đề "
     "xuất kiến trúc mạng mới cho tầng phát hiện và tầng nhận dạng. Hai mô hình nền (phân loại "
     "theo vị trí và CRNN – CTC) được tinh chỉnh trên tập huấn luyện rồi giữ cố định trong toàn "
     "bộ thực nghiệm. Với mã in dọc hoặc xếp thành nhiều dòng, các vùng văn bản được ghép theo "
     "thứ tự đọc thành chuỗi mười một ký tự trước khi giải mã."),
    ("body", "Dữ liệu dự kiến gồm:"),
    ("gach", "Bộ dữ liệu ký hiệu container được công bố kèm nghiên cứu {LIANG} và các bộ dữ liệu "
             "ảnh mã container công khai khác;"),
    ("gach", "Ảnh chụp thực địa tại cảng cạn hoặc depot khi được đơn vị quản lý chấp thuận."),
    ("body",
     "Tập huấn luyện và tập kiểm định được lấy từ dữ liệu công khai; tập kiểm thử được tách biệt "
     "với hai tập này theo mã container và theo đợt chụp. Ảnh chỉ có một loại suy giảm được xếp "
     "vào tầng tương ứng; ảnh có từ hai loại trở lên được xếp vào nhóm đa điều kiện và báo cáo "
     "riêng. Trường hợp không được chấp thuận chụp thực địa, tập kiểm thử được xây dựng từ dữ "
     "liệu công khai và tầng nào không đủ cỡ mẫu sẽ được báo cáo kèm khoảng tin cậy tương ứng."),
    ("body",
     "Đề tài không nghiên cứu bài toán nhận dạng biển số phương tiện do quy cách biển số Việt Nam "
     "không có cơ chế số kiểm tra; việc tích hợp vào hệ thống SmartPort chỉ nhằm minh họa khả năng "
     "triển khai."),
    ("nhan", "Cách tiếp cận:"),
    ("body",
     "Đề tài tiếp cận theo hướng nghiên cứu thực nghiệm có đối chứng. Ràng buộc số kiểm tra được "
     "biểu diễn bằng automat hữu hạn có trạng thái là phần dư modulo 11 của tổng có trọng số; "
     "trạng thái kết thúc được chấp nhận khi phần dư, sau khi lấy modulo 10, trùng với chữ số "
     "kiểm tra, nhờ đó trường hợp phần dư 10 được xử lý trực tiếp. Phép giải mã được thực hiện "
     "bằng quy hoạch động trên tích của lưới xác suất và automat; xác suất hậu nghiệm của chuỗi "
     "được chọn được tính bằng thuật toán tiến trên cùng lưới và dùng làm căn cứ từ chối."),
    ("body",
     "Thực nghiệm được thiết kế theo dạng loại bỏ từng thành phần, mỗi cấu hình chỉ khác cấu hình "
     "liền trước ở một yếu tố: (B0) argmax tự do; (B1) argmax có ràng buộc loại ký tự, bốn vị trí "
     "đầu là chữ cái và bảy vị trí sau là chữ số; (B2) B1 kết hợp hậu kiểm số kiểm tra; (B3) B2 "
     "mở rộng thành liệt kê hai ứng viên tốt nhất tại mỗi vị trí, tức 2^11^ = 2.048 tổ hợp; (P1) "
     "giải mã có ràng buộc trên toàn bộ phân phối bằng quy hoạch động; (P2) P1 kết hợp tiên nghiệm "
     "ký tự thứ tư; (P3) P2 kết hợp từ điển mã chủ sở hữu BIC. Các cấu hình được so sánh ở chế độ "
     "luôn trả kết quả và tại cùng tỷ lệ từ chối."),
    ("nhan", "Phương pháp nghiên cứu:"),
    ("gach", "Nghiên cứu lý thuyết: khảo sát các công trình về nhận dạng mã container, CTC, giải "
             "mã WFST, hiệu chỉnh độ tin cậy và tùy chọn từ chối."),
    ("gach", "Xây dựng dữ liệu: hai thành viên gán nhãn độc lập; nhãn được kiểm tra chéo bằng số "
             "kiểm tra ISO 6346; mức đồng thuận gán tầng được đo bằng hệ số Cohen’s κ. Cỡ mẫu 300 "
             "ảnh mỗi tầng được xác định bằng phân tích lực kiểm định cho kiểm định McNemar (α = "
             "0,05; lực kiểm định 0,8; chênh lệch cần phát hiện 5 điểm phần trăm; tỷ lệ cặp bất đồng "
             "giả định 9%, được ước lượng lại trên 100 ảnh đầu tiên). Từ điển mã chủ sở hữu được "
             "xây dựng từ tra cứu công khai trên cơ sở dữ liệu BIC; tỷ lệ mã nằm ngoài từ điển được "
             "báo cáo riêng."),
    ("gach", "Thực nghiệm: cài đặt các cấu hình bằng PyTorch; tinh chỉnh mô hình nền với ba hạt "
             "giống ngẫu nhiên và hiệu chỉnh nhiệt độ trên tập kiểm định; kết quả được báo cáo dưới "
             "dạng trung bình và độ lệch chuẩn qua ba lần tinh chỉnh."),
    ("gach", "So sánh: đánh giá theo độ chính xác cấp ký tự và cấp chuỗi, tỷ lệ chấp nhận sai, tỷ "
             "lệ từ chối và đường cong độ chính xác – từ chối; kiểm định McNemar kèm khoảng tin cậy "
             "bootstrap 95%. Thời gian giải mã được đo lặp, loại bỏ các lần chạy khởi động."),
    ("gach", "Phân tích lỗi: phân loại các trường hợp nhận dạng sai theo nguyên nhân (ăn mòn, "
             "ngược sáng, góc chụp, nhóm chữ cái đồng dư, va chạm phần dư)."),
]

CHUONG = [
    ("CHƯƠNG 1. GIỚI THIỆU", [
        "1.1. Lý do chọn đề tài",
        "1.2. Mục tiêu nghiên cứu",
        "1.3. Đối tượng và phạm vi nghiên cứu",
        "1.4. Phương pháp nghiên cứu"]),
    ("CHƯƠNG 2. CƠ SỞ LÝ THUYẾT", [
        "2.1. Chuẩn ISO 6346 và phép tính số kiểm tra",
        "2.2. Mô hình nhận dạng phân loại theo vị trí và CRNN – CTC",
        "2.3. Giải mã có ràng buộc bằng automat hữu hạn",
        "2.4. Hiệu chỉnh độ tin cậy và tùy chọn từ chối"]),
    ("CHƯƠNG 3. PHƯƠNG PHÁP GIẢI MÃ CÓ RÀNG BUỘC", [
        "3.1. Phát biểu bài toán",
        "3.2. Automat số kiểm tra và thuật toán quy hoạch động",
        "3.3. Mở rộng cho lưới căn chỉnh CTC",
        "3.4. Xác suất hậu nghiệm và quy tắc từ chối",
        "3.5. Tiên nghiệm ký hiệu loại thiết bị và từ điển mã chủ sở hữu"]),
    ("CHƯƠNG 4. DỮ LIỆU VÀ MÔ HÌNH NỀN", [
        "4.1. Thu thập dữ liệu và phân tích cỡ mẫu",
        "4.2. Gán nhãn, phân tầng và kiểm tra chéo",
        "4.3. Tinh chỉnh mô hình nền và hiệu chỉnh nhiệt độ"]),
    ("CHƯƠNG 5. THỰC NGHIỆM VÀ ĐÁNH GIÁ", [
        "5.1. Thiết lập thực nghiệm và các đường cơ sở",
        "5.2. Kết quả độ chính xác theo từng tầng",
        "5.3. Tỷ lệ chấp nhận sai và tỷ lệ từ chối",
        "5.4. Kiểm định ý nghĩa thống kê",
        "5.5. Phân tích giới hạn và các trường hợp nhận dạng sai"]),
    ("CHƯƠNG 6. KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN", [
        "6.1. Kết luận",
        "6.2. Hướng phát triển"]),
]

HAI = "Lê Hữu Anh Tài\nNguyễn Quang Long"
TIEN_DO = [
    ("Tháng 10-2026",
     "Khảo sát, tổng quan tài liệu; chốt mô hình nền, chỉ số đánh giá và các đường cơ sở; liên "
     "hệ xin phép chụp ảnh thực địa",
     "Báo cáo tổng quan, tài liệu tham khảo", HAI),
    ("Từ tháng 11-2026 đến tháng 12-2026",
     "Thu thập, gán nhãn hai lượt độc lập và phân tầng dữ liệu; cài đặt phép giải mã có ràng "
     "buộc; tinh chỉnh mô hình nền",
     "Bộ dữ liệu kiểm thử, mã nguồn phép giải mã, mô hình nền", HAI),
    ("Tháng 1-2027",
     "Thực nghiệm đối chứng và loại bỏ thành phần; kiểm định thống kê; báo cáo giữa kỳ",
     "Kết quả thực nghiệm, bảng và biểu đồ đánh giá", HAI),
    ("Tháng 2-2027",
     "Phân tích giới hạn của số kiểm tra và các trường hợp nhận dạng sai; soạn và gửi bản thảo "
     "bài báo",
     "Báo cáo phân tích, bản thảo bài báo", HAI),
    ("Tháng 3-2027",
     "Tích hợp minh họa vào hệ thống SmartPort; dự phòng, bổ sung thực nghiệm và hoàn thiện "
     "báo cáo",
     "Ứng dụng minh họa, báo cáo tổng kết", HAI),
]

MUC_13 = [
    ("nhan", "Sản phẩm đạt được:"),
    ("gach", "Phương pháp giải mã có ràng buộc số kiểm tra ISO 6346 kèm quy tắc từ chối, được cài "
             "đặt cho hai dạng mô hình nhận dạng."),
    ("gach", "Bộ dữ liệu kiểm thử ảnh mã container phân thành bốn tầng theo loại điều kiện chụp, "
             "gán nhãn theo chuẩn ISO 6346, kèm tài liệu quy tắc gán nhãn."),
    ("gach", "Báo cáo kết quả thực nghiệm đối chứng và phân tích giới hạn của số kiểm tra."),
    ("gach", "Ứng dụng minh họa tích hợp phương pháp vào phân hệ cổng của hệ thống SmartPort, cho "
             "phép tải ảnh container lên và hiển thị mã nhận dạng, độ tin cậy và quyết định chấp "
             "nhận hoặc từ chối."),
    ("gach", "Bản thảo bài báo khoa học và báo cáo tổng kết đề tài."),
    ("nhan", "Khả năng ứng dụng:"),
    ("body",
     "Kết quả đề tài góp phần nâng cao tỷ lệ ghi nhận tự động chính xác mã container tại cổng "
     "cảng, cảng cạn và depot, đồng thời chuyển các trường hợp có độ tin cậy thấp cho nhân viên "
     "kiểm tra thay vì ghi nhận nhầm, qua đó giảm số lượt hiệu chỉnh thủ công. Phương pháp chỉ "
     "can thiệp vào khâu giải "
     "mã nên tích hợp được vào hệ thống nhận dạng sẵn có mà không cần huấn luyện lại mô hình, và "
     "có thể mở rộng cho các chuỗi định danh khác có số kiểm tra như vùng đọc máy trên giấy tờ tùy "
     "thân hoặc mã số sách ISBN-10."),
]

# IEEE viet tat giong mau Sang; khong ghi DOI. Tac gia/tap/so/trang da doi chieu Crossref 29/09/2026.
REFS = {
    # So lieu 2025 = tong 12 thang cot CONT_TEU trong Khac/HoSo/BaoCao/DuLieuThat/container_vn_thang.csv
    # (34.464 nghin TEU), lam sach tu bao cao thang cua Cuc Hang hai, tai ngay 17/09/2026.
    "VIMAWA": "Cục Hàng hải và Đường thủy Việt Nam, “Thống kê sản lượng hàng hóa thông qua cảng "
              "biển Việt Nam,” truy cập ngày 17/09/2026. [Trực tuyến]. Địa chỉ: "
              "https://vimawa.gov.vn/vi/thong-ke",
    "MOSZYK":"K. Moszyk, M. Deja, and M. Dobrzynski, “Automation of the road gate operations "
              "process at the container terminal—A case study of DCT Gdańsk SA,” __Sustainability__, "
              "vol. 13, no. 11, Art. no. 6291, 2021.",
    "GARMOUCH": "H. Garmouch and O. Abdoun, “Enhancing crane and gate OCR efficiency at container "
                "terminal using a hybrid genetic algorithm and neural network model: Case study of "
                "Tangier Med port,” __Discover Appl. Sci.__, vol. 7, no. 7, Art. no. 714, 2025.",
    "ISO6346": "__Freight Containers — Coding, Identification and Marking__, ISO 6346:2022, "
               "International Organization for Standardization, 2022.",
    "HLABISA": "S. Hlabisa, R. L. Khuboni, and J.-R. Tapamo, “Confidence-guided code recognition for "
               "shipping containers using deep learning,” __Big Data Cogn. Comput.__, vol. 9, no. 12, "
               "Art. no. 316, 2025.",
    "ZHAO": "X. Zhao, P. Zhou, K. Xu, and L. Xiao, “An improved character recognition framework for "
            "containers based on DETR algorithm,” __Sensors__, vol. 21, no. 13, Art. no. 4612, 2021.",
    "LIANG": "Z. Liang __et al.__, “Dimension decoupling vision-language transformer for industrial "
             "container marking and natural scene text spotting,” __J. Ind. Inf. Integr.__, vol. 48, "
             "Art. no. 100926, 2025.",
    "SANTOS": "J. Santos, D. Canedo, and A. J. R. Neves, “Automating code recognition for cargo "
              "containers,” __Electronics__, vol. 14, no. 22, Art. no. 4437, 2025.",
    "TA": "A. S. Ta, L. T. Le, and L. Bui-Duy, “Advanced vehicle monitoring in smart port utilizing "
          "deep denoising real-time object detectors integrated multi-resolution attention-augmented "
          "CRNN,” __Ain Shams Eng. J.__, vol. 16, no. 8, Art. no. 103483, 2025.",
    "MIAO": "Y. Miao, M. Gowayyed, and F. Metze, “EESEN: End-to-end speech recognition using deep "
            "RNN models and WFST-based decoding,” in __Proc. IEEE Workshop Autom. Speech Recognit. "
            "Understanding (ASRU)__, 2015, pp. 167–174.",
    "HOKAMP": "C. Hokamp and Q. Liu, “Lexically constrained decoding for sequence generation using "
              "grid beam search,” in __Proc. 55th Annu. Meeting Assoc. Comput. Linguist. (ACL)__, "
              "2017, pp. 1535–1546.",
    "GAYER": "A. V. Gayer, Y. S. Chernyshova, and V. V. Arlazarov, “Recognition of machine-readable "
             "zone in identity documents: A review,” __IEEE Access__, vol. 13, pp. 89300–89313, 2025.",
    "GOODFELLOW": "I. J. Goodfellow, Y. Bulatov, J. Ibarz, S. Arnoud, and V. Shet, “Multi-digit "
                  "number recognition from Street View imagery using deep convolutional neural "
                  "networks,” in __Proc. Int. Conf. Learn. Represent. (ICLR)__, 2014.",
    "SHI": "B. Shi, X. Bai, and C. Yao, “An end-to-end trainable neural network for image-based "
           "sequence recognition and its application to scene text recognition,” __IEEE Trans. "
           "Pattern Anal. Mach. Intell.__, vol. 39, no. 11, pp. 2298–2304, 2017.",
    "GRAVES": "A. Graves, S. Fernández, F. Gomez, and J. Schmidhuber, “Connectionist temporal "
              "classification: Labelling unsegmented sequence data with recurrent neural "
              "networks,” in __Proc. 23rd Int. Conf. Mach. Learn. (ICML)__, 2006, pp. 369–376.",
    "LI_TROCR": "M. Li __et al.__, “TrOCR: Transformer-based optical character recognition with "
                "pre-trained models,” in __Proc. AAAI Conf. Artif. Intell.__, vol. 37, no. 11, 2023, "
                "pp. 13094–13102.",
    "GUO": "C. Guo, G. Pleiss, Y. Sun, and K. Q. Weinberger, “On calibration of modern neural "
           "networks,” in __Proc. 34th Int. Conf. Mach. Learn. (ICML)__, 2017, pp. 1321–1330.",
    "CHOW": "C. K. Chow, “On optimum recognition error and reject tradeoff,” __IEEE Trans. Inf. "
            "Theory__, vol. 16, no. 1, pp. 41–46, 1970.",
    "GEIFMAN": "Y. Geifman and R. El-Yaniv, “Selective classification for deep neural networks,” in "
               "__Proc. Adv. Neural Inf. Process. Syst. (NeurIPS)__, vol. 30, 2017.",
}

# =====================================================================================
# Danh so trich dan theo thu tu xuat hien dau tien
# =====================================================================================
_thu_tu = []
for _, chuoi in MUC_8 + MUC_9 + MUC_10 + MUC_11:
    for k in re.findall(r"\{([A-Z0-9_]+)\}", chuoi):
        if k not in _thu_tu:
            _thu_tu.append(k)
SO = {k: i + 1 for i, k in enumerate(_thu_tu)}
if set(REFS) != set(SO):
    raise SystemExit(f"Lech trich dan: thua {set(REFS) - set(SO)}, thieu {set(SO) - set(REFS)}")


def gan_so(chuoi):
    return re.sub(r"\{([A-Z0-9_]+)\}", lambda m: f"[{SO[m.group(1)]}]", chuoi)


# =====================================================================================
# Cong cu XML
# =====================================================================================
TOKEN = re.compile(r"(\*\*.+?\*\*|__.+?__|\^.+?\^|~.+?~)")


def phan_tich(markup):
    ra = []
    for tok in TOKEN.split(markup):
        if not tok:
            continue
        if tok.startswith("**"):
            ra.append((tok[2:-2], True, False, None))
        elif tok.startswith("__"):
            ra.append((tok[2:-2], False, True, None))
        elif tok.startswith("^"):
            ra.append((tok[1:-1], False, False, "superscript"))
        elif tok.startswith("~"):
            ra.append((tok[1:-1], False, False, "subscript"))
        else:
            ra.append((tok, False, False, None))
    return ra


def tao_run(text, dam=False, nghieng=False, vi_tri=None, co=26):
    r = OxmlElement("w:r")
    rpr = OxmlElement("w:rPr")
    f = OxmlElement("w:rFonts")
    for a in ("ascii", "hAnsi", "cs", "eastAsia"):
        f.set(qn("w:" + a), "Times New Roman")
    rpr.append(f)
    for tag, bat in (("w:b", dam), ("w:bCs", dam), ("w:i", nghieng), ("w:iCs", nghieng)):
        e = OxmlElement(tag)
        if not bat:
            e.set(qn("w:val"), "0")
        rpr.append(e)
    for tag in ("w:sz", "w:szCs"):
        e = OxmlElement(tag)
        e.set(qn("w:val"), str(co))
        rpr.append(e)
    if vi_tri:
        e = OxmlElement("w:vertAlign")
        e.set(qn("w:val"), vi_tri)
        rpr.append(e)
    r.append(rpr)
    for k, manh in enumerate(text.split("\t")):
        if k:
            r.append(OxmlElement("w:tab"))
        if manh:
            t = OxmlElement("w:t")
            t.set(qn("xml:space"), "preserve")
            t.text = manh
            r.append(t)
    return r


THUT_DAU = Emu(400050)  # 1,11 cm - dung so do cua mau Sang


def tao_doan(kieu, markup):
    """kieu: body | nhan | gach | ref | ch | sec | trong"""
    p = OxmlElement("w:p")
    para = Paragraph(p, None)
    pf = para.paragraph_format
    pf.right_indent = Cm(0)
    pf.space_before = Pt(0)
    pf.space_after = Pt(0)
    pf.line_spacing = 1.5
    para.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    co = 26  # 13 pt nhu than bai mau Sang
    if kieu in ("body", "nhan", "trong"):
        pf.left_indent = Cm(0)
        pf.first_line_indent = THUT_DAU
    elif kieu == "gach":
        pf.left_indent = Cm(1.75)
        pf.first_line_indent = Cm(-0.64)
        markup = "–\t" + markup
    elif kieu == "ref":
        pf.left_indent = Cm(0)
        pf.first_line_indent = Cm(0)
        pf.line_spacing = 1.0
        para.alignment = WD_ALIGN_PARAGRAPH.LEFT
    elif kieu in ("ch", "sec"):
        pf.left_indent = Cm(0)
        pf.first_line_indent = Cm(1.0)
        para.alignment = WD_ALIGN_PARAGRAPH.LEFT
    if kieu in ("nhan", "ch"):
        pf.keep_with_next = True
    dam_het = kieu in ("nhan", "ch")
    for text, b, i, vt in phan_tich(gan_so(markup)):
        p.append(tao_run(text, b or dam_het, i, vt, co))
    return p


def text_of(el):
    return "".join(t.text or "" for t in el.iter(qn("w:t")))


def luu(doc, dich, lan=5, cho=2.0):
    for i in range(lan):
        try:
            doc.save(dich)
            return dich
        except PermissionError:
            if i == lan - 1:
                alt = dich.replace(".docx", "_MOI.docx")
                doc.save(alt)
                print(f"CANH BAO: file dang mo trong Word -> ghi ra {os.path.basename(alt)}")
                return alt
            time.sleep(cho)


# =====================================================================================
# THUC HIEN
# =====================================================================================
if not os.path.exists(GOC):
    raise SystemExit("Khong thay ban goc " + GOC)
if os.path.exists(FILE) and not os.path.exists(BAN_PHIEU):
    shutil.copyfile(FILE, BAN_PHIEU)
    print("Da giu ban theo phieu nhan xet:", os.path.basename(BAN_PHIEU))

doc = Document(GOC)
body = doc.element.body
T_THANHVIEN, T_MUCTIEU, T_TIENDO, T_KY = doc.tables[:4]


def tim_p(bat_dau):
    for el in body.iterchildren(qn("w:p")):
        if text_of(el).strip().startswith(bat_dau):
            return el
    raise SystemExit(f"Khong tim thay doan: {bat_dau!r}")


def xoa_giua(dau_el, cuoi_bat_dau):
    """Xoa moi doan (ke ca doan trong) giua dau_el va doan bat dau bang cuoi_bat_dau; giu bang."""
    el = dau_el.getnext()
    while el is not None:
        nxt = el.getnext()
        if el.tag == qn("w:p"):
            if text_of(el).strip().startswith(cuoi_bat_dau):
                break
            body.remove(el)
        el = nxt


def chen_sau(neo, cac):
    for e in cac:
        neo.addnext(e)
        neo = e
    return neo


def khoi(muc):
    return [tao_doan(k, nd) for k, nd in muc]


def thay_trong_doan(p, cu, moi):
    for t in p.iter(qn("w:t")):
        if t.text and cu in t.text:
            t.text = t.text.replace(cu, moi)
            return True
    return False


# ---------------------------------------------------------------- Trang bia
n_ten = n_so3 = n_ngay = 0
for tx in body.iter(qn("w:txbxContent")):
    for p in list(tx.iter(qn("w:p"))):
        if p.find(".//" + qn("w:p")) is not None:
            continue
        t = text_of(p).strip()
        if t == TEN_CU_HOA:
            ts = list(p.iter(qn("w:t")))
            ts[0].text = TEN.upper()
            for x in ts[1:]:
                x.text = ""
            n_ten += 1
        elif t == "3.":
            p.getparent().remove(p)
            n_so3 += 1
        elif t.startswith("Thành phố Hồ Chí Minh, ngày"):
            ts = list(p.iter(qn("w:t")))
            ghep = "".join(x.text or "" for x in ts)
            ghep = re.sub(r"tháng\s+năm", "tháng   9   năm", ghep)
            ts[0].text = ghep
            for x in ts[1:]:
                x.text = ""
            n_ngay += 1
print(f"Bia: ten {n_ten} | bo dong '3.' {n_so3} | dien thang 9: {n_ngay}")

# ---------------------------------------------------------------- Muc 1: ten cung dong
h1 = tim_p("1. Tên đề tài")
p_ten = h1.getnext()
while not text_of(p_ten).strip():
    p_ten = p_ten.getnext()
body.remove(p_ten)
for t in h1.iter(qn("w:t")):
    t.text = t.text.rstrip() + " " if t.text and "Tên đề tài" in t.text else t.text
h1.append(tao_run(TEN, dam=True, co=24))

# ---------------------------------------------------------------- Muc 4, muc 7
p4 = tim_p("4. Thời gian thực hiện")
thay_trong_doan(p4, "06", "6")
p4b = tim_p("Từ tháng")
thay_trong_doan(p4b, "03", "3")
p7 = tim_p("Thông tin SV chịu trách nhiệm chính")
p7.append(tao_run(" Lê Hữu Anh Tài", co=24))


def ghi_o(cell, markup, canh="l", dam=False, co=24):
    tc = cell._tc
    for p in list(tc.iter(qn("w:p"))):
        p.getparent().remove(p)
    p = OxmlElement("w:p")
    tc.append(p)
    para = Paragraph(p, cell)
    pf = para.paragraph_format
    pf.left_indent = Cm(0)
    pf.right_indent = Cm(0)
    pf.first_line_indent = Cm(0)
    pf.line_spacing = 1.15
    pf.space_before = Pt(2)
    pf.space_after = Pt(2)
    para.alignment = {"c": WD_ALIGN_PARAGRAPH.CENTER, "l": WD_ALIGN_PARAGRAPH.LEFT}[canh]
    for k, d in enumerate(markup.split("\n")):
        if k:
            br = OxmlElement("w:r")
            br.append(OxmlElement("w:br"))
            p.append(br)
        for text, b, i, vt in phan_tich(d):
            p.append(tao_run(text, b or dam, i, vt, co))
    cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER


ghi_o(T_THANHVIEN.rows[2].cells[4], "", "c")  # mau Sang de trong o ghi chu thanh vien

# ---------------------------------------------------------------- Muc 8
h8 = tim_p("8. Tính cấp thiết")
xoa_giua(h8, "9. Mục tiêu")
chen_sau(h8, khoi(MUC_8))

# ---------------------------------------------------------------- Muc 9 (bo bang muc tieu)
h9 = tim_p("9. Mục tiêu")
body.remove(T_MUCTIEU._tbl)
xoa_giua(h9, "10. Tổng quan")
chen_sau(h9, khoi(MUC_9))

# ---------------------------------------------------------------- Muc 10 + tai lieu
h10 = tim_p("10. Tổng quan")
xoa_giua(h10, "11. Đối tượng")
cac = khoi(MUC_10)
nhan_tl = tao_doan("nhan", "Tài liệu tham khảo")
Paragraph(nhan_tl, None).paragraph_format.line_spacing = 1.0
cac.append(nhan_tl)
cac += [tao_doan("ref", f"[{SO[k]}] " + REFS[k]) for k in _thu_tu]
chen_sau(h10, cac)

# ---------------------------------------------------------------- Muc 11
h11 = tim_p("11. Đối tượng")
xoa_giua(h11, "12. Nội dung")
chen_sau(h11, khoi(MUC_11))

# ---------------------------------------------------------------- Muc 12.1
h121 = tim_p("12.1.")
xoa_giua(h121, "12.2.")
cac = []
for ch, secs in CHUONG:
    cac.append(tao_doan("ch", ch))
    cac += [tao_doan("sec", s) for s in secs]
chen_sau(h121, cac)

# ---------------------------------------------------------------- Muc 12.2
h122 = tim_p("12.2.")
xoa_giua(h122, "13. Sản phẩm")
while len(T_TIENDO.rows) > len(TIEN_DO) + 1:
    T_TIENDO._tbl.remove(T_TIENDO.rows[-1]._tr)
for i, (tg, nd, sp, nguoi) in enumerate(TIEN_DO, start=1):
    r = T_TIENDO.rows[i]
    ghi_o(r.cells[0], tg)
    ghi_o(r.cells[1], nd)
    ghi_o(r.cells[2], sp)
    ghi_o(r.cells[3], nguoi)


def dat_rong(tbl, cols):
    g = tbl._tbl.find(qn("w:tblGrid"))
    for gc in list(g):
        g.remove(gc)
    for w in cols:
        gc = OxmlElement("w:gridCol")
        gc.set(qn("w:w"), str(w))
        g.append(gc)
    for r in tbl.rows:
        trpr = r._tr.get_or_add_trPr()
        for h in trpr.findall(qn("w:trHeight")):
            trpr.remove(h)
        cot = 0
        for tc in r._tr.findall(qn("w:tc")):
            tcPr = tc.get_or_add_tcPr()
            gs = tcPr.find(qn("w:gridSpan"))
            span = int(gs.get(qn("w:val"))) if gs is not None else 1
            tcW = tcPr.find(qn("w:tcW"))
            if tcW is None:
                tcW = OxmlElement("w:tcW")
                tcPr.insert(0, tcW)
            tcW.set(qn("w:w"), str(sum(cols[cot:cot + span])))
            tcW.set(qn("w:type"), "dxa")
            cot += span
    tblPr = tbl._tbl.find(qn("w:tblPr"))
    tblW = tblPr.find(qn("w:tblW"))
    if tblW is None:
        tblW = OxmlElement("w:tblW")
        tblPr.append(tblW)
    tblW.set(qn("w:w"), str(sum(cols)))
    tblW.set(qn("w:type"), "dxa")
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    tbl.autofit = False


dat_rong(T_TIENDO, (1800, 2770, 1900, 2600))

# ---------------------------------------------------------------- Muc 13
h13 = tim_p("13. Sản phẩm")
xoa_giua(h13, "14. Kinh phí")
chen_sau(h13, khoi(MUC_13))

# ---------------------------------------------------------------- Bang: thut le, font, ngat trang
for tb in (T_THANHVIEN, T_TIENDO):
    for ri, r in enumerate(tb.rows):
        trpr = r._tr.get_or_add_trPr()
        if trpr.find(qn("w:cantSplit")) is None:
            trpr.append(OxmlElement("w:cantSplit"))
        if ri == 0 and trpr.find(qn("w:tblHeader")) is None:
            trpr.append(OxmlElement("w:tblHeader"))
        for c in r.cells:
            for p in c.paragraphs:
                pf = p.paragraph_format
                pf.left_indent = Cm(0)
                pf.right_indent = Cm(0)
                pf.first_line_indent = Cm(0)
                if ri == 0:
                    pf.keep_with_next = True
                for run in p.runs:
                    run.font.name = "Times New Roman"
                    run._element.get_or_add_rPr().get_or_add_rFonts().set(
                        qn("w:eastAsia"), "Times New Roman")

# ---------------------------------------------------------------- Mau chu + tieu de muc
for p in body.iterchildren(qn("w:p")):
    for c in list(p.iter(qn("w:color"))):
        if any(a.tag == qn("w:txbxContent") for a in c.iterancestors()):
            continue
        c.getparent().remove(c)

LA_TIEU_DE = re.compile(r"^(\d{1,2}\.\s|12\.[12]\.\s)")
for p in body.iterchildren(qn("w:p")):
    t = text_of(p).strip()
    if LA_TIEU_DE.match(t) and len(t) < 200:
        pf = Paragraph(p, None).paragraph_format
        pf.keep_with_next = True
        if re.match(r"^(8|9|10|11|12|13|14)\.\s", t):
            pf.space_before = Pt(6)

# Mau Sang dung 13 pt cho moi chu ngoai trang bia (ke ca muc 1-7, bang va danh muc TLTK)
n_co = 0
for el in body.iterchildren():
    if el.tag not in (qn("w:p"), qn("w:tbl")):
        continue
    for r in el.iter(qn("w:r")):
        if any(a.tag == qn("w:txbxContent") for a in r.iterancestors()):
            continue
        # Dung API python-docx de w:sz duoc chen dung thu tu schema trong w:rPr
        Run(r, None).font.size = Pt(13)
        szcs = r.find(qn("w:rPr") + "/" + qn("w:szCs"))
        if szcs is not None:
            szcs.set(qn("w:val"), "26")
        n_co += 1
print(f"Co chu 13 pt: {n_co} run")

out = luu(doc, FILE)

# Ban goc con ten nhom/GVHD cu -> dat lai theo thong tin chot 30/09/2026 (nhom truong Hung,
# GVHD ThS. Trinh Trong Thanh). Sua thong tin nhom thi sua trong dong_bo_thanh_vien.py.
import dong_bo_thanh_vien  # noqa: E402
dong_bo_thanh_vien.ap_dung(out)
tu = sum(len(text_of(p).split()) for p in body.iter(qn("w:p")))
print(f"Da luu: {out}\nTai lieu tham khao: {len(_thu_tu)} | so tu ~{tu}")
