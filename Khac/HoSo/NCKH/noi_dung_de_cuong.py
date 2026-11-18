# -*- coding: utf-8 -*-
"""
NOI DUNG de cuong NCKH — ban viet lai theo yeu cau cua GVHD (29/09/2026):
  (1) Noi dung phai bam sat TEN DE TAI;
  (2) Van phong khoa hoc, khong dung van noi.

Quy uoc van phong ap dung cho toan bo file nay:
  - Khong dung dau gach ngang em-dash; thay bang dau phay, hai cham hoac ngoac don.
  - Khong dung thanh ngu, khau ngu ("khong bat dau tu con so khong", "go duoc e ngai",
    "chua ai do", "nham toi"...).
  - Khong xung ho ngoi thu nhat so it; dung "de tai", "nghien cuu nay", "nhom nghien cuu".
  - Thuat ngu tieng Anh dat trong ngoac don o lan xuat hien dau tien.
  - Cau tran thuat, uu tien the bi dong hoac vo nhan xung.
"""

TEN_VI = ("Nâng cao độ chính xác mô hình học sâu nhận dạng mã container "
          "bằng giải mã có ràng buộc ISO 6346")
TEN_EN = ("Improving Deep Learning Container Code Recognition "
          "through ISO 6346 Checksum-Constrained Decoding")

# =====================================================================
# MUC 8 — TINH CAP THIET
# =====================================================================
MUC_8 = [
    "Vận tải container là phương thức chủ đạo trong chuỗi cung ứng hàng hóa. Tại các cảng cạn (Inland "
    "Container Depot, ICD) và depot container, cổng ra vào là điểm nghẽn cố hữu của quy trình khai thác. "
    "Việc ghi nhận mã container tại cổng hiện được thực hiện thủ công hoặc bán tự động, dẫn đến thời "
    "gian chờ kéo dài và sai sót trong đối chiếu chứng từ.",

    "Nhận dạng mã container bằng thị giác máy tính là hướng tiếp cận đã được nghiên cứu, song độ chính "
    "xác suy giảm đáng kể trong điều kiện thực địa. Nguyên nhân đến từ đặc thù vật lý của đối tượng: mã "
    "container được in trên vách thép gợn sóng (corrugated surface), khiến ký tự bị biến dạng hình học "
    "và đứt nét khi đi qua các rãnh sóng; bề mặt vỏ container thường bị rỉ sét, trầy xước hoặc bám bụi; "
    "mã được in theo cả phương ngang trên vách hông và phương dọc trên mép cửa.",

    "Chuẩn ISO 6346 quy định mã định danh container gồm mười một ký tự, trong đó ký tự cuối là số kiểm "
    "tra (check digit) được tính từ mười ký tự đứng trước theo một phép biến đổi đại số trên trường dư "
    "modulo 11. Ràng buộc này cung cấp một nguồn thông tin tiên nghiệm có giá trị, song các hệ thống "
    "nhận dạng hiện nay chỉ khai thác nó ở mức hậu kiểm: chuỗi ký tự được giải mã độc lập tại từng vị "
    "trí theo nguyên tắc cực đại hóa xác suất cục bộ (argmax), sau đó số kiểm tra được dùng để chấp "
    "nhận hoặc loại bỏ toàn bộ kết quả.",

    "Cách sử dụng nêu trên bộc lộ hai hạn chế. Thứ nhất, phép giải mã cục bộ loại bỏ toàn bộ phân phối "
    "xác suất do mô hình sinh ra, chỉ giữ lại ứng viên xếp hạng nhất tại mỗi vị trí; khi một ký tự bị "
    "suy giảm chất lượng, ứng viên đúng có thể xếp hạng thứ hai và bị loại bỏ vĩnh viễn. Thứ hai, khi "
    "kết quả không thỏa mãn số kiểm tra, hệ thống loại bỏ toàn bộ chuỗi thay vì khai thác ràng buộc đại "
    "số để hiệu chỉnh ký tự nghi vấn.",

    "Bên cạnh đó, khả năng phát hiện sai sót của chuẩn ISO 6346 không đồng đều giữa các vị trí. Do bảng "
    "giá trị quy đổi chữ cái trải trên hai mươi sáu giá trị nhưng chỉ phân biệt được mười một lớp thặng "
    "dư modulo 11, tồn tại các nhóm chữ cái đồng dư mà phép kiểm tra không phân biệt được, chẳng hạn "
    "nhóm (A, K, U) hoặc nhóm (B, L, V). Với sáu vị trí chữ số, sai lệch giá trị luôn nhỏ hơn 11 nên "
    "mọi sai sót đơn ký tự đều bị phát hiện; với bốn vị trí chữ cái đầu, tính chất này không được bảo "
    "đảm. Mức độ ảnh hưởng thực tế của hiện tượng này đối với bài toán nhận dạng mã container chưa được "
    "định lượng trong các công bố đã khảo sát.",

    "Từ những phân tích trên, việc nghiên cứu một phương pháp giải mã tích hợp trực tiếp ràng buộc số "
    "kiểm tra ISO 6346 vào quá trình suy luận, thay vì sử dụng ràng buộc này ở khâu hậu kiểm, là hướng "
    "tiếp cận có cơ sở khoa học và có giá trị ứng dụng đối với hoạt động khai thác cảng.",
]

# =====================================================================
# MUC 9 — MUC TIEU
# =====================================================================
MUC_9_TQ = (
    "Mục tiêu tổng quát: đề xuất và đánh giá thực nghiệm phương pháp giải mã có ràng buộc số kiểm tra "
    "ISO 6346 nhằm nâng cao độ chính xác nhận dạng tự động mã container trong điều kiện ảnh suy giảm "
    "chất lượng, đồng thời định lượng giới hạn của ràng buộc này tại các vị trí chữ cái đồng dư."
)

MUC_9_CT = [
    ("MT1",
     "Xây dựng bộ dữ liệu kiểm thử ảnh mã container có phân tầng theo điều kiện chụp",
     "Bộ dữ liệu được phân tầng theo bốn mức suy giảm chất lượng (thuận lợi, biến dạng do gợn sóng, rỉ "
     "sét hoặc trầy xước, ngược sáng hoặc thiếu sáng); mỗi tầng bảo đảm số lượng mẫu đủ cho kiểm định "
     "thống kê; kèm tài liệu mô tả quy tắc gán nhãn cho phép tái lập"),

    ("MT2",
     "Đề xuất thuật toán giải mã có ràng buộc số kiểm tra trên không gian thặng dư modulo 11",
     "Thuật toán được phát biểu hình thức, chứng minh tính đúng đắn và đánh giá độ phức tạp; cài đặt "
     "được trên nền suy luận tại biên với chi phí tính toán không vượt quá 5% tổng thời gian xử lý"),

    ("MT3",
     "Đánh giá hiệu quả của phương pháp đề xuất so với các đường cơ sở",
     "So sánh với giải mã cực đại hóa xác suất cục bộ (argmax) và với phương pháp hậu kiểm số kiểm tra; "
     "báo cáo độ chính xác cấp ký tự và cấp chuỗi trên từng tầng điều kiện ảnh; kiểm định ý nghĩa thống "
     "kê của chênh lệch"),

    ("MT4",
     "Định lượng giới hạn của ràng buộc số kiểm tra tại các vị trí chữ cái đồng dư",
     "Xác định tỷ lệ sai sót không bị phát hiện do hiện tượng đồng dư modulo 11; đề xuất và kiểm chứng "
     "cơ chế bổ sung nhằm bù đắp giới hạn này"),

    ("MT5",
     "Công bố kết quả nghiên cứu",
     "Tối thiểu một bài báo gửi hội nghị hoặc tạp chí chuyên ngành công nghệ thông tin, định hướng hội "
     "nghị quốc tế có chỉ mục"),
]

# =====================================================================
# MUC 10 — TONG QUAN (muc e giu ket qua so bo, viet lai van phong)
# =====================================================================
MUC_10 = [
    ("a) Nhận dạng mã container bằng mô hình học sâu", [
        "Nhận dạng mã container là bài toán nhận dạng văn bản trong cảnh thực với điều kiện đặc thù, do "
        "đó thu hút nhiều nghiên cứu trong giai đoạn gần đây. Zhao và cộng sự [1] cải tiến khung phát "
        "hiện đối tượng DETR bằng cấu trúc đặc trưng chú ý phân tách và mã hóa vị trí đa tỷ lệ, áp dụng "
        "cho ký tự trên vỏ container. Liang và cộng sự [2] đề xuất kiến trúc Transformer thị giác, ngôn "
        "ngữ tách chiều (Dimension Decoupling Vision-Language Transformer) cho bài toán phát hiện và "
        "nhận dạng đồng thời ký hiệu container công nghiệp, đạt kết quả tốt nhất trên tập dữ liệu "
        "chuyên biệt. Hlabisa và cộng sự [3] xây dựng mạng định vị mã container trên nền ResNet và UNet "
        "kết hợp mạng nhận dạng lai tích chập và hồi tiếp, đạt độ chính xác 97,93% ở tốc độ 64,11 khung "
        "hình mỗi giây. Santos và cộng sự [4] kết hợp YOLOv7 cho phát hiện văn bản với TrOCR cho nhận "
        "dạng ký tự. Ta và cộng sự [5] đề xuất mạng hồi tiếp tích chập tăng cường chú ý đa phân giải "
        "kết hợp khử nhiễu sâu cho môi trường cảng thông minh.",

        "Các công trình nêu trên đều tập trung nâng cao năng lực biểu diễn của mô hình thị giác thông "
        "qua cải tiến kiến trúc mạng. Chuẩn ISO 6346 được nhắc tới trong các nghiên cứu này chủ yếu ở "
        "vai trò mô tả cấu trúc mã, trong khi ràng buộc đại số mà số kiểm tra cung cấp chưa được khai "
        "thác trong quá trình giải mã chuỗi ký tự.",
    ]),
    ("b) Giải mã có ràng buộc và hiệu chỉnh kết quả nhận dạng", [
        "Việc đưa tri thức tiên nghiệm vào quá trình giải mã chuỗi là hướng nghiên cứu đã được xác lập. "
        "Hokamp và Liu [6] đề xuất thuật toán tìm kiếm chùm theo lưới (Grid Beam Search), cho phép áp "
        "đặt ràng buộc từ vựng ngay trong quá trình sinh chuỗi thay vì lọc kết quả sau khi sinh. Trong "
        "lĩnh vực hiệu chỉnh kết quả nhận dạng ký tự quang học, Ramirez-Orta và cộng sự [7] sử dụng tổ "
        "hợp lớn các mô hình chuỗi sang chuỗi ở cấp ký tự, còn Thomas và cộng sự [8] vận dụng mô hình "
        "ngôn ngữ lớn cho hiệu chỉnh hậu kỳ trên văn bản lịch sử.",

        "Đề tài kế thừa hướng tiếp cận giải mã có ràng buộc từ các công trình nêu trên. Điểm khác biệt "
        "nằm ở bản chất của ràng buộc được sử dụng: các ràng buộc từ vựng và mô hình ngôn ngữ mang bản "
        "chất thống kê, trong khi ràng buộc số kiểm tra của chuẩn ISO 6346 mang bản chất đại số và xác "
        "định trên trường thặng dư modulo 11, cho phép thu hẹp không gian nghiệm một cách chặt chẽ và "
        "giải bằng quy hoạch động với chi phí tính toán thấp.",
    ]),
    ("c) Mô hình nền được sử dụng trong đề tài", [
        "Đề tài sử dụng các mô hình học sâu đã được công bố làm thành phần nền. Hassani và cộng sự [9] "
        "đề xuất kiến trúc Compact Convolutional Transformer, thay phép nhúng mảnh ảnh bằng bộ token "
        "hóa tích chập, cho phép mô hình Transformer hoạt động hiệu quả trên tập dữ liệu quy mô nhỏ. Li "
        "và cộng sự [10] giới thiệu TrOCR, mô hình nhận dạng ký tự quang học dựa hoàn toàn trên kiến "
        "trúc Transformer với tiền huấn luyện quy mô lớn. Cheng và cộng sự [11] trình bày khung nhận "
        "dạng văn bản cảnh thực hai giai đoạn kết hợp mạng phát hiện và mạng nhận dạng.",
    ]),
    ("d) Định vị của đề tài", [
        "Trên cơ sở khảo sát nêu trên, đề tài xác định ba điểm định vị. Thứ nhất, về phương pháp, đề "
        "tài chuyển ràng buộc số kiểm tra từ vai trò hậu kiểm sang vai trò ràng buộc trong quá trình "
        "giải mã; hướng vận dụng này chưa được trình bày cho bài toán mã container trong các công bố đã "
        "khảo sát. Thứ hai, đề tài định lượng giới hạn của ràng buộc tại bốn vị trí chữ cái, nơi bảng "
        "giá trị quy đổi của chuẩn tồn tại các nhóm đồng dư modulo 11; nội dung này cũng chưa được đề "
        "cập trong các công bố đã khảo sát. Thứ ba, đề tài đóng góp bộ dữ liệu kiểm thử phân tầng theo "
        "điều kiện chụp kèm quy tắc gán nhãn, phục vụ tái lập kết quả.",
    ]),
]

# =====================================================================
# MUC 14 — TAI LIEU THAM KHAO
# Tieu chi lua chon (theo yeu cau GVHD): uu tien cong bo moi (2021-2025),
# lien quan TRUC TIEP den thuat toan va bai toan cua de tai, co xep hang
# quartile. Da loai bo cac tai lieu ve van hanh cang, du bao luu luong va
# blog ky thuat vi khong lien quan den thuat toan dang nghien cuu.
# =====================================================================
REFS = [
    "X. Zhao et al., “An Improved Character Recognition Framework for "
    "Container Number Based on DETR Algorithm,” Sensors, vol. 21, no. 13, art. 4612, 2021. (Q1) "
    "doi: 10.3390/s21134612",

    "Z. Liang, Y. Xu, Y. Zhai, H. Zhu, J. Xi, P. Coscia, and A. Genovese, “Dimension Decoupling "
    "Vision-Language Transformer for industrial container marking and natural scene text spotting,” "
    "Journal of Industrial Information Integration, 2025. (Q1, IF 11,2)",

    "S. Hlabisa et al., “Confidence-Guided Code Recognition for Shipping Containers "
    "Using Deep Learning,” Big Data and Cognitive Computing, vol. 9, no. 12, art. 316, 2025. (Q1) "
    "doi: 10.3390/bdcc9120316",

    "J. Santos et al., “Automating Code Recognition for Cargo Containers,” "
    "Electronics, vol. 14, no. 22, art. 4437, 2025. (Q2) doi: 10.3390/electronics14224437",

    "A. S. Ta, L. T. Le, and L. Bui-Duy, “Advanced vehicle monitoring in smart port utilizing deep "
    "denoising real-time object detectors integrated multi-resolution attention-augmented CRNN,” Ain "
    "Shams Engineering Journal, vol. 16, no. 8, 2025. (Q1, IF 6,2) doi: 10.1016/j.asej.2025.103483",

    "C. Hokamp and Q. Liu, “Lexically Constrained Decoding for Sequence Generation Using Grid Beam "
    "Search,” in Proc. 55th Annual Meeting of the Association for Computational Linguistics (ACL), "
    "2017, pp. 1535–1546.",

    "J. A. Ramirez-Orta et al., “Post-OCR Document "
    "Correction with Large Ensembles of Character Sequence-to-Sequence Models,” in Proc. AAAI "
    "Conference on Artificial Intelligence, vol. 36, 2022.",

    "A. Thomas et al., “Leveraging LLMs for Post-OCR Correction of Historical "
    "Newspapers,” in Proc. 3rd Workshop on Language Technologies for Historical and Ancient Languages "
    "(LT4HALA), LREC-COLING, 2024.",

    "A. Hassani, S. Walton, N. Shah, A. Abuduweili, J. Li, and H. Shi, “Escaping the Big Data Paradigm "
    "with Compact Transformers,” arXiv preprint arXiv:2104.05704, 2021.",

    "M. Li et al., “TrOCR: "
    "Transformer-Based Optical Character Recognition with Pre-trained Models,” in Proc. AAAI Conference "
    "on Artificial Intelligence, vol. 37, 2023.",

    "Y. Cheng et al., “A Two-Stage End-to-End Framework for Robust Scene Text "
    "Recognition,” Electronics, vol. 14, no. 23, art. 4594, 2025. (Q2) doi: 10.3390/electronics14234594",

    "ISO 6346:2022, Freight containers — Coding, identification and marking, International Organization "
    "for Standardization, Geneva, 2022.",
]

# =====================================================================
# MUC 11 — DOI TUONG, PHAM VI, CACH TIEP CAN, PHUONG PHAP
# =====================================================================
MUC_11 = [
    ("a) Đối tượng nghiên cứu", [
        "Phép giải mã đầu ra của mô hình nhận dạng ký tự quang học dưới ràng buộc số kiểm tra ISO 6346.",
        "Mã định danh container theo chuẩn ISO 6346 gồm bốn ký tự chữ và bảy ký tự số, trong đó ký tự "
        "cuối là số kiểm tra.",
        "Ảnh mã container thu nhận tại cổng cảng trong các điều kiện suy giảm chất lượng khác nhau.",
    ]),
    ("b) Phạm vi nghiên cứu", [
        "Phạm vi nội dung: đề tài tập trung vào khâu giải mã chuỗi ký tự. Đề tài không đề xuất kiến trúc "
        "mạng nơ-ron mới cho tầng phát hiện và tầng nhận dạng, mà sử dụng các mô hình đã được công bố "
        "làm thành phần nền, qua đó cô lập ảnh hưởng của phương pháp giải mã được đề xuất.",
        "Phạm vi đối tượng: đề tài giới hạn ở mã container. Bài toán nhận dạng biển số phương tiện "
        "không thuộc phạm vi nghiên cứu do quy cách biển số Việt Nam không có cơ chế số kiểm tra, vì "
        "vậy không áp dụng được phương pháp đề xuất. Nội dung này được trình bày trong phần thảo luận "
        "nhằm làm rõ điều kiện áp dụng của phương pháp.",
        "Phạm vi dữ liệu: ảnh mã container được thu thập từ các bộ dữ liệu công khai, bổ sung ảnh chụp "
        "thực địa tại cảng cạn hoặc depot trong điều kiện được đơn vị cho phép, và ảnh mẫu của hệ thống "
        "SmartPort. Đề tài không sử dụng dữ liệu vận hành của doanh nghiệp.",
        "Phạm vi triển khai: phương pháp đề xuất được cài đặt và đo đạc trên nền suy luận tại biên, cụ "
        "thể là trình duyệt của máy tính cá nhân phổ thông không trang bị bộ xử lý đồ họa rời. Giai "
        "đoạn huấn luyện mô hình được thực hiện ngoại tuyến.",
    ]),
    ("c) Cách tiếp cận", [
        "Đề tài tiếp cận theo hướng nghiên cứu thực nghiệm có đối chứng. Phương pháp đề xuất được phát "
        "biểu hình thức, chứng minh tính đúng đắn về mặt toán học, sau đó được kiểm chứng bằng thực "
        "nghiệm trên bộ dữ liệu phân tầng, với các đường cơ sở được cài đặt trong cùng điều kiện. Mọi "
        "kết luận về hiệu quả được rút ra từ số đo lặp lại và có kiểm định ý nghĩa thống kê.",
    ]),
    ("d) Phương pháp nghiên cứu", [
        "Phương pháp nghiên cứu tài liệu: tổng hợp và phân tích các công bố giai đoạn 2022 đến 2026 về "
        "nhận dạng mã container, giải mã có ràng buộc và suy luận tại thiết bị biên; xác lập bộ chỉ số "
        "đánh giá và danh mục đường cơ sở đối chứng.",
        "Phương pháp mô hình hóa toán học: phát biểu bài toán giải mã dưới dạng bài toán tối ưu có ràng "
        "buộc trên trường thặng dư modulo 11; thiết kế lời giải bằng quy hoạch động; chứng minh tính "
        "đúng đắn và đánh giá độ phức tạp tính toán.",
        "Phương pháp xây dựng dữ liệu: thu thập ảnh từ các nguồn công khai và thực địa; gán nhãn theo "
        "chuẩn ISO 6346; phân tầng mẫu theo điều kiện chụp; kiểm tra chéo nhãn nhằm đánh giá mức độ "
        "đồng thuận giữa những người gán nhãn.",
        "Phương pháp thực nghiệm đối chứng: cài đặt phương pháp đề xuất và các đường cơ sở trên cùng mô "
        "hình nền và cùng bộ dữ liệu; mỗi cấu hình chỉ khác nhau ở một yếu tố nhằm cô lập ảnh hưởng của "
        "yếu tố đó; lặp lại phép đo nhiều lần và loại bỏ các lần chạy khởi động.",
        "Phương pháp phân tích lỗi: phân loại các trường hợp nhận dạng sai theo nguyên nhân (biến dạng "
        "do gợn sóng, rỉ sét, ngược sáng, đồng dư chữ cái) nhằm xác định giới hạn áp dụng của phương "
        "pháp đề xuất.",
    ]),
]

# =====================================================================
# MUC 12.1 — DE CUONG CHI TIET
# =====================================================================
CHUONG = [
    ("CHƯƠNG 1. TỔNG QUAN", [
        "1.1. Lý do chọn đề tài",
        "1.2. Mục tiêu và nhiệm vụ nghiên cứu",
        "1.3. Đối tượng và phạm vi nghiên cứu",
        "1.4. Phương pháp nghiên cứu",
        "1.5. Bố cục của báo cáo",
    ]),
    ("CHƯƠNG 2. CƠ SỞ LÝ THUYẾT", [
        "2.1. Chuẩn ISO 6346 và phép tính số kiểm tra",
        "2.2. Tính chất đại số của phép kiểm tra trên trường thặng dư modulo 11",
        "2.3. Kiến trúc nhận dạng ký tự quang học hai tầng",
        "2.4. Các phương pháp giải mã chuỗi và giải mã có ràng buộc",
    ]),
    ("CHƯƠNG 3. PHƯƠNG PHÁP ĐỀ XUẤT", [
        "3.1. Phát biểu bài toán giải mã có ràng buộc số kiểm tra",
        "3.2. Thuật toán quy hoạch động trên không gian thặng dư modulo 11",
        "3.3. Chứng minh tính đúng đắn và đánh giá độ phức tạp",
        "3.4. Phân tích giới hạn tại các vị trí chữ cái đồng dư",
        "3.5. Cơ chế bổ sung nhằm bù đắp giới hạn của ràng buộc",
    ]),
    ("CHƯƠNG 4. XÂY DỰNG DỮ LIỆU THỰC NGHIỆM", [
        "4.1. Nguồn dữ liệu và tiêu chí lựa chọn",
        "4.2. Quy tắc gán nhãn và kiểm tra chéo",
        "4.3. Phân tầng mẫu theo điều kiện chụp",
    ]),
    ("CHƯƠNG 5. THỰC NGHIỆM VÀ ĐÁNH GIÁ", [
        "5.1. Thiết lập thực nghiệm và các đường cơ sở đối chứng",
        "5.2. Kết quả độ chính xác cấp ký tự và cấp chuỗi theo từng tầng",
        "5.3. Kiểm định ý nghĩa thống kê của chênh lệch",
        "5.4. Chi phí tính toán của phương pháp đề xuất",
        "5.5. Phân tích các trường hợp nhận dạng sai",
    ]),
    ("CHƯƠNG 6. KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN", [
        "6.1. Kết quả đạt được",
        "6.2. Hạn chế của nghiên cứu",
        "6.3. Hướng phát triển",
    ]),
]

# =====================================================================
# MUC 12.2 — TIEN DO
# =====================================================================
TIEN_DO = [
    ("10–11/2026",
     "Nghiên cứu tổng quan tài liệu; xác lập bộ chỉ số đánh giá và danh mục đường cơ sở đối chứng",
     "Báo cáo tổng quan; danh mục tài liệu tham khảo"),
    ("11/2026 – 01/2027",
     "Phát biểu hình thức bài toán giải mã có ràng buộc; thiết kế thuật toán quy hoạch động; chứng minh "
     "tính đúng đắn và đánh giá độ phức tạp",
     "Đặc tả thuật toán; chứng minh toán học"),
    ("12/2026 – 02/2027",
     "Thu thập, gán nhãn và phân tầng bộ dữ liệu kiểm thử",
     "Bộ dữ liệu kèm tài liệu mô tả quy tắc gán nhãn"),
    ("02–04/2027",
     "Cài đặt phương pháp đề xuất và các đường cơ sở; thực nghiệm đối chứng trên từng tầng điều kiện ảnh",
     "Mã nguồn thực nghiệm; bảng kết quả đối chứng"),
    ("04–06/2027",
     "Định lượng giới hạn tại các vị trí chữ cái đồng dư; kiểm chứng cơ chế bổ sung; phân tích các "
     "trường hợp nhận dạng sai",
     "Báo cáo phân tích lỗi và giới hạn áp dụng"),
    ("06–08/2027",
     "Biên soạn bài báo khoa học và gửi hội nghị",
     "Bản thảo bài báo đã gửi"),
    ("08–09/2027",
     "Hoàn thiện báo cáo tổng kết và hồ sơ nghiệm thu",
     "Báo cáo tổng kết; hồ sơ nghiệm thu"),
]

# =====================================================================
# MUC 13 — SAN PHAM
# =====================================================================
SAN_PHAM = [
    ("1", "Bài báo khoa học",
     "Hội nghị hoặc tạp chí chuyên ngành công nghệ thông tin, định hướng hội nghị quốc tế có chỉ mục", "01"),
    ("2", "Báo cáo tổng kết đề tài", "Theo mẫu quy định của Trường", "01"),
    ("3", "Đặc tả thuật toán giải mã có ràng buộc số kiểm tra",
     "Phát biểu hình thức, chứng minh tính đúng đắn, đánh giá độ phức tạp, kèm mã nguồn cài đặt", "01"),
    ("4", "Bộ dữ liệu kiểm thử ảnh mã container",
     "Phân tầng theo bốn mức điều kiện chụp, gán nhãn theo ISO 6346, kèm tài liệu mô tả cho phép tái lập", "01 bộ"),
    ("5", "Bộ kết quả thực nghiệm đối chứng",
     "Độ chính xác cấp ký tự và cấp chuỗi theo từng tầng, có kiểm định ý nghĩa thống kê", "01 bộ"),
]

UNG_DUNG = (
    "Kết quả nghiên cứu có thể áp dụng tại các cảng cạn, depot container và doanh nghiệp kho vận trong "
    "khâu ghi nhận mã container tại cổng, góp phần rút ngắn thời gian xử lý và giảm sai sót đối chiếu "
    "chứng từ. Do phương pháp đề xuất chỉ can thiệp vào khâu giải mã, chi phí tính toán bổ sung không "
    "đáng kể, cho phép triển khai trên thiết bị sẵn có mà không yêu cầu hạ tầng máy chủ chuyên dụng. "
    "Về mặt phương pháp luận, cách tiếp cận đưa ràng buộc đại số vào quá trình giải mã có thể mở rộng "
    "cho các bài toán nhận dạng chuỗi định danh khác có cơ chế số kiểm tra, chẳng hạn mã số sách tiêu "
    "chuẩn quốc tế hoặc mã số hàng hóa. Kết quả đề tài đồng thời là học liệu minh họa cho các học phần "
    "Trí tuệ nhân tạo, Học máy và Thị giác máy tính tại Viện Công nghệ số."
)
