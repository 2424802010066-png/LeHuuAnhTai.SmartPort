using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace SmartPort.Gateway
{
    /// <summary>
    /// Thong bao tieng Viet khi gateway khong chuyen duoc yeu cau. Tram cong (Nhom giao dien) dua vao MA de quyet dinh:
    /// 502/503/504 = loi mang / dich vu -> xep hang gui lai (cung Idempotency-Key); 4xx = tu choi nghiep vu -> hien do.
    /// </summary>
    public static class BaoLoiGateway
    {
        public static (int Ma, string ThongBao) TaoThongBao(ForwarderError loi, string tenDichVu, int giayCho)
        {
            switch (loi)
            {
                case ForwarderError.NoAvailableDestinations:
                    return (StatusCodes.Status503ServiceUnavailable,
                            "Dịch vụ " + tenDichVu + " đang tạm ngừng (kiểm tra sức khỏe không đạt — có thể mất kết nối CSDL hoặc đang khởi động lại). "
                            + "Dữ liệu đã nhập chưa mất — thử lại sau ít giây.");
                case ForwarderError.RequestTimedOut:
                    return (StatusCodes.Status504GatewayTimeout,
                            "Dịch vụ " + tenDichVu + " không trả lời sau " + giayCho + " giây (quá giờ). Thao tác CÓ THỂ đã hoặc chưa được ghi — "
                            + "gửi lại với cùng Idempotency-Key, hoặc kiểm tra lại trước khi làm lần nữa.");
                default:
                    return (StatusCodes.Status502BadGateway,
                            "Không kết nối được dịch vụ " + tenDichVu + " (dịch vụ chưa chạy hoặc vừa dừng). Thử lại sau ít giây.");
            }
        }
    }
}
