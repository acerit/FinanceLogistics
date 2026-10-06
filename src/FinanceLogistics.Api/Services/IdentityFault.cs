namespace FinanceLogistics.Api.Services;

public sealed class IdentityFault(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public static IdentityFault InvalidLogin() => new(401, "INVALID_CREDENTIALS", "Tài khoản hoặc mật khẩu không hợp lệ, hoặc tài khoản chưa được đăng ký cho ứng dụng.");
    public static IdentityFault SessionExpired() => new(401, "SESSION_EXPIRED", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    public static IdentityFault AccessDisabled() => new(403, "ACCESS_NOT_GRANTED", "Tài khoản chưa có quyền hoạt động trong Finance–Logistics.");
}
