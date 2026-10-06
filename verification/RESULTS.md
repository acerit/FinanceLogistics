# Kết quả kiểm tra · 23/09/2026

## Đã chạy trong môi trường soạn mã

- `node --test tests/frontend/identity.test.mjs`: 20/20 pass, exit code 0.
- JSON appsettings/launchSettings đọc được; các csproj là XML hợp lệ.
- Kiểm tra tĩnh HTML: script/CSS trỏ tới file có thật, các ID JavaScript sử dụng tồn tại và không trùng. Không chạy kiểm thử giao diện trình duyệt end-to-end.
- Đã đối chiếu mapping bảng Identity/catalog với schema SQL V1 đã cung cấp. Giữ nguyên bảng/khóa cũ, bổ sung DirectoryAccount, LoginAlias, AuthSession, IdentityAudit.

20 kiểm tra JS bao gồm mật khẩu có khoảng trắng, URL chuyển hướng, error parsing, CSRF, request Bearer, khôi phục sau reload, đồng thời nhiều request, không tự replay POST, đổi user giữa tab và logout.

## Chưa chạy được

- `dotnet restore/build` và executable C# checks: môi trường soạn mã không cài .NET SDK/compiler.
- DDL/constraint/EF queries: không có SQL Server ở môi trường này.
- AD Negotiate, SID lookup, account lockout/password expiry: không có kết nối AD công ty.
- IIS, DPAPI user profile/key persistence, chứng thư HTTPS, browser session end-to-end: cần môi trường Windows thật.

Không coi JS tests hoặc việc đọc XML thành công là bằng chứng C# compile, EF query đúng hay SQL script đã chạy. Thực hiện theo `docs/02_windows_setup.md` và bảng nghiệm thu trước khi triển khai sử dụng.
