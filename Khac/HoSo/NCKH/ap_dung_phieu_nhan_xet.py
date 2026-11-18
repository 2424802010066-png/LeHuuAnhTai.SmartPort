# -*- coding: utf-8 -*-
"""
Ap dung phieu nhan xet de cuong (NhanXet_DeCuong_NCKH_LeHuuAnhTai.docx, 29/09/2026).

Script luon dung lai tu ban sao luu *_TRUOC_NHANXET.docx nen chay lai bao nhieu lan
cung cho cung mot ket qua.

Nhung thay doi chinh theo phieu:
  1. Ten de tai moi (muc 1 + trang bia).
  2. Muc 8: viet thang cong thuc so kiem tra, va cham phan du 0/10 (MKSU511193 va
     MKSU311193), rui ro "sua thanh ma hop le khac"; them so lieu thuc tien co trich dan.
  3. Muc 9: MT1 co co mau cu the, MT2 bo tieu chi 5%, MT4 neu ro co che U/J/Z + BIC,
     MT5 ghi "gui ban thao".
  4. Muc 10: chot mo hinh nen (phan loai theo vi tri + CRNN-CTC), dinh vi lai tinh moi
     (Viterbi x automat / WFST la ky thuat da co), them muc do tin cay va tu choi.
  5. Muc 11: tach doi tuong / khach the, them baseline B1 (rang buoc loai ky tu) va B3
     (liet ke top-2), chi so chap nhan sai / tu choi, McNemar + bootstrap, tach do
     thoi gian khoi do do chinh xac, quy tac gan tang, buoc ghep chuoi nhieu dong.
  6. Muc 12: khung chuong moi; tien do co buoc tinh chinh mo hinh nen, gan nhan 2 thang,
     bao cao giua ky, du phong, phan cong ro cho thanh vien 2.
  7. Muc 13: bo mau thuan "Gioi han ap dung"; trinh duyet chi con la minh hoa.
  8. Muc 14: them bang du toan chi tiet, tong 4.950.000 dong.
  9. Tai lieu tham khao: IEEE, bo ghi chu Q/IF, tac gia va DOI da xac minh qua Crossref.
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
from docx.shared import Cm, Pt

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")
GOC = FILE.replace(".docx", "_TRUOC_NHANXET.docx")

TEN_MOI = ("Giải mã có ràng buộc số kiểm tra ISO 6346 nhằm nâng cao độ chính xác nhận dạng "
           "mã container trong điều kiện ảnh suy giảm chất lượng")
TEN_CU_HOA = ("NÂNG CAO ĐỘ CHÍNH XÁC MÔ HÌNH HỌC SÂU NHẬN DẠNG MÃ CONTAINER BẰNG GIẢI MÃ CÓ "
              "RÀNG BUỘC ISO 6346")

# =====================================================================================
# NOI DUNG. Quy uoc danh dau: **dam**  __nghieng__  ^tren^  ~duoi~  {KHOA} = trich dan
# =====================================================================================

MUC_8 = [
    "Vận tải container là phương thức chủ đạo trong chuỗi cung ứng hàng hóa, và cổng ra vào "
    "của cảng biển, cảng cạn (Inland Container Depot, ICD) hoặc depot container là điểm nghẽn "
    "cố hữu của quy trình khai thác. Nghiên cứu tình huống tại cảng container DCT Gdańsk ghi "
    "nhận thời gian trễ điển hình tại cổng vào từ bốn đến năm phút cho mỗi lượt xe; ngay cả khi "
    "cổng đã được tự động hóa bằng hệ thống nhận dạng ký tự quang học (Optical Character "
    "Recognition, OCR), khoảng 3–5% lượt nhận dạng thất bại do mã số bị hư hại và phải chuyển "
    "sang xử lý ngoại lệ, với thời gian hiệu chỉnh thủ công được ước tính trung bình 75 giây "
    "mỗi trường hợp {MOSZYK}. Nghiên cứu tại cảng Tangier Med cho thấy tỷ lệ lỗi của hệ thống "
    "OCR tại cổng và cần trục có thể dao động từ 10% đến 30% trong các điều kiện vận hành bất "
    "lợi, kéo theo nhu cầu can thiệp thủ công {GARMOUCH}.",

    "Nguyên nhân của hiện tượng trên xuất phát từ đặc thù vật lý của đối tượng: mã container "
    "được in trên vách thép gợn sóng (corrugated surface) nên ký tự bị biến dạng hình học và "
    "đứt nét khi đi qua các rãnh sóng; bề mặt vỏ container thường bị ăn mòn, trầy xước hoặc "
    "bám bẩn; mã được in theo phương ngang trên vách hông và theo phương dọc hoặc thành nhiều "
    "dòng trên mép cửa.",

    "Chuẩn ISO 6346 quy định mã định danh container gồm mười một ký tự: ba chữ cái mã chủ sở "
    "hữu, một chữ cái ký hiệu loại thiết bị (U, J hoặc Z), sáu chữ số sê-ri và một chữ số kiểm "
    "tra {ISO6346}. Gọi c~0~, c~1~, …, c~9~ là mười ký tự đầu và v(c~i~) là giá trị quy đổi của "
    "ký tự (chữ số giữ nguyên giá trị; chữ cái nhận các giá trị từ 10 đến 38, bỏ qua 11, 22 và "
    "33), số kiểm tra được xác định bởi d = (S mod 11) mod 10, trong đó S = Σ v(c~i~)·2^i^ với "
    "i = 0, 1, …, 9. Ràng buộc này là một nguồn tri thức tiên nghiệm có giá trị, song các hệ "
    "thống nhận dạng hiện nay chỉ khai thác nó ở khâu hậu kiểm: chuỗi ký tự được giải mã độc "
    "lập tại từng vị trí theo nguyên tắc cực đại hóa xác suất cục bộ (argmax), chẳng hạn giải mã "
    "tham lam CTC trong {HLABISA}, sau đó số kiểm tra chỉ được dùng để xác nhận tính hợp lệ của "
    "kết quả.",

    "Cách khai thác nêu trên bộc lộ hai hạn chế. Thứ nhất, phép giải mã cục bộ loại bỏ phân "
    "phối xác suất do mô hình sinh ra, chỉ giữ lại ứng viên xếp hạng nhất tại mỗi vị trí; khi "
    "một ký tự bị suy giảm chất lượng, ứng viên đúng có thể xếp hạng thứ hai và bị loại bỏ. Thứ "
    "hai, khi kết quả không thỏa mãn số kiểm tra, hệ thống loại bỏ toàn bộ chuỗi và chuyển sang "
    "xử lý thủ công thay vì khai thác ràng buộc để tìm chuỗi hợp lệ có xác suất cao nhất.",

    "Bên cạnh đó, khả năng phát hiện sai sót của số kiểm tra không đồng đều giữa các vị trí. "
    "Tại bốn vị trí chữ cái, hai mươi sáu chữ cái chỉ phân bố vào mười lớp thặng dư modulo 11, "
    "tạo thành các nhóm chữ cái đồng dư mà phép kiểm tra không phân biệt được: (A, K, U), "
    "(B, L, V), (C, M, W), (D, N, X), (E, O, Y), (F, P, Z), (G, Q), (H, R), (I, S) và (J, T). "
    "Tại sáu vị trí chữ số sê-ri, bước ánh xạ phần dư 10 thành chữ số 0 gây ra hiện tượng va "
    "chạm: hai chuỗi chỉ khác nhau một chữ số vẫn có thể có cùng số kiểm tra, chẳng hạn "
    "MKSU511193 (phần dư 10) và MKSU311193 (phần dư 0) đều có số kiểm tra 0. Chuẩn khuyến nghị "
    "tránh các số sê-ri cho phần dư 10, song khuyến nghị này không được tuân thủ tuyệt đối trên "
    "thực tế. Ngoài ra, một chuỗi sai ngẫu nhiên vẫn thỏa mãn số kiểm tra với xác suất xấp xỉ "
    "1/11; do đó, một bộ giải mã luôn trả về chuỗi hợp lệ có thể chuyển một ảnh chất lượng thấp "
    "thành mã của một container khác. Tại cổng cảng, sai sót chấp nhận nhầm này nguy hiểm hơn "
    "việc từ chối kết quả và chuyển cho nhân viên kiểm tra. Mức độ ảnh hưởng của các hiện tượng "
    "nêu trên đối với bài toán nhận dạng mã container chưa được định lượng trong các công bố đã "
    "khảo sát.",

    "Từ những phân tích trên, việc nghiên cứu phương pháp giải mã tích hợp trực tiếp ràng buộc "
    "số kiểm tra ISO 6346 vào quá trình suy luận, đồng thời đánh giá định lượng cả lợi ích về độ "
    "chính xác lẫn rủi ro chấp nhận sai, là hướng tiếp cận có cơ sở khoa học và có giá trị ứng "
    "dụng đối với hoạt động khai thác cảng. Đề tài được thực hiện trên nền SmartPort, hệ thống "
    "phần mềm điều hành depot và cảng cạn do sinh viên chủ nhiệm đề tài xây dựng, có phân hệ "
    "cổng tự động nhận dạng mã container bằng mô hình học sâu; hệ thống này là nguồn ảnh mẫu và "
    "là môi trường minh họa khả năng triển khai của phương pháp.",
]

MUC_9 = [
    "Mục tiêu tổng quát: xây dựng và đánh giá thực nghiệm phương pháp giải mã có ràng buộc số "
    "kiểm tra ISO 6346 nhằm nâng cao độ chính xác nhận dạng mã container trong điều kiện ảnh "
    "suy giảm chất lượng, đồng thời kiểm soát tỷ lệ chấp nhận sai và định lượng giới hạn của "
    "ràng buộc tại các nhóm chữ cái đồng dư và tại trường hợp va chạm phần dư 0 và 10.",
    "Mục tiêu cụ thể của đề tài được trình bày trong bảng sau.",
]

MUC_TIEU = [
    ("MT1",
     "Xây dựng bộ dữ liệu kiểm thử ảnh mã container phân tầng theo loại điều kiện chụp",
     "Tối thiểu 1.200 ảnh kiểm thử, chia bốn loại điều kiện (thuận lợi; suy giảm bề mặt; suy "
     "giảm chiếu sáng; suy giảm hình học), mỗi loại tối thiểu 300 ảnh theo phân tích lực kiểm "
     "định; ảnh có từ hai loại suy giảm trở lên được xếp vào nhóm đa điều kiện và báo cáo riêng; "
     "tập kiểm thử tách biệt hoàn toàn với dữ liệu huấn luyện; hệ số đồng thuận gán tầng "
     "Cohen’s κ ≥ 0,6; kèm tài liệu quy tắc gán nhãn cho phép tái lập"),
    ("MT2",
     "Xây dựng phương pháp giải mã có ràng buộc số kiểm tra cho hai dạng đầu ra của mô hình "
     "nhận dạng: phân loại theo vị trí và CTC",
     "Phép giải mã được đặc tả dưới dạng tích của lưới xác suất và automat 11 trạng thái thặng "
     "dư, xử lý đầy đủ trường hợp phần dư 10; nghiệm trùng khớp 100% với phép vét cạn trên tập "
     "lưới xác suất mô phỏng; cung cấp xác suất hậu nghiệm của chuỗi được chọn làm cơ sở cho "
     "quyết định từ chối"),
    ("MT3",
     "Đánh giá hiệu quả của phương pháp so với các đường cơ sở",
     "So sánh với bốn đường cơ sở: argmax tự do, argmax có ràng buộc loại ký tự, hậu kiểm số "
     "kiểm tra và liệt kê hai ứng viên tốt nhất mỗi vị trí (2^11^ = 2.048 tổ hợp); báo cáo độ "
     "chính xác cấp ký tự và cấp chuỗi, tỷ lệ chấp nhận sai, tỷ lệ từ chối và đường cong độ "
     "chính xác – từ chối trên từng loại điều kiện; kiểm định McNemar kèm khoảng tin cậy "
     "bootstrap 95%"),
    ("MT4",
     "Định lượng giới hạn của ràng buộc số kiểm tra và kiểm chứng tri thức bổ sung",
     "Xác định tỷ lệ sai sót không bị phát hiện do các nhóm chữ cái đồng dư và do va chạm phần "
     "dư 0 và 10; kiểm chứng hai cơ chế bổ sung: tiên nghiệm ký tự thứ tư thuộc tập {U, J, Z} "
     "và từ điển mã chủ sở hữu đăng ký với BIC cho ba ký tự đầu; báo cáo mức giảm tỷ lệ chấp "
     "nhận sai do từng cơ chế"),
    ("MT5",
     "Công bố kết quả nghiên cứu",
     "Hoàn thành và gửi bản thảo một bài báo khoa học đến hội nghị hoặc tạp chí chuyên ngành "
     "công nghệ thông tin"),
]

# (loai, noi dung): 'sub' = tieu muc a), b)...; 'body' = doan van
MUC_10 = [
    ("sub", "a) Nhận dạng mã container bằng mô hình học sâu"),
    ("body",
     "Nhận dạng mã container là bài toán nhận dạng văn bản trong cảnh thực với điều kiện đặc "
     "thù. Zhao và cộng sự {ZHAO} cải tiến khung phát hiện đối tượng DETR bằng cấu trúc đặc "
     "trưng chú ý phân tách và mã hóa vị trí đa tỷ lệ, áp dụng cho ký tự trên vỏ container. "
     "Liang và cộng sự {LIANG} đề xuất kiến trúc Transformer thị giác – ngôn ngữ tách chiều cho "
     "bài toán phát hiện và nhận dạng đồng thời ký hiệu container công nghiệp. Hlabisa và cộng "
     "sự {HLABISA} xây dựng mạng định vị mã container trên nền ResNet và UNet kết hợp mạng nhận "
     "dạng lai tích chập – hồi tiếp huấn luyện với hàm mất mát CTC, đạt độ chính xác 97,93% ở "
     "tốc độ 64,11 khung hình mỗi giây. Santos và cộng sự {SANTOS} kết hợp YOLOv7 cho phát hiện "
     "văn bản với TrOCR cho nhận dạng ký tự. Ta và cộng sự {TA} đề xuất mạng hồi tiếp tích chập "
     "tăng cường chú ý đa phân giải kết hợp khử nhiễu sâu cho môi trường cảng thông minh. Cheng "
     "và cộng sự {CHENG} trình bày khung nhận dạng văn bản cảnh thực hai giai đoạn gồm mạng phát "
     "hiện tự hiệu chỉnh và mạng nhận dạng theo ngữ cảnh."),
    ("body",
     "Các công trình nêu trên tập trung nâng cao năng lực biểu diễn của mô hình thị giác thông "
     "qua cải tiến kiến trúc mạng. Chuẩn ISO 6346 được nhắc tới chủ yếu ở vai trò mô tả cấu "
     "trúc mã hoặc xác nhận kết quả; ràng buộc số kiểm tra chưa được đưa vào quá trình giải mã "
     "chuỗi ký tự, và rủi ro chấp nhận một mã hợp lệ nhưng sai chưa được đánh giá trong các công "
     "bố đã khảo sát."),

    ("sub", "b) Giải mã có ràng buộc và mã định danh có số kiểm tra"),
    ("body",
     "Việc đưa tri thức tiên nghiệm vào quá trình giải mã chuỗi là hướng nghiên cứu đã được xác "
     "lập. Trong nhận dạng tiếng nói, Miao và cộng sự {MIAO} biểu diễn từ điển và mô hình ngôn "
     "ngữ dưới dạng bộ chuyển đổi trạng thái hữu hạn có trọng số (Weighted Finite-State "
     "Transducer, WFST) và kết hợp với đầu ra CTC, qua đó giải mã có ràng buộc bằng tìm kiếm "
     "Viterbi trên tích của lưới xác suất và automat. Hokamp và Liu {HOKAMP} đề xuất thuật toán "
     "tìm kiếm chùm theo lưới (Grid Beam Search) cho phép áp đặt ràng buộc từ vựng ngay trong quá "
     "trình sinh chuỗi của mô hình tự hồi quy. Trong hiệu chỉnh hậu kỳ kết quả OCR, "
     "Ramirez-Orta và cộng sự {RAMIREZ} sử dụng tổ hợp các mô hình chuỗi sang chuỗi ở cấp ký "
     "tự, còn Thomas và cộng sự {THOMAS} vận dụng mô hình ngôn ngữ lớn. Đối với các chuỗi định "
     "danh khác có chứa số kiểm tra, tổng quan của Gayer và cộng sự {GAYER} về nhận dạng vùng "
     "đọc máy (Machine-Readable Zone, MRZ) trên giấy tờ tùy thân chỉ ra rằng các mô hình nhận "
     "dạng và mô hình ngôn ngữ chuyên biệt cho loại chuỗi này vẫn chưa được nghiên cứu đầy đủ."),
    ("body",
     "Xét về bản chất thuật toán, tập các mã thỏa mãn số kiểm tra ISO 6346 là một ngôn ngữ chính "
     "quy, được nhận biết bởi automat hữu hạn có 11 trạng thái tương ứng với 11 lớp thặng dư "
     "modulo 11. Do đó, giải mã có ràng buộc số kiểm tra là trường hợp riêng của tìm kiếm "
     "Viterbi trên tích của lưới xác suất và automat, cùng họ với giải mã WFST {MIAO}. Đề tài "
     "không xem thuật toán này là đóng góp mới; đóng góp được đặt vào việc vận dụng kỹ thuật cho "
     "bài toán mã container, đánh giá thực nghiệm có đối chứng đầy đủ và phân tích giới hạn của "
     "chính ràng buộc."),

    ("sub", "c) Mô hình nền và điều kiện áp dụng phép giải mã"),
    ("body",
     "Tính đúng đắn của phép giải mã có ràng buộc phụ thuộc vào dạng đầu ra của mô hình nhận "
     "dạng. Đề tài lựa chọn hai họ mô hình có đầu ra phân rã theo vị trí. Họ thứ nhất là mô hình "
     "phân loại theo vị trí cố định, trong đó mỗi vị trí ký tự có một đầu phân loại riêng, theo "
     "cách tiếp cận của Goodfellow và cộng sự {GOODFELLOW}; với dạng đầu ra này, xác suất của "
     "chuỗi là tích xác suất tại các vị trí và quy hoạch động cho nghiệm cực đại hậu nghiệm chính "
     "xác. Họ thứ hai là mô hình CRNN của Shi và cộng sự {SHI}, huấn luyện với hàm mất mát phân "
     "loại thời gian kết nối (Connectionist Temporal Classification, CTC) của Graves và cộng sự "
     "{GRAVES}; với dạng đầu ra này, ràng buộc được áp lên lưới căn chỉnh có ký hiệu trống "
     "(blank) thay vì lên mười một vị trí cố định, và phép giải mã cho đường căn chỉnh tốt nhất "
     "thỏa ràng buộc theo xấp xỉ Viterbi chuẩn của giải mã WFST {MIAO}."),
    ("body",
     "Mô hình TrOCR của Li và cộng sự {LI_TROCR} nằm ngoài phạm vi của đề tài. Đây là mô hình tự "
     "hồi quy, xác suất tại mỗi vị trí phụ thuộc vào các ký tự đã sinh trước đó, nên quy hoạch "
     "động theo vị trí độc lập không bảo đảm nghiệm tối ưu; ràng buộc đối với loại mô hình này "
     "chỉ áp đặt được qua tìm kiếm chùm có ràng buộc {HOKAMP}."),

    ("sub", "d) Độ tin cậy của mô hình và tùy chọn từ chối"),
    ("body",
     "Xác suất đầu ra của mạng nơ-ron sâu hiện đại thường không được hiệu chỉnh tốt {GUO}, trong "
     "khi cả phép giải mã lẫn quyết định chấp nhận kết quả đều dựa trực tiếp vào các xác suất "
     "này. Vì vậy, đề tài áp dụng phép hiệu chỉnh nhiệt độ (temperature scaling) {GUO} trên tập "
     "kiểm định trước khi giải mã, và xây dựng quy tắc từ chối theo xác suất hậu nghiệm của chuỗi "
     "được chọn, kế thừa lý thuyết đánh đổi giữa sai sót và từ chối của Chow {CHOW} và khung "
     "phân loại có chọn lọc (selective classification) của Geifman và El-Yaniv {GEIFMAN}."),

    ("sub", "e) Định vị của đề tài"),
    ("body",
     "Trên cơ sở khảo sát nêu trên, đề tài xác định ba điểm định vị. Thứ nhất, về phương pháp, "
     "đề tài vận dụng kỹ thuật tìm kiếm Viterbi trên tích lưới xác suất và automat để chuyển số "
     "kiểm tra ISO 6346 từ vai trò hậu kiểm sang vai trò ràng buộc trong quá trình giải mã, kết "
     "hợp với quy tắc từ chối theo xác suất hậu nghiệm; hướng vận dụng này chưa được trình bày "
     "cho bài toán mã container trong các công bố đã khảo sát. Thứ hai, đề tài định lượng giới "
     "hạn của ràng buộc tại các nhóm chữ cái đồng dư và tại trường hợp va chạm phần dư 0 và 10, "
     "đồng thời đánh giá mức bù đắp của tri thức về ký hiệu loại thiết bị và mã chủ sở hữu. Thứ "
     "ba, đề tài đóng góp bộ dữ liệu kiểm thử phân tầng theo loại điều kiện chụp kèm quy tắc gán "
     "nhãn, phục vụ tái lập kết quả. Đóng góp chính của đề tài vì vậy nằm ở bằng chứng thực "
     "nghiệm và phân tích giới hạn, không nằm ở bản thân thuật toán giải mã."),
]

MUC_11 = [
    ("sub", "a) Đối tượng và khách thể nghiên cứu"),
    ("body",
     "__Đối tượng nghiên cứu:__ phép giải mã chuỗi ký tự dưới ràng buộc số kiểm tra ISO 6346 và "
     "quy tắc từ chối theo xác suất hậu nghiệm, áp dụng trên đầu ra của mô hình nhận dạng mã "
     "container."),
    ("body",
     "__Khách thể nghiên cứu:__ mã định danh container theo chuẩn ISO 6346, gồm bốn chữ cái, sáu "
     "chữ số sê-ri và một chữ số kiểm tra; ảnh mã container thu nhận tại cổng cảng, cảng cạn và "
     "depot trong các điều kiện chụp khác nhau."),

    ("sub", "b) Phạm vi nghiên cứu"),
    ("body",
     "__Phạm vi nội dung:__ đề tài tập trung vào khâu giải mã chuỗi ký tự và quyết định chấp "
     "nhận hoặc từ chối kết quả. Đề tài không đề xuất kiến trúc mạng nơ-ron mới cho tầng phát "
     "hiện và tầng nhận dạng; các mô hình nền được tinh chỉnh (fine-tune) trên tập huấn luyện rồi "
     "giữ cố định trong toàn bộ thực nghiệm, qua đó cô lập ảnh hưởng của phương pháp giải mã. Với "
     "mã in theo phương dọc hoặc xếp thành hai đến ba dòng, các vùng văn bản được sắp xếp theo "
     "thứ tự đọc và ghép thành chuỗi mười một ký tự trước khi giải mã; trạng thái của automat "
     "được truyền liên tục qua ranh giới giữa các vùng."),
    ("body",
     "__Phạm vi đối tượng:__ đề tài giới hạn ở mã container. Bài toán nhận dạng biển số phương "
     "tiện không thuộc phạm vi nghiên cứu do quy cách biển số Việt Nam không có cơ chế số kiểm "
     "tra, vì vậy không áp dụng được phương pháp."),
    ("body",
     "__Phạm vi dữ liệu:__ ảnh được thu thập từ các bộ dữ liệu công khai, ảnh chụp thực địa tại "
     "cảng cạn hoặc depot khi được sự chấp thuận của đơn vị quản lý, và ảnh mẫu của hệ thống "
     "SmartPort. Tập kiểm thử được tách biệt hoàn toàn với tập huấn luyện và tập kiểm định theo "
     "mã container và theo đợt chụp nhằm loại trừ rò rỉ dữ liệu. Đề tài không sử dụng dữ liệu "
     "vận hành của doanh nghiệp."),
    ("body",
     "__Phạm vi triển khai:__ thực nghiệm được thực hiện ngoại tuyến trên máy tính cá nhân. Việc "
     "tích hợp phương pháp vào phân hệ cổng của hệ thống SmartPort chỉ nhằm minh họa khả năng "
     "triển khai và không thuộc nội dung đánh giá."),

    ("sub", "c) Cách tiếp cận"),
    ("body",
     "Đề tài tiếp cận theo hướng nghiên cứu thực nghiệm có đối chứng. Phương pháp giải mã được "
     "đặc tả hình thức và kiểm chứng tính đúng đắn bằng đối chiếu với phép vét cạn, sau đó được "
     "đánh giá trên bộ dữ liệu phân tầng với các đường cơ sở cài đặt trong cùng điều kiện. Thực "
     "nghiệm được thiết kế theo dạng loại bỏ từng thành phần (ablation): mỗi cấu hình chỉ khác "
     "cấu hình liền trước ở một yếu tố. Kết luận về hiệu quả được rút ra từ kiểm định thống kê "
     "ghép cặp trên cùng tập mẫu."),

    ("sub", "d) Phương pháp nghiên cứu"),
    ("body",
     "__Phương pháp nghiên cứu tài liệu:__ tổng hợp và phân tích các công bố nền tảng về CTC, "
     "giải mã WFST, hiệu chỉnh độ tin cậy và tùy chọn từ chối, cùng các công bố giai đoạn "
     "2021–2025 về nhận dạng mã container; xác lập bộ chỉ số đánh giá và danh mục đường cơ sở."),
    ("body",
     "__Phương pháp mô hình hóa toán học:__ biểu diễn ràng buộc ISO 6346 bằng automat hữu hạn "
     "có trạng thái là phần dư modulo 11 của tổng có trọng số; trạng thái kết thúc được chấp "
     "nhận khi phần dư, sau khi lấy modulo 10, trùng với chữ số kiểm tra, nhờ đó trường hợp phần "
     "dư 10 ánh xạ về chữ số 0 được xử lý trực tiếp trong mô hình. Giải mã được thực hiện bằng "
     "quy hoạch động (tìm kiếm Viterbi) trên tích của lưới xác suất và automat, với độ phức tạp "
     "tuyến tính theo độ dài chuỗi. Xác suất hậu nghiệm của chuỗi được chọn trong tập các chuỗi "
     "hợp lệ được tính bằng thuật toán tiến (forward) trên cùng lưới. Tiên nghiệm ký tự thứ tư "
     "và từ điển mã chủ sở hữu BIC được tích hợp dưới dạng automat bổ sung."),
    ("body",
     "__Phương pháp xây dựng dữ liệu:__ hai thành viên gán nhãn độc lập; nhãn được kiểm tra chéo "
     "bằng chính số kiểm tra ISO 6346, các trường hợp bất đồng do giảng viên hướng dẫn phân xử. "
     "Mỗi ảnh được gán nhãn đa thuộc tính theo bốn loại điều kiện; ảnh chỉ có một loại suy giảm "
     "được xếp vào tầng tương ứng, ảnh có từ hai loại trở lên được xếp vào nhóm đa điều kiện và "
     "báo cáo riêng. Đặc điểm vách gợn sóng xuất hiện ở phần lớn mã in trên vách hông nên được "
     "ghi nhận như một thuộc tính mô tả thay vì một tầng riêng. Mức đồng thuận gán tầng được đo "
     "bằng hệ số Cohen’s κ."),
    ("body",
     "__Phương pháp xác định cỡ mẫu:__ cỡ mẫu mỗi tầng được xác định bằng phân tích lực kiểm "
     "định cho kiểm định McNemar {DIETTERICH}. Với mức ý nghĩa α = 0,05, lực kiểm định 0,8, chênh "
     "lệch độ chính xác cấp chuỗi cần phát hiện là 5 điểm phần trăm và tỷ lệ cặp bất đồng giả "
     "định 9%, cỡ mẫu tối thiểu là 281 ảnh, được làm tròn thành 300 ảnh mỗi tầng. Tỷ lệ cặp bất "
     "đồng được ước lượng lại trên mẫu thử 100 ảnh đầu tiên và cỡ mẫu được điều chỉnh nếu cần."),
    ("body",
     "__Phương pháp thực nghiệm đối chứng:__ các cấu hình được so sánh gồm (B0) argmax tự do; "
     "(B1) argmax có ràng buộc loại ký tự, bốn vị trí đầu là chữ cái và bảy vị trí sau là chữ số; "
     "(B2) B1 kết hợp hậu kiểm số kiểm tra; (B3) liệt kê hai ứng viên tốt nhất tại mỗi vị trí và "
     "chọn tổ hợp hợp lệ có xác suất cao nhất trong 2^11^ = 2.048 tổ hợp; (P1) giải mã có ràng "
     "buộc số kiểm tra bằng quy hoạch động; (P2) P1 kết hợp tiên nghiệm ký tự thứ tư thuộc tập "
     "{U, J, Z}; (P3) P2 kết hợp từ điển mã chủ sở hữu BIC. Mỗi cấu hình được đánh giá ở chế độ "
     "luôn trả kết quả và ở chế độ có ngưỡng từ chối theo xác suất hậu nghiệm."),
    ("body",
     "__Chỉ số đánh giá:__ độ chính xác cấp ký tự và cấp chuỗi; tỷ lệ chấp nhận sai, tức tỷ lệ "
     "mẫu được chấp nhận nhưng sai; tỷ lệ từ chối; đường cong độ chính xác – từ chối khi thay đổi "
     "ngưỡng; sai số hiệu chỉnh kỳ vọng (Expected Calibration Error, ECE) trước và sau hiệu chỉnh "
     "nhiệt độ; thời gian giải mã."),
    ("body",
     "__Phương pháp phân tích thống kê:__ chênh lệch giữa hai cấu hình trên cùng tập mẫu được "
     "kiểm định bằng kiểm định McNemar {DIETTERICH}, kèm khoảng tin cậy bootstrap 95% với "
     "10.000 lần lấy mẫu lại và hiệu chỉnh Holm cho so sánh bội. Do phép giải mã là tất định, độ "
     "biến thiên của độ chính xác được đánh giá bằng bootstrap và bằng tinh chỉnh mô hình nền với "
     "ba hạt giống ngẫu nhiên (seed). Riêng thời gian giải mã được đo lặp nhiều lần, loại bỏ các "
     "lần chạy khởi động (warm-up) và báo cáo giá trị trung vị."),
    ("body",
     "__Phương pháp phân tích lỗi:__ phân loại các trường hợp nhận dạng sai theo nguyên nhân (ăn "
     "mòn bề mặt, ngược sáng, góc chụp, nhóm chữ cái đồng dư, va chạm phần dư) nhằm xác định giới "
     "hạn áp dụng của phương pháp."),
]

CHUONG = [
    ("CHƯƠNG 1. TỔNG QUAN", [
        "1.1. Lý do chọn đề tài",
        "1.2. Mục tiêu và nhiệm vụ nghiên cứu",
        "1.3. Đối tượng và phạm vi nghiên cứu",
        "1.4. Phương pháp nghiên cứu",
        "1.5. Bố cục của báo cáo"]),
    ("CHƯƠNG 2. CƠ SỞ LÝ THUYẾT", [
        "2.1. Chuẩn ISO 6346 và phép tính số kiểm tra",
        "2.2. Tính chất của phép kiểm tra trên các lớp thặng dư modulo 11 và trường hợp phần dư 10",
        "2.3. Mô hình phân loại theo vị trí và mô hình CRNN – CTC",
        "2.4. Giải mã có ràng buộc bằng automat hữu hạn và WFST",
        "2.5. Hiệu chỉnh độ tin cậy và tùy chọn từ chối"]),
    ("CHƯƠNG 3. PHƯƠNG PHÁP GIẢI MÃ CÓ RÀNG BUỘC SỐ KIỂM TRA", [
        "3.1. Phát biểu bài toán giải mã cực đại hậu nghiệm có ràng buộc",
        "3.2. Automat số kiểm tra và quy hoạch động cho mô hình phân loại theo vị trí",
        "3.3. Mở rộng cho lưới căn chỉnh CTC và bước ghép chuỗi nhiều dòng",
        "3.4. Xác suất hậu nghiệm và quy tắc từ chối",
        "3.5. Tích hợp tiên nghiệm ký hiệu loại thiết bị và từ điển mã chủ sở hữu BIC",
        "3.6. Kiểm chứng tính đúng đắn và đánh giá độ phức tạp"]),
    ("CHƯƠNG 4. XÂY DỰNG DỮ LIỆU VÀ MÔ HÌNH NỀN", [
        "4.1. Nguồn dữ liệu, tiêu chí lựa chọn và phân tích cỡ mẫu",
        "4.2. Quy tắc gán nhãn, gán tầng và kiểm tra chéo",
        "4.3. Phân chia tập huấn luyện, tập kiểm định và tập kiểm thử",
        "4.4. Tinh chỉnh mô hình nền và hiệu chỉnh nhiệt độ"]),
    ("CHƯƠNG 5. THỰC NGHIỆM VÀ ĐÁNH GIÁ", [
        "5.1. Thiết lập thực nghiệm, các đường cơ sở và thiết kế loại bỏ thành phần",
        "5.2. Độ chính xác cấp ký tự và cấp chuỗi theo từng tầng",
        "5.3. Tỷ lệ chấp nhận sai, tỷ lệ từ chối và đường cong độ chính xác – từ chối",
        "5.4. Kiểm định ý nghĩa thống kê",
        "5.5. Giới hạn tại các nhóm chữ cái đồng dư và trường hợp va chạm phần dư",
        "5.6. Chi phí tính toán và phân tích các trường hợp nhận dạng sai"]),
    ("CHƯƠNG 6. KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN", [
        "6.1. Kết quả đạt được",
        "6.2. Hạn chế của nghiên cứu",
        "6.3. Hướng phát triển"]),
]

TAI = "Lê Hữu Anh Tài"
HAI = "Lê Hữu Anh Tài\nNguyễn Quang Long"
TIEN_DO = [
    ("10/2026",
     "Tổng quan tài liệu; chốt hai mô hình nền, bộ chỉ số đánh giá và danh mục đường cơ sở; lập "
     "danh mục nguồn dữ liệu công khai. Nguyễn Quang Long liên hệ xin phép chụp ảnh thực địa",
     "Báo cáo tổng quan; đặc tả thiết kế thực nghiệm",
     HAI),
    ("11/2026",
     "Đặc tả automat số kiểm tra, cài đặt phép giải mã cho hai dạng đầu ra và kiểm chứng với phép "
     "vét cạn. Thu thập, gán nhãn dữ liệu đợt 1; Nguyễn Quang Long gán nhãn độc lập lượt thứ hai",
     "Mã nguồn phép giải mã; biên bản kiểm chứng; dữ liệu đợt 1",
     HAI),
    ("12/2026",
     "Tinh chỉnh hai mô hình nền với ba hạt giống ngẫu nhiên; hiệu chỉnh nhiệt độ. Thu thập, gán "
     "nhãn đợt 2, kiểm tra chéo, tính hệ số κ và đóng băng tập kiểm thử (Nguyễn Quang Long phụ "
     "trách dữ liệu)",
     "Mô hình nền đã tinh chỉnh; bộ dữ liệu kiểm thử kèm quy tắc gán nhãn",
     HAI),
    ("01/2027",
     "Thực nghiệm đối chứng và loại bỏ thành phần (B0–B3, P1–P3); đo tỷ lệ chấp nhận sai, tỷ lệ "
     "từ chối; kiểm định thống kê. Báo cáo giữa kỳ với giảng viên hướng dẫn",
     "Bảng kết quả đối chứng; báo cáo giữa kỳ",
     TAI),
    ("02/2027",
     "Định lượng giới hạn tại các nhóm chữ cái đồng dư và va chạm phần dư; đánh giá tiên nghiệm "
     "U, J, Z và từ điển BIC; phân tích lỗi (Nguyễn Quang Long tra cứu mã BIC, phân loại lỗi); "
     "soạn và gửi bản thảo bài báo",
     "Báo cáo phân tích giới hạn; bản thảo bài báo đã gửi",
     HAI),
    ("03/2027",
     "Nửa đầu tháng: dự phòng cho các hạng mục chậm tiến độ và bổ sung thực nghiệm theo góp ý. "
     "Nửa cuối tháng: hoàn thiện báo cáo tổng kết và hồ sơ nghiệm thu",
     "Báo cáo tổng kết; hồ sơ nghiệm thu",
     HAI),
]

SAN_PHAM = [
    "Phương pháp giải mã có ràng buộc số kiểm tra ISO 6346 cho hai dạng đầu ra của mô hình nhận "
    "dạng, gồm đặc tả automat, quy tắc từ chối theo xác suất hậu nghiệm và biên bản kiểm chứng "
    "tính đúng đắn bằng phép vét cạn.",
    "Mã nguồn cài đặt phương pháp và các đường cơ sở, kèm kịch bản tái lập toàn bộ thực nghiệm.",
    "Bộ dữ liệu kiểm thử tối thiểu 1.200 ảnh mã container phân tầng theo bốn loại điều kiện chụp, "
    "gán nhãn theo ISO 6346, kèm tài liệu quy tắc gán nhãn và gán tầng.",
    "Bộ kết quả thực nghiệm đối chứng và loại bỏ thành phần, gồm độ chính xác, tỷ lệ chấp nhận "
    "sai, tỷ lệ từ chối, đường cong độ chính xác – từ chối và kết quả kiểm định McNemar.",
    "Báo cáo định lượng giới hạn của số kiểm tra tại các nhóm chữ cái đồng dư và trường hợp va "
    "chạm phần dư 0 và 10, cùng mức bù đắp của tiên nghiệm U, J, Z và từ điển BIC.",
    "Bản thảo một bài báo khoa học đã gửi hội nghị hoặc tạp chí chuyên ngành công nghệ thông tin.",
    "Báo cáo tổng kết đề tài theo mẫu quy định của Trường.",
]

UNG_DUNG = [
    ("Ứng dụng tại cổng cảng, cảng cạn và depot",
     "Tăng tỷ lệ mã container được ghi nhận tự động chính xác, đồng thời kiểm soát tỷ lệ chấp "
     "nhận sai bằng ngưỡng từ chối, qua đó giảm số lượt hiệu chỉnh thủ công mà không làm tăng rủi "
     "ro ghi nhận nhầm container."),
    ("Tích hợp vào hệ thống sẵn có",
     "Phương pháp chỉ can thiệp vào khâu giải mã sau mô hình nhận dạng, không yêu cầu huấn luyện "
     "lại mô hình hay bổ sung phần cứng; khả năng triển khai được minh họa trên phân hệ cổng của "
     "hệ thống SmartPort."),
    ("Khả năng mở rộng",
     "Cách tiếp cận áp dụng được cho các chuỗi định danh có số kiểm tra khác, chẳng hạn vùng đọc "
     "máy trên giấy tờ tùy thân hoặc mã số sách tiêu chuẩn quốc tế ISBN-10, vốn cũng sử dụng "
     "phép kiểm tra modulo 11."),
    ("Phục vụ đào tạo",
     "Kết quả đề tài là học liệu minh họa cho các học phần Trí tuệ nhân tạo, Học máy và Thị giác "
     "máy tính tại Viện Công nghệ số."),
    ("Giới hạn áp dụng",
     "Phép giải mã không cần biết trước vị trí nghi ngờ, song chỉ khôi phục được ký tự khi ký tự "
     "đúng còn mang xác suất đáng kể trong phân phối đầu ra của mô hình; khi ký tự bị che khuất "
     "hoàn toàn, quy tắc từ chối chuyển kết quả cho nhân viên kiểm tra thay vì tự động hiệu "
     "chỉnh. Tại các nhóm chữ cái đồng dư, số kiểm tra không cung cấp thông tin phân biệt, nên độ "
     "chính xác phụ thuộc vào mô hình nền và các tiên nghiệm bổ sung. Phương pháp không áp dụng "
     "cho biển số phương tiện do quy cách biển số Việt Nam không có cơ chế số kiểm tra."),
]

DU_TOAN = [
    ("1", "Đi lại thu thập ảnh thực địa tại cảng cạn, depot", "6 lượt người", "150.000", "900.000"),
    ("2", "Công gán nhãn lượt thứ hai và kiểm tra chéo dữ liệu", "1.200 ảnh", "1.000", "1.200.000"),
    ("3", "Thuê tài nguyên tính toán đám mây phục vụ tinh chỉnh mô hình nền", "30 giờ", "30.000",
     "900.000"),
    ("4", "Hiệu đính ngôn ngữ bản thảo bài báo tiếng Anh", "1 bài", "1.000.000", "1.000.000"),
    ("5", "In ấn, đóng quyển báo cáo tổng kết và hồ sơ nghiệm thu", "5 quyển", "100.000", "500.000"),
    ("6", "Văn phòng phẩm và thiết bị lưu trữ dữ liệu", "1 gói", "450.000", "450.000"),
]
TONG_TIEN = "4.950.000"

# Danh muc tai lieu: tac gia, tap, so, trang, DOI da doi chieu Crossref ngay 29/09/2026.
# THOMAS va GOODFELLOW, GUO, GEIFMAN khong co DOI (ky yeu workshop / ICLR / PMLR / NeurIPS).
REFS = {
    "MOSZYK": "K. Moszyk, M. Deja, and M. Dobrzynski, “Automation of the road gate operations "
              "process at the container terminal—A case study of DCT Gdańsk SA,” __Sustainability__, "
              "vol. 13, no. 11, art. no. 6291, 2021, doi: 10.3390/su13116291.",
    "GARMOUCH": "H. Garmouch and O. Abdoun, “Enhancing crane and gate OCR efficiency at container "
                "terminal using a hybrid genetic algorithm and neural network model: Case study of "
                "Tangier Med port,” __Discover Applied Sciences__, vol. 7, no. 7, art. no. 714, 2025, "
                "doi: 10.1007/s42452-025-07289-3.",
    "ISO6346": "__ISO 6346:2022, Freight containers — Coding, identification and marking__, "
               "International Organization for Standardization, Geneva, 2022.",
    "HLABISA": "S. Hlabisa, R. L. Khuboni, and J.-R. Tapamo, “Confidence-guided code recognition for "
               "shipping containers using deep learning,” __Big Data and Cognitive Computing__, "
               "vol. 9, no. 12, art. no. 316, 2025, doi: 10.3390/bdcc9120316.",
    "ZHAO": "X. Zhao, P. Zhou, K. Xu, and L. Xiao, “An improved character recognition framework for "
            "containers based on DETR algorithm,” __Sensors__, vol. 21, no. 13, art. no. 4612, 2021, "
            "doi: 10.3390/s21134612.",
    "LIANG": "Z. Liang, Y. Xu, Y. Zhai, H. Zhu, J. Xi, P. Coscia, and A. Genovese, “Dimension "
             "decoupling vision-language transformer for industrial container marking and natural "
             "scene text spotting,” __Journal of Industrial Information Integration__, vol. 48, "
             "art. no. 100926, 2025, doi: 10.1016/j.jii.2025.100926.",
    "SANTOS": "J. Santos, D. Canedo, and A. J. R. Neves, “Automating code recognition for cargo "
              "containers,” __Electronics__, vol. 14, no. 22, art. no. 4437, 2025, "
              "doi: 10.3390/electronics14224437.",
    "TA": "A. S. Ta, L. T. Le, and L. Bui-Duy, “Advanced vehicle monitoring in smart port utilizing "
          "deep denoising real-time object detectors integrated multi-resolution attention-augmented "
          "CRNN,” __Ain Shams Engineering Journal__, vol. 16, no. 8, art. no. 103483, 2025, "
          "doi: 10.1016/j.asej.2025.103483.",
    "CHENG": "Y. Cheng et al., “A two-stage end-to-end framework for robust scene text spotting with "
             "self-calibrated detection and contextual recognition,” __Electronics__, vol. 14, "
             "no. 23, art. no. 4594, 2025, doi: 10.3390/electronics14234594.",
    "MIAO": "Y. Miao, M. Gowayyed, and F. Metze, “EESEN: End-to-end speech recognition using deep "
            "RNN models and WFST-based decoding,” in __Proc. IEEE Workshop on Automatic Speech "
            "Recognition and Understanding (ASRU)__, 2015, pp. 167–174, "
            "doi: 10.1109/ASRU.2015.7404790.",
    "HOKAMP": "C. Hokamp and Q. Liu, “Lexically constrained decoding for sequence generation using "
              "grid beam search,” in __Proc. 55th Annual Meeting of the Association for "
              "Computational Linguistics (ACL)__, 2017, pp. 1535–1546, doi: 10.18653/v1/P17-1141.",
    "RAMIREZ": "J. A. Ramirez-Orta, E. Xamena, A. Maguitman, E. Milios, and A. J. Soto, “Post-OCR "
               "document correction with large ensembles of character sequence-to-sequence "
               "models,” in __Proc. AAAI Conference on Artificial Intelligence__, vol. 36, no. 10, "
               "2022, pp. 11192–11199, doi: 10.1609/aaai.v36i10.21369.",
    "THOMAS": "A. Thomas et al., “Leveraging LLMs for post-OCR correction of historical newspapers,” "
              "in __Proc. 3rd Workshop on Language Technologies for Historical and Ancient "
              "Languages (LT4HALA), LREC-COLING__, 2024.",
    "GAYER": "A. V. Gayer, Y. S. Chernyshova, and V. V. Arlazarov, “Recognition of machine-readable "
             "zone in identity documents: A review,” __IEEE Access__, vol. 13, pp. 89300–89313, "
             "2025, doi: 10.1109/ACCESS.2025.3571547.",
    "GOODFELLOW": "I. J. Goodfellow, Y. Bulatov, J. Ibarz, S. Arnoud, and V. Shet, “Multi-digit "
                  "number recognition from Street View imagery using deep convolutional neural "
                  "networks,” in __Proc. International Conference on Learning Representations "
                  "(ICLR)__, 2014.",
    "SHI": "B. Shi, X. Bai, and C. Yao, “An end-to-end trainable neural network for image-based "
           "sequence recognition and its application to scene text recognition,” __IEEE "
           "Transactions on Pattern Analysis and Machine Intelligence__, vol. 39, no. 11, "
           "pp. 2298–2304, 2017, doi: 10.1109/TPAMI.2016.2646371.",
    "GRAVES": "A. Graves, S. Fernández, F. Gomez, and J. Schmidhuber, “Connectionist temporal "
              "classification: Labelling unsegmented sequence data with recurrent neural "
              "networks,” in __Proc. 23rd International Conference on Machine Learning (ICML)__, "
              "2006, pp. 369–376, doi: 10.1145/1143844.1143891.",
    "LI_TROCR": "M. Li et al., “TrOCR: Transformer-based optical character recognition with "
                "pre-trained models,” in __Proc. AAAI Conference on Artificial Intelligence__, "
                "vol. 37, no. 11, 2023, pp. 13094–13102, doi: 10.1609/aaai.v37i11.26538.",
    "GUO": "C. Guo, G. Pleiss, Y. Sun, and K. Q. Weinberger, “On calibration of modern neural "
           "networks,” in __Proc. 34th International Conference on Machine Learning (ICML)__, "
           "PMLR, vol. 70, 2017, pp. 1321–1330.",
    "CHOW": "C. K. Chow, “On optimum recognition error and reject tradeoff,” __IEEE Transactions on "
            "Information Theory__, vol. 16, no. 1, pp. 41–46, 1970, doi: 10.1109/TIT.1970.1054406.",
    "GEIFMAN": "Y. Geifman and R. El-Yaniv, “Selective classification for deep neural networks,” in "
               "__Advances in Neural Information Processing Systems (NeurIPS)__, vol. 30, 2017.",
    "DIETTERICH": "T. G. Dietterich, “Approximate statistical tests for comparing supervised "
                  "classification learning algorithms,” __Neural Computation__, vol. 10, no. 7, "
                  "pp. 1895–1923, 1998, doi: 10.1162/089976698300017197.",
}

# =====================================================================================
# DANH SO TRICH DAN THEO THU TU XUAT HIEN DAU TIEN (chuan IEEE)
# =====================================================================================
_thu_tu = []
for chuoi in (MUC_8 + [t for _, t in MUC_10] + [t for _, t in MUC_11]):
    for k in re.findall(r"\{([A-Z0-9_]+)\}", chuoi):
        if k not in _thu_tu:
            _thu_tu.append(k)
SO = {k: i + 1 for i, k in enumerate(_thu_tu)}
thieu = set(REFS) - set(SO)
if thieu:
    raise SystemExit(f"Tai lieu khong duoc trich trong than bai: {thieu}")


def gan_so(chuoi):
    return re.sub(r"\{([A-Z0-9_]+)\}", lambda m: f"[{SO[m.group(1)]}]", chuoi)


# =====================================================================================
# CONG CU XML
# =====================================================================================
TOKEN = re.compile(r"(\*\*.+?\*\*|__.+?__|\^.+?\^|~.+?~)")


def phan_tich(markup):
    """Tach chuoi danh dau thanh [(text, dam, nghieng, vi_tri)]; vi_tri: None/superscript/subscript."""
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


def tao_run(text, dam=False, nghieng=False, vi_tri=None, co=24):
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
    # Ky tu \t chuyen thanh <w:tab/> de nhan [n] trong danh muc tai lieu thang cot
    for k, manh in enumerate(text.split("\t")):
        if k:
            r.append(OxmlElement("w:tab"))
        if manh:
            t = OxmlElement("w:t")
            t.set(qn("xml:space"), "preserve")
            t.text = manh
            r.append(t)
    return r


def tao_doan(ppr, markup, dam_het=False, nghieng_het=False):
    p = OxmlElement("w:p")
    if ppr is not None:
        p.append(copy.deepcopy(ppr))
    for text, dam, ngh, vt in phan_tich(gan_so(markup)):
        p.append(tao_run(text, dam or dam_het, ngh or nghieng_het, vt))
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
    shutil.copyfile(FILE, GOC)
    print("Da sao luu ban truoc nhan xet:", os.path.basename(GOC))

doc = Document(GOC)
body = doc.element.body
T_THANHVIEN, T_MUCTIEU, T_TIENDO, T_KY = doc.tables[:4]


def tim_p(bat_dau, sau=None):
    """Tim doan than bai (khong nam trong bang/textbox) bat dau bang chuoi cho truoc."""
    gap = sau is None
    for el in body.iterchildren(qn("w:p")):
        if not gap:
            gap = el is sau
            continue
        if text_of(el).strip().startswith(bat_dau):
            return el
    raise SystemExit(f"Khong tim thay doan: {bat_dau!r}")


def lay_ppr(bat_dau):
    p = tim_p(bat_dau)
    ppr = p.find(qn("w:pPr"))
    return copy.deepcopy(ppr) if ppr is not None else None


PPR_BODY = lay_ppr("Vận tải container là")
PPR_SUB = lay_ppr("a) Nhận dạng mã container")
PPR_REF = lay_ppr("[1]")
PPR_CH = lay_ppr("CHƯƠNG 1.")
PPR_SEC = lay_ppr("1.1. Lý do")
PPR_BUL = lay_ppr("- Thuật toán giải mã")
PPR_LABEL = lay_ppr("Sản phẩm đạt được:")


def xoa_giua(dau_el, cuoi_bat_dau, ca_doan_rong=False):
    """Xoa cac doan (khong xoa bang) nam giua dau_el va doan bat dau bang cuoi_bat_dau."""
    xoa = 0
    el = dau_el.getnext()
    while el is not None:
        nxt = el.getnext()
        if el.tag == qn("w:p"):
            t = text_of(el).strip()
            if t.startswith(cuoi_bat_dau):
                break
            if t or ca_doan_rong:
                body.remove(el)
                xoa += 1
        el = nxt
    return xoa


def chen_sau(neo, cac_el):
    for e in cac_el:
        neo.addnext(e)
        neo = e
    return neo


def khoi_van_ban(muc):
    ra = []
    for loai, nd in muc:
        if loai == "sub":
            ra.append(tao_doan(PPR_SUB, nd, dam_het=True, nghieng_het=True))
        else:
            ra.append(tao_doan(PPR_BODY, nd))
    return ra


# ---------------------------------------------------------------- 0. Trang bia
ten_hoa = TEN_MOI.upper()
n_bia = n_so3 = 0
for tx in body.iter(qn("w:txbxContent")):
    for p in list(tx.iter(qn("w:p"))):
        if p.find(".//" + qn("w:p")) is not None:
            continue
        t = text_of(p).strip()
        if t == TEN_CU_HOA:
            ts = list(p.iter(qn("w:t")))
            ts[0].text = ten_hoa
            for x in ts[1:]:
                x.text = ""
            n_bia += 1
        elif t == "3.":
            p.getparent().remove(p)
            n_so3 += 1
print(f"0) Bia: doi ten {n_bia} textbox; bo dong '3.' trong {n_so3} textbox")

# ---------------------------------------------------------------- 1. Muc 1
h1 = tim_p("1. Tên đề tài")
p_ten = h1.getnext()
while not text_of(p_ten).strip():
    p_ten = p_ten.getnext()
for r in list(p_ten.iter(qn("w:r"))):
    r.getparent().remove(r)
p_ten.append(tao_run(TEN_MOI))
print("1) Muc 1: da doi ten de tai")

# ---------------------------------------------------------------- 2. Muc 8
h8 = tim_p("8. Tính cấp thiết")
print(f"2) Muc 8: xoa {xoa_giua(h8, '9. Mục tiêu')} doan cu")
chen_sau(h8, [tao_doan(PPR_BODY, x) for x in MUC_8])

# ---------------------------------------------------------------- 3. Muc 9
h9 = tim_p("9. Mục tiêu")
xoa_giua(h9, "10. Tổng quan")
chen_sau(h9, [tao_doan(PPR_BODY, x) for x in MUC_9])


def ghi_o(cell, markup, canh="l", dam=False, co=24):
    tc = cell._tc
    for p in list(tc.iter(qn("w:p"))):
        p.getparent().remove(p)
    dong = markup.split("\n")
    p = OxmlElement("w:p")
    tc.append(p)
    from docx.text.paragraph import Paragraph
    para = Paragraph(p, cell)
    pf = para.paragraph_format
    pf.left_indent = Cm(0)
    pf.right_indent = Cm(0)
    pf.first_line_indent = Cm(0)
    pf.line_spacing = 1.15
    pf.space_before = Pt(2)
    pf.space_after = Pt(2)
    para.alignment = {"c": WD_ALIGN_PARAGRAPH.CENTER, "l": WD_ALIGN_PARAGRAPH.LEFT,
                      "r": WD_ALIGN_PARAGRAPH.RIGHT}[canh]
    for k, d in enumerate(dong):
        if k:
            br_r = OxmlElement("w:r")
            br_r.append(OxmlElement("w:br"))
            p.append(br_r)
        for text, b, i, vt in phan_tich(d):
            p.append(tao_run(text, b or dam, i, vt, co))
    cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER


def dat_rong(tbl, cols):
    g = tbl._tbl.find(qn("w:tblGrid"))
    for gc in list(g):
        g.remove(gc)
    for w in cols:
        gc = OxmlElement("w:gridCol")
        gc.set(qn("w:w"), str(w))
        g.append(gc)
    for r in tbl.rows:
        trpr = r._tr.find(qn("w:trPr"))
        if trpr is not None:
            for h in trpr.findall(qn("w:trHeight")):
                trpr.remove(h)
        # Duyet tung <w:tc> that (o gop ngang co gridSpan > 1 lay tong be rong cac cot)
        cot = 0
        for tc in r._tr.findall(qn("w:tc")):
            tcPr = tc.get_or_add_tcPr()
            gs = tcPr.find(qn("w:gridSpan"))
            span = int(gs.get(qn("w:val"))) if gs is not None else 1
            w = sum(cols[cot:cot + span])
            cot += span
            tcW = tcPr.find(qn("w:tcW"))
            if tcW is None:
                tcW = OxmlElement("w:tcW")
                tcPr.insert(0, tcW)
            tcW.set(qn("w:w"), str(w))
            tcW.set(qn("w:type"), "dxa")
    tblPr = tbl._tbl.find(qn("w:tblPr"))
    tblW = tblPr.find(qn("w:tblW"))
    if tblW is None:
        tblW = OxmlElement("w:tblW")
        tblPr.append(tblW)
    tblW.set(qn("w:w"), str(sum(cols)))
    tblW.set(qn("w:type"), "dxa")
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    tbl.autofit = False


for i, (ma, mt, tc) in enumerate(MUC_TIEU, start=1):
    row = T_MUCTIEU.rows[i]
    ghi_o(row.cells[0], ma, "c")
    ghi_o(row.cells[1], mt)
    ghi_o(row.cells[2], tc)
dat_rong(T_MUCTIEU, (900, 3000, 5170))
print("3) Muc 9: muc tieu tong quat + bang MT1-MT5")

# ---------------------------------------------------------------- 4. Muc 10 + tai lieu
h10 = tim_p("10. Tổng quan")
xoa_giua(h10, "Tài liệu tham khảo:")
chen_sau(h10, khoi_van_ban(MUC_10))

htl = tim_p("Tài liệu tham khảo:")
xoa_giua(htl, "11. Đối tượng")
refs = []
for k in _thu_tu:
    p = tao_doan(PPR_REF, f"[{SO[k]}]\t" + REFS[k])
    # Can trai + treo 1,0 cm: nhan [n] sat le, noi dung thang cot; tranh gian chu o dong co DOI dai
    from docx.text.paragraph import Paragraph
    para = Paragraph(p, None)
    para.alignment = WD_ALIGN_PARAGRAPH.LEFT
    para.paragraph_format.left_indent = Cm(1.0)
    para.paragraph_format.first_line_indent = Cm(-1.0)
    refs.append(p)
chen_sau(htl, refs)
print(f"4) Muc 10: 5 tieu muc; danh muc {len(refs)} tai lieu theo thu tu trich dan")

# ---------------------------------------------------------------- 5. Muc 11
h11 = tim_p("11. Đối tượng")
xoa_giua(h11, "12. Nội dung")
chen_sau(h11, khoi_van_ban(MUC_11))
print("5) Muc 11: viet lai a-d")

# ---------------------------------------------------------------- 6. Muc 12.1
h121 = tim_p("12.1.")
xoa_giua(h121, "12.2.", ca_doan_rong=True)
cac = []
for k, (ch, secs) in enumerate(CHUONG):
    p = tao_doan(PPR_CH, ch, dam_het=True)
    from docx.text.paragraph import Paragraph
    Paragraph(p, None).paragraph_format.space_before = Pt(6 if k else 4)
    cac.append(p)
    cac += [tao_doan(PPR_SEC, s) for s in secs]
cac.append(tao_doan(PPR_BODY, ""))
chen_sau(h121, cac)
print("6) Muc 12.1: khung 6 chuong moi")

# ---------------------------------------------------------------- 7. Bang tien do 12.2
for i, (tg, nd, sp, nguoi) in enumerate(TIEN_DO, start=1):
    row = T_TIENDO.rows[i]
    ghi_o(row.cells[0], tg, "c")
    ghi_o(row.cells[1], nd)
    ghi_o(row.cells[2], sp)
    ghi_o(row.cells[3], nguoi, "c")
dat_rong(T_TIENDO, (1250, 3570, 1850, 2400))
print("7) Bang tien do: 6 giai doan moi")

# ---------------------------------------------------------------- 8. Muc 13
h13 = tim_p("13. Sản phẩm")
xoa_giua(h13, "14. Kinh phí")
m13 = [tao_doan(PPR_LABEL, "Sản phẩm đạt được:", dam_het=True, nghieng_het=True)]
m13 += [tao_doan(PPR_BUL, "- " + s) for s in SAN_PHAM]
m13.append(tao_doan(PPR_LABEL, "Khả năng ứng dụng:", dam_het=True, nghieng_het=True))
m13 += [tao_doan(PPR_BUL, f"**- {nhan}: **{nd}") for nhan, nd in UNG_DUNG]
chen_sau(h13, m13)
print("8) Muc 13: san pham + kha nang ung dung (bo mau thuan gioi han ap dung)")

# ---------------------------------------------------------------- 9. Muc 14: du toan
p_bc = tim_p("Bằng chữ:")
nhan_dt = tao_doan(PPR_LABEL, "Dự toán chi tiết:", dam_het=True, nghieng_het=True)
p_bc.addnext(nhan_dt)

bang = doc.add_table(rows=len(DU_TOAN) + 2, cols=5)
bang._tbl.remove(bang._tbl.tblPr)
bang._tbl.insert(0, copy.deepcopy(T_MUCTIEU._tbl.tblPr))
TIEU_DE = ("TT", "Nội dung chi", "Số lượng", "Đơn giá (đồng)", "Thành tiền (đồng)")
for j, h in enumerate(TIEU_DE):
    ghi_o(bang.rows[0].cells[j], h, "c", dam=True)
for i, (tt, nd, sl, dg, tt2) in enumerate(DU_TOAN, start=1):
    for j, (v, c) in enumerate(((tt, "c"), (nd, "l"), (sl, "c"), (dg, "r"), (tt2, "r"))):
        ghi_o(bang.rows[i].cells[j], v, c)
cuoi = bang.rows[-1].cells
gop = cuoi[0].merge(cuoi[3])
ghi_o(gop, "Tổng cộng", "c", dam=True)
ghi_o(bang.rows[-1].cells[-1], TONG_TIEN, "r", dam=True)
dat_rong(bang, (600, 3870, 1500, 1450, 1650))
nhan_dt.addnext(bang._tbl)
bang._tbl.addnext(tao_doan(PPR_BODY, ""))

tong = sum(int(x[4].replace(".", "")) for x in DU_TOAN)
assert tong == 4950000, tong
print(f"9) Muc 14: bang du toan {len(DU_TOAN)} khoan, tong {tong:,} dong".replace(",", "."))

# ---------------------------------------------------------------- 10. Ep le moi o bang
# Thut le = 0 (style Normal cua mau thut 1,75 cm); font TNR cho moi run (o "Nhom truong"
# cua mau dang de font khac); hang khong bi cat ngang trang; hang tieu de lap lai.
for tb in (T_THANHVIEN, T_MUCTIEU, T_TIENDO, bang):
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
                for run in p.runs:
                    run.font.name = "Times New Roman"
                    run._element.get_or_add_rPr().get_or_add_rFonts().set(
                        qn("w:eastAsia"), "Times New Roman")

# ---------------------------------------------------------------- 11. Mau chu va giu tieu de
# Mau goc to do tieu de muc 10 (EE0000) - day la dau vet cua mau, dua ve mau den.
n_mau = 0
for p in body.iterchildren(qn("w:p")):
    for c in list(p.iter(qn("w:color"))):
        # Giu nguyen mau cua textbox trang bia (ten de tai mau do la thiet ke cua mau)
        if any(a.tag == qn("w:txbxContent") for a in c.iterancestors()):
            continue
        c.getparent().remove(c)
        n_mau += 1

# Tieu de muc, tieu muc, nhan va ten chuong luon di cung doan ngay sau (khong mo coi cuoi trang)
# Chi cac tieu de muc lon (1.-14.), 12.1./12.2., tieu muc a)-e) va ten chuong. KHONG ap cho
# dong muc con "1.1. ..." trong khung chuong, neu khong ca khung bi dinh lam mot khoi.
LA_TIEU_DE = re.compile(r"^(\d{1,2}\.\s|12\.[12]\.\s|[a-e]\)\s|CHƯƠNG\s)")
NHAN = ("Tài liệu tham khảo:", "Sản phẩm đạt được:", "Khả năng ứng dụng:", "Dự toán chi tiết:",
        "Mục tiêu cụ thể của đề tài", "Các thành viên tham gia")
n_giu = 0
from docx.text.paragraph import Paragraph
for p in body.iterchildren(qn("w:p")):
    t = text_of(p).strip()
    if t and (LA_TIEU_DE.match(t) and len(t) < 110 or t.startswith(NHAN)):
        Paragraph(p, None).paragraph_format.keep_with_next = True
        n_giu += 1
# Hang tieu de bang khong duoc nam tro troi cuoi trang: giu hang 0 voi hang 1.
# Bang du toan ngan (8 hang) thi giu nguyen ca bang tren mot trang.
for tb, ca_bang in ((T_MUCTIEU, False), (T_TIENDO, False), (bang, True)):
    hang = tb.rows[:-1] if ca_bang else tb.rows[:1]
    for r in hang:
        for c in r.cells:
            for p in c.paragraphs:
                p.paragraph_format.keep_with_next = True
print(f"10) Bang: ep le, cantSplit, lap tieu de | 11) bo {n_mau} mau chu, keep_with_next {n_giu} tieu de")

out = luu(doc, FILE)
print("\nDA LUU:", out)
