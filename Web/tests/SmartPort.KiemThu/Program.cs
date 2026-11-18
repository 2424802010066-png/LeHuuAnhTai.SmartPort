using System;
using System.Threading.Tasks;
using SmartPort.KiemThu;

// Tra ve ma thoat 0 khi moi kiem tra dat — dung duoc trong script / CI
Console.OutputEncoding = System.Text.Encoding.UTF8;
var nhom = args.Length > 0 ? args[0] : "kien-truc";
var bo = new BoKiemTra();

if (nhom == "kien-truc" || nhom == "tat-ca")
    KiemTraKienTruc.Chay(bo);

// Thuat toan AI van hanh (khong can CSDL): so voi gia tri tinh tay
if (nhom == "ai" || nhom == "tat-ca")
    KiemTraAi.Chay(bo);

// Bao mat mat khau (khong can CSDL): quy tac mat khau, ma xac nhan quen mat khau, dau phien
if (nhom == "mat-khau" || nhom == "tat-ca")
    KiemTraMatKhau.Chay(bo);

// Boc bang lai tu van ban OCR (khong can CSDL): OCR tieng Viet co dau va tieng Anh khong dau
if (nhom == "giay-to" || nhom == "tat-ca")
    KiemTraGiayTo.Chay(bo);

// Dang nhap Google / so dien thoai (khong can CSDL, khong can mang): so dien thoai, ve HMAC, PKCE, id_token
if (nhom == "dang-nhap-ngoai" || nhom == "tat-ca")
    await KiemTraDangNhapNgoai.ChayAsync(bo);

if (nhom == "tich-hop" || nhom == "tat-ca")
    await KiemTraTichHop.ChayAsync(bo);

// Kien truc MICROSERVICES tren he thong dang chay (MicroServices/chay_microservices.ps1): sao chep du lieu qua
// RabbitMQ, dang nhap qua gateway, luong nghiep vu xuyen dich vu. Chua chay he thong thi tu bo qua.
if (nhom == "microservices" || nhom == "tat-ca")
    await KiemTraMicroservices.ChayAsync(bo);

// CHAY KHI MAT MANG: dong bo cong ngoai tuyen (idempotent, gio goc, doi soat) — sau microservices vi dung tai khoan kiem thu do nhom do tao
if (nhom == "ngoai-tuyen" || nhom == "tat-ca")
    await KiemTraNgoaiTuyen.ChayAsync(bo);

// DO BEN BACKEND: chong gui lap, phan loai loi tam thoi, suc khoe suy giam, gateway, chuoi ket noi
if (nhom == "do-ben" || nhom == "tat-ca")
    await KiemTraDoBen.ChayAsync(bo);

// TIEN + BAO MAT: tru the nguyen tu + so cai, VietQR tu sinh (CRC), OTP khong lo ma
if (nhom == "tien-bao-mat" || nhom == "tat-ca")
    await KiemTraTienVaBaoMat.ChayAsync(bo);

return bo.InKetQua();
