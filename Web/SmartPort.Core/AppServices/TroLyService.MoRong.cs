using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ============ TRỢ LÝ NGHIỆP VỤ — PHẦN MỞ RỘNG ============
    ///
    /// Tep TroLyService.cs lo phan loi: bai, tien, booking, EOR, agent.
    /// Tep nay phu kin PHAN CON LAI cua he thong — tau va cau ben, cong va
    /// giay to, EDI, AI, cham soc khach — cong voi thu quan trong nhat:
    /// VIEC THEO LOAT.
    ///
    /// VI SAO "VIEC THEO LOAT" MOI LA CAI LAM NEN MOT TRO LY THAT SU:
    /// Tra loi tung cau mot thi van chi la tra cuu nhanh hon. Nguoi truc ca
    /// khong hoi tung cau — ho mo ca, nhin xem hom nay con dong gi, roi lam
    /// het. "viec hom nay" + "lam het viec" chinh la hai cau do. Tro ly quet
    /// ca he thong, dem ra tung dau viec, roi lam ca loat trong MOT lan bam.
    ///
    /// RANH GIOI KHONG DOI, KE CA O DAY:
    ///   · DOC  -> lam ngay
    ///   · GHI  -> hoi xac nhan, ghi nhat ky, hoan tac duoc
    ///   · CAM  -> xoa du lieu / phan quyen / mat khau / bieu gia / cau hinh
    ///            khong co cong cu, xin cung khong lam.
    /// Lam theo loat cang phai giu chat ranh gioi nay: mot lan bam ma dong
    /// vao 30 ban ghi thi cai gia cua mot nham lan cung nhan len 30 lan.
    /// Vi vay moi loat deu phai: dem truoc — noi ro — hoi — roi moi lam.
    /// </summary>
    public static partial class TroLyService
    {
        /// <summary>Ma depot dung khi sinh EDI — trung voi EdiController.MaDepot</summary>
        private static string MaDepotEdi
        {
            get
            {
                var s = CauHinh.AppSettings["MaDepotEDI"];
                return string.IsNullOrWhiteSpace(s) ? "SMARTPORT" : s;
            }
        }

        // ============================================================
        // DANH MUC MO RONG
        // ============================================================

        public static readonly MoTaCongCu[] DanhMucMoRong = new[]
        {
            // -------- viec theo loat: dat len dau vi day la thu nguoi truc ca can nhat
            new MoTaCongCu { Nhom = "Việc theo loạt", Ten = "Việc cần làm hôm nay", ViDu = "việc hôm nay" },
            new MoTaCongCu { Nhom = "Việc theo loạt", Ten = "Làm hết việc trong ca", ViDu = "làm hết việc", DungTien = true },
            new MoTaCongCu { Nhom = "Việc theo loạt", Ten = "Chốt công nợ hàng loạt", ViDu = "chốt công nợ hết", DungTien = true },
            new MoTaCongCu { Nhom = "Việc theo loạt", Ten = "Duyệt hàng loạt booking", ViDu = "duyệt hết booking", DungTien = true },
            new MoTaCongCu { Nhom = "Việc theo loạt", Ten = "Duyệt hàng loạt EOR", ViDu = "duyệt hết eor", DungTien = true },

            // -------- tau & cau ben
            new MoTaCongCu { Nhom = "Tàu & cầu bến", Ten = "Lịch tàu", ViDu = "tàu nào sắp đến" },
            new MoTaCongCu { Nhom = "Tàu & cầu bến", Ten = "Cầu bến trống", ViDu = "bến nào trống" },
            new MoTaCongCu { Nhom = "Tàu & cầu bến", Ten = "Xếp cầu bến cho chuyến", ViDu = "xếp bến cho chuyến VS2601", DungTien = true },
            new MoTaCongCu { Nhom = "Tàu & cầu bến", Ten = "Cập bến / rời bến", ViDu = "chuyến VS2601 đã cập bến", DungTien = true },

            // -------- cong & xe
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Thẻ xe sắp hết hạn", ViDu = "thẻ xe nào sắp hết hạn" },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Gia hạn thẻ xe", ViDu = "gia hạn thẻ xe 61C-12345", DungTien = true },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Phiếu cân", ViDu = "phiếu cân hôm nay" },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Đối soát cổng", ViDu = "đối soát cổng hôm nay" },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Thu phí cổng", ViDu = "thu phí cổng hôm nay" },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Tra cứu tài xế", ViDu = "tài xế Nguyễn Văn A" },

            // -------- giay to
            new MoTaCongCu { Nhom = "Giấy tờ", Ten = "Chứng từ đã phát hành", ViDu = "chứng từ của MSCU2277456" },
            new MoTaCongCu { Nhom = "Giấy tờ", Ten = "Kiểm đếm", ViDu = "kiểm đếm gần đây" },
            new MoTaCongCu { Nhom = "Giấy tờ", Ten = "Duyệt / hủy lệnh giao nhận", ViDu = "hủy lệnh LGN-DEMO-001", DungTien = true },

            // -------- tien
            new MoTaCongCu { Nhom = "Tiền & công nợ", Ten = "Ghi nhận đã thu tiền", ViDu = "đã thu phiếu PLB-2609-0030", DungTien = true },
            new MoTaCongCu { Nhom = "Tiền & công nợ", Ten = "Bảng giá dịch vụ", ViDu = "bảng giá dịch vụ" },
            new MoTaCongCu { Nhom = "Tiền & công nợ", Ten = "Ước tính chi phí", ViDu = "ước tính 5 cont 40HC lưu 10 ngày" },

            // -------- bai
            new MoTaCongCu { Nhom = "Bãi & container", Ten = "Di chuyển container", ViDu = "chuyển MSCU2277456 sang B-01-05-2", DungTien = true },
            new MoTaCongCu { Nhom = "Bãi & container", Ten = "Đổi trạng thái container", ViDu = "MSCU2277456 đã ra bãi", DungTien = true },

            // -------- agent & edi
            new MoTaCongCu { Nhom = "Agent & EDI", Ten = "Bản tin EDI chờ gửi", ViDu = "bản tin nào chưa gửi" },
            new MoTaCongCu { Nhom = "Agent & EDI", Ten = "Đánh dấu đã gửi bản tin", ViDu = "đã gửi bản tin 5", DungTien = true },

            // -------- phan tich
            new MoTaCongCu { Nhom = "Phân tích", Ten = "Cảnh báo toàn hệ thống", ViDu = "có gì bất thường" },
            new MoTaCongCu { Nhom = "Phân tích", Ten = "Phân khúc khách hàng", ViDu = "phân khúc khách" },
            new MoTaCongCu { Nhom = "Phân tích", Ten = "Bất thường sản lượng", ViDu = "sản lượng có bất thường không" },
            new MoTaCongCu { Nhom = "Phân tích", Ten = "Top khách hàng", ViDu = "khách nào lớn nhất" },

            // -------- van hanh khac
            new MoTaCongCu { Nhom = "Vận hành khác", Ten = "Tình trạng camera", ViDu = "camera nào lỗi" },
            new MoTaCongCu { Nhom = "Vận hành khác", Ten = "Nhận diện AI", ViDu = "ai nhận diện hôm nay" },
            new MoTaCongCu { Nhom = "Vận hành khác", Ten = "Liên hệ của khách", ViDu = "có liên hệ mới không" },
            new MoTaCongCu { Nhom = "Vận hành khác", Ten = "Xử lý liên hệ", ViDu = "đã trả lời liên hệ 3", DungTien = true },
            new MoTaCongCu { Nhom = "Vận hành khác", Ten = "Nhật ký hệ thống", ViDu = "ai vừa làm gì" },
        };

        // ============================================================
        // HOOK 1 — VIEC THEO LOAT (xet TRUOC moi lenh don le)
        // ============================================================

        private static KetQua ViecTheoLoat(AppDb db, string tho, string t, string nguoiDung)
        {
            // Tong quan ca truc — cua ngo vao moi thu con lai
            if (Co(t, "viec hom nay", "viec cua toi", "ca truc", "truc ca", "co gi can lam",
                      "can lam gi", "toi can lam gi", "tinh hinh hom nay", "tong quan",
                      "bat dau ca", "dau ca", "viec ton", "con viec gi"))
                return ViecHomNay(db);

            // Lam het — chi nhan khi cau NOI RO la "het/tat ca", khong doan bua
            if (Co(t, "lam het viec", "lam het cac viec", "xu ly het viec", "lam tat ca viec",
                      "xu ly tat ca", "lam giup het", "lam het giup", "tu dong lam het",
                      "lam het di", "xu ly het di"))
                return ChuanBiLamHetViec(db);

            if (Co(t, "chot cong no het", "chot het cong no", "lap phieu het", "lap phieu tat ca",
                      "chot cong no hang loat", "chot het phi", "lap het phieu", "chot cong no cuoi ngay"))
                return ChuanBiChotCongNoLoat(db);

            if (Co(t, "duyet het booking", "duyet tat ca booking", "duyet hang loat booking",
                      "duyet het don", "duyet toan bo booking"))
                return ChuanBiDuyetLoatBooking(db);

            if (Co(t, "duyet het eor", "duyet tat ca eor", "duyet hang loat eor", "duyet toan bo eor"))
                return ChuanBiDuyetLoatEor(db);

            return null;
        }

        // ============================================================
        // HOOK 2 — MO RONG SOM
        // Lenh GHI moi + vai cau DOC co tu khoa de bi luat cu nuot mat.
        // ============================================================

        private static KetQua MoRongSom(AppDb db, string tho, string t,
                                        string soCont, string soMa, int? soNguyen, string bienSo)
        {
            // ---- DOC bi trung tu khoa voi luat cu ----
            // Bon cau duoi day deu tung bi luat cu nuot mat, phat hien khi chay test:
            //   "phan khuc khach hang" / "top khach hang"  chua "khach hang"
            //   "the xe nao sap HET HAN"                   chua "het han" (luat bang lai)
            //   "SAN LUONG co bat thuong khong"            chua "san luong" (luat du bao)
            // Bai hoc: luat sau khong the sua duoc luat truoc — phai xet truoc.
            if (Co(t, "phan khuc", "xep hang khach", "khach vip", "nhom khach"))
                return PhanKhucKhach(db);
            if (Co(t, "top khach", "khach nao lon", "khach lon nhat", "khach doanh thu cao"))
                return TopKhachHang(db);
            if (Co(t, "the xe", "the thang", "the ra vao") && !Co(t, "gia han"))
                return TheXeSapHetHan(db);
            if (Co(t, "san luong bat thuong", "san luong co bat thuong", "bien dong san luong",
                      "san luong co gi la", "san luong that thuong"))
                return BatThuongSanLuong(db);
            // "thanh toan cho phieu..." de xuong duoi se roi vao "thanh toan chua thu"
            if (Co(t, "da thu phieu", "da thu tien phieu", "ghi nhan thu", "xac nhan thu tien",
                      "thu tien phieu", "danh dau da thu"))
                return ChuanBiGhiNhanThu(db, soMa);

            // ---- DOC: cau nhan vien go that ma luat cu bo sot ----
            // Tim ra bang cach DO: 53 cau nhan vien depot hay go, 25 cau roi vao
            // "tôi chưa hiểu". Xet o day (som) vi chung chua tu khoa cua luat cu:
            // "khách nào nợ" chua "khach nao", "xe trong bãi" chua "trong bai".
            // Dung CoTu (nguyen tu) chu khong Co (chuoi con): "khach no" khong
            // duoc khop "khách nói", "cont hong" khong duoc khop giua chu "không".
            if (soCont == null && CoTu(t, "phi luu bai", "gia luu bai", "bieu phi luu bai", "don gia luu bai",
                                      "ngay mien phi", "mien phi may ngay", "mien phi bao nhieu ngay", "luu bai tinh sao"))
                return BieuPhiLuuBai(db);
            if (CoTu(t, "khach no", "khach nao no", "no nhieu", "dang no", "con no", "ai no", "no theo khach"))
                return CongNoTheoKhach(db);
            if (CoTu(t, "no qua han", "qua han thanh toan", "tre han thanh toan", "no tre han", "no xau", "cham thanh toan"))
                return ThanhToanChuaThu(db);
            if (CoTu(t, "xe dang trong", "xe trong cang", "xe con trong", "xe chua ra", "xe trong bai")
                || (CoTu(t, "xe") && CoTu(t, "trong cang", "trong bai")))
                return XeQuaCong(db);
            if (CoTu(t, "cont rong", "container rong", "ton rong", "vo rong"))
                return TonTheoHangTau(db);
            if (CoTu(t, "hu hong", "cont hong", "container hong", "vo hong", "bi hong", "cont hu", "container hu"))
                return EorChoDuyet(db);
            if (CoTu(t, "cho trong", "con cho", "suc chua", "day bai", "bai con", "cong suat bai"))
                return TonBai(db);
            if (CoTu(t, "nam lau", "lau nhat", "lau ngay"))
                return ContainerTonLau(db, TimSoNgay(t) ?? 1);

            // ---- GHI moi ----
            if (Co(t, "xep ben", "gan ben", "bo tri ben", "cap ben cho chuyen"))
                return ChuanBiXepBen(db, tho, soNguyen);

            // Cau HOI ("tàu nào đang làm hàng?") khong phai cau LENH — dung dua the
            // doi trang thai chuyen tau. De no roi xuong luat lich tau ben duoi.
            var laCauHoi = tho.Contains("?") || CoTu(t, "nao", "khong", "bao nhieu", "co tau", "chua");
            if (!laCauHoi && Co(t, "da cap ben", "cap ben luc", "tau cap ben"))
                return ChuanBiChuyenTau(db, tho, "Đã cập bến");
            if (!laCauHoi && Co(t, "da roi ben", "roi ben luc", "tau roi ben", "tau da di"))
                return ChuanBiChuyenTau(db, tho, "Đã rời bến");
            if (!laCauHoi && Co(t, "bat dau lam hang", "dang lam hang"))
                return ChuanBiChuyenTau(db, tho, "Đang làm hàng");

            // Cau ma nguoi ta go that la "chuyen MAEU3305891 sang B-01-05-2" — giua
            // dong tu va gioi tu co so container chen vao, nen bat theo tu ghep
            // ("chuyen sang") la truot. Co DU ca so cont lan vi tri bai thi y dinh
            // da ro rang roi, khong can dong tu nua.
            if ((soCont != null && TimViTriBai(tho) != null)
                || Co(t, "chuyen cont", "di doi cont", "doi vi tri", "chuyen sang", "ha xuong",
                         "dua cont sang", "chuyen container", "di doi container"))
                return ChuanBiDiChuyenCont(db, tho, soCont);

            if (Co(t, "da ra bai", "da vao bai", "cho xuat tau", "doi trang thai cont",
                      "doi trang thai container"))
                return ChuanBiDoiTrangThaiCont(db, tho, t, soCont);

            if (Co(t, "duyet lenh", "hoan tat lenh", "xong lenh"))
                return ChuanBiLenhGiaoNhan(db, soMa, soNguyen, "Hoàn tất");
            if (Co(t, "huy lenh", "bo lenh"))
                return ChuanBiLenhGiaoNhan(db, soMa, soNguyen, "Hủy");

            if (Co(t, "gia han the xe", "gia han the", "nap the xe", "gia han cho xe"))
                return ChuanBiGiaHanThe(db, bienSo, t);

            if (Co(t, "da tra loi lien he", "xu ly lien he", "dong lien he", "da lien he lai"))
                return ChuanBiXuLyLienHe(db, soNguyen);

            if (Co(t, "da gui ban tin", "danh dau da gui", "gui ban tin"))
                return ChuanBiDanhDauDaGui(db, soNguyen);

            return null;
        }

        // ============================================================
        // HOOK 3 — MO RONG MUON (chay sau toan bo luat cu)
        // ============================================================

        private static KetQua MoRongMuon(AppDb db, string tho, string t,
                                         string soCont, string soMa, int? soNguyen, string bienSo)
        {
            if (soMa != null && soMa.StartsWith("PLB")) return TraCuuPhieuLuuBai(db, soMa);
            if (soMa != null && soMa.StartsWith("LGN")) return TraCuuLenh(db, soMa);

            if (Co(t, "canh bao he thong", "bat thuong", "co gi la", "diem nong", "co van de gi"))
                return CanhBaoHeThong(db);

            if (Co(t, "lich tau", "tau nao", "chuyen tau", "tau sap", "tau den", "eta", "tau dang"))
                return LichTau(db);
            if (Co(t, "cau ben", "ben nao", "ben trong", "ben so"))
                return CauBen(db);

            if (Co(t, "phieu can", "can xe", "vgm", "khoi luong hang"))
                return PhieuCanGanDay(db);
            if (Co(t, "doi soat", "lech cong", "dem xe", "xung dem", "sai lech"))
                return DoiSoatCong(db);
            if (Co(t, "phi cong", "thu phi", "tien cong", "doanh thu cong"))
                return ThuPhiCong(db);
            if (Co(t, "tai xe", "lai xe", "nguoi lai"))
                return TraCuuTaiXe(db, tho);

            if (Co(t, "chung tu", "giay to", "ho so giay", "phieu da phat hanh"))
                return ChungTuGanDay(db, soCont);
            if (Co(t, "kiem dem", "tally", "dem hang"))
                return KiemDemGanDay(db);

            if (Co(t, "bang gia", "don gia", "gia dich vu", "gia bao nhieu", "phi dich vu"))
                return BangGiaDichVu(db, tho);
            if (Co(t, "uoc tinh", "bao gia", "chi phi cho", "het bao nhieu tien", "tinh gium"))
                return UocTinhChiPhi(db, tho, t);

            if (Co(t, "ban tin", "edi nao", "chua gui", "codeco", "coarri"))
                return BanTinEdiChoGui(db);

            if (Co(t, "camera"))
                return TinhTrangCamera(db);
            if (Co(t, "nhan dien", "ai dem", "model ai", "thi giac"))
                return NhanDienGanDay(db);
            if (Co(t, "lien he", "khach nhan", "khach gui", "phan hoi khach"))
                return LienHeMoi(db);
            if (Co(t, "nhat ky", "ai vua lam", "lich su thao tac", "ai lam gi"))
                return NhatKyGanDay(db);

            // ---- DOC: cau go that, xet MUON vi tu khoa rong ----
            // "báo cáo doanh thu" phai ra doanh thu, "chi phí sửa chữa" phai ra EOR —
            // nen may luat rong nay dung sau het, chi bat phan con lai.
            if (CoTu(t, "bao cao", "thong ke", "tong ket", "so lieu"))
                return BaoCaoNhanh(db);
            if (CoTu(t, "phieu thu", "phieu luu bai", "phieu da lap", "phieu cho thu"))
                return PhieuThuGanDay(db);
            if (CoTu(t, "chi phi", "tinh phi", "tinh tien", "bao nhieu tien"))
                return UocTinhChiPhi(db, tho, t);
            if (CoTu(t, "hang tau"))
                return TonTheoHangTau(db);
            if (CoTu(t, "tau"))
                return LichTau(db);
            if (t.StartsWith("khach "))
                return TraCuuKhachHang(db, tho);

            return null;
        }

        // ============================================================
        // HOOK 4 — THUC HIEN (sau khi nguoi bam dong y)
        // ============================================================

        private static KetQua ThucHienMoRong(AppDb db, string congCu, string thamSo, string nguoiDung)
        {
            switch (congCu)
            {
                case "LamHetViec": return LamHetViecThat(db, nguoiDung);
                case "ChotCongNoLoat": return ChotCongNoLoatThat(db, nguoiDung);
                case "DuyetLoatBooking": return DuyetLoatBookingThat(db, nguoiDung);
                case "DuyetLoatEor": return DuyetLoatEorThat(db, nguoiDung);
                case "GhiNhanThu": return GhiNhanThuThat(db, thamSo, nguoiDung);
                case "XepBen": return XepBenThat(db, thamSo, nguoiDung);
                case "ChuyenTau": return ChuyenTauThat(db, thamSo, nguoiDung);
                case "DiChuyenCont": return DiChuyenContThat(db, thamSo, nguoiDung);
                case "DoiTrangThaiCont": return DoiTrangThaiContThat(db, thamSo, nguoiDung);
                case "LenhGiaoNhan": return LenhGiaoNhanThat(db, thamSo, nguoiDung);
                case "GiaHanThe": return GiaHanTheThat(db, thamSo, nguoiDung);
                case "XuLyLienHe": return XuLyLienHeThat(db, thamSo, nguoiDung);
                case "DanhDauDaGui": return DanhDauDaGuiThat(db, thamSo, nguoiDung);
                case "SinhEdiThat": return SinhEdiThatSu(db, thamSo, nguoiDung);
                default: return null;
            }
        }

        // ============================================================
        // VIEC HOM NAY — ban do ca truc
        // ============================================================

        /// <summary>Mot dau viec dang cho nguoi lam. Dem duoc thi moi lam theo loat duoc.</summary>
        private class DauViec
        {
            public string Ten;
            public int So;
            public string Cau;        // cau de tro ly tu hoi lai chinh minh
            public bool TuLamDuoc;    // co nam trong "lam het viec" khong
        }

        private static List<DauViec> SoatViec(AppDb db)
        {
            var ds = new List<DauViec>();
            var mai = DateTime.Today.AddDays(30);

            // Cong no chua chot — dem tren bang ke dang chay cho dung
            // voi con so man hinh Luu bai hien, khong tu tinh kieu khac.
            var bangKe = LuuBaiService.BangKeDangChay(db);
            var chuaChot = bangKe == null ? 0
                : bangKe.Count(d => !d.DaCoPhieu && d.Phi != null && d.Phi.TongTien > 0);

            // Ten ngan vi no nam tren MOT DONG bam duoc, canh con so — khong phai cau van.
            ds.Add(new DauViec { Ten = "Cont quá hạn, chưa lập phiếu", So = chuaChot,
                                 Cau = "chốt công nợ hết", TuLamDuoc = true });
            ds.Add(new DauViec { Ten = "Việc agent chờ duyệt", So = db.NhatKyAgents.Count(n => n.TrangThai == "Chờ duyệt"),
                                 Cau = "agent đang chờ duyệt gì", TuLamDuoc = true });
            ds.Add(new DauViec { Ten = "Booking chờ duyệt", So = db.DatDichVus.Count(b => b.TrangThai == "Chờ duyệt"),
                                 Cau = "booking nào chờ duyệt", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "EOR chờ duyệt", So = db.PhieuSuaChuas.Count(e => e.TrangThai == "Chờ duyệt"),
                                 Cau = "eor nào chờ duyệt", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "Sự cố an ninh đang mở", So = db.SuCoAnNinhs.Count(s => s.TrangThai == "Mới"),
                                 Cau = "sự cố nào đang mở", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "Phiếu chờ thu tiền", So = db.PhieuLuuBais.Count(p => p.TrangThai == "Chờ thu"),
                                 Cau = "công nợ hôm nay", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "Thanh toán quá hạn", So = db.ThanhToans.Count(x => x.TrangThai == "Quá hạn"),
                                 Cau = "ai chưa thanh toán", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "EDI chờ gửi hãng tàu", So = db.BanTinEDIs.Count(b => b.TrangThai == "Chờ gửi"),
                                 Cau = "bản tin nào chưa gửi", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "Lệnh giao nhận quá hạn", So = db.LenhGiaoNhans.Count(l => l.TrangThai == "Quá hạn"),
                                 Cau = "lệnh nào đang chạy", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "Liên hệ chưa trả lời", So = db.LienHes.Count(l => !l.DaXuLy),
                                 Cau = "có liên hệ mới không", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "GPLX hết hạn trong 30 ngày",
                                 So = db.TaiXes.Count(x => x.HanGPLX != null && x.HanGPLX <= mai),
                                 Cau = "bằng lái nào sắp hết hạn", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "Thẻ xe hết hạn trong 30 ngày",
                                 So = db.TheXes.Count(x => x.KichHoat && x.HanDen != null && x.HanDen <= mai),
                                 Cau = "thẻ xe nào sắp hết hạn", TuLamDuoc = false });
            ds.Add(new DauViec { Ten = "Tàu cập trong 3 ngày",
                                 So = db.ChuyenTaus.Count(c => c.TrangThai == "Dự kiến" && c.ETA <= DateTime.Today.AddDays(3)),
                                 Cau = "tàu nào sắp đến", TuLamDuoc = false });

            return ds;
        }

        private static KetQua ViecHomNay(AppDb db)
        {
            var kq = BanDoViec(db, "");
            kq.CongCu = "Việc cần làm hôm nay";
            return kq;
        }

        /// <summary>
        /// Moi dau viec dang cho = MOT DONG BAM DUOC kem con so; bam la mo chi tiet.
        /// Ban truoc viet thanh doan chu liet ke roi lap lai y het thanh nut ben
        /// duoi — nguoi dung doc hai lan cung mot thu. Dung chung cho "việc hôm nay"
        /// va cho loi chao ("alo" -> chao kem luon viec dang cho).
        /// </summary>
        private static KetQua BanDoViec(AppDb db, string loiChao)
        {
            var kq = new KetQua();
            var con = SoatViec(db).Where(v => v.So > 0)
                                  .OrderByDescending(v => v.TuLamDuoc).ThenByDescending(v => v.So).ToList();

            if (con.Count == 0)
            {
                kq.TraLoi = loiChao + "<b>Sạch việc</b> — không có gì đang chờ bạn.";
                kq.LienKets.Add(LK("Tồn bãi", "#goi:tồn bãi", "bi-box-seam"));
                kq.LienKets.Add(LK("Xe qua cổng hôm nay", "#goi:xe qua cổng hôm nay", "bi-truck"));
                kq.LienKets.Add(LK("Doanh thu tháng này", "#goi:doanh thu tháng này", "bi-graph-up-arrow"));
                return kq;
            }

            var s = new StringBuilder(loiChao).Append("Có <b>").Append(con.Sum(v => v.So)).Append(" việc</b> đang chờ:")
                                              .Append("<div class=\"tl-ds\">");
            foreach (var v in con) s.Append(Dong(v.Ten, v.So, v.Cau, v.TuLamDuoc));
            kq.TraLoi = s.Append("</div>").ToString();

            // Viec vach cam tro ly tu lam duoc theo luat — dua nut lam mot luot.
            var tuLam = con.Where(v => v.TuLamDuoc).Sum(v => v.So);
            if (tuLam > 0)
                kq.LienKets.Add(LK("Tự làm " + tuLam + " việc vạch cam", "#goi:làm hết việc", "bi-lightning-charge-fill"));
            return kq;
        }

        // ============================================================
        // LAM HET VIEC — loat lon nhat
        // ============================================================

        private static KetQua ChuanBiLamHetViec(AppDb db)
        {
            var kq = new KetQua { CongCu = "Làm hết việc trong ca" };
            var ds = SoatViec(db);
            var tuLam = ds.Where(v => v.TuLamDuoc && v.So > 0).ToList();

            if (tuLam.Count == 0)
            {
                kq.TraLoi = "Không có việc nào tôi tự làm được lúc này.";
                kq.LienKets.Add(LK("Việc còn cần bạn quyết", "#goi:việc hôm nay", "bi-list-check"));
                return kq;
            }

            var s = new StringBuilder("Tôi sẽ làm:<div class=\"tl-ds\">");
            foreach (var v in tuLam) s.Append(DongTinh(v.Ten, v.So.ToString("#,##0", Vi)));
            s.Append(DongTinh("Chạy agent rà soát việc mới", null)).Append("</div>");

            kq.TraLoi = s.ToString();
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "LamHetViec",
                ThamSo = "",
                MoTa = "Làm hết " + tuLam.Sum(v => v.So) + " việc trong một lượt?",
                // Booking va EOR la cam ket voi khach va hang tau — phai co nguoi nhin,
                // nen loat nay KHONG duyet hai thu do; muon thi go rieng "duyệt hết booking".
                CanhBao = "Có ghi nhật ký, hoàn tác được từng việc. Không tự duyệt booking và EOR."
            };
            return kq;
        }

        private static KetQua LamHetViecThat(AppDb db, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Làm hết việc trong ca" };
            var buoc = new List<string>();

            // 1) Chot cong no cho moi container qua han chua co phieu
            int soPhieu = 0; decimal tienPhieu = 0;
            var batDau = DateTime.Now;
            var bangKe = LuuBaiService.BangKeDangChay(db);
            if (bangKe != null)
                foreach (var d in bangKe.Where(x => !x.DaCoPhieu && x.Phi != null && x.Phi.TongTien > 0).ToList())
                {
                    var c = db.Containers.FirstOrDefault(x => x.MaCont == d.Cont.MaCont);
                    if (c == null) continue;
                    var p = LuuBaiService.LapPhieu(db, c, nguoiDung);
                    // LapPhieu chong chot trung bang cach TRA VE PHIEU CU chu khong tra null.
                    // Khong loc theo NgayLap thi loat se bao "vua lap N phieu" ke ca khi
                    // no chi doc lai phieu co san — bao cao sai so tien cho ke toan.
                    if (p == null || p.NgayLap < batDau) continue;
                    soPhieu++; tienPhieu += p.TongTien;
                }
            if (soPhieu > 0)
            {
                db.SaveChanges();
                buoc.Add("Lập <b>" + soPhieu + "</b> phiếu thu lưu bãi — tổng <b>" + Tien(tienPhieu) + "</b>");
            }

            // 2) Duyet cac viec agent dang cho
            //    Goi thang AgentTuDongService.Duyet de dung MOT bo quy tac, mot cho ghi vet.
            var choDuyet = db.NhatKyAgents.AsNoTracking()
                             .Where(n => n.TrangThai == "Chờ duyệt")
                             .Select(n => n.MaNK).ToList();
            int daDuyet = 0;
            foreach (var id in choDuyet)
            {
                try { AgentTuDongService.Duyet(db, id, nguoiDung); daDuyet++; }
                catch { /* mot viec hong khong duoc keo do ca loat */ }
            }
            if (daDuyet > 0) buoc.Add("Duyệt <b>" + daDuyet + "</b> việc agent đang chờ");

            // 3) Chay agent ra soat de bat viec moi
            var r = AgentTuDongService.ChayTatCa(db, nguoiDung);
            buoc.Add("Chạy agent rà soát: <b>" + r.DaLam + "</b> việc tự làm, <b>" + r.ChoDuyet + "</b> việc mới chờ duyệt");

            NhatKyHelper.Ghi("ThaoTac", nguoiDung,
                "Trợ lý làm loạt: " + soPhieu + " phiếu, " + daDuyet + " việc agent, chạy rà soát");

            kq.TraLoi = buoc.Count == 0
                ? "Chạy xong nhưng không có gì để làm."
                : "<b>Xong.</b><br>· " + string.Join("<br>· ", buoc);
            kq.LienKets.Add(LK("Nhật ký Agent", "/Admin/Agent", "bi-robot"));
            if (soPhieu > 0) kq.LienKets.Add(LK("Phiếu lưu bãi vừa lập", "/Admin/LuuBai/DanhSach", "bi-receipt"));
            kq.LienKets.Add(LK("Soát lại việc còn tồn", "#goi:việc hôm nay", "bi-list-check"));
            return kq;
        }

        // ============================================================
        // CHOT CONG NO HANG LOAT
        // ============================================================

        private static KetQua ChuanBiChotCongNoLoat(AppDb db)
        {
            var kq = new KetQua { CongCu = "Chốt công nợ hàng loạt" };
            var bangKe = LuuBaiService.BangKeDangChay(db);
            var can = bangKe == null ? new List<LuuBaiService.DongBangKe>()
                    : bangKe.Where(d => !d.DaCoPhieu && d.Phi != null && d.Phi.TongTien > 0).ToList();

            if (can.Count == 0)
            {
                kq.TraLoi = "Không có container nào cần chốt — mọi cont quá hạn miễn phí đều đã có phiếu.";
                kq.LienKets.Add(LK("Bảng kê lưu bãi", "/Admin/LuuBai", "bi-table"));
                return kq;
            }

            var tong = can.Sum(d => d.Phi.TongTien);
            var s = new StringBuilder("Có <b>" + can.Count + " container</b> quá hạn miễn phí mà chưa lập phiếu — "
                  + "tổng <b>" + Tien(tong) + "</b>:");
            foreach (var d in can.OrderByDescending(x => x.Phi.TongTien).Take(8))
                s.Append("<br>· <b>").Append(d.Cont.SoCont).Append("</b> · ").Append(d.TenKhach)
                 .Append(" · ").Append(d.Phi.NgayTinhPhi).Append(" ngày · <b>").Append(Tien(d.Phi.TongTien)).Append("</b>");
            if (can.Count > 8) s.Append("<br><i>… và ").Append(can.Count - 8).Append(" container nữa</i>");

            kq.TraLoi = s.ToString();
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "ChotCongNoLoat", ThamSo = "",
                MoTa = "Lập " + can.Count + " phiếu thu, tổng " + Tien(tong) + "?",
                CanhBao = "Mỗi phiếu được đánh số riêng và sinh chứng từ riêng. Hủy được từng phiếu, "
                        + "nhưng số phiếu đã cấp thì không dùng lại — kế toán cần dãy số liền mạch."
            };
            return kq;
        }

        private static KetQua ChotCongNoLoatThat(AppDb db, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Chốt công nợ hàng loạt" };
            var bangKe = LuuBaiService.BangKeDangChay(db);
            var can = bangKe == null ? new List<LuuBaiService.DongBangKe>()
                    : bangKe.Where(d => !d.DaCoPhieu && d.Phi != null && d.Phi.TongTien > 0).ToList();

            int n = 0, daCoSan = 0; decimal tong = 0; var soPhieu = new List<string>();
            var batDau = DateTime.Now;
            foreach (var d in can)
            {
                var c = db.Containers.FirstOrDefault(x => x.MaCont == d.Cont.MaCont);
                if (c == null) continue;
                var p = LuuBaiService.LapPhieu(db, c, nguoiDung);
                if (p == null) continue;
                // Phieu co NgayLap truoc luc bat dau loat = phieu CU do LapPhieu tra ve
                // (co nguoi vua chot trong luc minh dang hoi). Khong dem vao "vua lap".
                if (p.NgayLap < batDau) { daCoSan++; continue; }
                n++; tong += p.TongTien;
                if (soPhieu.Count < 5) soPhieu.Add(p.SoPhieu);
            }
            if (n > 0) db.SaveChanges();

            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Trợ lý chốt công nợ loạt: " + n + " phiếu, " + tong.ToString("N0") + "đ");

            kq.TraLoi = n == 0
                ? (daCoSan > 0
                    ? "Không lập phiếu nào — <b>" + daCoSan + " container</b> vừa được người khác chốt trước tôi."
                    : "Không lập được phiếu nào.")
                : "Đã lập <b>" + n + " phiếu</b> — tổng <b>" + Tien(tong) + "</b>."
                  + "<br><i>" + string.Join(" · ", soPhieu) + (n > 5 ? " …" : "") + "</i>"
                  + (daCoSan > 0 ? "<br>(" + daCoSan + " container đã có người chốt trước, tôi bỏ qua.)" : "");
            kq.LienKets.Add(LK("Danh sách phiếu", "/Admin/LuuBai/DanhSach", "bi-receipt"));
            return kq;
        }

        // ============================================================
        // DUYET LOAT BOOKING / EOR
        // ============================================================

        private static KetQua ChuanBiDuyetLoatBooking(AppDb db)
        {
            var kq = new KetQua { CongCu = "Duyệt hàng loạt booking" };
            var ds = db.DatDichVus.AsNoTracking().Include(b => b.KhachHang)
                       .Where(b => b.TrangThai == "Chờ duyệt").OrderBy(b => b.NgayTao).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có booking nào chờ duyệt."; return kq; }

            var tong = ds.Sum(b => b.TongTien);
            var s = new StringBuilder("<b>" + ds.Count + " booking</b> đang chờ — tổng giá trị <b>" + Tien(tong) + "</b>:");
            foreach (var b in ds.Take(8))
                s.Append("<br>· <b>").Append(b.MaSoBooking).Append("</b> · ")
                 .Append(b.KhachHang != null ? b.KhachHang.TenCongTy : "").Append(" · ").Append(Tien(b.TongTien));
            if (ds.Count > 8) s.Append("<br><i>… và ").Append(ds.Count - 8).Append(" booking nữa</i>");

            kq.TraLoi = s.ToString();
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "DuyetLoatBooking", ThamSo = "",
                MoTa = "Duyệt cả " + ds.Count + " booking (" + Tien(tong) + ")?",
                CanhBao = "Duyệt là CAM KẾT với khách — khách thấy trạng thái mới ngay và bắt đầu trông chờ dịch vụ. "
                        + "Đọc lại danh sách trên trước khi bấm; nếu có cái nào lấn cấn thì thoát ra, "
                        + "duyệt từng cái bằng “duyệt booking BK…”."
            };
            return kq;
        }

        private static KetQua DuyetLoatBookingThat(AppDb db, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Duyệt hàng loạt booking" };
            var ds = db.DatDichVus.Where(b => b.TrangThai == "Chờ duyệt").ToList();
            foreach (var b in ds) b.TrangThai = "Đã duyệt";
            if (ds.Count > 0) db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Trợ lý duyệt loạt " + ds.Count + " booking");
            kq.TraLoi = ds.Count == 0 ? "Không còn booking nào chờ duyệt."
                      : "Đã duyệt <b>" + ds.Count + " booking</b>: <i>"
                        + string.Join(" · ", ds.Take(6).Select(b => b.MaSoBooking)) + (ds.Count > 6 ? " …" : "") + "</i>";
            kq.LienKets.Add(LK("Danh sách booking", "/Admin/Bookings", "bi-journal-check"));
            return kq;
        }

        private static KetQua ChuanBiDuyetLoatEor(AppDb db)
        {
            var kq = new KetQua { CongCu = "Duyệt hàng loạt EOR" };
            var ds = db.PhieuSuaChuas.AsNoTracking().Where(e => e.TrangThai == "Chờ duyệt").ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có EOR nào chờ duyệt."; return kq; }

            var tong = ds.Where(e => e.ChiPhiUocTinh != null).Sum(e => e.ChiPhiUocTinh.Value);
            var s = new StringBuilder("<b>" + ds.Count + " EOR</b> chờ duyệt — ước tính <b>" + Tien(tong) + "</b>:");
            foreach (var e in ds.Take(8))
                s.Append("<br>· <b>").Append(e.SoEOR).Append("</b> · ").Append(e.SoCont).Append(" · ").Append(e.ViTriHuHong)
                 .Append(e.ChiPhiUocTinh != null ? " · " + Tien(e.ChiPhiUocTinh.Value) : "");
            if (ds.Count > 8) s.Append("<br><i>… và ").Append(ds.Count - 8).Append(" phiếu nữa</i>");

            kq.TraLoi = s.ToString();
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "DuyetLoatEor", ThamSo = "",
                MoTa = "Duyệt cả " + ds.Count + " EOR (ước tính " + Tien(tong) + ")?",
                CanhBao = "Sau khi duyệt, xưởng được phép sửa và tính chi phí cho hãng tàu. "
                        + "Đây là tiền của hãng — duyệt loạt chỉ nên làm khi bạn đã xem ảnh hư hỏng của cả lô."
            };
            return kq;
        }

        private static KetQua DuyetLoatEorThat(AppDb db, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Duyệt hàng loạt EOR" };
            var ds = db.PhieuSuaChuas.Where(e => e.TrangThai == "Chờ duyệt").ToList();
            foreach (var e in ds) { e.TrangThai = "Đã duyệt"; e.NgayDuyet = DateTime.Now; }
            if (ds.Count > 0) db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Trợ lý duyệt loạt " + ds.Count + " EOR");
            kq.TraLoi = ds.Count == 0 ? "Không còn EOR nào chờ duyệt."
                      : "Đã duyệt <b>" + ds.Count + " EOR</b>: <i>"
                        + string.Join(" · ", ds.Take(6).Select(e => e.SoEOR)) + (ds.Count > 6 ? " …" : "") + "</i>";
            kq.LienKets.Add(LK("Xưởng depot", "/Admin/Depot", "bi-tools"));
            return kq;
        }

        // ============================================================
        // TIEN — GHI NHAN DA THU
        // ============================================================

        private static KetQua ChuanBiGhiNhanThu(AppDb db, string soMa)
        {
            var kq = new KetQua { CongCu = "Ghi nhận đã thu tiền" };
            if (soMa == null)
            {
                kq.TraLoi = "Thu tiền phiếu nào? Ví dụ <i>“đã thu phiếu PLB-2609-0030”</i>.";
                kq.LienKets.Add(LK("Xem phiếu chờ thu", "#goi:công nợ hôm nay", "bi-list"));
                return kq;
            }
            var p = db.PhieuLuuBais.AsNoTracking().FirstOrDefault(x => x.SoPhieu == soMa);
            if (p == null) { kq.TraLoi = "Không tìm thấy phiếu <b>" + soMa + "</b>."; return kq; }
            if (p.TrangThai == "Đã thu")
            { kq.TraLoi = "Phiếu <b>" + p.SoPhieu + "</b> đã ghi nhận thu ngày " + (p.NgayThu != null ? p.NgayThu.Value.ToString("dd/MM/yyyy") : "") + " rồi."; return kq; }
            if (p.TrangThai == "Hủy") { kq.TraLoi = "Phiếu <b>" + p.SoPhieu + "</b> đã bị hủy — không thu được."; return kq; }

            kq.TraLoi = "Phiếu <b>" + p.SoPhieu + "</b> · " + p.SoCont + " · <b>" + Tien(p.TongTien) + "</b>"
                      + "<br>Lập ngày " + p.NgayLap.ToString("dd/MM/yyyy") + " · treo <b>" + p.SoNgayTreo + " ngày</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "GhiNhanThu", ThamSo = p.MaPhieu.ToString(),
                MoTa = "Ghi nhận đã thu " + Tien(p.TongTien) + " của phiếu " + p.SoPhieu + "?",
                CanhBao = "Chỉ bấm khi tiền đã THỰC SỰ về — phiếu đã thu thì không hủy được nữa, "
                        + "muốn sửa phải lập phiếu điều chỉnh. Mặc định ghi hình thức “Chuyển khoản”."
            };
            return kq;
        }

        private static KetQua GhiNhanThuThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Ghi nhận đã thu tiền" };
            int id; if (!int.TryParse(thamSo, out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var p = db.PhieuLuuBais.Find(id);
            if (p == null) { kq.TraLoi = "Không tìm thấy phiếu."; return kq; }
            if (p.TrangThai == "Đã thu") { kq.TraLoi = "Phiếu này đã thu trước đó rồi."; return kq; }
            // Cung quy tac voi LuuBaiController.ThuTien
            p.TrangThai = "Đã thu";
            p.NgayThu = DateTime.Now;
            p.HinhThucThu = "Chuyển khoản";
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung,
                "Thu tiền phiếu " + p.SoPhieu + " — " + p.TongTien.ToString("N0") + "đ (trợ lý)");
            kq.TraLoi = "Đã ghi nhận thu <b>" + Tien(p.TongTien) + "</b> của phiếu <b>" + p.SoPhieu + "</b>.";
            kq.LienKets.Add(LK("Mở phiếu", "/Admin/LuuBai/Phieu/" + p.MaPhieu, "bi-receipt"));
            return kq;
        }

        private static KetQua TraCuuPhieuLuuBai(AppDb db, string soPhieu)
        {
            var kq = new KetQua { CongCu = "Phiếu lưu bãi" };
            var p = db.PhieuLuuBais.AsNoTracking().Include(x => x.KhachHang)
                      .FirstOrDefault(x => x.SoPhieu == soPhieu);
            if (p == null) { kq.TraLoi = "Không tìm thấy phiếu <b>" + soPhieu + "</b>."; return kq; }

            var s = new StringBuilder("<b>" + p.SoPhieu + "</b> · " + p.SoCont + " · <b>" + p.TrangThai + "</b>");
            s.Append("<br>Khách: ").Append(p.KhachHang != null ? p.KhachHang.TenCongTy : "—");
            s.Append("<br>Từ ").Append(p.TuNgay.ToString("dd/MM")).Append(" đến ").Append(p.DenNgay.ToString("dd/MM"))
             .Append(" · ").Append(p.TongNgay).Append(" ngày (miễn ").Append(p.NgayMienPhi)
             .Append(", tính ").Append(p.NgayTinhPhi).Append(")");
            s.Append("<br>Lưu bãi ").Append(Tien(p.PhiLuuBai));
            if (p.PhiDien > 0) s.Append(" + điện lạnh ").Append(Tien(p.PhiDien));
            s.Append(" = <b>").Append(Tien(p.TongTien)).Append("</b>");
            if (p.TrangThai == "Đã thu" && p.NgayThu != null)
                s.Append("<br>Đã thu ").Append(p.NgayThu.Value.ToString("dd/MM/yyyy")).Append(" · ").Append(p.HinhThucThu);
            else if (p.TrangThai == "Chờ thu")
                s.Append("<br>Đang treo <b>").Append(p.SoNgayTreo).Append(" ngày</b>");

            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Mở phiếu " + p.SoPhieu, "/Admin/LuuBai/Phieu/" + p.MaPhieu, "bi-receipt"));
            if (p.TrangThai == "Chờ thu")
                kq.LienKets.Add(LK("Ghi nhận đã thu", "#goi:đã thu phiếu " + p.SoPhieu, "bi-cash-coin"));
            return kq;
        }

        // ============================================================
        // TAU & CAU BEN
        // ============================================================

        private static KetQua LichTau(AppDb db)
        {
            var kq = new KetQua { CongCu = "Lịch tàu" };
            var ds = db.ChuyenTaus.AsNoTracking().Include(c => c.Tau).Include(c => c.Ben)
                       .Where(c => c.TrangThai != "Hủy" && c.TrangThai != "Đã rời bến")
                       .OrderBy(c => c.ETA).Take(10).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có chuyến tàu nào đang mở."; kq.LienKets.Add(LK("Lịch tàu", "/Admin/VesselCalls", "bi-calendar-week")); return kq; }

            var s = new StringBuilder("<b>" + ds.Count + " chuyến</b> đang mở:");
            foreach (var c in ds)
            {
                var conLai = (c.ETA - DateTime.Now).TotalHours;
                s.Append("<br>· <b>").Append(c.MaChuyenHieu).Append("</b> · ")
                 .Append(c.Tau != null ? c.Tau.TenTau : "—")
                 .Append(" · ETA ").Append(c.ETA.ToString("dd/MM HH:mm"))
                 .Append(" · ").Append(c.TrangThai)
                 .Append(c.Ben != null ? " · " + c.Ben.TenBen : " · <i>chưa xếp bến</i>");
                if (c.TrangThai == "Dự kiến" && conLai > 0 && conLai < 48)
                    s.Append(" <span class='tl-do'>còn " + (int)conLai + "h</span>");
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Lịch tàu", "/Admin/VesselCalls", "bi-calendar-week"));
            kq.LienKets.Add(LK("Xem cầu bến trống", "#goi:bến nào trống", "bi-water"));
            return kq;
        }

        private static KetQua CauBen(AppDb db)
        {
            var kq = new KetQua { CongCu = "Cầu bến" };
            var bens = db.Bens.AsNoTracking().ToList();
            if (bens.Count == 0) { kq.TraLoi = "Chưa khai báo cầu bến nào."; return kq; }

            var dangDung = db.ChuyenTaus.AsNoTracking()
                             .Where(c => c.MaBen != null && (c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng"))
                             .ToList();

            var s = new StringBuilder("<b>" + bens.Count + " cầu bến</b>:");
            foreach (var b in bens)
            {
                var dang = dangDung.FirstOrDefault(c => c.MaBen == b.MaBen);
                s.Append("<br>· <b>").Append(b.TenBen).Append("</b> · dài ").Append(b.ChieuDai.ToString("#,##0"))
                 .Append("m · sâu ").Append(b.DoSau.ToString("0.0")).Append("m · ")
                 .Append(dang != null
                     ? "<span class='tl-do'>đang có " + dang.MaChuyenHieu + "</span>"
                     : b.TrangThai == "Hoạt động" ? "<span class='tl-xanh'>trống</span>" : b.TrangThai);
            }

            // Goi y AI cho cac chuyen chua xep ben
            var goiY = BerthService.Suggest(db);
            if (goiY != null && goiY.Count > 0)
            {
                s.Append("<br><br><b>Gợi ý xếp bến</b> cho chuyến chưa có bến:");
                foreach (var g in goiY.Take(3))
                {
                    var tot = g.Options != null ? g.Options.OrderByDescending(o => o.Score).FirstOrDefault() : null;
                    if (tot == null) continue;
                    s.Append("<br>· ").Append(g.Chuyen.MaChuyenHieu).Append(" → <b>").Append(tot.Ben.TenBen)
                     .Append("</b> (điểm ").Append(tot.Score).Append(", dự kiến ").Append(tot.GioLamHang.ToString("0.#")).Append("h)");
                    kq.LienKets.Add(LK("Xếp " + tot.Ben.TenBen + " cho " + g.Chuyen.MaChuyenHieu,
                        "#goi:xếp bến " + tot.Ben.MaBen + " cho chuyến " + g.Chuyen.MaChuyenHieu, "bi-water"));
                }
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Sơ đồ cầu bến", "/Admin/Berths", "bi-water"));
            return kq;
        }

        private static ChuyenTau TimChuyen(AppDb db, string tho)
        {
            var hoa = (tho ?? "").ToUpperInvariant();
            var ds = db.ChuyenTaus.AsNoTracking().Include(c => c.Tau).ToList();
            var c1 = ds.FirstOrDefault(c => !string.IsNullOrEmpty(c.MaChuyenHieu)
                                         && hoa.Contains(c.MaChuyenHieu.ToUpperInvariant()));
            if (c1 != null) return c1;
            var t = BoDau(tho).ToLowerInvariant();
            return ds.FirstOrDefault(c => c.Tau != null && !string.IsNullOrEmpty(c.Tau.TenTau)
                                       && t.Contains(BoDau(c.Tau.TenTau).ToLowerInvariant()));
        }

        private static KetQua ChuanBiXepBen(AppDb db, string tho, int? soNguyen)
        {
            var kq = new KetQua { CongCu = "Xếp cầu bến" };
            var c = TimChuyen(db, tho);
            if (c == null)
            {
                kq.TraLoi = "Xếp bến cho chuyến nào? Ví dụ <i>“xếp bến 2 cho chuyến VS2601”</i>.";
                kq.LienKets.Add(LK("Xem lịch tàu", "#goi:tàu nào sắp đến", "bi-calendar-week"));
                return kq;
            }

            Ben ben = null;
            if (soNguyen != null) ben = db.Bens.AsNoTracking().FirstOrDefault(b => b.MaBen == soNguyen);
            if (ben == null)
            {
                // Khong chi dinh ben -> lay goi y tot nhat cua BerthService
                var goiY = BerthService.Suggest(db);
                var g = goiY == null ? null : goiY.FirstOrDefault(x => x.Chuyen.MaChuyen == c.MaChuyen);
                var tot = g == null || g.Options == null ? null : g.Options.OrderByDescending(o => o.Score).FirstOrDefault();
                if (tot == null)
                {
                    kq.TraLoi = "Chưa gợi ý được bến cho <b>" + c.MaChuyenHieu + "</b>. Cho tôi số bến, ví dụ <i>“xếp bến 2 cho chuyến "
                              + c.MaChuyenHieu + "”</i>.";
                    kq.LienKets.Add(LK("Sơ đồ cầu bến", "/Admin/Berths", "bi-water"));
                    return kq;
                }
                ben = tot.Ben;
                kq.TraLoi = "Chuyến <b>" + c.MaChuyenHieu + "</b> chưa có bến. AI chấm <b>" + ben.TenBen
                          + "</b> cao nhất (" + tot.Score + " điểm)"
                          + (tot.LyDo != null && tot.LyDo.Count > 0 ? "<br><i>" + string.Join(" · ", tot.LyDo) + "</i>" : "");
            }
            else
            {
                kq.TraLoi = "Xếp chuyến <b>" + c.MaChuyenHieu + "</b> (" + (c.Tau != null ? c.Tau.TenTau : "") + ") vào <b>"
                          + ben.TenBen + "</b> — dài " + ben.ChieuDai.ToString("#,##0") + "m, sâu " + ben.DoSau.ToString("0.0") + "m.";
            }

            var dangDung = db.ChuyenTaus.AsNoTracking().FirstOrDefault(
                x => x.MaBen == ben.MaBen && x.MaChuyen != c.MaChuyen
                  && (x.TrangThai == "Đã cập bến" || x.TrangThai == "Đang làm hàng"));

            kq.CanXacNhan = new XacNhan
            {
                CongCu = "XepBen", ThamSo = c.MaChuyen + "|" + ben.MaBen,
                MoTa = "Xếp " + c.MaChuyenHieu + " vào " + ben.TenBen + "?",
                CanhBao = dangDung != null
                    ? "CẨN THẬN: " + ben.TenBen + " đang có chuyến " + dangDung.MaChuyenHieu
                      + " nằm. Xếp chồng hai tàu một bến là lỗi điều độ nặng."
                    : "Đổi bến được bất cứ lúc nào trước khi tàu cập. Điều độ và hoa tiêu đọc thông tin này."
            };
            return kq;
        }

        private static KetQua XepBenThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Xếp cầu bến" };
            var p = (thamSo ?? "").Split('|');
            int maChuyen, maBen;
            if (p.Length != 2 || !int.TryParse(p[0], out maChuyen) || !int.TryParse(p[1], out maBen))
            { kq.TraLoi = "Tham số không hợp lệ."; return kq; }

            var c = db.ChuyenTaus.Find(maChuyen);
            var b = db.Bens.Find(maBen);
            if (c == null || b == null) { kq.TraLoi = "Không tìm thấy chuyến hoặc bến."; return kq; }
            c.MaBen = maBen;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Xếp " + c.MaChuyenHieu + " vào " + b.TenBen + " (trợ lý)");
            kq.TraLoi = "Đã xếp <b>" + c.MaChuyenHieu + "</b> vào <b>" + b.TenBen + "</b>.";
            kq.LienKets.Add(LK("Sơ đồ cầu bến", "/Admin/Berths", "bi-water"));
            return kq;
        }

        private static KetQua ChuanBiChuyenTau(AppDb db, string tho, string trangThaiMoi)
        {
            var kq = new KetQua { CongCu = "Cập nhật chuyến tàu" };
            var c = TimChuyen(db, tho);
            if (c == null)
            {
                kq.TraLoi = "Chuyến nào? Ví dụ <i>“chuyến VS2601 đã cập bến”</i>.";
                kq.LienKets.Add(LK("Xem lịch tàu", "#goi:tàu nào sắp đến", "bi-calendar-week"));
                return kq;
            }
            if (c.TrangThai == trangThaiMoi)
            { kq.TraLoi = "Chuyến <b>" + c.MaChuyenHieu + "</b> đã ở trạng thái <b>" + trangThaiMoi + "</b> rồi."; return kq; }
            if (trangThaiMoi == "Đã cập bến" && c.MaBen == null)
            {
                kq.TraLoi = "Chuyến <b>" + c.MaChuyenHieu + "</b> <b>chưa được xếp bến</b> — không thể ghi cập bến. "
                          + "Xếp bến trước đã.";
                kq.LienKets.Add(LK("Xếp bến cho " + c.MaChuyenHieu, "#goi:xếp bến cho chuyến " + c.MaChuyenHieu, "bi-water"));
                return kq;
            }

            kq.TraLoi = "Chuyến <b>" + c.MaChuyenHieu + "</b> · " + (c.Tau != null ? c.Tau.TenTau : "")
                      + " · đang <b>" + c.TrangThai + "</b> · ETA " + c.ETA.ToString("dd/MM HH:mm")
                      + " · ETD " + c.ETD.ToString("dd/MM HH:mm");
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "ChuyenTau", ThamSo = c.MaChuyen + "|" + trangThaiMoi,
                MoTa = "Chuyển " + c.MaChuyenHieu + " → " + trangThaiMoi + "?",
                CanhBao = trangThaiMoi == "Đã cập bến"
                    ? "Giờ cập bến thật (ATA) sẽ ghi là bây giờ — đây là mốc tính thời gian làm hàng và phí cầu bến."
                    : trangThaiMoi == "Đã rời bến"
                    ? "Giờ rời bến thật (ATD) ghi là bây giờ, và bến được giải phóng cho chuyến sau."
                    : "Điều độ và khách theo dõi trạng thái này theo thời gian thực."
            };
            return kq;
        }

        private static KetQua ChuyenTauThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Cập nhật chuyến tàu" };
            var p = (thamSo ?? "").Split('|');
            int id; if (p.Length != 2 || !int.TryParse(p[0], out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var c = db.ChuyenTaus.Find(id);
            if (c == null) { kq.TraLoi = "Không tìm thấy chuyến."; return kq; }

            c.TrangThai = p[1];
            // Cung quy tac voi VesselCallsController: cap ben ghi ATA, roi ben ghi ATD.
            if (p[1] == "Đã cập bến" && c.ATA == null) c.ATA = DateTime.Now;
            if (p[1] == "Đã rời bến" && c.ATD == null) c.ATD = DateTime.Now;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Chuyến " + c.MaChuyenHieu + " → " + p[1] + " (trợ lý)");
            kq.TraLoi = "Chuyến <b>" + c.MaChuyenHieu + "</b> → <b>" + c.TrangThai + "</b>"
                      + (c.ATA != null && p[1] == "Đã cập bến" ? "<br>ATA ghi " + c.ATA.Value.ToString("dd/MM HH:mm") : "")
                      + (c.ATD != null && p[1] == "Đã rời bến" ? "<br>ATD ghi " + c.ATD.Value.ToString("dd/MM HH:mm") : "");
            kq.LienKets.Add(LK("Lịch tàu", "/Admin/VesselCalls", "bi-calendar-week"));
            return kq;
        }

        // ============================================================
        // BAI — DI CHUYEN / DOI TRANG THAI CONTAINER
        // ============================================================

        /// <summary>Vi tri bai co dang A-03-01-1 (khu - block - hang - tang)</summary>
        private static string TimViTriBai(string s)
        {
            var m = Regex.Match((s ?? "").ToUpperInvariant(), @"\b([ABCRXE]-\d{2}-\d{2}-\d)\b");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static KetQua ChuanBiDiChuyenCont(AppDb db, string tho, string soCont)
        {
            var kq = new KetQua { CongCu = "Di chuyển container" };
            if (soCont == null) { kq.TraLoi = "Chuyển container nào? Ví dụ <i>“chuyển MSCU2277456 sang B-01-05-2”</i>."; return kq; }
            var c = db.Containers.AsNoTracking().FirstOrDefault(x => x.SoCont == soCont);
            if (c == null) { kq.TraLoi = "Không tìm thấy container <b>" + soCont + "</b>."; return kq; }

            var viTri = TimViTriBai(tho);
            if (viTri == null)
            {
                var g = YardService.Suggest(db, c.LoaiCont, c.TrangThai, c.TinhTrang, c.NhietDo);
                kq.TraLoi = "Chuyển <b>" + soCont + "</b> đi đâu? Cho tôi vị trí dạng <i>B-01-05-2</i>."
                          + (g != null ? "<br><br>Gợi ý của hệ thống: <b>" + g.ViTri + "</b>"
                                         + (g.LyDo != null && g.LyDo.Count > 0 ? "<br><i>" + string.Join(" · ", g.LyDo) + "</i>" : "")
                                       : "");
                if (g != null) kq.LienKets.Add(LK("Chuyển sang " + g.ViTri, "#goi:chuyển " + soCont + " sang " + g.ViTri, "bi-arrow-left-right"));
                kq.LienKets.Add(LK("Sơ đồ bãi", "/Admin/Containers/YardMap", "bi-grid-3x3"));
                return kq;
            }

            var dangO = db.Containers.AsNoTracking().FirstOrDefault(x => x.ViTriBai == viTri && x.MaCont != c.MaCont);
            kq.TraLoi = "Container <b>" + soCont + "</b> đang ở <b>" + (c.ViTriBai ?? "chưa có vị trí")
                      + "</b> → chuyển sang <b>" + viTri + "</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "DiChuyenCont", ThamSo = c.MaCont + "|" + viTri,
                MoTa = "Chuyển " + soCont + " sang " + viTri + "?",
                CanhBao = dangO != null
                    ? "CẨN THẬN: ô " + viTri + " đang ghi nhận container " + dangO.SoCont + ". Kiểm tra lại thực địa."
                    : "Chỉ đổi vị trí trên hệ thống — xe nâng vẫn phải làm thật ngoài bãi. Đổi lại được."
            };
            return kq;
        }

        private static KetQua DiChuyenContThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Di chuyển container" };
            var p = (thamSo ?? "").Split('|');
            int id; if (p.Length != 2 || !int.TryParse(p[0], out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var c = db.Containers.Find(id);
            if (c == null) { kq.TraLoi = "Không tìm thấy container."; return kq; }
            var cu = c.ViTriBai;
            c.ViTriBai = p[1];
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Chuyển " + c.SoCont + ": " + (cu ?? "—") + " → " + p[1] + " (trợ lý)");
            kq.TraLoi = "Đã chuyển <b>" + c.SoCont + "</b>: " + (cu ?? "—") + " → <b>" + p[1] + "</b>.";
            kq.LienKets.Add(LK("Hồ sơ container", "/Admin/Containers/HoSo/" + c.MaCont, "bi-box-seam"));
            kq.LienKets.Add(LK("Sơ đồ bãi", "/Admin/Containers/YardMap", "bi-grid-3x3"));
            return kq;
        }

        private static KetQua ChuanBiDoiTrangThaiCont(AppDb db, string tho, string t, string soCont)
        {
            var kq = new KetQua { CongCu = "Đổi trạng thái container" };
            if (soCont == null) { kq.TraLoi = "Container nào? Ví dụ <i>“MSCU2277456 đã ra bãi”</i>."; return kq; }
            var c = db.Containers.AsNoTracking().FirstOrDefault(x => x.SoCont == soCont);
            if (c == null) { kq.TraLoi = "Không tìm thấy container <b>" + soCont + "</b>."; return kq; }

            // "đã ra bãi" / "đã giao khách" -> trạng thái CHUẨN "Đã giao khách" (danh mục: Trong bãi, Chờ xuất tàu,
            // Đã xuất tàu, Đã giao khách). Trước đây ghi "Đã ra bãi" — chuỗi lạ, bảng kê / đồng hồ lưu bãi không nhận ra.
            string moi = Co(t, "da ra bai", "da giao khach") ? "Đã giao khách"
                       : Co(t, "cho xuat tau") ? "Chờ xuất tàu"
                       : Co(t, "da vao bai", "trong bai") ? "Trong bãi" : null;
            if (moi == null)
            {
                kq.TraLoi = "Đổi <b>" + soCont + "</b> sang trạng thái nào? Tôi nhận: "
                          + "<i>đã vào bãi</i> · <i>chờ xuất tàu</i> · <i>đã ra bãi</i>.";
                return kq;
            }
            if (c.TrangThai == moi) { kq.TraLoi = "Container <b>" + soCont + "</b> đang là <b>" + moi + "</b> rồi."; return kq; }

            var phi = LuuBaiService.Tinh(db, c);
            var canhBao = "Trạng thái này quyết định container có bị tính phí lưu bãi tiếp hay không.";
            // Phan CHUA CHOT cua LUOT NAY (ke ca phat sinh sau lan chot giua ky) — khong phai "co phieu nao chua"
            var chuaChot = moi == "Đã giao khách" ? LuuBaiService.TinhPhanChuaChot(db, c) : null;
            if (chuaChot != null)
                canhBao = "CHÚ Ý: container này còn <b>" + Tien(chuaChot.TongTien) + "</b> phí lưu bãi CHƯA chốt phiếu"
                        + (chuaChot.SauPhieu != null ? " (phát sinh sau phiếu " + chuaChot.SauPhieu.SoPhieu + ")" : "") + ". "
                        + "Cho ra bãi trước khi chốt phí là để tiền đi theo cont ra cổng. Chốt phí trước đi.";

            kq.TraLoi = "Container <b>" + soCont + "</b> · " + c.LoaiCont + " · đang <b>" + c.TrangThai + "</b>"
                      + (c.ViTriBai != null ? " · " + c.ViTriBai : "") + " → <b>" + moi + "</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "DoiTrangThaiCont", ThamSo = c.MaCont + "|" + moi,
                MoTa = "Chuyển " + soCont + " → " + moi + "?",
                CanhBao = canhBao
            };
            if (chuaChot != null)
                kq.LienKets.Add(LK("Lập phiếu thu trước", "#goi:lập phiếu cho " + soCont, "bi-receipt"));
            return kq;
        }

        private static KetQua DoiTrangThaiContThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Đổi trạng thái container" };
            var p = (thamSo ?? "").Split('|');
            int id; if (p.Length != 2 || !int.TryParse(p[0], out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var c = db.Containers.Find(id);
            if (c == null) { kq.TraLoi = "Không tìm thấy container."; return kq; }
            var cu = c.TrangThai;
            c.TrangThai = p[1];
            // Ra bai thi dong moc ngay ra — moc nay la can cu chot phi luu bai.
            if ((p[1] == "Đã giao khách" || p[1] == "Đã ra bãi") && c.NgayRaBai == null) c.NgayRaBai = DateTime.Now;
            if (p[1] == "Trong bãi" && c.NgayVaoBai == null) c.NgayVaoBai = DateTime.Now;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Container " + c.SoCont + ": " + cu + " → " + p[1] + " (trợ lý)");
            kq.TraLoi = "Container <b>" + c.SoCont + "</b>: " + cu + " → <b>" + c.TrangThai + "</b>.";
            kq.LienKets.Add(LK("Hồ sơ container", "/Admin/Containers/HoSo/" + c.MaCont, "bi-box-seam"));
            return kq;
        }

        // ============================================================
        // CONG & XE
        // ============================================================

        private static KetQua TheXeSapHetHan(AppDb db)
        {
            var kq = new KetQua { CongCu = "Thẻ xe" };
            var moc = DateTime.Today.AddDays(30);
            var ds = db.TheXes.AsNoTracking().Include(x => x.KhachHang)
                       .Where(x => x.KichHoat && x.HanDen != null && x.HanDen <= moc)
                       .OrderBy(x => x.HanDen).Take(10).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có thẻ xe nào sắp hết hạn trong 30 ngày."; kq.LienKets.Add(LK("Quản lý thẻ xe", "/Admin/Gate", "bi-credit-card")); return kq; }

            var s = new StringBuilder("<b>" + ds.Count + " thẻ xe</b> sắp/đã hết hạn:");
            foreach (var x in ds)
            {
                var conLai = (x.HanDen.Value.Date - DateTime.Today).Days;
                s.Append("<br>· <b>").Append(x.BienSo).Append("</b> · ").Append(x.LoaiThe)
                 .Append(" · ").Append(x.KhachHang != null ? x.KhachHang.TenCongTy : x.TenChuXe)
                 .Append(conLai < 0 ? " · <span class='tl-do'>hết hạn " + (-conLai) + " ngày</span>"
                                    : " · còn <b>" + conLai + "</b> ngày");
                if (x.LoaiThe == "Trả trước") s.Append(" · số dư ").Append(Tien(x.SoDu));
                kq.LienKets.Add(LK("Gia hạn " + x.BienSo, "#goi:gia hạn thẻ xe " + x.BienSo, "bi-arrow-clockwise"));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Quản lý thẻ xe", "/Admin/Gate", "bi-credit-card"));
            return kq;
        }

        private static KetQua ChuanBiGiaHanThe(AppDb db, string bienSo, string t)
        {
            var kq = new KetQua { CongCu = "Gia hạn thẻ xe" };
            if (bienSo == null)
            {
                kq.TraLoi = "Gia hạn thẻ của xe nào? Ví dụ <i>“gia hạn thẻ xe 61C-12345”</i>.";
                kq.LienKets.Add(LK("Thẻ sắp hết hạn", "#goi:thẻ xe nào sắp hết hạn", "bi-list"));
                return kq;
            }
            var the = db.TheXes.AsNoTracking().FirstOrDefault(x => x.BienSo == bienSo);
            if (the == null) { kq.TraLoi = "Chưa có thẻ nào cho xe <b>" + bienSo + "</b>. Cấp thẻ mới ở màn hình Cổng."; kq.LienKets.Add(LK("Quản lý thẻ xe", "/Admin/Gate", "bi-credit-card")); return kq; }

            var soThang = TimSoThang(t) ?? 12;
            var moc = the.HanDen != null && the.HanDen > DateTime.Today ? the.HanDen.Value : DateTime.Today;
            var hanMoi = moc.AddMonths(soThang);

            kq.TraLoi = "Thẻ <b>" + the.BienSo + "</b> · " + the.LoaiThe + " · hạn hiện tại <b>"
                      + (the.HanDen != null ? the.HanDen.Value.ToString("dd/MM/yyyy") : "chưa có") + "</b>"
                      + "<br>Gia hạn <b>" + soThang + " tháng</b> → hạn mới <b>" + hanMoi.ToString("dd/MM/yyyy") + "</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "GiaHanThe", ThamSo = the.MaThe + "|" + soThang,
                MoTa = "Gia hạn thẻ " + the.BienSo + " thêm " + soThang + " tháng?",
                CanhBao = "Chỉ gia hạn khi khách đã đóng tiền thẻ. Muốn số tháng khác thì gõ kèm, "
                        + "ví dụ “gia hạn thẻ xe " + the.BienSo + " 6 tháng”."
            };
            return kq;
        }

        private static KetQua GiaHanTheThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Gia hạn thẻ xe" };
            var p = (thamSo ?? "").Split('|');
            int id, thang;
            if (p.Length != 2 || !int.TryParse(p[0], out id) || !int.TryParse(p[1], out thang))
            { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var the = db.TheXes.Find(id);
            if (the == null) { kq.TraLoi = "Không tìm thấy thẻ."; return kq; }
            var moc = the.HanDen != null && the.HanDen > DateTime.Today ? the.HanDen.Value : DateTime.Today;
            the.HanDen = moc.AddMonths(thang);
            the.KichHoat = true;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Gia hạn thẻ " + the.BienSo + " thêm " + thang + " tháng (trợ lý)");
            kq.TraLoi = "Đã gia hạn thẻ <b>" + the.BienSo + "</b> → hạn <b>" + the.HanDen.Value.ToString("dd/MM/yyyy") + "</b>.";
            kq.LienKets.Add(LK("Quản lý thẻ xe", "/Admin/Gate", "bi-credit-card"));
            return kq;
        }

        private static KetQua PhieuCanGanDay(AppDb db)
        {
            var kq = new KetQua { CongCu = "Phiếu cân" };
            var tu = DateTime.Today;
            var ds = db.PhieuCans.AsNoTracking().Where(p => p.Luc1 >= tu)
                       .OrderByDescending(p => p.Luc1).Take(10).ToList();
            if (ds.Count == 0)
            {
                ds = db.PhieuCans.AsNoTracking().OrderByDescending(p => p.Luc1).Take(5).ToList();
                if (ds.Count == 0) { kq.TraLoi = "Chưa có phiếu cân nào."; kq.LienKets.Add(LK("Trạm cân", "/Admin/Gate/Can", "bi-speedometer2")); return kq; }
                kq.TraLoi = "Hôm nay chưa cân xe nào. <b>" + ds.Count + " phiếu</b> gần nhất:";
            }
            else kq.TraLoi = "Hôm nay có <b>" + ds.Count + " phiếu cân</b>:";

            var s = new StringBuilder(kq.TraLoi);
            int chuaXong = 0;
            foreach (var p in ds)
            {
                s.Append("<br>· <b>").Append(p.SoPhieu).Append("</b> · ").Append(p.BienSoXe)
                 .Append(p.SoCont != null ? " · " + p.SoCont : "")
                 .Append(" · lần 1 ").Append(p.CanLan1.ToString("#,##0")).Append("kg");
                if (p.CanLan2 != null)
                    s.Append(" · lần 2 ").Append(p.CanLan2.Value.ToString("#,##0")).Append("kg · hàng <b>")
                     .Append((p.KhoiLuongHang ?? 0).ToString("#,##0")).Append("kg</b>");
                else { s.Append(" · <span class='tl-do'>chờ cân lần 2</span>"); chuaXong++; }
            }
            if (chuaXong > 0) s.Append("<br><br><b>").Append(chuaXong).Append(" phiếu</b> chưa cân lần 2 — chưa ra được khối lượng hàng (VGM).");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Trạm cân", "/Admin/Gate/Can", "bi-speedometer2"));
            return kq;
        }

        private static KetQua DoiSoatCong(AppDb db)
        {
            var kq = new KetQua { CongCu = "Đối soát cổng" };
            var ds = db.DemXeCongs.AsNoTracking().OrderByDescending(d => d.TuLuc).Take(8).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Chưa có ca đối soát nào."; kq.LienKets.Add(LK("Đối soát cổng", "/Admin/DoiSoatCong", "bi-clipboard-data")); return kq; }

            var lech = ds.Where(d => d.ChenhLech != 0).ToList();
            var s = new StringBuilder("<b>" + ds.Count + " ca</b> gần nhất — <b>" + lech.Count + " ca lệch</b>:");
            foreach (var d in ds)
            {
                s.Append("<br>· ").Append(d.TuLuc.ToString("dd/MM")).Append(" ca ").Append(d.Ca)
                 .Append(" · ").Append(d.Cong).Append(" · hệ thống ").Append(d.SoHeThong)
                 .Append(" / thực tế ").Append(d.SoDemThucTe);
                if (d.ChenhLech != 0)
                    s.Append(" · <span class='tl-do'>lệch ").Append(d.ChenhLech > 0 ? "+" : "").Append(d.ChenhLech).Append("</span>")
                     .Append(d.DaGiaiTrinh ? " · đã giải trình" : " · <b>chưa giải trình</b>");
                if (d.MoBarieTay > 0) s.Append(" · mở tay ").Append(d.MoBarieTay).Append(" lần");
            }
            var chuaGiaiTrinh = lech.Count(d => !d.DaGiaiTrinh);
            if (chuaGiaiTrinh > 0)
                s.Append("<br><br><b>").Append(chuaGiaiTrinh).Append(" ca lệch chưa giải trình</b> — mỗi lượt lệch là một lượt xe "
                       + "không thu được phí hoặc một lần barie mở tay không rõ lý do.");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Đối soát cổng", "/Admin/DoiSoatCong", "bi-clipboard-data"));
            return kq;
        }

        private static KetQua ThuPhiCong(AppDb db)
        {
            var kq = new KetQua { CongCu = "Thu phí cổng" };
            var tu = DateTime.Today;
            var den = tu.AddDays(1);
            var tk = PhiCongService.TongKet(db, tu, den);
            if (tk == null || tk.SoLuot == 0)
            {
                kq.TraLoi = "Hôm nay chưa có lượt xe nào qua cổng thu phí.";
                kq.LienKets.Add(LK("Biểu phí cổng", "/Admin/PhiCong", "bi-cash-stack"));
                return kq;
            }
            var s = new StringBuilder("Phí cổng hôm nay — <b>" + tk.SoLuot + " lượt</b>, thu <b>" + Tien(tk.TongThu) + "</b>:");
            s.Append("<br>· Tiền mặt: <b>").Append(Tien(tk.TienMat)).Append("</b>");
            s.Append("<br>· Trừ thẻ trả trước: <b>").Append(Tien(tk.TruTaiKhoan)).Append("</b>");
            s.Append("<br>· Thẻ tháng (không thu thêm): <b>").Append(tk.LuotTheThang).Append(" lượt</b>");
            if (tk.CongNo > 0)
                s.Append("<br>· Công nợ chờ thu (lượt ghi trễ lúc mất mạng): <b>").Append(Tien(tk.CongNo)).Append("</b>");
            if (tk.TheoLoaiXe != null && tk.TheoLoaiXe.Count > 0)
            {
                s.Append("<br><br><b>Theo loại xe:</b>");
                foreach (var l in tk.TheoLoaiXe.Take(6))
                    s.Append("<br>· ").Append(l.LoaiXe).Append(" — ").Append(l.SoLuot).Append(" lượt · ").Append(Tien(l.ThanhTien));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Biểu phí cổng", "/Admin/PhiCong", "bi-cash-stack"));
            return kq;
        }

        private static KetQua TraCuuTaiXe(AppDb db, string tho)
        {
            var kq = new KetQua { CongCu = "Tra cứu tài xế" };
            var t = BoDau(tho).ToLowerInvariant();
            var ds = db.TaiXes.AsNoTracking().ToList()
                       .Where(x => !string.IsNullOrEmpty(x.HoTen) && t.Contains(BoDau(x.HoTen).ToLowerInvariant()))
                       .Take(5).ToList();
            if (ds.Count == 0)
            {
                var moi = db.TaiXes.AsNoTracking().OrderByDescending(x => x.LanVaoCuoi).Take(5).ToList();
                if (moi.Count == 0) { kq.TraLoi = "Chưa có tài xế nào trong hệ thống."; return kq; }
                var sb = new StringBuilder("Không khớp tên nào. <b>" + moi.Count + " tài xế</b> vào gần nhất:");
                foreach (var x in moi)
                    sb.Append("<br>· <b>").Append(x.HoTen).Append("</b> · ").Append(x.BienSo)
                      .Append(x.LanVaoCuoi != null ? " · " + x.LanVaoCuoi.Value.ToString("dd/MM HH:mm") : "");
                kq.TraLoi = sb.ToString();
                kq.LienKets.Add(LK("Hồ sơ tài xế", "/Admin/TaiXe", "bi-person-badge"));
                return kq;
            }
            var s = new StringBuilder();
            foreach (var x in ds)
            {
                s.Append(s.Length > 0 ? "<br><br>" : "").Append("<b>").Append(x.HoTen).Append("</b> · ").Append(x.BienSo);
                s.Append("<br>GPLX ").Append(x.SoGPLX).Append(" hạng ").Append(x.HangGPLX);
                if (x.HanGPLX != null)
                {
                    var conLai = (x.HanGPLX.Value.Date - DateTime.Today).Days;
                    s.Append(" · hạn ").Append(x.HanGPLX.Value.ToString("dd/MM/yyyy"))
                     .Append(conLai < 0 ? " <span class='tl-do'>ĐÃ HẾT HẠN</span>"
                            : conLai < 30 ? " <span class='tl-do'>còn " + conLai + " ngày</span>" : "");
                }
                s.Append("<br>Đã vào cổng <b>").Append(x.SoLanVao).Append(" lượt</b>")
                 .Append(x.LanVaoCuoi != null ? " · gần nhất " + x.LanVaoCuoi.Value.ToString("dd/MM HH:mm") : "");
                kq.LienKets.Add(LK("Hồ sơ " + x.HoTen, "/Admin/TaiXe?bienSo=" + Uri.EscapeDataString(x.BienSo ?? ""), "bi-person-badge"));
            }
            kq.TraLoi = s.ToString();
            return kq;
        }

        // ============================================================
        // LENH GIAO NHAN
        // ============================================================

        private static KetQua TraCuuLenh(AppDb db, string soLenh)
        {
            var kq = new KetQua { CongCu = "Lệnh giao nhận" };
            var l = db.LenhGiaoNhans.AsNoTracking().Include(x => x.KhachHang)
                      .FirstOrDefault(x => x.SoLenh == soLenh);
            if (l == null) { kq.TraLoi = "Không tìm thấy lệnh <b>" + soLenh + "</b>."; return kq; }

            var s = new StringBuilder("<b>" + l.SoLenh + "</b> · " + l.TenLoaiLenh + " · <b>" + l.TrangThai + "</b>");
            s.Append("<br>Khách: ").Append(l.KhachHang != null ? l.KhachHang.TenCongTy : "—");
            if (l.SoCont != null) s.Append("<br>Container: <b>").Append(l.SoCont).Append("</b>");
            s.Append("<br>Xe ").Append(l.BienSoXe).Append(" · ").Append(l.TenTaiXe).Append(" · ").Append(l.SdtTaiXe);
            s.Append("<br>Hẹn ").Append(l.HenTuLuc.ToString("dd/MM HH:mm")).Append(" – ").Append(l.HenDenLuc.ToString("HH:mm"));
            if (l.VaoCongLuc != null) s.Append("<br>Vào cổng ").Append(l.VaoCongLuc.Value.ToString("dd/MM HH:mm"));
            if (l.HoanTatLuc != null) s.Append("<br>Hoàn tất ").Append(l.HoanTatLuc.Value.ToString("dd/MM HH:mm"));
            if (l.MaPin != null) s.Append("<br>Mã PIN: <b>").Append(l.MaPin).Append("</b>");

            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Mở lệnh", "/Admin/GatePass/Chi Tiet/" + l.MaLenh, "bi-qr-code"));
            if (l.TrangThai != "Hoàn tất" && l.TrangThai != "Hủy")
            {
                kq.LienKets.Add(LK("Hoàn tất lệnh", "#goi:hoàn tất lệnh " + l.SoLenh, "bi-check2-circle"));
                kq.LienKets.Add(LK("Hủy lệnh", "#goi:hủy lệnh " + l.SoLenh, "bi-x-circle"));
            }
            return kq;
        }

        private static KetQua ChuanBiLenhGiaoNhan(AppDb db, string soMa, int? soNguyen, string trangThaiMoi)
        {
            var kq = new KetQua { CongCu = "Lệnh giao nhận" };
            LenhGiaoNhan l = null;
            if (soMa != null) l = db.LenhGiaoNhans.AsNoTracking().FirstOrDefault(x => x.SoLenh == soMa);
            if (l == null && soNguyen != null) l = db.LenhGiaoNhans.AsNoTracking().FirstOrDefault(x => x.MaLenh == soNguyen);
            if (l == null)
            {
                kq.TraLoi = "Lệnh nào? Ví dụ <i>“hủy lệnh LGN-DEMO-001”</i>.";
                kq.LienKets.Add(LK("Lệnh đang chạy", "#goi:lệnh nào đang chạy", "bi-list"));
                return kq;
            }
            if (l.TrangThai == trangThaiMoi) { kq.TraLoi = "Lệnh <b>" + l.SoLenh + "</b> đã ở trạng thái <b>" + trangThaiMoi + "</b> rồi."; return kq; }
            if (l.TrangThai == "Hoàn tất") { kq.TraLoi = "Lệnh <b>" + l.SoLenh + "</b> đã hoàn tất — không đổi được nữa."; return kq; }

            kq.TraLoi = "Lệnh <b>" + l.SoLenh + "</b> · " + l.TenLoaiLenh + " · " + l.BienSoXe
                      + (l.SoCont != null ? " · " + l.SoCont : "") + " · đang <b>" + l.TrangThai + "</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "LenhGiaoNhan", ThamSo = l.MaLenh + "|" + trangThaiMoi,
                MoTa = "Chuyển lệnh " + l.SoLenh + " → " + trangThaiMoi + "?",
                CanhBao = trangThaiMoi == "Hủy"
                    ? "Xe mang mã PIN này sẽ KHÔNG qua được cổng nữa. Báo tài xế trước khi hủy."
                    : "Hoàn tất là chốt lượt giao nhận — mốc thời gian ghi ngay bây giờ."
            };
            return kq;
        }

        private static KetQua LenhGiaoNhanThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Lệnh giao nhận" };
            var p = (thamSo ?? "").Split('|');
            int id; if (p.Length != 2 || !int.TryParse(p[0], out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var l = db.LenhGiaoNhans.Find(id);
            if (l == null) { kq.TraLoi = "Không tìm thấy lệnh."; return kq; }
            l.TrangThai = p[1];
            if (p[1] == "Hoàn tất" && l.HoanTatLuc == null) l.HoanTatLuc = DateTime.Now;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Lệnh " + l.SoLenh + " → " + p[1] + " (trợ lý)");
            kq.TraLoi = "Lệnh <b>" + l.SoLenh + "</b> → <b>" + l.TrangThai + "</b>.";
            kq.LienKets.Add(LK("Lệnh giao nhận", "/Admin/GatePass", "bi-qr-code"));
            return kq;
        }

        // ============================================================
        // GIAY TO
        // ============================================================

        private static KetQua ChungTuGanDay(AppDb db, string soCont)
        {
            var kq = new KetQua { CongCu = "Chứng từ" };
            var q = db.ChungTus.AsNoTracking().AsQueryable();
            if (soCont != null) q = q.Where(x => x.SoCont == soCont);
            var ds = q.OrderByDescending(x => x.NgayLap).Take(10).ToList();
            if (ds.Count == 0)
            { kq.TraLoi = soCont != null ? "Chưa có chứng từ nào cho <b>" + soCont + "</b>." : "Chưa có chứng từ nào."; return kq; }

            var s = new StringBuilder(soCont != null
                ? "<b>" + ds.Count + " chứng từ</b> của " + soCont + ":"
                : "<b>" + ds.Count + " chứng từ</b> gần nhất:");
            foreach (var x in ds)
            {
                s.Append("<br>· <b>").Append(x.SoChungTu).Append("</b> · ").Append(x.TenLoai)
                 .Append(" · ").Append(x.NgayLap.ToString("dd/MM HH:mm")).Append(" · ").Append(x.NguoiLap);
                if (!string.IsNullOrEmpty(x.LienKet)) kq.LienKets.Add(LK(x.SoChungTu, x.LienKet, "bi-file-earmark-text"));
            }
            s.Append("<br><br><i>Mỗi chứng từ có mã băm SHA-256 nối vào chứng từ trước — sửa một dòng "
                   + "là gãy cả chuỗi, nên sổ này chống sửa lén.</i>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Sổ chứng từ", "/Admin/Docs", "bi-journal-text"));
            return kq;
        }

        private static KetQua KiemDemGanDay(AppDb db)
        {
            var kq = new KetQua { CongCu = "Kiểm đếm" };
            var ds = db.KiemDems.AsNoTracking().OrderByDescending(k => k.ThoiGian).Take(8).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Chưa có phiên kiểm đếm nào."; kq.LienKets.Add(LK("Kiểm đếm", "/Admin/Vision", "bi-camera-video")); return kq; }

            var s = new StringBuilder("<b>" + ds.Count + " phiên kiểm đếm</b> gần nhất:");
            int lech = 0;
            foreach (var k in ds)
            {
                s.Append("<br>· <b>").Append(k.TenLo).Append("</b> · ").Append(k.ThoiGian.ToString("dd/MM HH:mm"))
                 .Append(" · dự kiến ").Append(k.DuKien).Append(" / đếm được <b>").Append(k.DemDuoc).Append("</b>");
                if (k.DuKien != null && k.DemDuoc != null && k.DuKien != k.DemDuoc)
                { s.Append(" <span class='tl-do'>lệch ").Append(k.DemDuoc.Value - k.DuKien.Value).Append("</span>"); lech++; }
            }
            if (lech > 0) s.Append("<br><br><b>").Append(lech).Append(" phiên lệch số</b> — cần đối chiếu lại với ảnh chụp.");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Kiểm đếm bằng AI", "/Admin/Vision", "bi-camera-video"));
            return kq;
        }

        // ============================================================
        // GIA & CHI PHI
        // ============================================================

        private static KetQua BangGiaDichVu(AppDb db, string tho)
        {
            var kq = new KetQua { CongCu = "Bảng giá dịch vụ" };
            var ds = db.DichVus.AsNoTracking().Where(d => d.KichHoat).OrderBy(d => d.NhomDV).ThenBy(d => d.TenDV).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Chưa khai báo dịch vụ nào."; return kq; }

            // Neu cau hoi co nhac ten mot dich vu thi tra rieng cai do
            var t = BoDau(tho).ToLowerInvariant();
            var khop = ds.Where(d => !string.IsNullOrEmpty(d.TenDV) && t.Contains(BoDau(d.TenDV).ToLowerInvariant())).ToList();
            var hien = khop.Count > 0 ? khop : ds;

            var s = new StringBuilder(khop.Count > 0
                ? "Giá dịch vụ bạn hỏi:"
                : "<b>" + ds.Count + " dịch vụ</b> đang áp dụng:");
            string nhomTruoc = null;
            foreach (var d in hien.Take(20))
            {
                if (khop.Count == 0 && d.NhomDV != nhomTruoc) { s.Append("<br><b>").Append(d.NhomDV).Append("</b>"); nhomTruoc = d.NhomDV; }
                s.Append("<br>· ").Append(d.TenDV).Append(" — <b>").Append(Tien(d.DonGia)).Append("</b>/").Append(d.DonViTinh);
            }
            s.Append("<br><br><i>Muốn sửa giá thì phải vào màn hình Dịch vụ — tôi không đổi biểu giá qua chat.</i>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Bảng giá dịch vụ", "/Admin/Services", "bi-tags"));
            return kq;
        }

        private static KetQua UocTinhChiPhi(AppDb db, string tho, string t)
        {
            var kq = new KetQua { CongCu = "Ước tính chi phí" };

            var loai = Regex.Match(tho.ToUpperInvariant(), @"\b(20(?:GP|DC|RF|OT|FR)?|40(?:GP|HC|DC|RF|OT|FR)?|45(?:HC)?)\b");
            var sl = Regex.Match(t, @"(\d{1,3})\s*(?:cont|container|cai|chiec)");
            var ngay = TimSoNgay(t);

            var req = new CostRequestVM
            {
                LoaiCont = loai.Success ? (loai.Groups[1].Value.Length <= 2 ? loai.Groups[1].Value + "GP" : loai.Groups[1].Value) : "40HC",
                SoLuong = sl.Success ? int.Parse(sl.Groups[1].Value) : 1,
                ChieuHang = Co(t, "xuat") ? "Xuất" : "Nhập",
                SoNgayLuuBai = ngay ?? 0,
                VeSinh = Co(t, "ve sinh", "rua cont"),
                CanVGM = Co(t, "vgm", "can"),
                DongRut = Co(t, "dong rut", "dong hang", "rut hang"),
                GiamDinh = Co(t, "giam dinh", "kiem dinh"),
                VanChuyen = null
            };
            if (req.SoLuong < 1) req.SoLuong = 1;
            if (req.SoLuong > 200) req.SoLuong = 200;

            var e = CostService.Estimate(db, req);
            if (e == null || e.Lines == null || e.Lines.Count == 0)
            { kq.TraLoi = "Chưa ước tính được — có thể bảng giá còn thiếu dịch vụ cho loại này."; return kq; }

            var s = new StringBuilder("Ước tính cho <b>" + req.SoLuong + " × " + req.LoaiCont + "</b> · hàng "
                  + req.ChieuHang.ToLowerInvariant() + (req.SoNgayLuuBai > 0 ? " · lưu bãi " + req.SoNgayLuuBai + " ngày" : "") + ":");
            foreach (var l in e.Lines)
                s.Append("<br>· ").Append(l.Ten).Append(" — <b>").Append(Tien(l.ThanhTien)).Append("</b>");
            s.Append("<br><br><b>Tổng: ").Append(Tien(e.Tong)).Append("</b>");
            if (req.SoLuong > 1) s.Append(" · <b>").Append(Tien(e.TrenMoiCont)).Append("</b>/cont");
            if (e.AiNhanXet != null && e.AiNhanXet.Count > 0)
                s.Append("<br><br><b>Nhận xét:</b><br>· ").Append(string.Join("<br>· ", e.AiNhanXet));
            s.Append("<br><br><i>Đây là ước tính theo biểu giá hiện hành, chưa gồm VAT và phụ phí phát sinh.</i>");

            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Máy tính chi phí", "/Cost", "bi-calculator"));
            return kq;
        }

        /// <summary>"phí lưu bãi bao nhiêu" — gia lay tu CUNG cho voi phieu thu, khong tu ghi so.</summary>
        private static KetQua BieuPhiLuuBai(AppDb db)
        {
            var kq = new KetQua { CongCu = "Biểu phí lưu bãi" };
            var g = LuuBaiService.BieuGia(db);
            kq.TraLoi = "Miễn phí <b>" + LuuBaiService.NGAY_MIEN_PHI + " ngày</b> đầu, sau đó mỗi ngày:"
                      + "<div class=\"tl-ds\">"
                      + DongTinh("Cont 20'", Tien(g.Gia20))
                      + DongTinh("Cont 40' / 45'", Tien(g.Gia40))
                      + DongTinh("Điện cont lạnh — không miễn", Tien(g.GiaDien))
                      + "</div>";
            kq.LienKets.Add(LK("Tiền lưu bãi đang treo", "#goi:công nợ hôm nay", "bi-cash-stack"));
            kq.LienKets.Add(LK("Ước tính cho lô hàng", "#dien:ước tính 1 cont 40HC lưu 10 ngày", "bi-calculator"));
            return kq;
        }

        /// <summary>"khách nào nợ nhiều nhất" — gom tien luu bai dang treo theo khach.</summary>
        private static KetQua CongNoTheoKhach(AppDb db)
        {
            var kq = new KetQua { CongCu = "Công nợ theo khách" };
            var nhom = LuuBaiService.BangKeDangChay(db)
                .Where(d => d.Phi != null && d.Phi.TongTien > 0)
                .GroupBy(d => d.TenKhach)
                .Select(g => new { Ten = g.Key, Tien = g.Sum(d => d.Phi.TongTien), So = g.Count() })
                .OrderByDescending(x => x.Tien).ToList();
            if (nhom.Count == 0) { kq.TraLoi = "Không khách nào đang nợ tiền lưu bãi."; return kq; }

            var s = new StringBuilder("<b>" + nhom.Count(x => x.Ten != null) + " khách</b> đang nợ, tổng <b>"
                                      + Tien(nhom.Sum(x => x.Tien)) + "</b>:<div class=\"tl-ds\">");
            foreach (var x in nhom.Take(6))
            {
                // Ten khach lay tu CSDL — ma hoa truoc khi dua vao HTML.
                var nhan = MaHoa(x.Ten ?? "Chưa gán khách") + " · " + x.So + " cont";
                s.Append(x.Ten == null ? DongTinh(nhan, TienGon(x.Tien))
                                       : DongGiaTri(nhan, TienGon(x.Tien), "khách hàng " + x.Ten));
            }
            kq.TraLoi = s.Append("</div>").ToString();
            kq.LienKets.Add(LK("Bảng kê lưu bãi", "/Admin/LuuBai", "bi-table"));
            return kq;
        }

        /// <summary>"phiếu thu hôm nay" — so phieu lap hom nay va cac phieu dang cho thu tien.</summary>
        private static KetQua PhieuThuGanDay(AppDb db)
        {
            var kq = new KetQua { CongCu = "Phiếu lưu bãi" };
            var homNay = DateTime.Today;
            var lapHomNay = db.PhieuLuuBais.AsNoTracking().Count(p => p.NgayLap >= homNay && p.TrangThai != "Hủy");
            var choThu = db.PhieuLuuBais.AsNoTracking().Where(p => p.TrangThai == "Chờ thu")
                           .OrderByDescending(p => p.TongTien).ToList();

            var s = new StringBuilder("Hôm nay lập <b>" + lapHomNay + " phiếu</b>. ");
            if (choThu.Count == 0) s.Append("Không phiếu nào chờ thu.");
            else
            {
                s.Append("<b>").Append(choThu.Count).Append(" phiếu</b> chờ thu, tổng <b>")
                 .Append(Tien(choThu.Sum(p => p.TongTien))).Append("</b>:<div class=\"tl-ds\">");
                foreach (var p in choThu.Take(5))
                    s.Append(DongGiaTri(MaHoa(p.SoPhieu + " · " + p.SoCont), TienGon(p.TongTien), p.SoPhieu));
                s.Append("</div>");
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Danh sách phiếu", "/Admin/LuuBai/DanhSach", "bi-receipt"));
            return kq;
        }

        /// <summary>
        /// "báo cáo hôm nay", "thống kê" — bon con so dieu hanh nhin la biet tinh hinh,
        /// bam vao so nao thi mo chi tiet so do. Doanh thu dem bang DoanhThuKy — cung
        /// cho voi cau "doanh thu", de hai noi khong bao gio ra hai con so.
        /// </summary>
        private static KetQua BaoCaoNhanh(AppDb db)
        {
            var kq = new KetQua { CongCu = "Báo cáo nhanh" };
            var homNay = DateTime.Today;
            var mai = homNay.AddDays(1);
            var dauThang = new DateTime(homNay.Year, homNay.Month, 1);

            var tonBai = db.Containers.AsNoTracking().Count(c => c.TrangThai == "Trong bãi");
            var luotXe = db.XeRaVaos.AsNoTracking().Count(x => x.ThoiGian >= homNay && x.ThoiGian < mai);
            var doanhThu = DoanhThuKy(db, dauThang, dauThang.AddMonths(1));
            var dangTreo = LuuBaiService.BangKeDangChay(db).Where(d => d.Phi != null).Sum(d => d.Phi.TongTien);

            kq.TraLoi = "Số liệu lúc <b>" + DateTime.Now.ToString("HH:mm dd/MM") + "</b>:"
                      + "<div class=\"tl-ds\">"
                      + DongGiaTri("Container trong bãi", tonBai.ToString("#,##0", Vi), "tồn bãi")
                      + DongGiaTri("Xe qua cổng hôm nay", luotXe.ToString("#,##0", Vi), "xe qua cổng hôm nay")
                      + DongGiaTri("Doanh thu tháng " + homNay.Month, TienGon(doanhThu.Tong), "doanh thu tháng này")
                      + DongGiaTri("Tiền lưu bãi đang treo", TienGon(dangTreo), "công nợ hôm nay")
                      + "</div>";
            kq.LienKets.Add(LK("Báo cáo đầy đủ · Excel", "/Admin/Reports", "bi-graph-up-arrow"));
            return kq;
        }

        // ============================================================
        // EDI
        // ============================================================

        private static KetQua BanTinEdiChoGui(AppDb db)
        {
            var kq = new KetQua { CongCu = "Bản tin EDI" };
            var ds = db.BanTinEDIs.AsNoTracking().Include(b => b.HangTau)
                       .OrderByDescending(b => b.NgayTao).Take(10).ToList();
            if (ds.Count == 0)
            {
                kq.TraLoi = "Chưa sinh bản tin EDI nào. Gõ <i>“sinh codeco cho &lt;hãng tàu&gt;”</i> để tạo.";
                kq.LienKets.Add(LK("Màn hình EDI", "/Admin/Edi", "bi-broadcast"));
                return kq;
            }
            var cho = ds.Count(b => b.TrangThai == "Chờ gửi");
            var s = new StringBuilder("<b>" + ds.Count + " bản tin</b> gần nhất" + (cho > 0 ? " — <b>" + cho + " chờ gửi</b>" : "") + ":");
            foreach (var b in ds)
            {
                s.Append("<br>· <b>").Append(b.SoChungTu).Append("</b> · ").Append(b.LoaiBT).Append(" ").Append(b.Huong)
                 .Append(" · ").Append(b.HangTau != null ? b.HangTau.TenHangTau : "—")
                 .Append(" · ").Append(b.SoLuongCont).Append(" cont · ").Append(b.TrangThai);
                if (b.TrangThai == "Chờ gửi")
                    kq.LienKets.Add(LK("Đánh dấu đã gửi " + b.SoChungTu, "#goi:đã gửi bản tin " + b.MaBT, "bi-send-check"));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Màn hình EDI", "/Admin/Edi", "bi-broadcast"));
            return kq;
        }

        private static KetQua ChuanBiDanhDauDaGui(AppDb db, int? so)
        {
            var kq = new KetQua { CongCu = "Bản tin EDI" };
            if (so == null) { kq.TraLoi = "Bản tin số mấy? Ví dụ <i>“đã gửi bản tin 5”</i>."; kq.LienKets.Add(LK("Bản tin chờ gửi", "#goi:bản tin nào chưa gửi", "bi-list")); return kq; }
            var b = db.BanTinEDIs.AsNoTracking().Include(x => x.HangTau).FirstOrDefault(x => x.MaBT == so);
            if (b == null) { kq.TraLoi = "Không tìm thấy bản tin <b>#" + so + "</b>."; return kq; }
            if (b.TrangThai == "Đã gửi") { kq.TraLoi = "Bản tin <b>" + b.SoChungTu + "</b> đã đánh dấu gửi rồi."; return kq; }

            kq.TraLoi = "Bản tin <b>" + b.SoChungTu + "</b> · " + b.LoaiBT + " · "
                      + (b.HangTau != null ? b.HangTau.TenHangTau : "") + " · " + b.SoLuongCont + " container.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "DanhDauDaGui", ThamSo = b.MaBT.ToString(),
                MoTa = "Đánh dấu " + b.SoChungTu + " đã gửi hãng tàu?",
                CanhBao = "Chỉ đánh dấu khi bản tin ĐÃ THỰC SỰ được gửi đi qua kênh EDI/email của hãng. "
                        + "Đánh dấu nhầm sẽ khiến các lượt trong bản tin không bao giờ được báo lại."
            };
            return kq;
        }

        private static KetQua DanhDauDaGuiThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Bản tin EDI" };
            int id; if (!int.TryParse(thamSo, out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var b = db.BanTinEDIs.Find(id);
            if (b == null) { kq.TraLoi = "Không tìm thấy bản tin."; return kq; }
            if (b.TrangThai == "Đã gửi") { kq.TraLoi = "Bản tin này đã đánh dấu gửi rồi."; return kq; }
            b.TrangThai = "Đã gửi";
            b.NgayGui = DateTime.Now;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Đánh dấu đã gửi " + b.SoChungTu + " (trợ lý)");
            kq.TraLoi = "Đã đánh dấu <b>" + b.SoChungTu + "</b> gửi lúc " + b.NgayGui.Value.ToString("dd/MM HH:mm") + ".";
            kq.LienKets.Add(LK("Xem bản tin", "/Admin/Edi/Xem/" + b.MaBT, "bi-broadcast"));
            return kq;
        }

        /// <summary>
        /// Sinh CODECO THAT — lam dung cac buoc cua EdiController.SinhCodeco:
        /// gom luot chua bao (MaBT null), sinh EDIFACT, luu ban tin, roi DONG DAU
        /// cac luot da bao de lan sau khong bao trung.
        /// </summary>
        private static KetQua SinhEdiThatSu(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Sinh bản tin EDI" };
            int maHangTau; if (!int.TryParse(thamSo, out maHangTau)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var hang = db.HangTaus.Find(maHangTau);
            if (hang == null) { kq.TraLoi = "Không tìm thấy hãng tàu."; return kq; }

            var tu = DateTime.Now.AddDays(-7);
            var contHang = db.Containers.Where(c => c.MaHangTau == maHangTau).Select(c => c.SoCont).ToList();
            if (contHang.Count == 0)
            {
                kq.TraLoi = "Chưa có container nào gán cho hãng <b>" + hang.TenHangTau + "</b>.";
                kq.LienKets.Add(LK("Tồn rỗng theo hãng", "/Admin/Edi", "bi-broadcast"));
                return kq;
            }
            var luot = db.XeRaVaos
                         .Where(x => x.MaBT == null && x.SoCont != null && x.ThoiGian >= tu && contHang.Contains(x.SoCont))
                         .OrderBy(x => x.ThoiGian).Take(500).ToList();
            if (luot.Count == 0)
            {
                kq.TraLoi = "Không có lượt vào/ra nào của hãng <b>" + hang.TenHangTau
                          + "</b> trong 7 ngày qua cần báo (hoặc đã báo hết rồi).";
                kq.LienKets.Add(LK("Màn hình EDI", "/Admin/Edi", "bi-broadcast"));
                return kq;
            }

            // Dung chung CodecoHelper (ban gop: lap ngay; microservices: gui lenh cho dich vu Tich hop)
            var bt = CodecoHelper.LapHoacYeuCau(db, hang, luot, MaDepotEdi, nguoiDung,
                                                "Gom " + luot.Count + " lượt qua cổng (trợ lý)");
            if (bt == null)
            {
                kq.TraLoi = "Đã gửi dịch vụ <b>Tích hợp</b> sinh CODECO cho <b>" + luot.Count + " lượt</b> của hãng <b>"
                          + hang.TenHangTau + "</b> — bản tin xuất hiện ở màn hình EDI sau vài giây.";
                kq.LienKets.Add(LK("Màn hình EDI", "/Admin/Edi", "bi-broadcast"));
                return kq;
            }
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Sinh CODECO " + bt.SoChungTu + " cho " + hang.TenHangTau + " (" + bt.SoLuongCont + " cont, trợ lý)");
            kq.TraLoi = "Đã sinh <b>" + bt.SoChungTu + "</b> — <b>" + bt.SoLuongCont + " container</b>, "
                      + "gom " + luot.Count + " lượt qua cổng."
                      + "<br>Bản tin đang ở trạng thái <b>Chờ gửi</b> — kiểm tra nội dung rồi gửi cho hãng.";
            kq.LienKets.Add(LK("Xem bản tin", "/Admin/Edi/Xem/" + bt.MaBT, "bi-broadcast"));
            kq.LienKets.Add(LK("Đánh dấu đã gửi", "#goi:đã gửi bản tin " + bt.MaBT, "bi-send-check"));
            return kq;
        }

        // ============================================================
        // PHAN TICH
        // ============================================================

        private static KetQua CanhBaoHeThong(AppDb db)
        {
            var kq = new KetQua { CongCu = "Cảnh báo toàn hệ thống" };
            var ds = AlertService.Build(db);
            if (ds == null || ds.Count == 0)
            {
                kq.TraLoi = "Không có cảnh báo nào. Hệ thống đang bình thường.";
                kq.LienKets.Add(LK("Soát việc tồn", "#goi:việc hôm nay", "bi-list-check"));
                return kq;
            }
            var s = new StringBuilder("<b>" + ds.Count + " cảnh báo</b> đang bật:");
            foreach (var a in ds.Take(10))
            {
                s.Append("<br>· ").Append(a.Muc == "danger" ? "<span class='tl-do'>●</span> "
                       : a.Muc == "warning" ? "<span class='tl-cam'>●</span> " : "· ").Append(a.Msg);
                if (!string.IsNullOrEmpty(a.Url)) kq.LienKets.Add(LK(Rut(a.Msg, 40), a.Url, a.Icon ?? "bi-exclamation-triangle"));
            }
            kq.TraLoi = s.ToString();
            return kq;
        }

        private static KetQua PhanKhucKhach(AppDb db)
        {
            var kq = new KetQua { CongCu = "Phân khúc khách hàng" };
            var r = MLService.PhanKhucKhachHang(db);
            if (r == null || r.Clusters == null || r.Clusters.Count == 0)
            { kq.TraLoi = "Chưa đủ dữ liệu để phân khúc — cần thêm lịch sử booking."; return kq; }

            var s = new StringBuilder("Chia <b>" + r.K + " nhóm</b> bằng K-Means (" + r.SoVongLap + " vòng lặp"
                  + (r.HoiTu ? ", đã hội tụ" : ", chưa hội tụ") + "):");
            foreach (var c in r.Clusters)
            {
                var so = c.KhachHangs != null ? c.KhachHangs.Count : 0;
                s.Append("<br><br><b>").Append(c.Ten).Append("</b> — ").Append(so).Append(" khách");
                if (!string.IsNullOrEmpty(c.MoTa)) s.Append("<br>").Append(c.MoTa);
                s.Append("<br>Trung bình: ").Append(Tien((decimal)c.DoanhThuTB)).Append(" · ")
                 .Append(c.BookingTB.ToString("0.#")).Append(" booking · ")
                 .Append(c.ContainerTB.ToString("0.#")).Append(" container");
                if (so > 0)
                    s.Append("<br><i>Tiêu biểu: ")
                     .Append(string.Join(" · ", c.KhachHangs.OrderByDescending(k => k.DoanhThu).Take(3).Select(k => k.TenCongTy)))
                     .Append("</i>");
            }
            if (!string.IsNullOrEmpty(r.ThongBao)) s.Append("<br><br><i>").Append(r.ThongBao).Append("</i>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Phòng thí nghiệm AI", "/Admin/AiLab", "bi-cpu"));
            return kq;
        }

        private static KetQua TopKhachHang(AppDb db)
        {
            var kq = new KetQua { CongCu = "Top khách hàng" };
            var ds = db.DatDichVus.AsNoTracking().Include(b => b.KhachHang)
                       .Where(b => b.TrangThai != "Đã hủy").ToList()
                       .GroupBy(b => b.MaKH)
                       .Select(g => new
                       {
                           Ten = g.First().KhachHang != null ? g.First().KhachHang.TenCongTy : "—",
                           So = g.Count(),
                           Tong = g.Sum(x => x.TongTien)
                       })
                       .OrderByDescending(x => x.Tong).Take(8).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Chưa có booking nào để xếp hạng."; return kq; }

            var tongAll = ds.Sum(x => x.Tong);
            var s = new StringBuilder("<b>" + ds.Count + " khách</b> lớn nhất theo giá trị booking:");
            int i = 1;
            foreach (var x in ds)
                s.Append("<br>").Append(i++).Append(". <b>").Append(x.Ten).Append("</b> · ")
                 .Append(x.So).Append(" booking · <b>").Append(Tien(x.Tong)).Append("</b>")
                 .Append(tongAll > 0 ? " (" + (x.Tong * 100 / tongAll).ToString("0.#") + "%)" : "");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Danh sách khách hàng", "/Admin/Customers", "bi-people"));
            kq.LienKets.Add(LK("Phân khúc khách", "#goi:phân khúc khách", "bi-diagram-3"));
            return kq;
        }

        private static KetQua BatThuongSanLuong(AppDb db)
        {
            var kq = new KetQua { CongCu = "Bất thường sản lượng" };
            var ds = MLService.PhatHienBatThuong(db);
            if (ds == null || ds.Count == 0)
            { kq.TraLoi = "Chưa đủ dữ liệu sản lượng để so sánh."; return kq; }

            var bt = ds.Where(x => x.BatThuong).ToList();
            var s = new StringBuilder(bt.Count == 0
                ? "Soát <b>" + ds.Count + " tháng</b> — <b>không có tháng nào bất thường</b>. Sản lượng đi đều."
                : "Soát <b>" + ds.Count + " tháng</b> — <b>" + bt.Count + " tháng bất thường</b>:");
            foreach (var x in (bt.Count > 0 ? bt : ds.OrderByDescending(a => Math.Abs(a.ZScore)).Take(3).ToList()))
                s.Append("<br>· <b>").Append(x.Nhan).Append("</b> · ").Append(x.TEU).Append(" TEU · ")
                 .Append(x.BienDongPct >= 0 ? "+" : "").Append(x.BienDongPct.ToString("0.#")).Append("% · z=")
                 .Append(x.ZScore.ToString("0.00"))
                 .Append(x.BatThuong ? " <span class='tl-do'>" + x.Huong + "</span>" : "");
            s.Append("<br><br><i>Ngưỡng |z| ≥ 1.8 so với trung bình các tháng — z càng lớn càng lệch khỏi nếp thường.</i>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Báo cáo sản lượng", "/Admin/Reports", "bi-graph-up"));
            return kq;
        }

        // ============================================================
        // VAN HANH KHAC
        // ============================================================

        private static KetQua TinhTrangCamera(AppDb db)
        {
            var kq = new KetQua { CongCu = "Tình trạng camera" };
            var ds = db.CameraIPs.AsNoTracking().OrderBy(c => c.ViTri).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Chưa khai báo camera nào."; kq.LienKets.Add(LK("Quản lý camera", "/Admin/Camera", "bi-camera-video")); return kq; }

            var tat = ds.Where(c => !c.KichHoat).ToList();
            var loi = ds.Where(c => c.KichHoat && !string.IsNullOrEmpty(c.KetQuaThu)
                                 && c.KetQuaThu.IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0).ToList();
            var s = new StringBuilder("<b>" + ds.Count + " camera</b> · bật " + (ds.Count - tat.Count)
                  + " · tắt " + tat.Count + (loi.Count > 0 ? " · <span class='tl-do'>lỗi " + loi.Count + "</span>" : ""));
            foreach (var c in ds.Take(10))
            {
                s.Append("<br>· <b>").Append(c.TenCam).Append("</b> · ").Append(c.ViTri)
                 .Append(!c.KichHoat ? " · <i>đang tắt</i>" : "");
                if (c.LanThuCuoi != null)
                    s.Append(" · thử ").Append(c.LanThuCuoi.Value.ToString("dd/MM HH:mm")).Append(" → ").Append(Rut(c.KetQuaThu, 30));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Quản lý camera", "/Admin/Camera", "bi-camera-video"));
            return kq;
        }

        private static KetQua NhanDienGanDay(AppDb db)
        {
            var kq = new KetQua { CongCu = "Nhận diện AI" };
            var tu = DateTime.Today;
            var ds = db.NhanDienAIs.AsNoTracking().Where(n => n.ThoiGian >= tu).ToList();
            if (ds.Count == 0)
            {
                ds = db.NhanDienAIs.AsNoTracking().OrderByDescending(n => n.ThoiGian).Take(20).ToList();
                if (ds.Count == 0) { kq.TraLoi = "Chưa có lượt nhận diện nào."; kq.LienKets.Add(LK("Thị giác máy", "/Admin/Vision", "bi-eye")); return kq; }
            }
            var s = new StringBuilder("<b>" + ds.Count + " lượt nhận diện</b>"
                  + (ds.Any(x => x.ThoiGian >= tu) ? " hôm nay" : " gần nhất") + ":");
            s.Append("<br>· Tổng đối tượng: <b>").Append(ds.Sum(x => x.TongDoiTuong)).Append("</b>");
            s.Append("<br>· Xe tải <b>").Append(ds.Sum(x => x.SoXeTai)).Append("</b> · ô tô <b>")
             .Append(ds.Sum(x => x.SoOto)).Append("</b> · người <b>").Append(ds.Sum(x => x.SoNguoi))
             .Append("</b> · tàu <b>").Append(ds.Sum(x => x.SoTauThuyen)).Append("</b>");
            var tin = ds.Where(x => x.DoTinCayTB != null).ToList();
            if (tin.Count > 0)
                s.Append("<br>· Độ tin cậy trung bình: <b>").Append((tin.Average(x => x.DoTinCayTB.Value) * 100).ToString("0.#")).Append("%</b>");
            var canhBao = ds.Where(x => !string.IsNullOrEmpty(x.MucCanhBao) && x.MucCanhBao != "Bình thường").ToList();
            if (canhBao.Count > 0) s.Append("<br>· <span class='tl-do'>").Append(canhBao.Count).Append(" lượt có cảnh báo</span>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Thị giác máy", "/Admin/Vision", "bi-eye"));
            return kq;
        }

        private static KetQua LienHeMoi(AppDb db)
        {
            var kq = new KetQua { CongCu = "Liên hệ của khách" };
            var ds = db.LienHes.AsNoTracking().OrderByDescending(l => l.NgayGui).Take(10).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Chưa có liên hệ nào từ khách."; return kq; }
            var chua = ds.Where(l => !l.DaXuLy).ToList();
            var s = new StringBuilder(chua.Count > 0
                ? "<b>" + chua.Count + " liên hệ chưa xử lý</b> (trong " + ds.Count + " gần nhất):"
                : "Tất cả " + ds.Count + " liên hệ gần nhất đều đã xử lý:");
            foreach (var l in ds.Take(8))
            {
                s.Append("<br>· ").Append(l.DaXuLy ? "" : "<b>#").Append(l.DaXuLy ? "#" + l.MaLH : l.MaLH + "</b>")
                 .Append(" · ").Append(l.HoTen).Append(" · ").Append(Rut(l.ChuDe, 40))
                 .Append(" · ").Append(l.NgayGui.ToString("dd/MM HH:mm"))
                 .Append(l.DaXuLy ? "" : " · <span class='tl-do'>chưa xử lý</span>");
                if (!l.DaXuLy) kq.LienKets.Add(LK("Đã trả lời #" + l.MaLH, "#goi:đã trả lời liên hệ " + l.MaLH, "bi-check2"));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Hộp thư liên hệ", "/Admin/Contacts", "bi-envelope"));
            return kq;
        }

        private static KetQua ChuanBiXuLyLienHe(AppDb db, int? so)
        {
            var kq = new KetQua { CongCu = "Xử lý liên hệ" };
            if (so == null) { kq.TraLoi = "Liên hệ số mấy? Ví dụ <i>“đã trả lời liên hệ 3”</i>."; kq.LienKets.Add(LK("Liên hệ chưa xử lý", "#goi:có liên hệ mới không", "bi-list")); return kq; }
            var l = db.LienHes.AsNoTracking().FirstOrDefault(x => x.MaLH == so);
            if (l == null) { kq.TraLoi = "Không tìm thấy liên hệ <b>#" + so + "</b>."; return kq; }
            if (l.DaXuLy) { kq.TraLoi = "Liên hệ <b>#" + so + "</b> đã đánh dấu xử lý rồi."; return kq; }

            kq.TraLoi = "<b>#" + l.MaLH + "</b> · " + l.HoTen + " · " + l.Email + " · " + l.SoDienThoai
                      + "<br><b>" + l.ChuDe + "</b><br><i>" + Rut(l.NoiDung, 200) + "</i>";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "XuLyLienHe", ThamSo = l.MaLH.ToString(),
                MoTa = "Đánh dấu đã xử lý liên hệ #" + l.MaLH + "?",
                CanhBao = "Tôi chỉ đánh dấu trong hệ thống — tôi KHÔNG gửi email hay gọi cho khách. "
                        + "Chỉ bấm khi bạn đã thật sự liên hệ lại với họ."
            };
            return kq;
        }

        private static KetQua XuLyLienHeThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Xử lý liên hệ" };
            int id; if (!int.TryParse(thamSo, out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var l = db.LienHes.Find(id);
            if (l == null) { kq.TraLoi = "Không tìm thấy liên hệ."; return kq; }
            if (l.DaXuLy) { kq.TraLoi = "Liên hệ này đã xử lý rồi."; return kq; }
            l.DaXuLy = true;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", nguoiDung, "Đánh dấu xử lý liên hệ #" + id + " của " + l.HoTen + " (trợ lý)");
            kq.TraLoi = "Đã đánh dấu xử lý liên hệ <b>#" + id + "</b> của " + l.HoTen + ".";
            kq.LienKets.Add(LK("Hộp thư liên hệ", "/Admin/Contacts", "bi-envelope"));
            return kq;
        }

        private static KetQua NhatKyGanDay(AppDb db)
        {
            var kq = new KetQua { CongCu = "Nhật ký hệ thống" };
            var ds = db.NhatKys.AsNoTracking().OrderByDescending(n => n.ThoiGian).Take(12).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Nhật ký còn trống."; return kq; }
            var s = new StringBuilder("<b>" + ds.Count + " thao tác</b> gần nhất:");
            foreach (var n in ds)
                s.Append("<br>· ").Append(n.ThoiGian.ToString("dd/MM HH:mm")).Append(" · <b>")
                 .Append(n.TaiKhoan).Append("</b> · ").Append(Rut(n.NoiDung, 80));
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Nhật ký hệ thống", "/Admin/System/NhatKy", "bi-clock-history"));
            return kq;
        }

        // ============================================================
        // TIEN ICH RIENG CUA PHAN MO RONG
        // ============================================================

        private static string Rut(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        private static int? TimSoThang(string daBoDau)
        {
            var m = Regex.Match(daBoDau ?? "", @"(\d{1,2})\s*thang");
            int n;
            return m.Success && int.TryParse(m.Groups[1].Value, out n) && n > 0 && n <= 60 ? (int?)n : null;
        }
    }
}
