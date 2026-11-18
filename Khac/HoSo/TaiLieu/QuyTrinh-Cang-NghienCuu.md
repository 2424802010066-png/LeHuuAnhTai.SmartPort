# NGHIEN CUU QUY TRINH CANG CONTAINER VIET NAM
(Tong hop tu nguon cong khai: ePort SNP/Cat Lai, VASSCM, TOS thuong mai — 25/08/2026)



---

# PHAN 1 — QUY TRINH GIAO NHAN

# QUY TRÌNH GIAO NHẬN CONTAINER TẠI CẢNG CONTAINER VIỆT NAM
## (Khảo sát thực tế: Tân Cảng Cát Lái — hệ sinh thái ePort của Tổng công ty Tân Cảng Sài Gòn SNP)

---

## A. CÁC HỆ THỐNG THAM GIA (bức tranh tổng thể)

| Hệ thống | Chủ quản | Vai trò |
|---|---|---|
| **ePort** (eport.saigonnewport.com.vn) | SNP | Cổng dịch vụ trực tuyến: khai báo phương án nâng/hạ container, xác thực eDO, thanh toán phí, xuất hóa đơn điện tử, xuất EIR điện tử. Khai 1 lô chỉ ~2 phút |
| **App SNP ePort / ePort Driver** | SNP | App cho đơn vị vận tải & tài xế: gán chuyến, check-in online trước khi đến cảng, trình mã QR/EIR điện tử tại cổng, tra cứu tình trạng cont |
| **TOPX + TOPOVN** (TOS lõi, nền Oracle, thay CMS cũ từ ~2015) | SNP | TOPX = lập kế hoạch khai thác (bãi, tàu, thiết bị); TOPO = quản lý dữ liệu container tại cổng, kết nối khách hàng. Giảm thời gian xử lý/cont từ 2–3 giờ xuống ~50 phút |
| **eDO** (lệnh giao hàng điện tử, áp dụng tại Cát Lái từ 01/10/2018) | Hãng tàu (Maersk, MCC, Safmarine, KMTC...) | Thay D/O giấy; hãng tàu gửi eDO, chủ hàng nhập thẳng vào ePort, không phải đến quầy đổi lệnh |
| **VASSCM** (vận hành tại Cát Lái + Tân Thuận từ 24/8/2018) | Tổng cục Hải quan | Quản lý hải quan tự động: cảng gửi thông tin **Get In / Get Out**, hệ thống tự đối chiếu "container đủ điều kiện qua khu vực giám sát" — bỏ bước hải quan đóng dấu xác nhận tại cổng |
| **eWMS + Autogate Cổng C** (go-live 10/5/2024) | Tân Cảng Kho Vận | Cổng tự động cho hàng lẻ/kho CFS: bắt buộc đăng ký xe + tài xế trên eWMS, check-in online, chưa đăng ký không được làm hàng (hiệu lực 02/5/2024) |
| **Smart gate / camera OCR** | Cảng | Nhận dạng số container, biển số đầu kéo + rơ moóc, chụp ảnh các mặt vỏ cont, tự động mở barrier (mô hình đã chạy tại Tân Vũ – Hải Phòng: 10–25 giây/xe; SNP dùng check-in online + QR tương tự) |

---

## B. CHUẨN BỊ CHUNG TRƯỚC KHI XE ĐẾN CẢNG (cả 2 chiều)

1. **Mở tài khoản ePort** — *Chủ hàng/Forwarder/Đơn vị vận tải (ĐVVT)* gửi hồ sơ (MST, thông tin doanh nghiệp) cho Phòng thủ tục cảng; đăng nhập bằng MST.
2. **Đăng ký phương tiện & tài xế** — *ĐVVT/chủ xe* khai biển số đầu kéo, rơ moóc, GPLX, CCCD tài xế (app hỗ trợ quét OCR giấy tờ). Với Cổng C (hàng lẻ) bắt buộc đăng ký trên eWMS.
3. **Phân xe / gán chuyến** — *Điều độ ĐVVT* gán lệnh (lô cont) cho từng xe–tài xế trên ePort/app; tài xế thấy chuyến trong mục "My Trip".
4. **Check-in online** — *Tài xế* check-in trên app trước khi tới cổng → hệ thống chuẩn bị sẵn dữ liệu, giảm thời gian tại cổng.

---

## C. CHIỀU 1 — NHẬN CONTAINER HÀNG NHẬP (import pick-up)

| # | Bước | Ai làm | Hệ thống |
|---|---|---|---|
| 1 | Nhận **Thông báo hàng đến**, lấy **lệnh giao hàng**: eDO (hãng tàu gửi điện tử) hoặc D/O giấy với hãng chưa áp dụng eDO | Chủ hàng/FWD ↔ hãng tàu | Email/portal hãng tàu → ePort |
| 2 | **Khai báo hải quan nhập**, nộp thuế, chờ thông quan (luồng xanh/vàng/đỏ) | Nhân viên chứng từ | ECUS/VNACCS |
| 3 | **Khai ePort phương án "Nhận container hàng nhập"**: nhập số cont, số bill, hãng khai thác, hạn lệnh; xác thực eDO | Nhân viên chứng từ/OPS | ePort (đối chiếu dữ liệu TOPOVN) |
| 4 | **Thanh toán phí nâng cont, lưu bãi** online (ATM/chuyển khoản, OTP) → hóa đơn điện tử, lệnh có hiệu lực | Chủ hàng/FWD | ePort |
| 5 | **Kiểm tra điều kiện thông quan**: hệ thống cảng đối chiếu danh sách "đủ điều kiện qua khu vực giám sát" do hải quan đẩy sang; tờ khai luồng đỏ phải kiểm hóa xong | Tự động + hải quan giám sát | VASSCM ↔ TOPOVN |
| 6 | **Khai số xe, rơ moóc, tài xế** cho lô; gán chuyến cho tài xế | Điều độ ĐVVT | ePort / app Driver |
| 7 | **Xe đến cổng (gate-in)**: tài xế trình **mã QR / EIR điện tử** trên app; hệ thống/nhân viên cổng đối chiếu biển số–rơ moóc–tài xế với lệnh, kiểm tra phí đã đóng, điều kiện hải quan; ghi nhận Get In | Tài xế + nhân viên trực cổng (hoặc cổng tự động) | TOPO + VASSCM, camera OCR |
| 8 | **Nhận Phiếu hướng dẫn làm hàng / vị trí bãi** (in tại cổng hoặc hiển thị trên app): block–bay–row–tier của cont | Hệ thống cấp tự động | TOPX (kế hoạch bãi) |
| 9 | **Vào bãi làm hàng**: xe đậu đúng làn tại block; điều độ bãi điều phối; cẩu RTG/khung nâng lấy cont đặt lên rơ moóc | Tài xế, điều độ bãi, lái cẩu | TOPX điều thiết bị, xác nhận job trên thiết bị cầm tay |
| 10 | **Kiểm tra tại chỗ**: tài xế/checker đối chiếu số cont, số seal, tình trạng vỏ; hư hỏng ghi nhận vào EIR | Tài xế + nhân viên giao nhận bãi | TOPO/EIR |
| 11 | **Ra cổng (gate-out)**: đối chiếu lần cuối cont trên xe khớp lệnh; hệ thống ghi **Get Out** gửi hải quan; barrier mở — không còn phải trình hải quan đóng dấu tại cổng như quy trình giấy cũ | Tài xế + cổng/bảo vệ | TOPO + VASSCM |
| 12 | **Hoàn tất**: giao hàng về kho, sau đó **trả vỏ rỗng** về depot theo chỉ định hãng tàu (khai ePort phương án trả rỗng, đóng phí hạ rỗng) — vòng đời lệnh khép | Tài xế/ĐVVT | ePort depot |

---

## D. CHIỀU 2 — GIAO CONTAINER HÀNG XUẤT (export drop-off, hạ bãi chờ tàu)

| # | Bước | Ai làm | Hệ thống |
|---|---|---|---|
| 1 | Lấy **booking** từ hãng tàu; lấy **cont rỗng** tại depot (lệnh cấp rỗng), đóng hàng, bấm seal | Chủ hàng/FWD, tài xế | Portal hãng tàu, ePort depot |
| 2 | **Khai VGM** (khối lượng xác minh). Chưa có VGM thì đăng ký **cân tại trạm cân của cảng** | Chủ hàng | ePort/hãng tàu, trạm cân điện tử |
| 3 | **Khai ePort phương án "Giao container hàng xuất / hạ bãi chờ xuất"**: số booking, số cont, số seal, VGM, cảng đích — hệ thống đối chiếu booking hãng tàu đã truyền vào cảng | Nhân viên chứng từ | ePort ↔ TOPOVN (dữ liệu booking từ hãng tàu qua EDI) |
| 4 | **Thanh toán phí hạ bãi** online → có số đăng ký/phiếu mới được hạ cont | Chủ hàng/FWD | ePort |
| 5 | **Khai số xe, tài xế, gán chuyến; check-in online** | Điều độ ĐVVT, tài xế | ePort/app Driver |
| 6 | **Gate-in**: trình QR; cổng đối chiếu biển số/cont/seal với khai báo; **kiểm tra tình trạng vỏ cont, seal** (smart gate chụp ảnh các mặt vỏ làm bằng chứng); cấp vị trí hạ bãi theo cảng đích/tàu chuyến | Tài xế + checker cổng | TOPO, camera OCR, TOPX phân bãi |
| 7 | **Vào bãi**: đến đúng block, RTG nhấc cont hạ bãi; xác nhận hoàn tất job | Tài xế, lái cẩu, điều độ bãi | TOPX |
| 8 | **Gate-out xe rỗng**: xe ra cổng, hệ thống ghi nhận | Tài xế | TOPO |
| 9 | **Thông quan & thanh lý**: tờ khai xuất thông quan; luồng xanh thanh lý ngay trên ePort; hệ thống đối chiếu tự động cont đã hạ bãi + tờ khai đủ điều kiện | Chủ hàng + hải quan | VNACCS + VASSCM + ePort |
| 10 | **Vào sổ tàu (đăng ký tàu xuất)**: xác nhận cont lên danh sách xếp tàu trước giờ cắt máng (closing time); trễ = rớt tàu | Thương vụ hàng xuất/ePort | TOPX (kế hoạch xếp tàu) |
| 11 | **Hoàn tất**: cảng xếp cont lên tàu theo sơ đồ; chủ hàng nhận B/L từ hãng tàu | Cảng, hãng tàu | TOPX/CATOS hãng tàu |

---

## E. CHI TIẾT NGHIỆP VỤ TẠI CỔNG (điểm quan trọng cho thiết kế TOS)

- **Đối chiếu tại gate-in**: (1) lệnh hợp lệ + đã đóng phí (ePort), (2) biển số đầu kéo + rơ moóc + tài xế khớp khai báo, (3) số cont/seal khớp (chiều xuất), (4) điều kiện hải quan (VASSCM), (5) hạn lệnh/closing time còn hiệu lực. Khớp hết → in/hiển thị **EIR + vị trí bãi**, mở barrier.
- **EIR là "giấy thông hành" duy nhất** trong cảng; bản giấy truyền thống 4 liên (bãi giữ 1, các liên xanh/đỏ/vàng cho cảng–hãng tàu–tài xế ra cổng), nay thay dần bằng **EIR điện tử** tra được trên eport.saigonnewport.com.vn/Eir.
- **Cân xe**: không cân đại trà; cân phục vụ **VGM** (hàng xuất chưa khai VGM) và các trường hợp nghi vấn — trạm cân điện tử của cảng, tích hợp phí qua ePort.
- **Kiểm tra vỏ cont**: chiều xuất kiểm kỹ vỏ + seal tại cổng (tranh chấp trách nhiệm hư hỏng); smart gate chụp và lưu ảnh 4–6 mặt vỏ tự động.
- **Phân cổng ở Cát Lái**: cổng cho xe cont nguyên (FCL) và **Cổng C riêng cho hàng lẻ/kho CFS** (xe <15 tấn; ≥15 tấn đi cổng B) chạy Autogate/eWMS; thanh lý giám sát hải quan trước đây tập trung khu vực cổng B/G, nay tự động hóa qua VASSCM.

## F. GHI CHÚ
- Quy trình "giấy" cũ (đổi D/O 4 liên, đóng dấu hải quan tại cổng, in EIR giấy tại thương vụ) vẫn xuất hiện trong nhiều tài liệu hướng dẫn — thực tế từ 2018–2024 đã được thay thế dần bằng chuỗi eDO → ePort → VASSCM → EIR điện tử/QR; TOS mới nên mô hình hóa cả 2 chế độ.
- Mô hình tham chiếu ngoài SNP: Smart gate cảng Tân Vũ (Hải Phòng) — app Container Driver nhận "phiếu vị trí", bảng LED tại cổng, barrier tự động, 10–25 giây/xe.

## NGUỒN
- ePort SNP — hướng dẫn đăng ký giao nhận container & thanh toán: https://eport.saigonnewport.com.vn/ và https://eport.saigonnewport.com.vn/Images/ePort_Huong_dan_dang_ky_giao_nhan_container_v12024.pdf ; tra cứu EIR: https://eport.saigonnewport.com.vn/Eir
- eDO tại Cát Lái (Maersk/MCC): https://itslogisticsvn.blog/huong-dan-ve-edo-lenh-giao-hang-dien-tu-cua-maersk-mcc-tai-cang-cat-lai/ ; https://xuatnhapkhauleanh.edu.vn/lenh-giao-hang-dien-tu-edo.html
- Khai ePort giao cont hàng xuất: https://hocxuatnhapkhau.vn/huong-dan-khai-bao-eport-giao-cont-hang-cho-cang-cat-lai/ ; hạ cont chờ xuất: https://camnangxnk-logistics.net/quy-trinh-dang-ky-ha-container-cho-xuat-khau-tren-eport/
- Quy trình nhập tại Cát Lái: https://vinalogs.com/tham-khao/quy-trinh-thu-tuc-nhap-khau-tai-cang-cat-lai/ ; https://ptnlogistics.com/cang-cat-lai/
- TOPX/TOPOVN: https://tapchicongthuong.vn/ap-dung-phan-mem-topovn-tai-cang-tan-cang-cat-lai-39866.htm ; https://saigonnewport.com.vn/tin-tuc/hoat-dong-kinh-doanh/tan-cang-sai-gon-tien-phong-ung-dung-phan-mem-quan-ly-container-topovn.html
- VASSCM tại Cát Lái/Tân Thuận: https://dantocmiennui.baotintuc.vn/thanh-pho-ho-chi-minh-trien-khai-he-thong-quan-ly-hai-quan-tu-dong-post179500.html ; https://vimc.co/en/dua-he-thong-quan-ly-hai-quan-tu-dong-tai-cang-bien-vao-hoat-dong-giam-thoi-gian-thu-tuc-va-chi-phi/
- App ePort Driver + check-in online: https://apps.apple.com/vn/app/eport-driver/id1519436272?l=vi ; https://saigonnewport.com.vn/en/article/operation-news/saigon-newport-corporationb-organizes-activities-guiding-the-installation-and-use-of-the-snp-eport-app-to-support-drivers-using-the-online-check-in-feature.html ; https://damvietxnk.weebly.com/blog/huong-dan-su-dung-app-snp-eport-cua-tan-cang-sai-gon
- Autogate Cổng C + eWMS (bắt buộc từ 5/2024): https://tthqsaigon.net/dang-ky-thong-tin-phuong-tien-tai-xe-qua-cong-c/ ; https://thitruongtaichinhtiente.vn/kho-van-tan-cang-chinh-thuc-van-hanh-he-thong-cong-tu-dong-tai-cong-c-cang-tan-cang-cat-lai-58884.html ; https://thuvienxuatnhapkhau.com/huong-dan-ve-cong-auto-gate-tai-cong-c-cang-cat-lai-thao-tac-tren-ewms.html
- Smart gate Tân Vũ (Hải Phòng, tham chiếu): https://haiphongport.com.vn/vi/tin-tuc-828/he-thong-giao-nhan-cong-tu-dong-smart-gate-tai-chi-nhanh-cang-tan-vu-cang-hai-phong-1671.html
- Phiếu EIR: https://vinatrain.edu.vn/phieu-eir/ ; https://tanthanhcontainer.com/en/explore/news/phieu-giao-nhan-container-eir-la-gi

---

# PHAN 2 — CHUNG TU GIAY TO

# BÁO CÁO: CHỨNG TỪ/GIẤY TỜ TRONG GIAO NHẬN CONTAINER TẠI CẢNG VIỆT NAM

## A. CĂN CỨ PHÁP LÝ VỀ LƯU TRỮ (áp dụng chung, ghi tắt ở phần B)

1. **Hồ sơ hải quan — 5 năm**: Điều 18 khoản 2(đ) Luật Hải quan 2014: người khai hải quan lưu hồ sơ hải quan (bản chính hoặc bản chụp/bản điện tử) trong **5 năm kể từ ngày đăng ký tờ khai**; chi tiết danh mục chứng từ phải lưu tại Điều 16a Thông tư 38/2015/TT-BTC (bổ sung bởi Thông tư 39/2018/TT-BTC). Nếu khai qua đại lý hải quan thì **chủ hàng** vẫn là người chịu trách nhiệm lưu.
2. **Chứng từ kế toán — 5 năm / 10 năm / vĩnh viễn**: Điều 41 Luật Kế toán 2015 + Điều 12–14 Nghị định 174/2016/NĐ-CP:
   - Tối thiểu **5 năm**: chứng từ không dùng trực tiếp để ghi sổ (phiếu thu/chi, phiếu xuất nhập kho, tài liệu quản lý điều hành).
   - Tối thiểu **10 năm**: chứng từ dùng trực tiếp ghi sổ và lập BCTC (hóa đơn, bảng kê, sổ kế toán, báo cáo quyết toán). Hóa đơn điện tử cũng theo mốc 10 năm này (Nghị định 123/2020/NĐ-CP dẫn chiếu pháp luật kế toán).
   - **Vĩnh viễn**: tài liệu có ý nghĩa quan trọng về kinh tế, quốc phòng, an ninh.
3. **Chứng từ tác nghiệp khai thác cảng** (EIR, phiếu cân, lệnh nâng hạ, phiếu xuất/nhập bãi): pháp luật **không ấn định thời hạn riêng** — cảng lưu theo quy chế nội bộ (thực tế phổ biến 1–5 năm); nhưng (a) nếu là căn cứ thu tiền/xuất hóa đơn thì kéo theo chuẩn kế toán 10 năm, (b) nếu phục vụ khiếu nại hư hỏng hàng hóa thì lưu tối thiểu theo **thời hiệu khởi kiện 1 năm** về mất mát/hư hỏng hàng hóa vận chuyển đường biển (Bộ luật Hàng hải VN 2015, Điều 169).
→ **Gợi ý cho TOS**: mỗi loại chứng từ nên có thuộc tính "retention class" = {HQ-5y, KT-5y, KT-10y, Nội bộ-định cấu hình}, hỗ trợ lưu trữ điện tử (Luật Kế toán cho phép lưu trên phương tiện điện tử).

## B. DANH MỤC CHỨNG TỪ THEO LUỒNG NGHIỆP VỤ

### Nhóm 1 — Chứng từ hãng tàu / đặt chỗ (trước khi vào cảng)

1. **Booking note / Booking confirmation — Giấy lưu khoang / Xác nhận đặt chỗ**
   - Phát hành: hãng tàu hoặc đại lý (qua forwarder). Dùng: bước đầu hàng xuất — chủ hàng đặt chỗ, làm căn cứ lấy vỏ container và hạ bãi chờ xuất (trên booking có số booking, cảng hạ bãi, closing time). Lưu: không thuộc hồ sơ hải quan bắt buộc; lưu nội bộ theo bộ hồ sơ lô hàng (khuyến nghị đồng bộ 5 năm).
2. **Lệnh cấp container rỗng — Empty container release order**
   - Phát hành: hãng tàu (hoặc in từ booking). Dùng: chủ hàng xuất trình tại depot/bãi rỗng để mượn vỏ đóng hàng. Lưu: nội bộ.
3. **Giấy báo hàng đến — Arrival Notice (A/N)**
   - Phát hành: hãng tàu/đại lý gửi consignee trước khi tàu cập. Dùng: căn cứ đi đổi lệnh D/O, chuẩn bị thủ tục nhập. Lưu: nội bộ.
4. **Vận đơn — Bill of Lading (B/L) / Seaway Bill**
   - Phát hành: hãng tàu (master B/L) hoặc forwarder (house B/L) sau khi hàng lên tàu. Dùng: hàng nhập — xuất trình B/L gốc (hoặc telex release/surrendered) để đổi lấy D/O. Lưu: **thuộc hồ sơ hải quan → 5 năm** (Điều 16a TT 38/2015); nếu ghi sổ kế toán (chi phí vận tải) → 10 năm.
5. **Lệnh giao hàng — Delivery Order (D/O)**
   - Phát hành: hãng tàu/đại lý (D/O hãng tàu) và forwarder (D/O nhà) sau khi consignee nộp B/L + phí D/O, phí THC... Dùng: xuất trình tại thương vụ cảng để làm thủ tục nhận container (kèm giấy giới thiệu, CMND/CCCD tài xế). Lưu: cảng giữ 1 liên làm chứng từ giao hàng; doanh nghiệp lưu theo bộ hồ sơ lô hàng (khuyến nghị 5 năm).
6. **Lệnh giao hàng điện tử — eDO (electronic Delivery Order)**
   - Phát hành: hãng tàu qua kênh điện tử (email PDF hoặc đẩy thẳng dữ liệu vào hệ thống cảng). Dùng: thay D/O giấy; tại Việt Nam Tân Cảng Sài Gòn triển khai qua **ePort từ 01/10/2018**, các hãng đi đầu là Maersk, MCC, Safmarine; nay phổ biến ở hầu hết cảng lớn (ePort SNP, Cảng Hải Phòng, VICT...). Doanh nghiệp khai số eDO trên cổng, không phải đến quầy. Lưu: bản điện tử — TOS phải lưu log/đối chiếu; giá trị pháp lý theo Luật Giao dịch điện tử.
7. **Giấy mượn container / Giấy cược container — Container deposit slip**
   - Phát hành: hãng tàu khi consignee kéo nguyên container về kho riêng rút hàng. Dùng: cam kết trả vỏ đúng hạn, đúng tình trạng; hoàn cược khi trả vỏ (kèm EIR trả rỗng). Lưu: đến khi quyết toán cược; biên lai thu/hoàn cược là chứng từ kế toán (5–10 năm).

### Nhóm 2 — Chứng từ do chủ hàng lập

8. **Phiếu đóng gói — Packing List (P/L)**
   - Lập: người xuất khẩu/shipper. Dùng: kèm bộ chứng từ, đối chiếu khi đóng/rút hàng, kiểm hóa. Lưu: **thuộc hồ sơ hải quan (khi phải nộp/xuất trình) → 5 năm**.
9. **Hóa đơn thương mại — Commercial Invoice**
   - Lập: người bán. Dùng: khai trị giá hải quan, thanh toán. Lưu: **hồ sơ hải quan 5 năm**; đồng thời là chứng từ ghi sổ kế toán → **10 năm**.
10. **Phiếu xác nhận khối lượng toàn bộ container — VGM (Verified Gross Mass)**
    - Lập/ký: **shipper chịu trách nhiệm khai**, có thể dựa trên phiếu cân của cảng. Cơ sở: Công ước SOLAS (Chương VI/2), hiệu lực 01/7/2016; tại VN hướng dẫn bằng **Công văn 2428/CHHVN-VTDVHH của Cục Hàng hải Việt Nam** (kèm mẫu VGM). Dùng: nộp cho cảng/hãng tàu **trước giờ cắt máng (cut-off VGM)** — không có VGM container không được xếp lên tàu. Lưu: SOLAS/VN không ấn định thời hạn lưu riêng; thực tiễn lưu cùng bộ hồ sơ lô hàng và trên hệ thống cảng (khuyến nghị cấu hình ≥ 1 năm theo thời hiệu khiếu nại, đồng bộ 5 năm với hồ sơ lô hàng).
11. **Giấy giới thiệu — Letter of introduction**
    - Lập: doanh nghiệp chủ hàng cấp cho nhân viên/tài xế. Dùng: khi đổi D/O tại hãng tàu và làm thủ tục tại thương vụ cảng. Lưu: nội bộ, ngắn hạn.

### Nhóm 3 — Chứng từ do cảng/terminal phát hành (lõi của TOS)

12. **Phiếu giao nhận container — EIR (Equipment Interchange Receipt)**
    - Phát hành: bộ phận thủ tục/cổng của cảng, depot (in từ TOS). Dùng: tại **gate in / gate out** — là biên bản xác nhận chuyển giao container giữa cảng và người vận tải, ghi số cont, số seal, **tình trạng vỏ container** (móp, thủng, sạch/bẩn), đồng thời là bằng chứng đã đóng phí nâng hạ. EIR giấy in 2 liên (cảng giữ 1, tài xế giữ 1); nhiều cảng đã chuyển **eEIR** trên app/ePort. Đây là chứng từ phân định trách nhiệm hư hỏng vỏ cont → rất quan trọng khi hãng tàu đòi phí sửa chữa. Lưu: theo quy chế cảng (thường 2–5 năm); tối thiểu qua thời hiệu khiếu nại 1 năm.
13. **Lệnh nâng / hạ container — Lift-On/Lift-Off order (LOLO)**
    - Phát hành: thương vụ cảng sau khi khách đóng phí nâng (hàng nhập: nâng cont lên xe khách) hoặc phí hạ (hàng xuất: hạ cont từ xe xuống bãi chờ xuất). Nhiều cảng gộp chức năng này vào EIR/phiếu tác nghiệp; trên ePort là "đăng ký nâng/hạ" điện tử. Lưu: nội bộ; biên lai thu phí kèm theo là chứng từ kế toán.
14. **Phiếu xuất bãi / nhập bãi — Yard out/in slip, Gate pass**
    - Phát hành: TOS của cảng khi container được phép ra/vào bãi. Dùng: chốt kiểm soát cổng, đối chiếu với giám sát hải quan. Lưu: nội bộ (dữ liệu TOS).
15. **Phiếu cân — Weighbridge ticket / Weighing slip**
    - Phát hành: trạm cân của cảng/ICD. Dùng: (a) làm cơ sở khai VGM (phương pháp cân cả cont), (b) kiểm soát tải trọng xe ra đường bộ (giới hạn theo Thông tư 46/2015/TT-BGTVT), (c) tính phí theo trọng lượng. Lưu: nội bộ + kèm hồ sơ VGM.
16. **Biên bản bất thường / hư hỏng — COR (Cargo Outturn Report), Biên bản giám định hư hỏng container**
    - Lập: cảng cùng đại diện hãng tàu/chủ hàng khi phát hiện container/hàng hư hỏng, sai seal, móp méo lúc dỡ tàu hoặc tại cổng. Dùng: căn cứ khiếu nại bảo hiểm, phân trách nhiệm. Lưu: tối thiểu hết thời hiệu khởi kiện 1 năm (BLHH 2015 Điều 169); khuyến nghị 5 năm.
17. **Hóa đơn, biên lai thu phí — Invoice/Receipt (phí nâng hạ, lưu bãi, cân, vệ sinh cont, điện lạnh...)**
    - Phát hành: cảng (hóa đơn điện tử theo NĐ 123/2020). Dùng: điều kiện để in EIR/lệnh nâng hạ. Lưu: **chứng từ kế toán 10 năm** (cả phía cảng và phía khách hàng).

### Nhóm 4 — Chứng từ hải quan tại khu vực cảng

18. **Tờ khai hải quan — Customs declaration** (khai trên VNACCS/VCIS)
    - Phát hành: người khai lập, hệ thống hải quan cấp số. Dùng: thông quan trước khi container được xếp tàu (xuất) hoặc ra cổng (nhập). Lưu: **5 năm** (Điều 18 Luật HQ 2014).
19. **Danh sách container đủ điều kiện qua khu vực giám sát (mã vạch) — Container list with barcode**
    - Phát hành: in từ hệ thống hải quan (theo TT 39/2018). Dùng: xuất trình tại cổng cảng cùng D/O/EIR để hải quan giám sát xác nhận container qua khu vực giám sát; các cảng lớn đã liên thông dữ liệu tự động (VASSCM) không cần bản giấy. Lưu: thuộc bộ hồ sơ hải quan → 5 năm.
20. **Giấy phép chuyên ngành, C/O, giấy kiểm tra chuyên ngành** (nếu lô hàng thuộc diện)
    - Phát hành: cơ quan quản lý chuyên ngành / tổ chức cấp C/O. Lưu: **hồ sơ hải quan 5 năm**.

### Nhóm 5 — Vận tải đường bộ nội địa

21. **Lệnh điều xe / Giấy vận chuyển — Transport order / Trucking order**: nhà xe phát hành cho tài xế; đối chiếu tại cổng cảng cùng EIR. Lưu: nội bộ.

## C. TÓM TẮT LUỒNG SỬ DỤNG (để map vào TOS)

- **Hàng xuất**: Booking → lệnh cấp rỗng + EIR lấy rỗng tại depot → đóng hàng → phiếu cân/VGM → tờ khai HQ thông quan → hạ bãi (lệnh hạ + EIR hạ bãi + phiếu nhập bãi) → mã vạch giám sát → xếp tàu → B/L.
- **Hàng nhập**: A/N → đổi B/L lấy D/O (hoặc eDO) → tờ khai HQ thông quan → đóng phí nâng (hóa đơn) → lệnh nâng + EIR giao cont + phiếu xuất bãi + mã vạch giám sát → kéo về kho (giấy cược cont) → trả rỗng (EIR trả rỗng, hoàn cược).

## Nguồn tham khảo
- [Điều 18 Luật Hải quan 2014](https://hethongphapluat.com/luat-hai-quan-2014/dieu-18) · [Lưu giữ hồ sơ hải quan XNK — Thư viện Pháp luật](https://thuvienphapluat.vn/phap-luat-doanh-nghiep/cong-viec-phap-ly/luu-giu-ho-so-hai-quan-doi-voi-hang-hoa-xuat-khau-nhap-khau-229.html) · [Điều 16a TT 38/2015 (bổ sung TT 39/2018)](https://camnangxnk-logistics.net/luu-giu-ho-so-hai-quan-doi-voi-hang-hoa-xuat-khau-nhap-khau-dieu-16a-luat-hai-quan/)
- [Thời hạn lưu trữ chứng từ kế toán theo NĐ 174/2016/NĐ-CP — Cổng hỏi đáp Bộ Tài chính](https://portal.mof.gov.vn/hoidapcstc/home/cthoidap/162984) · [Tổng hợp quy định lưu trữ chứng từ kế toán](https://ifa.com.vn/vi/thoi-han-luu-tru-chung-tu-ke-toan)
- [Phiếu EIR là gì — VinaTrain](https://vinatrain.edu.vn/phieu-eir/) · [Chức năng phiếu EIR — Proship](https://proship.vn/news/phieu-eir-la-gi/)
- [VGM và Công văn 2428/CHHVN-VTDVHH — GOL](https://gol.vn/news/vgm-la-gi/) · [Mẫu VGM, cách tính — VinaTrain](https://vinatrain.edu.vn/mau-vgm-cach-tinh-vgm/)
- [Lệnh giao hàng điện tử eDO, triển khai ePort Tân Cảng 01/10/2018](https://xuatnhapkhauleanh.edu.vn/lenh-giao-hang-dien-tu-edo.html) · [eDO — Lê Nguyễn Logistics](https://lenguyentst.com.vn/lenh-giao-hang-dien-tu-edo/)

**Lưu ý độ tin cậy**: các mốc lưu trữ 5 năm (hải quan) và 5/10 năm/vĩnh viễn (kế toán) là quy định luật đã kiểm chứng; thời hạn lưu chứng từ tác nghiệp cảng (EIR, phiếu cân, lệnh nâng hạ) không có quy định thống nhất toàn quốc — khi thiết kế TOS nên để cấu hình được theo quy chế từng cảng.

---

# PHAN 3 — MODULE TOS THUONG MAI

# BÁO CÁO: Các phân hệ (module) chuẩn của phần mềm quản lý cảng container (TOS)

## 1. Bức tranh TOS thực tế tại cảng Việt Nam
- **Navis N4** (Kalmar/Navis, Mỹ): TOS phổ biến nhất thế giới, dùng tại nhiều cảng nước sâu (khu vực Cái Mép có các terminal liên doanh quốc tế dùng N4).
- **CATOS** (Total Soft Bank, Hàn Quốc): dùng tại Cảng Đà Nẵng (bản 7.7, go-live 19/04/2020), Cảng Sài Gòn (Tân Thuận, từ 2006 đến khi thay bằng SGP-TOS năm 2023), Cảng Hải Phòng.
- **TOPS (TOPX + TOPO/TOPOVN)** (RBS Úc + TCIS): Tân Cảng Sài Gòn triển khai TOPX tại Cát Lái từ 22/06/2008; TOPOVN là phân hệ quản lý dữ liệu container do RBS + TCIS phát triển riêng cho SNP (go-live 20/02/2015, kiến trúc 3 lớp .NET, CSDL Oracle, đầu tư 1,1 triệu USD; giảm thời gian xử lý container từ 2-3 giờ xuống ~50 phút).
- **TOS nội địa Việt Nam**: VTOS/SGP-TOS (CEH — go-live tại Cảng Tân Thuận 05/10/2023, tích hợp nền tảng VSL), Smart CTOS (VNPT-Intecom), V-TOS (Sao Bắc Đẩu).

## 2. Bộ phân hệ chuẩn của một TOS doanh nghiệp lớn

**(1) Kế hoạch tàu & cầu bến (Vessel/Berth Planning)**
- Đăng ký chuyến tàu, lịch tàu, phân bổ cầu bến, dự báo ETA.
- Sơ đồ xếp hàng tàu (stowage/bayplan), phân ca máng, lịch cẩu bờ; N4 có Vessel Autostow, CATOS có Auto Ship Planning tự phân lịch cẩu và kế hoạch xếp/dỡ.

**(2) Kế hoạch & quản lý bãi (Yard Planning/Yard Management)**
- Quy hoạch block/bay/row/tier, chiến lược hạ bãi theo hướng đi, hãng tàu, loại container.
- Theo dõi vị trí container thời gian thực, đảo chuyển (remarshalling/rehandling), điều phối RTG và xe đầu kéo nội bộ; N4 có Expert Decking.

**(3) Điều hành cổng (Gate Operations)**
- Đăng ký lệnh trước (Pre-Gate/appointment), cấp phiếu EIR ra/vào cổng.
- Nhận dạng số container và biển số xe (OCR/camera), barrier tự động, kiosk tự phục vụ — Cảng Hải Phòng (Tân Vũ) đã vận hành cổng tự động kiểm tra container kết nối TOS (~10 tỷ đồng).
- Trạm cân: điều hành cân, giám sát trọng tải, phục vụ khai báo VGM (SOLAS).

**(4) Điều độ thiết bị & tác nghiệp thời gian thực (Equipment Control/Operations)**
- Phát lệnh công việc xuống thiết bị (QC, RTG, xe nâng, đầu kéo) qua thiết bị cầm tay/màn hình trên cabin.
- Giám sát điều hành trên giao diện đồ họa; tối ưu hành trình thiết bị (N4 PrimeRoute); CATOS Operation giám sát toàn bộ bãi, kho, cổng theo thời gian thực.

**(5) Quản lý container & hàng hóa (Inventory/Container Control)**
- Danh mục container nhập/xuất/chuyển cảng/chuyển khẩu, hàng nguy hiểm (IMDG), hàng quá khổ.
- Giám sát container lạnh (cắm điện, nhiệt độ — N4 có app reefer monitoring), dịch vụ đóng/rút hàng (stuffing/unstuffing), vệ sinh - sửa chữa (M&R).

**(6) Thương vụ - Tính cước - Hóa đơn (Commercial/Billing)**
- Quản lý khách hàng, hợp đồng, biểu cước (tariff); tự động ghi nhận tác nghiệp phát sinh phí (lưu bãi, nâng hạ, cắm điện...).
- Phát hành hóa đơn điện tử, theo dõi thanh toán, kết nối phần mềm kế toán (yêu cầu bắt buộc ở VN).

**(7) EDI (Trao đổi dữ liệu điện tử)**
- Gửi/nhận tự động theo chuẩn UN/EDIFACT với hãng tàu: BAPLIE (sơ đồ tàu), COPRAR/COPARN (lệnh xếp dỡ/cấp rỗng), CODECO (xác nhận qua cổng), COARRI (xác nhận xếp/dỡ tàu).
- Là "mạch máu" nối TOS với hãng tàu, depot, đại lý.

**(8) Kết nối Hải quan & cảng vụ**
- Trao đổi dữ liệu với hải quan điện tử (ở VN: VNACCS/VCIS, giám sát hải quan tự động), thanh lý tờ khai; Smart CTOS có phân hệ "Quản lý Kết nối Hải quan" riêng.

**(9) Báo cáo - Thống kê - BI (Reporting)**
- Sản lượng thông qua (TEU), năng suất xếp dỡ (moves/giờ), thời gian tàu nằm bến, hiệu suất thiết bị; xuất XML/CSV/PDF; CATOS Management gom billing + reporting + analytics.

**(10) Cổng thông tin khách hàng / ePort (Customer Portal)**
- Tra cứu container, lịch tàu, tiến độ làm hàng; đăng ký dịch vụ và thanh toán trực tuyến, lệnh giao hàng điện tử eDO — mô hình ePort của Tân Cảng, nền tảng VSL của CEH (kết nối cảng - hãng tàu - doanh nghiệp XNK - ngân hàng).

**(11) Quản trị hệ thống (System Admin)**
- Người dùng, phân quyền, nhật ký, sao lưu, tham số hệ thống.

*(Phân hệ mở rộng tùy cảng: kế hoạch đường sắt/sà lan — CATOS Rail Planning, N4 Rail Autostow; quản lý hàng tổng hợp, nhiên liệu, nhân sự như Cảng Đà Nẵng bổ sung quanh CATOS.)*

## 3. Cách các TOS thực tế đóng gói phân hệ
- **CATOS**: 3 khối — **Planning** (Berth/Ship/Yard/Rail/Gate/Remarshalling Planning), **Operation** (giám sát - điều khiển thời gian thực), **Management** (billing, báo cáo, phân tích).
- **Navis N4**: nền tảng lõi (vessel - yard - gate - billing - EDI) + module tối ưu hóa: Expert Decking, PrimeRoute, Vessel Autostow, Rail Autostow, app reefer/lashing, multi-terminal.
- **TOPS (Tân Cảng)**: 2 phân hệ chính — **TOPX** (kế hoạch điều hành sản xuất: tàu, bãi, thiết bị) + **TOPO → TOPOVN** (quản lý dữ liệu container, thương vụ).
- **Smart CTOS (VNPT-Intecom)** — 13 phân hệ tiếng Việt nguyên văn: Điều hành khai thác; Quản lý cầu bến; Quản lý tàu/Sà lan; Giám sát điều khiển; Quản lý điều hành cổng; Quản lý khai thác trạm cân; Quản lý tài nguyên (kế hoạch xếp dỡ theo ca máng); Quản lý Kinh doanh; Quản lý Tính cước; Quản lý EDI; Web Access; Quản lý Kết nối Hải quan; Quản trị hệ thống.
- **SGP-TOS (CEH)**: bộ VTOS - GTOS (hàng tổng hợp) - VSL (cổng dịch vụ trực tuyến), thay CATOS tại Tân Thuận, Hiệp Phước.

## 4. Gợi ý cho dự án TOS Việt Nam
1. Bám khung 3 lớp kiểu CATOS: **Kế hoạch → Tác nghiệp thời gian thực → Quản lý/thương vụ**.
2. Bắt buộc có cho thị trường VN: hóa đơn điện tử, kết nối hải quan VNACCS, khai báo VGM, ePort/thanh toán online — đây là điểm các TOS nội (Smart CTOS, VTOS) nhấn mạnh hơn TOS ngoại.
3. EDI chuẩn EDIFACT (BAPLIE, CODECO, COARRI, COPRAR) là điều kiện làm việc với hãng tàu quốc tế.

## Nguồn
- AltexSoft — Terminal Operating Systems: https://www.altexsoft.com/blog/terminal-operating-system/
- Coaxsoft — TOS modules & providers: https://coaxsoft.com/blog/understanding-terminal-operating-systems
- VNPT-Intecom — Smart CTOS: https://intecom.vn/phan-mem-khai-thac-cang-container-smart-ctos.html
- Saigon Newport — TOPOVN: https://saigonnewport.com.vn/tin-tuc/hoat-dong-kinh-doanh/tan-cang-sai-gon-tien-phong-ung-dung-phan-mem-quan-ly-container-topovn.html
- Tạp chí Công Thương — TOPOVN tại Cát Lái: https://tapchicongthuong.vn/ap-dung-phan-mem-topovn-tai-cang-tan-cang-cat-lai-39866.htm
- Nhân Dân — TOPOVN: https://nhandan.vn/ung-dung-he-thong-phan-mem-quan-ly-cang-container-topovn-post225784.html
- Cảng Đà Nẵng — CATOS 7.7: https://danangport.com/doi-moi-he-thong-quan-ly-khai-thac-cang-container/?lang=en
- Cảng Sài Gòn — SGP-TOS go-live: https://saigonport.vn/cang-tan-thuan-chinh-thuc-go-live-ung-dung-sgptos-danh-dau-su-thanh-cong-tren-hanh-trinh-chuyen-doi-so-cua-cong-ty-co-phan-cang-sai-gon/
- TP. Hải Phòng — TOS Chùa Vẽ + cổng tự động Tân Vũ: https://thanhphohaiphong.gov.vn/cang-hai-phong-trien-khai-he-thong-quan-ly-container-tos-tai-chi-nhanh-chua-ve-va-van-hanh-he-thong-tu-dong-kiem-tra-container-xuat-nhap-tai-chi-nhanh-tan-vu.html
- Sao Bắc Đẩu — V-TOS: https://saobacdau.vn/en-US/terminal-operating-system-solution-v-tos
- Total Soft Bank — CATOS: https://www.tsb.co.kr/CATOS (tham chiếu qua ResearchGate/EC21 do trang gốc chặn truy cập)