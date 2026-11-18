// =====================================================================
// MENU KHU DIEU HANH (/admin) — MOT NOI DUY NHAT khai bao thu tu va nhom.
// THU TU MENU = THU TU NGHIEP VU tu NGOAI CONG vao TRONG CANG: so 1 la viec xay ra som nhat, ngoai cong.
// So tu dong danh theo vi tri khai bao (cuoi file), tai lieu huong dan dung dung so nay.
// Dia chi GIU NGUYEN nhu ban MVC cu (/Admin/Containers -> /admin/containers): link do
// nghiep vu sinh ra, link luu trong CSDL, ma QR in tren chung tu van mo dung man hinh.
// Muc nao chua co man hinh dang ky trong src/khu/*/tuyen.js thi TU AN khoi menu.
//   nhan: chu nho ben phai (vd "AI") · vaiTro: chi hien voi vai tro nay
// =====================================================================
export const NHOM_MENU = [
  {
    ma: 'chinh',
    muc: [{ den: '/admin', ten: 'Tổng quan', icon: 'bi-speedometer2' }],
  },
  {
    ma: 'ngoai-cong',
    ten: 'Ngoài cổng · trước khi xe đến',
    icon: 'bi-signpost-2',
    muc: [
      { den: '/admin/bookings', ten: 'Đơn đặt dịch vụ', icon: 'bi-receipt-cutoff' },
      { den: '/admin/edo', ten: 'Lệnh giao hàng điện tử', icon: 'bi-file-earmark-lock2', nhan: 'eDO' },
      { den: '/admin/hai-quan', ten: 'Thông quan hải quan', icon: 'bi-bank', nhan: 'VASSCM' },
      { den: '/admin/gatepass', ten: 'Lệnh giao nhận', icon: 'bi-qr-code-scan', nhan: 'EPASS' },
      { den: '/admin/lich-hen', ten: 'Lịch hẹn cổng (TAS)', icon: 'bi-calendar-week', nhan: 'TAS' },
      { den: '/admin/hang-cho-ao', ten: 'Hàng chờ ảo ngoài cổng', icon: 'bi-people', nhan: 'MỚI' },
      { den: '/admin/ai/cong', ten: 'Dự báo ùn tắc cổng', icon: 'bi-stoplights', nhan: 'M/M/C' },
    ],
  },
  {
    ma: 'tai-cong',
    ten: 'Tại cổng · xe vào và ra',
    icon: 'bi-truck-front',
    muc: [
      { den: '/admin/taixe/quet', ten: 'Quét CCCD tài xế', icon: 'bi-person-vcard', nhan: 'QR' },
      { den: '/admin/taixe', ten: 'Hồ sơ tài xế', icon: 'bi-person-badge' },
      { den: '/admin/gate/hosoxe', ten: 'Hồ sơ xe · tài xế', icon: 'bi-truck', nhan: '360°' },
      { den: '/admin/gate', ten: 'Cổng thông minh', icon: 'bi-upc-scan', nhan: 'ALPR' },
      { den: '/admin/doi-chieu-container', ten: 'Chống tráo container', icon: 'bi-shield-exclamation', nhan: 'AI' },
      { den: '/admin/gate/phieucan', ten: 'Trạm cân · VGM', icon: 'bi-speedometer' },
      { den: '/admin/phicong', ten: 'Thu phí cổng', icon: 'bi-cash-coin', nhan: 'POS' },
      { den: '/admin/doisoatcong', ten: 'Đối soát cổng', icon: 'bi-shield-check', nhan: 'ANTI' },
      { den: '/admin/ai/gian-lan', ten: 'Lượt xe bất thường', icon: 'bi-incognito', nhan: 'IFOREST' },
    ],
  },
  {
    ma: 'noi-bo',
    ten: 'Đường nội bộ · giám sát xe',
    icon: 'bi-sign-turn-right',
    muc: [
      { den: '/admin/driver', ten: 'Giám sát tài xế', icon: 'bi-person-video3', nhan: 'DMS' },
      { den: '/admin/vision', ten: 'Camera AI', icon: 'bi-camera-video', nhan: 'YOLO' },
      { den: '/admin/camera', ten: 'Camera IP hiện trường', icon: 'bi-webcam', nhan: 'IP' },
    ],
  },
  {
    ma: 'bai',
    ten: 'Bãi container',
    icon: 'bi-box-seam',
    muc: [
      { den: '/admin/containers/yardmap', ten: 'Sơ đồ bãi', icon: 'bi-grid-3x3-gap', nhan: 'AI' },
      { den: '/admin/containers', ten: 'Container', icon: 'bi-box-seam' },
      { den: '/admin/containers/tracuu', ten: 'Tra cứu container', icon: 'bi-search' },
      { den: '/admin/thiet-bi-bai', ten: 'Điều phối thiết bị bãi', icon: 'bi-truck-flatbed', nhan: 'MỚI' },
      { den: '/admin/hang-nguy-hiem', ten: 'Hàng nguy hiểm IMDG', icon: 'bi-radioactive', nhan: 'MỚI' },
      { den: '/admin/ai/bai', ten: 'Dự báo rời bãi', icon: 'bi-boxes', nhan: 'KNN' },
      { den: '/admin/reefer', ten: 'Container lạnh', icon: 'bi-thermometer-snow', nhan: 'IOT' },
      { den: '/admin/ai/cont-lanh', ten: 'Cảnh báo sớm cont lạnh', icon: 'bi-thermometer-snow', nhan: 'TREND' },
      { den: '/admin/security/tally', ten: 'Kiểm đếm AI', icon: 'bi-ui-checks-grid', nhan: 'TALLY' },
      { den: '/admin/depot/tonrong', ten: 'Tồn rỗng theo hãng', icon: 'bi-box2', nhan: 'DEPOT' },
      { den: '/admin/depot/eor', ten: 'Sửa chữa vỏ · EOR', icon: 'bi-tools', nhan: 'M&R' },
    ],
  },
  {
    ma: 'tau-ben',
    ten: 'Cầu bến & tàu',
    icon: 'bi-water',
    muc: [
      { den: '/admin/vesselcalls', ten: 'Điều độ chuyến tàu', icon: 'bi-calendar2-week' },
      { den: '/admin/aitools/berth', ten: 'AI phân bổ bến', icon: 'bi-stars', nhan: 'AI' },
      { den: '/admin/berths/gantt', ten: 'Gantt cầu bến', icon: 'bi-bar-chart-steps', nhan: 'TOS' },
      { den: '/admin/ke-hoach-xep-do', ten: 'Kế hoạch xếp dỡ tàu', icon: 'bi-layout-three-columns', nhan: 'MỚI' },
      { den: '/admin/berths', ten: 'Bến cảng', icon: 'bi-bounding-box' },
      { den: '/admin/vessels', ten: 'Tàu biển', icon: 'bi-water' },
      { den: '/admin/shippinglines', ten: 'Hãng tàu', icon: 'bi-globe2' },
      { den: '/admin/edi', ten: 'EDI hãng tàu', icon: 'bi-arrow-left-right', nhan: 'EDI' },
    ],
  },
  {
    ma: 'an-ninh',
    ten: 'An ninh & an toàn toàn cảng',
    icon: 'bi-shield-lock',
    muc: [
      { den: '/admin/security', ten: 'Phòng an ninh', icon: 'bi-building-lock', nhan: 'GUARD' },
      { den: '/admin/security/monitor', ten: 'Trạm camera AI', icon: 'bi-shield-exclamation' },
      { den: '/admin/an-toan-ai', ten: 'Cảnh báo nguy hiểm AI', icon: 'bi-cone-striped', nhan: 'TTC' },
      { den: '/admin/vision/pose', ten: 'SafePose AI', icon: 'bi-person-arms-up', nhan: 'POSE' },
      { den: '/admin/security/media', ten: 'Kho media AI', icon: 'bi-collection-play' },
    ],
  },
  {
    ma: 'tai-chinh',
    ten: 'Thu tiền & chứng từ',
    icon: 'bi-cash-coin',
    muc: [
      { den: '/admin/luubai', ten: 'Phí lưu bãi · công nợ', icon: 'bi-stopwatch', nhan: 'TIỀN' },
      { den: '/admin/payments', ten: 'Đối soát thanh toán', icon: 'bi-cash-stack' },
      { den: '/admin/docs', ten: 'Kho chứng từ', icon: 'bi-archive' },
      { den: '/admin/services', ten: 'Dịch vụ & giá', icon: 'bi-tags' },
      { den: '/admin/customers', ten: 'Khách hàng', icon: 'bi-buildings' },
      { den: '/admin/reports', ten: 'Báo cáo thống kê', icon: 'bi-graph-up-arrow' },
    ],
  },
  {
    ma: 'dieu-hanh',
    ten: 'Điều hành & AI',
    icon: 'bi-robot',
    muc: [
      { den: '/admin/ai', ten: 'Trung tâm AI', icon: 'bi-stars', nhan: 'AI' },
      { den: '/admin/agent', ten: 'Agent tự động', icon: 'bi-robot', nhan: 'ACT' },
      { den: '/admin/aitools/agent', ten: 'AI Agent', icon: 'bi-magic', nhan: 'AGENT' },
      { den: '/admin/ai-may-chu', ten: 'AI máy chủ (GPU)', icon: 'bi-cpu', nhan: 'RF-DETR' },
      { den: '/admin/ailab', ten: 'AI Lab dự báo', icon: 'bi-diagram-3', nhan: 'MLP' },
      { den: '/admin/aitools/ml', ten: 'Học máy', icon: 'bi-braces-asterisk', nhan: 'K-MEANS' },
      { den: '/admin/dashboard/quytrinh', ten: 'Quy trình vận hành', icon: 'bi-signpost-split', nhan: 'SOP' },
      { den: '/admin/dashboard/cangxanh', ten: 'Cảng xanh · ESG', icon: 'bi-tree', nhan: 'CO2' },
      { den: '/admin/dashboard/giatriai', ten: 'Giá trị AI', icon: 'bi-graph-up', nhan: 'ROI' },
    ],
  },
  {
    ma: 'he-thong',
    ten: 'Hệ thống',
    icon: 'bi-gear',
    muc: [
      { den: '/admin/microservices', ten: 'Giám sát microservices', icon: 'bi-diagram-3', nhan: 'MS', vaiTro: ['Admin'] },
      { den: '/admin/system', ten: 'Vận hành hệ thống', icon: 'bi-heart-pulse', nhan: 'HEALTH' },
      { den: '/admin/system/nhatky', ten: 'Nhật ký hoạt động', icon: 'bi-journal-text' },
      { den: '/admin/news', ten: 'Tin tức', icon: 'bi-newspaper' },
      { den: '/admin/gallery', ten: 'Thư viện ảnh', icon: 'bi-images' },
      { den: '/admin/contacts', ten: 'Hộp thư liên hệ', icon: 'bi-chat-left-text' },
      { den: '/admin/tichhop', ten: 'Tích hợp · API', icon: 'bi-plug', nhan: 'OPEN', vaiTro: ['Admin'] },
      { den: '/admin/accounts', ten: 'Tài khoản', icon: 'bi-people', vaiTro: ['Admin'] },
    ],
  },
]

// Danh so lien tuc 1, 2, 3... tu ngoai cong vao trong (Tong quan khong danh so: khong phai mot cong doan)
let soThuTu = 0
for (const nhom of NHOM_MENU) {
  if (!nhom.ten) continue
  for (const muc of nhom.muc) muc.so = ++soThuTu
}
