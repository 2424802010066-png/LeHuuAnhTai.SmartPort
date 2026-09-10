using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Controllers
{
    public class AccountController : AppController
    {
        private readonly AppDb db = new AppDb();

        /* ---------- DANG NHAP ---------- */

        /* Chong do mat khau (brute-force): 5 lan sai lien tiep theo
           (ten dang nhap + IP) -> khoa thu lai 60 giay. Bo nho tinh
           trong process — du dung cho quy mo demo/depot nho. */
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, LoginFail> loginFails
            = new System.Collections.Concurrent.ConcurrentDictionary<string, LoginFail>();
        private class LoginFail { public int SoLan; public DateTime KhoaDen; }
        private const int MAX_FAIL = 5;
        private const int LOCK_SECONDS = 60;

        public ActionResult Login(string returnUrl)
        {
            if (Session["MaTK"] != null) return RedirectToAction("Index", "Home");
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginVM());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginVM model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var username = model.TenDangNhap.Trim();
            var failKey = username.ToLowerInvariant() + "|" + (HttpContext.UserHostAddress() ?? "?");

            LoginFail fail;
            if (loginFails.TryGetValue(failKey, out fail) && fail.KhoaDen > DateTime.Now)
            {
                var conLai = (int)Math.Ceiling((fail.KhoaDen - DateTime.Now).TotalSeconds);
                ModelState.AddModelError("", "Nhập sai quá " + MAX_FAIL + " lần — vui lòng thử lại sau " + conLai + " giây.");
                return View(model);
            }

            var user = db.TaiKhoans.FirstOrDefault(t => t.TenDangNhap == username);
            if (user == null || !Security.VerifyPassword(username, model.MatKhau, user.MatKhauHash))
            {
                var f = loginFails.AddOrUpdate(failKey,
                    k => new LoginFail { SoLan = 1 },
                    (k, cu) => { cu.SoLan++; if (cu.SoLan >= MAX_FAIL) { cu.KhoaDen = DateTime.Now.AddSeconds(LOCK_SECONDS); cu.SoLan = 0; } return cu; });
                if (f.KhoaDen > DateTime.Now)
                    NhatKyHelper.Ghi("BaoMat", username, "Khóa đăng nhập " + LOCK_SECONDS + "s do nhập sai " + MAX_FAIL + " lần liên tiếp", HttpContext);
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }
            if (!user.KichHoat)
            {
                ModelState.AddModelError("", "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị.");
                return View(model);
            }

            // Dang nhap OK: xoa dem sai + nang cap hash cu SHA256 -> PBKDF2
            LoginFail bo; loginFails.TryRemove(failKey, out bo);
            if (Security.NeedsUpgrade(user.MatKhauHash))
            {
                try { user.MatKhauHash = Security.HashPasswordStrong(model.MatKhau); db.SaveChanges(); }
                catch { /* nang cap that bai van cho dang nhap */ }
            }

            SetSession(user);
            KiemTraMatKhauMacDinh(model.MatKhau);
            NhatKyHelper.Ghi("DangNhap", user.TenDangNhap, "Đăng nhập thành công (" + user.VaiTro + ")", HttpContext);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            if (user.VaiTro == "Admin" || user.VaiTro == "NhanVien")
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            return RedirectToAction("Index", "Home");
        }

        private void SetSession(TaiKhoan user)
        {
            Session["MaTK"] = user.MaTK;
            // BAT BUOC: AppController.TenNguoiDung doc dung khoa nay. Thieu no
            // thi MOI truong "nguoi lap / nguoi xu ly" trong he thong deu RONG
            // — ma do la thu ban dang ban ("truy vet duoc tung thao tac").
            Session["TenDangNhap"] = user.TenDangNhap;
            Session["HoTen"] = user.HoTen;
            Session["VaiTro"] = user.VaiTro;
            Session["MaKH"] = user.MaKH;
        }

        /// <summary>He thong VAN HANH THAT: ai con dung mat khau mac dinh se
        /// thay banner nhac doi ngay (gan co o buoc xac thuc thanh cong)</summary>
        private void KiemTraMatKhauMacDinh(string matKhau)
        {
            if (matKhau == "123456") Session["MkMacDinh"] = 1;
            else Session["MkMacDinh"] = null;
        }

        public ActionResult Logout()
        {
            // Huy han phien + vo hieu cookie phien cu (an toan tren may dung chung)
            Session.Clear();
            Session.Abandon();
            Response.Cookies.Delete(".SmartPort.Phien");
            return RedirectToAction("Index", "Home");
        }

        /* ---------- DANG KY (khach hang) ---------- */

        public ActionResult Register()
        {
            if (Session["MaTK"] != null) return RedirectToAction("Index", "Home");
            return View(new RegisterVM());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var username = model.TenDangNhap.Trim();
            if (db.TaiKhoans.Any(t => t.TenDangNhap == username))
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại, vui lòng chọn tên khác.");
                return View(model);
            }

            var kh = new KhachHang
            {
                TenCongTy = model.TenCongTy.Trim(),
                MaSoThue = model.MaSoThue,
                DiaChi = model.DiaChi,
                NguoiLienHe = model.HoTen.Trim(),
                Email = model.Email,
                SoDienThoai = model.SoDienThoai,
                NgayTao = DateTime.Now
            };
            db.KhachHangs.Add(kh);
            db.SaveChanges();

            var tk = new TaiKhoan
            {
                TenDangNhap = username,
                MatKhauHash = Security.HashPasswordStrong(model.MatKhau),
                HoTen = model.HoTen.Trim(),
                Email = model.Email,
                SoDienThoai = model.SoDienThoai,
                VaiTro = "KhachHang",
                MaKH = kh.MaKH,
                KichHoat = true,
                NgayTao = DateTime.Now
            };
            db.TaiKhoans.Add(tk);
            db.SaveChanges();

            SetSession(tk);
            NhatKyHelper.Ghi("ThaoTac", tk.TenDangNhap, "Đăng ký tài khoản khách hàng mới: " + kh.TenCongTy, HttpContext);
            TempData["Ok"] = "Đăng ký thành công! Chào mừng " + tk.HoTen + " đến với SmartPort.";
            return RedirectToAction("Index", "Home");
        }

        /* ---------- HO SO CA NHAN ---------- */

        [AppAuthorize]
        public new ActionResult Profile()
        {
            int maTK = (int)Session["MaTK"];
            var user = db.TaiKhoans.Find(maTK);
            if (user == null) return RedirectToAction("Logout");
            ViewBag.KhachHang = user.MaKH.HasValue ? db.KhachHangs.Find(user.MaKH.Value) : null;
            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken, AppAuthorize]
        public ActionResult UpdateProfile(string hoTen, string email, string soDienThoai)
        {
            int maTK = (int)Session["MaTK"];
            var user = db.TaiKhoans.Find(maTK);
            if (user == null) return RedirectToAction("Logout");

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Err"] = "Họ tên không được để trống.";
                return RedirectToAction("Profile");
            }
            user.HoTen = hoTen.Trim();
            user.Email = email;
            user.SoDienThoai = soDienThoai;
            db.SaveChanges();
            Session["HoTen"] = user.HoTen;
            TempData["Ok"] = "Cập nhật thông tin thành công.";
            return RedirectToAction("Profile");
        }

        [HttpPost, ValidateAntiForgeryToken, AppAuthorize]
        public ActionResult ChangePassword(ChangePassVM model)
        {
            int maTK = (int)Session["MaTK"];
            var user = db.TaiKhoans.Find(maTK);
            if (user == null) return RedirectToAction("Logout");

            if (!ModelState.IsValid)
            {
                TempData["Err"] = "Vui lòng kiểm tra lại thông tin đổi mật khẩu (tối thiểu 6 ký tự, xác nhận trùng khớp).";
                return RedirectToAction("Profile");
            }
            if (!Security.VerifyPassword(user.TenDangNhap, model.MatKhauCu, user.MatKhauHash))
            {
                TempData["Err"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction("Profile");
            }
            user.MatKhauHash = Security.HashPasswordStrong(model.MatKhauMoi);
            db.SaveChanges();
            NhatKyHelper.Ghi("BaoMat", user.TenDangNhap, "Đổi mật khẩu thành công", HttpContext);
            TempData["Ok"] = "Đổi mật khẩu thành công.";
            return RedirectToAction("Profile");
        }

        /* ============================================================
           FACE LOGIN — DANG NHAP / DANG KY BANG KHUON MAT
           Kien truc: dich vu AI (FastAPI + ArcFace, cong 8000) chi rut
           trich vector; moi logic phien + so khop nam ngay tai day.
           ============================================================ */

        /// <summary>JS ping truoc khi mo modal quet — biet dich vu AI song chua</summary>
        public JsonResult FaceCheck()
        {
            return Json(new
            {
                alive = FaceApiService.IsAlive(),
                nguongTinCay = FaceApiService.ConfidenceThreshold,
                soKhungCan = FaceApiService.FramesRequired
            });
        }

        /* --- Trang thai phien quet mat (chong do + bo phieu nhieu khung) --- */
        [Serializable]
        private class PhienQuet
        {
            public int MaTK;                  // ung vien dang dan diem
            public int SoPhieu;               // so khung hinh lien tiep cung khop nguoi nay
            public double TongConf;
            public double YawMin = 999, YawMax = -999;   // bien do quay dau da ghi nhan
            public int SoLan;                 // tong so khung da gui trong phien
            public DateTime BatDau = DateTime.Now;

            /// <summary>Vector + trong so chat luong cua cac khung ĐẠT — de gop lai</summary>
            public List<double[]> Vectors = new List<double[]>();
            public List<double> TrongSo = new List<double>();
            /// <summary>Diem moc cua khung truoc — phuc vu phep thu mat phang</summary>
            public List<List<double>> KpsTruoc;
            /// <summary>Sai so mat phang lon nhat quan sat duoc (cang lon cang chac la nguoi that)</summary>
            public double SaiSo3DMax;
        }
        private PhienQuet LayPhien()
        {
            var p = Session["FacePhien"] as PhienQuet;
            if (p == null || (DateTime.Now - p.BatDau).TotalMinutes > 3)
            {
                p = new PhienQuet();
                Session["FacePhien"] = p;
            }
            return p;
        }
        private const int MAX_FRAME_MOI_PHIEN = 40;   // chan do vet can (brute-force)

        /// <summary>
        /// Nhan 1 frame webcam (base64). QUY TRINH 5 LOP:
        ///   1. Kiem dinh khung hinh (so mat / liveness / chat luong) -> goi y sua
        ///   2. So khop da mau + BIEN AN TOAN so voi nguoi nhi
        ///   3. Bo phieu: can N khung LIEN TIEP cung khop 1 nguoi
        ///   4. Thu thach chuyen dong: phai co bien do quay dau (chong anh in dung im)
        ///   5. Dat Session + ghi nhat ky
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public JsonResult FaceLogin(string image, string returnUrl)
        {
            if (string.IsNullOrEmpty(image))
                return Json(new { ok = false, status = "bad_image", message = "Thiếu dữ liệu ảnh." });

            var phien = LayPhien();
            phien.SoLan++;
            if (phien.SoLan > MAX_FRAME_MOI_PHIEN)
                return Json(new { ok = false, status = "rate_limit", message = "Đã thử quá nhiều lần — vui lòng đăng nhập bằng mật khẩu hoặc tải lại trang." });

            // TTA=false khi ĐĂNG NHẬP: chạy ArcFace 1 lần thay vì 2 -> nhanh gấp đôi
            // (~1s -> ~0.5s mỗi khung). Bù lại bằng việc GỘP NHIỀU KHUNG bên dưới,
            // vốn cho lợi ích ổn định còn lớn hơn TTA. Khi ĐĂNG KÝ vẫn bật TTA.
            var ai = FaceApiService.Extract(image, tta: false);
            if (ai == null)
                return Json(new { ok = false, status = "service_down", message = "Đăng nhập khuôn mặt tạm không khả dụng — vui lòng dùng mật khẩu." });
            if (!ai.ok || ai.embedding == null)
                return Json(new { ok = false, status = ai.status ?? "no_face", message = ai.message ?? "Chưa thấy khuôn mặt — đưa mặt vào giữa khung hình." });

            // --- LOP 1: kiem dinh khung hinh ---
            var gate = FaceApiService.KiemDinh(ai);
            if (!gate.Pass)
            {
                if (gate.Status == "spoof")
                    NhatKyHelper.Ghi("BaoMat", null, "Face ID: nghi ngờ giả mạo (liveness " + ai.liveness + ")", HttpContext);
                return Json(new
                {
                    ok = false, status = gate.Status, message = gate.Message,
                    liveness = ai.liveness, chatLuong = ai.chat_luong != null ? (double?)ai.chat_luong.score : null
                });
            }

            // Ghi nhan bien do quay dau (thu thach chuyen dong)
            if (ai.chat_luong != null)
            {
                phien.YawMin = Math.Min(phien.YawMin, ai.chat_luong.yaw);
                phien.YawMax = Math.Max(phien.YawMax, ai.chat_luong.yaw);
            }

            // --- PHEP THU MAT PHANG: dau nguoi that la vat the 3D ---
            if (phien.KpsTruoc != null && ai.kps != null)
            {
                var ss = FaceApiService.SaiSoMatPhang(phien.KpsTruoc, ai.kps);
                if (ss >= 0) phien.SaiSo3DMax = Math.Max(phien.SaiSo3DMax, ss);
            }
            if (ai.kps != null) phien.KpsTruoc = ai.kps;

            // --- LOP 2: gop nhieu khung (co trong so chat luong) roi moi so khop ---
            var vecKhung = ai.embedding.ToArray();
            phien.Vectors.Add(vecKhung);
            phien.TrongSo.Add(ai.chat_luong != null ? ai.chat_luong.score / 100.0 : 0.6);
            if (phien.Vectors.Count > 5) { phien.Vectors.RemoveAt(0); phien.TrongSo.RemoveAt(0); }
            var vecGop = FaceApiService.GopVector(phien.Vectors, phien.TrongSo) ?? vecKhung;

            var best = FaceApiService.FindBestMatch(db, vecGop);
            var lyDo = FaceApiService.LyDoTuChoi(best);
            if (lyDo != null)
            {
                phien.SoPhieu = 0; phien.MaTK = 0;          // reset chuoi phieu (giu vector de gop tiep)
                return Json(new
                {
                    ok = false, status = "no_match",
                    confidence = best != null ? (double?)Math.Round(best.Confidence, 1) : null,
                    chatLuong = ai.chat_luong != null ? (double?)ai.chat_luong.score : null,
                    message = lyDo
                });
            }

            // --- LOP 3: bo phieu nhieu khung ---
            if (phien.MaTK == best.MaTK) { phien.SoPhieu++; phien.TongConf += best.Confidence; }
            else { phien.MaTK = best.MaTK; phien.SoPhieu = 1; phien.TongConf = best.Confidence; }

            var conf = Math.Round(best.Confidence, 1);
            if (phien.SoPhieu < FaceApiService.FramesRequired)
            {
                return Json(new
                {
                    ok = false, status = "verifying", confidence = conf,
                    buoc = phien.SoPhieu, canBuoc = FaceApiService.FramesRequired,
                    message = "Đã khớp " + conf + "% — đang xác minh lần " + (phien.SoPhieu + 1) + "/" + FaceApiService.FramesRequired + ", giữ nguyên khuôn mặt..."
                });
            }

            // --- LOP 4: thu thach chuyen dong + PHEP THU MAT PHANG (chong anh in/man hinh) ---
            var canChuyenDong = "true".Equals(CauHinh.AppSettings["FaceRequireMotion"], StringComparison.OrdinalIgnoreCase);
            var bienDoYaw = (phien.YawMax > -900 && phien.YawMin < 900) ? phien.YawMax - phien.YawMin : 0;
            if (canChuyenDong)
            {
                if (bienDoYaw < 7.0)
                {
                    return Json(new
                    {
                        ok = false, status = "challenge", confidence = conf, bienDo = Math.Round(bienDoYaw, 1),
                        message = "Xác minh người thật: hãy QUAY ĐẦU NHẸ sang trái rồi phải (ảnh in/màn hình sẽ không qua được bước này)."
                    });
                }
                // Da quay dau du nhieu ma cac diem moc van khop MOT MAT PHANG
                // => gan nhu chac chan la anh in / man hinh dang duoc nghieng qua lai
                if (phien.SaiSo3DMax < 0.035)
                {
                    NhatKyHelper.Ghi("BaoMat", null,
                        "Face ID: CHẶN giả mạo — điểm mốc khớp mặt phẳng (sai số 3D " + Math.Round(phien.SaiSo3DMax, 4) + ")", HttpContext);
                    return Json(new
                    {
                        ok = false, status = "spoof", saiSo3D = Math.Round(phien.SaiSo3DMax, 4),
                        message = "Phát hiện vật thể PHẲNG (ảnh in hoặc màn hình) thay vì khuôn mặt thật. Vui lòng dùng khuôn mặt thật hoặc đăng nhập bằng mật khẩu."
                    });
                }
            }

            var user = db.TaiKhoans.Find(best.MaTK);
            if (user == null || !user.KichHoat)
                return Json(new { ok = false, status = "no_match", message = "Tài khoản không tồn tại hoặc đã bị khóa." });

            // === DU DIEU KIEN: dang nhap that su ===
            var confTB = Math.Round(phien.TongConf / Math.Max(1, phien.SoPhieu), 1);
            Session["FacePhien"] = null;
            SetSession(user);
            NhatKyHelper.Ghi("DangNhap", user.TenDangNhap,
                "Đăng nhập KHUÔN MẶT — khớp " + confTB + "%, biên an toàn " + Math.Round(best.Margin, 3) +
                ", liveness " + ai.liveness + ", chất lượng " + (ai.chat_luong != null ? ai.chat_luong.score : 0) +
                ", gộp " + phien.Vectors.Count + " khung, sai số 3D " + Math.Round(phien.SaiSo3DMax, 4), HttpContext);

            string url;
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) url = returnUrl;
            else if (user.VaiTro == "Admin" || user.VaiTro == "NhanVien") url = Url.Action("Index", "Dashboard", new { area = "Admin" });
            else url = Url.Action("Index", "Home");

            return Json(new
            {
                ok = true, status = "success", confidence = confTB, liveness = ai.liveness,
                margin = Math.Round(best.Margin, 3),
                chatLuong = ai.chat_luong != null ? (double?)ai.chat_luong.score : null,
                hoTen = user.HoTen, redirectUrl = url,
                message = "Xin chào " + user.HoTen + "! (khớp " + confTB + "%)"
            });
        }

        /// <summary>Trang cai dat khuon mat (nguoi dung DA dang nhap)</summary>
        [AppAuthorize]
        public ActionResult FaceSetup()
        {
            int maTK = (int)Session["MaTK"];
            ViewBag.SoGoc = db.KhuonMats.Count(k => k.MaTK == maTK);
            ViewBag.AiAlive = FaceApiService.IsAlive();
            ViewBag.ChatLuongTB = db.KhuonMats.Where(k => k.MaTK == maTK && k.ChatLuong != null)
                                              .Select(k => k.ChatLuong).Average();
            ViewBag.SoKhungCan = FaceApiService.FramesRequired;
            return View();
        }

        /// <summary>Kiem dinh NHANH 1 anh mau khi dang ky — tra ve goi y sua ngay</summary>
        [HttpPost, ValidateAntiForgeryToken, AppAuthorize]
        public JsonResult FaceCheckShot(string image)
        {
            if (string.IsNullOrEmpty(image)) return Json(new { ok = false, message = "Thiếu ảnh." });
            var ai = FaceApiService.Extract(image, tta: true, dangKy: true);
            if (ai == null) return Json(new { ok = false, message = "Dịch vụ AI khuôn mặt chưa chạy." });
            if (!ai.ok || ai.embedding == null)
                return Json(new { ok = false, message = "Chưa thấy khuôn mặt — đưa mặt vào giữa khung." });
            var gate = FaceApiService.KiemDinh(ai, khiDangKy: true);
            return Json(new
            {
                ok = gate.Pass,
                message = gate.Pass ? "Ảnh đạt chuẩn ✔" : gate.Message,
                chatLuong = ai.chat_luong != null ? (double?)ai.chat_luong.score : null,
                yaw = ai.chat_luong != null ? (double?)ai.chat_luong.yaw : null
            });
        }

        /// <summary>
        /// Dang ky khuon mat: nhan 3-6 anh nhieu goc, kiem dinh CHAT LUONG tung
        /// anh, chong trung lap (2 anh gan y het nhau khong lam giau mau), roi luu.
        /// Dang ky lai se THAY THE toan bo vector cu cua tai khoan.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken, AppAuthorize]
        public JsonResult FaceEnroll(string img1, string img2, string img3, string img4, string img5)
        {
            int maTK = (int)Session["MaTK"];
            var anhs = new[] { img1, img2, img3, img4, img5 };
            var gocs = new[] { "Thẳng", "Trái", "Phải", "Ngẩng", "Cúi" };

            var soAnh = anhs.Count(a => !string.IsNullOrEmpty(a));
            if (soAnh < 3)
                return Json(new { ok = false, message = "Cần ít nhất 3 ảnh (thẳng / trái / phải) — càng nhiều góc càng nhận diện chính xác." });

            var vectors = new List<double[]>();
            var chatLuongs = new List<double>();
            var detScores = new List<double>();
            var gocLuu = new List<string>();

            for (int i = 0; i < anhs.Length; i++)
            {
                if (string.IsNullOrEmpty(anhs[i])) continue;
                var ai = FaceApiService.Extract(anhs[i], tta: true, dangKy: true);
                if (ai == null)
                    return Json(new { ok = false, message = "Dịch vụ AI khuôn mặt chưa chạy — hãy khởi động dịch vụ rồi thử lại." });
                if (!ai.ok || ai.embedding == null)
                    return Json(new { ok = false, message = "Góc " + gocs[i] + ": không thấy khuôn mặt. Hãy chụp lại nơi đủ sáng." });

                var gate = FaceApiService.KiemDinh(ai, khiDangKy: true);
                if (!gate.Pass)
                    return Json(new { ok = false, message = "Góc " + gocs[i] + ": " + gate.Message });

                var vec = ai.embedding.ToArray();
                // Chong trung: 2 anh gan y het nhau khong bo sung thong tin gi
                if (vectors.Any(v => FaceApiService.Cosine(v, vec) > 0.97))
                    return Json(new { ok = false, message = "Góc " + gocs[i] + " gần giống hệt ảnh trước — hãy đổi góc mặt rõ hơn để AI học được nhiều đặc trưng." });

                vectors.Add(vec);
                chatLuongs.Add(ai.chat_luong != null ? ai.chat_luong.score : 0);
                detScores.Add(ai.det_score ?? 0);
                gocLuu.Add(gocs[i]);
            }

            // An toan: neu mat nay DA khop >= nguong voi TAI KHOAN KHAC thi tu choi
            var thu = FaceApiService.FindBestMatch(db, vectors[0]);
            if (thu != null && thu.MaTK != maTK && thu.Confidence >= FaceApiService.ConfidenceThreshold)
                return Json(new { ok = false, message = "Khuôn mặt này đã được đăng ký cho một tài khoản khác." });

            // Thay the vector cu
            var cu = db.KhuonMats.Where(k => k.MaTK == maTK).ToList();
            db.KhuonMats.RemoveRange(cu);
            for (int i = 0; i < vectors.Count; i++)
            {
                db.KhuonMats.Add(new KhuonMat
                {
                    MaTK = maTK,
                    VectorJson = JsonConvert.SerializeObject(vectors[i]),
                    GocChup = gocLuu[i],
                    ChatLuong = (decimal?)Math.Round(chatLuongs[i], 1),
                    DetScore = (decimal?)Math.Round(detScores[i], 3),
                    NgayTao = DateTime.Now
                });
            }
            db.SaveChanges();

            var tb = Math.Round(chatLuongs.Average(), 1);
            NhatKyHelper.Ghi("BaoMat", Session["HoTen"] + "",
                "Đăng ký lại khuôn mặt: " + vectors.Count + " góc, chất lượng TB " + tb + "/100", HttpContext);

            return Json(new
            {
                ok = true, soGoc = vectors.Count, chatLuong = tb,
                message = "Đăng ký thành công " + vectors.Count + " góc mặt (chất lượng trung bình " + tb + "/100)! Từ giờ bạn có thể đăng nhập bằng khuôn mặt."
            });
        }

        /// <summary>Huy dang ky khuon mat cua chinh minh</summary>
        [HttpPost, ValidateAntiForgeryToken, AppAuthorize]
        public JsonResult FaceRemove()
        {
            int maTK = (int)Session["MaTK"];
            var cu = db.KhuonMats.Where(k => k.MaTK == maTK).ToList();
            db.KhuonMats.RemoveRange(cu);
            db.SaveChanges();
            return Json(new { ok = true, message = "Đã xóa đăng ký khuôn mặt." });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
