// =====================================================================
// DU LIEU TINH cua so do QUY TRINH VAN HANH — chuyen tu Areas/Admin/Views/Dashboard/QuyTrinh.cshtml (ban MVC).
// Sinh tu dong tu view cu (99 chuc nang, 35 co AI) de khong go tay sai. Duong dan GIU dia chi ban MVC,
// viet thuong: Url.Action("PhieuCan", "Gate") -> /admin/gate/phieucan, Url.Content("~/Tracking") -> /tracking.
// So song cua tung giai doan lay tu GET /api/quy-trinh-van-hanh (khop theo truong 'so').
// =====================================================================

export const GIAI_DOAN = [
  { so: 1, ten: "Khách đăng ký & đặt dịch vụ", vaiTro: "Khách hàng", moTa: "Khách mở tài khoản (có đăng nhập khuôn mặt AI), đặt dịch vụ bốc xếp/lưu bãi/vận chuyển online — tương đương khai phương án trên ePort.", duong: "/booking/create", ai: true, icon: "bi-person-plus" },
  { so: 2, ten: "Thanh toán phí trước", vaiTro: "Khách · Thu ngân", moTa: "Thanh toán VietQR động (quét là đúng số tiền), hóa đơn gửi email, thu ngân đối soát — lệnh chỉ hiệu lực khi đã nộp phí.", duong: "/booking/my", ai: false, icon: "bi-qr-code" },
  { so: 3, ten: "Lệnh giao nhận điện tử (eGate Pass)", vaiTro: "Điều độ", moTa: "Đăng ký TRƯỚC: xe nào · container nào · tài xế · khung giờ → hệ thống cấp SỐ LỆNH + MÃ PIN, in cho tài xế cầm theo.", duong: "/admin/gatepass", ai: false, icon: "bi-qr-code-scan" },
  { so: 4, ten: "Điều độ tàu & AI phân bổ bến", vaiTro: "Điều độ", moTa: "Lịch tàu ETA/ETD, AI chấm điểm xếp tàu vào bến theo chiều dài, mớn nước, lịch trống.", duong: "/admin/vesselcalls", ai: true, icon: "bi-water" },
  { so: 5, ten: "Cổng vào — ALPR đối chiếu lệnh", vaiTro: "Cổng (tự động)", moTa: "Camera đọc biển số (3 tầng AI) → TỰ ĐỐI CHIẾU lệnh → soi ngoại quan móp méo → cấp DOCK → lưu ảnh, mở barrier.", duong: "/admin/gate", ai: true, icon: "bi-upc-scan" },
  { so: 6, ten: "Trạm cân lần 1 (VGM)", vaiTro: "Trạm cân", moTa: "Cân xe + hàng lúc vào, phục vụ khai VGM theo công ước SOLAS.", duong: "/admin/gate/phieucan", ai: false, icon: "bi-speedometer" },
  { so: 7, ten: "Hạ bãi — AI gợi ý vị trí", vaiTro: "Bãi", moTa: "AI chọn block–bay–row–tier tối ưu, sơ đồ bãi trực quan cập nhật ngay.", duong: "/admin/containers/yardmap", ai: true, icon: "bi-grid-3x3-gap" },
  { so: 8, ten: "Giám sát trong cảng", vaiTro: "An ninh (tự động)", moTa: "Camera AI 24/7: lửa · ngã · đột nhập; DMS tài xế; kiểm đếm hàng — sự cố tự hú còi + video bằng chứng.", duong: "/admin/security/monitor", ai: true, icon: "bi-camera-video" },
  { so: 9, ten: "Hồ sơ container 360°", vaiTro: "Điều độ · Giao nhận", moTa: "Vòng đời 4 bước, dòng thời gian, PHÍ LƯU BÃI TỰ ĐỘNG, audit trail.", duong: "/admin/containers", ai: false, icon: "bi-box-seam" },
  { so: 10, ten: "Chứng từ — in & ghi sổ tự động", vaiTro: "Giao nhận", moTa: "EIR song ngữ, phiếu cân, lệnh — mọi bản in tự lưu KHO CHỨNG TỪ SỐ.", duong: "/admin/docs", ai: false, icon: "bi-archive" },
  { so: 11, ten: "Cổng ra — khép vòng đời lệnh", vaiTro: "Cổng (tự động)", moTa: "Cân lần 2 chốt hàng → đối chiếu lần cuối → gate-out → lệnh HOÀN TẤT.", duong: "/admin/gate", ai: true, icon: "bi-box-arrow-right" },
  { so: 12, ten: "Hậu kiểm & điều hành", vaiTro: "Quản trị", moTa: "Đối soát, báo cáo, xuất MISA, AI Agent đề xuất việc, dự báo sản lượng, nhật ký thanh tra.", duong: "/admin/reports", ai: true, icon: "bi-graph-up-arrow" },
]

/** Moi muc: [ten chuc nang, vai tro, duong dan, co AI] — nhom xep theo thu tu giai doan */
export const NHOM_CHUC_NANG = [
  {
    ten: "GĐ 1–2 · KHÁCH HÀNG & THANH TOÁN",
    muc: [
      ["Đăng ký tài khoản khách hàng", "Khách", "/account/register", false],
      ["Đăng nhập mật khẩu / KHUÔN MẶT (ArcFace + liveness)", "Khách", "/account/login", true],
      ["Cài đặt khuôn mặt 3 góc (FaceSetup)", "Khách", "/account/facesetup", true],
      ["Xem lịch tàu sắp cập cảng", "Khách", "/schedule", false],
      ["Tra cứu container — quét ảnh OCR + checksum ISO 6346", "Khách", "/tracking", true],
      ["Ước tính chi phí bằng AI", "Khách", "/cost", true],
      ["Đặt dịch vụ cảng (booking online)", "Khách", "/booking/create", false],
      ["Booking của tôi + thanh toán VietQR + hóa đơn email", "Khách", "/booking/my", false],
      ["PortBot — chatbot AI tra cứu 24/7", "Khách", "/", true],
      ["Duyệt đơn đặt dịch vụ (nội bộ)", "Điều độ", "/admin/bookings", false],
      ["Đối soát thanh toán + thống kê dòng tiền", "Thu ngân", "/admin/payments", false],
      ["Cổng khách hàng: container + lệnh + chứng từ + ảnh hàng AI chụp (học ePort Cát Lái)", "Khách hàng", "/hangcuatoi", false],
    ],
  },
  {
    ten: "GĐ 1–2 · TRƯỚC KHI XE ĐẾN",
    muc: [
      ["TAS — lưới slot đặt giờ vào cổng, chặn khung giờ đầy (học HHIT Lạch Huyện)", "Điều độ", "/admin/gatepass/form", false],
    ],
  },
  {
    ten: "GĐ 3–4 · LỆNH & ĐIỀU ĐỘ TÀU",
    muc: [
      ["Lệnh giao nhận điện tử eGate Pass (tạo/in/PIN/vòng đời)", "Điều độ", "/admin/gatepass", false],
      ["CỔNG TÀI XẾ — tra lệnh bằng PIN + check-in online (như app ePort Driver)", "Tài xế", "/taixe", false],
      ["Điều độ chuyến tàu (ETA/ETD/ATA/ATD)", "Điều độ", "/admin/vesselcalls", false],
      ["AI phân bổ bến (chấm điểm LOA, mớn nước, lịch trống)", "Điều độ", "/admin/aitools/berth", true],
      ["Danh mục tàu biển · hãng tàu · bến cảng", "Điều độ", "/admin/vessels", false],
      ["Gantt cầu bến — lịch tàu chiếm bến 9 ngày, tự phát hiện trùng lịch (chuẩn TOS CATOS/TOPX)", "Điều độ", "/admin/berths/gantt", false],
    ],
  },
  {
    ten: "GĐ 5–6 · CỔNG VÀO & TRẠM CÂN",
    muc: [
      ["Cổng thông minh ALPR — YOLO26 + CCT-S v2 + Tesseract, biển trắng & vàng", "Tự động", "/admin/gate", true],
      ["Đối chiếu lệnh tự động theo biển số (panel KHỚP LỆNH)", "Tự động", "/admin/gate", true],
      ["Soi ngoại quan — phát hiện móp méo/hư hại vỏ", "Tự động", "/admin/gate", true],
      ["Điều phối DOCK tự động + ảnh xe bằng chứng", "Tự động", "/admin/gate", true],
      ["Nhật ký ra/vào cổng + xuất Excel", "Bảo vệ", "/admin/gate/history", false],
      ["Trạm cân điện tử 2 lần — phiếu cân VGM", "Trạm cân", "/admin/gate/phieucan", false],
      ["Khách xem ảnh hàng thật do camera AI chụp tại cổng/kiểm đếm (thay camera truyền thống)", "Khách hàng", "/hangcuatoi", true],
      ["Màn hình CHỐT CỔNG cho bảo vệ — chữ to, 3 nút, ghi được cả khi MẤT MẠNG", "Bảo vệ", "/admin/gate/baove", false],
      ["Phiếu ra vào cổng GHI TAY — in khi hệ thống gián đoạn, nhập bù sau", "Bảo vệ", "/admin/gate/phieutay", false],
      ["Camera IP hiện trường — dùng chính Hikvision/Dahua cảng đã lắp (ảnh tĩnh HTTP, không cần RTSP)", "Kỹ thuật", "/admin/camera", true],
      ["Bảng tình trạng cổng CÔNG KHAI realtime (học CMIT/Tân Vũ)", "Công khai", "/cong", false],
    ],
  },
  {
    ten: "GĐ 7 · KHO BÃI",
    muc: [
      ["Quản lý container (CRUD + trạng thái vòng đời)", "Điều độ", "/admin/containers", false],
      ["AI gợi ý vị trí hạ bãi (block–bay–row–tier)", "Bãi", "/admin/containers", true],
      ["Sơ đồ bãi trực quan (stack, chiều cao chồng)", "Bãi", "/admin/containers/yardmap", true],
      ["Giám sát container lạnh — IoT đọc 30 phút/lần + đo tay, báo động lệch nhiệt (học bãi lạnh Cát Lái/TCIT)", "Điều độ", "/admin/reefer", false],
      ["Tồn rỗng tách theo HÃNG TÀU — nghiệp vụ depot container rỗng", "Điều độ", "/admin/depot/tonrong", false],
    ],
  },
  {
    ten: "GĐ 8 · GIÁM SÁT AI 24/7",
    muc: [
      ["Camera AI nhận diện vật thể (YOLO26 n/s/m/l + WebGPU)", "Tự động", "/admin/vision", true],
      ["SafePose AI — khung xương 17 khớp, phát hiện ngã/cúi", "Tự động", "/admin/vision/pose", true],
      ["Trạm camera an ninh — lửa · người ngã · vật tĩnh · đột nhập", "Tự động", "/admin/security/monitor", true],
      ["Giám sát tài xế DMS — ngủ gật · ngáp · điện thoại · quay đầu 5s", "Tự động", "/admin/driver", true],
      ["AI TỰ HỌC trạng thái lạ (autoencoder + phản hồi ✔/✖)", "Tự động", "/admin/driver", true],
      ["Giám sát khoang hàng — hàng nghiêng + người xâm nhập", "Tự động", "/admin/driver", true],
      ["Kiểm đếm AI — đếm hàng theo đơn, bắt hàng lạ (đăng ký mẫu ảnh/video)", "Kho", "/admin/security/tally", true],
      ["Phòng an ninh — tiếp nhận & xử lý sự cố", "An ninh", "/admin/security", false],
      ["Kho media AI — ảnh/video bằng chứng", "An ninh", "/admin/security/media", false],
    ],
  },
  {
    ten: "GĐ 9–10 · HỒ SƠ & CHỨNG TỪ",
    muc: [
      ["Hồ sơ container 360° (vòng đời + timeline + audit trail)", "Giao nhận", "/admin/containers", false],
      ["Phí lưu bãi TỰ ĐỘNG theo biểu giá (miễn phí 5 ngày)", "Thu ngân", "/admin/containers", false],
      ["In phiếu EIR song ngữ (Equipment Interchange Receipt)", "Giao nhận", "/admin/containers", false],
      ["KHO CHỨNG TỪ SỐ — ghi sổ tự động, tra cứu, in lại", "Giao nhận", "/admin/docs", false],
      ["Dịch vụ & biểu giá cảng", "Quản trị", "/admin/services", false],
      ["EOR — báo giá sửa chữa vỏ gửi hãng tàu duyệt (song ngữ, vào sổ chuỗi khối)", "Điều độ", "/admin/depot/eor", false],
      ["BLOCKCHAIN chuỗi khối xác thực chứng từ (mô hình eB/L)", "Hệ thống", "/admin/docs", false],
    ],
  },
  {
    ten: "GĐ 11–12 · RA CỔNG & HẬU KIỂM",
    muc: [
      ["Gate-out — đối chiếu ra, khép vòng đời lệnh + container", "Tự động", "/admin/gate", true],
      ["Báo cáo thống kê + xuất Excel/MISA", "Quản trị", "/admin/reports", false],
      ["AI Agent — quét toàn cảng, đề xuất việc cần làm", "Quản trị", "/admin/aitools/agent", true],
      ["Học máy K-Means — phân khúc khách + bắt bất thường", "Quản trị", "/admin/aitools/ml", true],
      ["AI Lab — mạng nơ-ron dự báo TEU + Q-Learning xe nâng", "Quản trị", "/admin/ailab", true],
      ["Quản lý khách hàng (hồ sơ, MST, liên hệ)", "Quản trị", "/admin/customers", false],
    ],
  },
  {
    ten: "NỀN TẢNG HỆ THỐNG",
    muc: [
      ["Giá trị AI · ROI — mỗi module AI tự đếm việc đã làm, quy ra giờ công + tiền tiết kiệm", "Quản trị", "/admin/dashboard/giatriai", true],
      ["Ứng dụng di động PWA — cài lên điện thoại như app thật (tài xế/chủ hàng/điều độ)", "Mọi vai trò", "/mobile", false],
      ["Cảng xanh · ESG — dashboard CO2/giấy/dầu (TCCS 02:2022/CHHVN)", "Quản trị", "/admin/dashboard/cangxanh", false],
      ["Tổng quan điều hành (dashboard)", "Quản trị", "/admin", false],
      ["Tin tức · thư viện ảnh · hộp thư liên hệ", "Quản trị", "/admin/news", false],
      ["Vận hành hệ thống (health) + Nhật ký đầy đủ", "Quản trị", "/admin/system", false],
      ["Quản trị tài khoản & phân quyền (chỉ Admin)", "Admin", "/admin/accounts", false],
    ],
  },
  {
    ten: "LIÊN KẾT DỮ LIỆU",
    muc: [
      ["Hồ sơ container 360° — dòng thời gian gom từ 7 nguồn", "Khai thác", "/admin/containers", false],
      ["Hồ sơ xe · tài xế — gõ biển số ra toàn bộ lịch sử", "Khai thác", "/admin/gate/hosoxe", false],
      ["Tìm kiếm toàn cục Ctrl+K (cont · xe · lệnh · khách · EOR · tàu)", "Mọi vai trò", "/admin", false],
      ["Đồng bộ trạng thái container ngay khi xe qua cổng", "Hệ thống", "/admin/gate/baove", false],
      ["API tích hợp mở — nối kế toán / WMS / TMS của khách", "Admin", "/admin/tichhop", false],
      ["Tài liệu API cho kỹ thuật bên khách (in ra PDF gửi được)", "Admin", "/admin/tichhop/tailieu", false],
    ],
  },
  {
    ten: "DÒNG TIỀN",
    muc: [
      ["Đồng hồ lưu bãi — bảng kê tiền đang treo ngoài bãi", "Kế toán", "/admin/luubai", false],
      ["Chốt kỳ hàng loạt cuối tháng", "Kế toán", "/admin/luubai", false],
      ["Sổ phiếu thu · theo dõi nợ quá hạn 30 ngày", "Kế toán", "/admin/luubai/danhsach", false],
      ["Phiếu thu song ngữ kèm QR chuyển khoản VietQR", "Kế toán", "/admin/luubai/danhsach", false],
      ["Tự chốt phí ngay khi container rời bãi (gate-out)", "Hệ thống", "/admin/gate/baove", false],
      ["Thu phí cổng — biểu giá theo loại xe (sửa được, không hardcode)", "Thu ngân", "/admin/phicong", false],
      ["Thẻ xe: thẻ tháng miễn lượt · thẻ trả trước trừ dần", "Thu ngân", "/admin/phicong/thexe", false],
      ["Bảng giao ca thu phí cổng (in ký nhận tiền)", "Thu ngân", "/admin/phicong/tongket", false],
    ],
  },
  {
    ten: "KẾT NỐI HÃNG TÀU · EDI",
    muc: [
      ["Sinh CODECO báo cáo vào/ra depot (UN/EDIFACT D:95B)", "Giao nhận", "/admin/edi", false],
      ["Nhận COPARN → tự lập lệnh giao nhận, không gõ tay", "Điều độ", "/admin/edi/nhancoparn", false],
      ["Dịch bản tin EDIFACT sang tiếng Việt để kiểm tra trước khi gửi", "Giao nhận", "/admin/edi", false],
      ["Sinh COARRI báo cáo dỡ/xếp container theo chuyến tàu", "Giao nhận", "/admin/edi", false],
    ],
  },
  {
    ten: "AN NINH · CHỐNG GIAN LẬN",
    muc: [
      ["Đối soát bộ đếm độc lập với số hệ thống ghi nhận", "An ninh", "/admin/doisoatcong", false],
      ["Lệch quá 5 lượt tự mở sự cố an ninh + ước tính tiền thất thoát", "Hệ thống", "/admin/doisoatcong", false],
      ["Theo dõi số lần mở barie bằng tay + bắt buộc giải trình", "An ninh", "/admin/doisoatcong", false],
      ["Quét mã QR thẻ CCCD gắn chip — đăng ký tài xế trong 2 giây", "Bảo vệ", "/admin/taixe/quet", false],
      ["OCR bằng lái + CẢNH BÁO bằng hết hạn", "Bảo vệ", "/admin/taixe/quet", true],
      ["Hồ sơ tài xế — nhận ra khách quen, gắn vào lượt qua cổng", "Bảo vệ", "/admin/taixe", false],
      ["API cho thiết bị đếm xe thật (cảm biến vòng từ) — chống gửi trùng", "Hệ thống", "/admin/tichhop/tailieu", false],
    ],
  },
  {
    ten: "AGENT TỰ HÀNH ĐỘNG",
    muc: [
      ["Bảng điều khiển Agent — 7 kỹ năng, 3 chế độ Tắt/Xin phép/Tự làm", "Quản trị", "/admin/agent", true],
      ["Agent TỰ CHỐT phí lưu bãi → lập phiếu thu thật (không chỉ đề xuất)", "Tự động", "/admin/agent", true],
      ["Agent TỰ SINH bản tin EDI CODECO gửi hãng tàu", "Tự động", "/admin/agent", true],
      ["Agent TỰ NHẮC công nợ phiếu lưu bãi quá hạn", "Tự động", "/admin/agent", true],
      ["Agent cảnh báo thẻ xe sắp/đã hết hạn trước khi xe tới cổng", "Tự động", "/admin/agent", true],
      ["Agent giục duyệt EOR sửa chữa tồn đọng", "Tự động", "/admin/agent", true],
      ["Agent cảnh báo container tồn bãi quá lâu (vốn chết ngoài bãi)", "Tự động", "/admin/agent", true],
      ["Agent chặn tài xế bằng lái hết hạn — TỰ MỞ sự cố an ninh", "Tự động", "/admin/agent", true],
      ["Nhật ký agent — mỗi việc có LÝ DO đọc được + ai chạy + HOÀN TÁC được", "Quản trị", "/admin/agent", false],
      ["Bộ chạy nền 30 phút · API /api/v1/agent/chay cho bộ hẹn giờ ngoài", "Hệ thống", "/admin/agent", false],
    ],
  },
]
