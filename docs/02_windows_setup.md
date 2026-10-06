# Cài đặt và thử trên Windows

## 1. Môi trường

- Máy Windows có .NET 10 SDK, hoặc Windows Server/IIS có .NET 10 Hosting Bundle.
- SQL Server development riêng; tạo database FinanceLogistics_Dev theo quy trình nội bộ. Không chạy script vào E-Leave.
- AD domain có thể truy cập được từ máy chạy API, cấu hình trust/DNS theo IT hạ tầng. Adapter dùng Negotiate + Signing + Sealing và danh tính DOMAIN\SAM được cấu hình. Không tự fallback sang SimpleBind không bảo vệ.
- FE và API cùng `https://localhost:7243` khi dev; production dùng một HTTPS origin riêng ở root. Chưa hỗ trợ IIS virtual-directory path base trong gói này.

Gói chọn .NET 10 LTS cho dự án mới. E-Leave net9.0 không cần thay đổi để chạy Finance–Logistics. Nếu hạ tầng bắt buộc net9.0 thì phải retarget toàn bộ package tương thích và chạy lại kiểm tra; không chỉ đổi một dòng TargetFramework. Kiểm tra bản vá Microsoft được công ty phê duyệt trước khi triển khai.

## 2. Database

Trong SQL Server Management Studio, chọn đúng database dev rồi chạy lần lượt:

1. `database/001_identity_schema.sql`.
2. `database/002_catalog.sql`.
3. Điền và chạy `database/003_provision_user_TEMPLATE.sql` cho từng tài khoản thử.

001 tạo phần Identity/catalog cần thiết nếu database rỗng. Nếu đã cài schema V1, nó giữ các bảng hiện có và bổ sung bốn bảng mới. Đây là script phát triển có guard tạo bảng, không phải migration tự sửa mọi schema đã bị tùy biến. Nếu schema đang dùng khác V1, so sánh trước khi chạy.

Không chạy `EnsureCreated`, không chạy toàn bộ schema V1 lên database đã có một phần bảng, không dùng EF migration tự suy diễn lại schema. Giai đoạn tích hợp toàn hệ thống phải hợp nhất migration chính thức từ schema thực tế.

Tài khoản SQL của ứng dụng chỉ cần SELECT trên bảng Identity/catalog, SELECT/INSERT/UPDATE trên `sec.AuthSession`, INSERT trên `sec.IdentityAudit`. Không cần db_owner hoặc quyền cấp role cho user. DBA/migration account riêng chạy DDL và provisioning.

## 3. Lấy danh tính test từ AD

IT AD có thể dùng Active Directory Users and Computers hoặc RSAT PowerShell, ví dụ:

```powershell
Get-ADUser -Identity '<sam-account-name>' -Server '<ad-domain>' -Properties UserPrincipalName,DisplayName |
  Select-Object SamAccountName,UserPrincipalName,DisplayName,@{Name='WindowsSid';Expression={$_.SID.Value}}
```

Điền SID, UPN, SAM đúng người vào provisioning script. SID trong AppUser phải trùng SID tìm được sau xác thực. Domain/UPN khác nhau không có nghĩa chắc chắn là hai AD directory khác nhau; phải nhờ IT xác nhận topology thực tế.

Đề nghị bốn cấu hình test: SYSTEM_ADMIN, FINANCE, LOGISTICS+1000, LOGISTICS+6000. Có thể một tài khoản được cấp nhiều role nhưng cần thêm các tài khoản Logistics riêng để kiểm tra chặn chéo site.

Đăng ký SYSTEM_ADMIN đầu tiên khi DB rỗng, sau đó dùng SID admin làm GrantedBySid cho những tài khoản sau. LoginAlias có khóa duy nhất toàn hệ thống. Nếu username ngắn bị trùng giữa hai domain, chỉ đăng ký UPN/DOMAIN\username cho trường hợp xung đột; không đoán người theo tên ngắn.

## 4. Cấu hình dev bằng User Secrets

Từ thư mục gói, PowerShell:

```powershell
$projectPath = 'src/FinanceLogistics.Api/FinanceLogistics.Api.csproj'
dotnet user-secrets set 'ConnectionStrings:FinanceLogistics' 'Server=<SQL_HOST>;Database=FinanceLogistics_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;' --project $projectPath
dotnet user-secrets set 'Identity:Directories:CORPORATE:DomainName' '<AD_DOMAIN_OR_DC>' --project $projectPath
dotnet user-secrets set 'Identity:Directories:CORPORATE:NetBiosName' '<DOMAIN_NETBIOS>' --project $projectPath
dotnet user-secrets set 'Identity:Directories:CORPORATE:SearchBaseDn' '<OU_OR_DOMAIN_DN>' --project $projectPath
$identityKeyPath = Join-Path $env:LOCALAPPDATA 'FinanceLogistics\Keys'
dotnet user-secrets set 'Identity:KeyRingPath' $identityKeyPath --project $projectPath
$randomBytes = New-Object byte[] 32
$randomProvider = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$randomProvider.GetBytes($randomBytes)
dotnet user-secrets set 'Identity:SigningKeyBase64' ([Convert]::ToBase64String($randomBytes)) --project $projectPath
$randomProvider.Dispose()
```

Không copy nguyên các placeholder. SearchBaseDn có thể để rỗng nếu IT chấp nhận tìm trên domain; OU chỉ dành cho một nhánh có thể khiến user ở OU khác không tìm được. Directory key `CORPORATE` phải khớp `sec.DirectoryAccount.DirectoryKey`. Có thể cấu hình thêm directory nếu thật sự có domain/trust khác.

SQL Server phải có certificate phù hợp được máy API tin cậy khi TrustServerCertificate=False. User Secrets là công cụ dev và không tự mã hóa file secrets; bảo vệ tài khoản/máy dev, không commit hoặc gửi chúng. Production dùng cách lưu secrets được IT quản lý.

## 5. Build và chạy

```powershell
dotnet --info
dotnet restore src/FinanceLogistics.Api/FinanceLogistics.Api.csproj
dotnet build src/FinanceLogistics.Api/FinanceLogistics.Api.csproj --no-restore
dotnet run --project tests/Identity.Checks/Identity.Checks.csproj
node --test tests/frontend/identity.test.mjs
dotnet dev-certs https --trust
dotnet run --project src/FinanceLogistics.Api/FinanceLogistics.Api.csproj --launch-profile https
```

Mở `https://localhost:7243/login.html`. Không mở file HTML bằng file:// và không dùng một Live Server khác port; bản V1.1 có chủ đích dùng cùng origin để đơn giản phiên/cookie/CORS.

Không cần Swagger, EPPlus, TimeKeepingDb, OTP hoặc module E-Travel cho bước này. Hợp đồng API nằm trong docs, endpoint có code thực hiện.

## 6. IIS cho đúng cơ chế form AD

- Tạo application/site Finance–Logistics riêng, HTTPS certificate đúng hostname.
- IIS Anonymous Authentication bật để form/API chạy được; không bật browser Windows Authentication cho ứng dụng này như thể đây là Integrated SSO. AD được gọi bên trong AuthService.
- Application pool No Managed Code; identity có quyền tới SQL theo cách được chọn.
- Cấu hình AllowedHosts đúng hostname, connection string, AD directories, signing key riêng qua cơ chế secrets của hạ tầng. Không dùng lại JWT key của E-Leave.
- Key ring nằm ngoài wwwroot, có ACL chỉ dành cho service identity và người quản trị được phép. Gói dùng DPAPI theo Windows identity hiện tại; IIS cần user profile phù hợp, kiểm tra Load User Profile và quyền thư mục khi triển khai. Đổi app pool identity có thể khiến key cũ không giải mã được.
- Một instance: rate limit theo IP nằm trong bộ nhớ. Nếu đi qua proxy, phải xác minh RemoteIpAddress đúng và chỉ tin forwarded headers từ proxy đã cấu hình; không tin X-Forwarded-For do client tự gửi. Không copy cấu hình trust mọi proxy.
- Liveness trả 200 chỉ cho biết tiến trình sống. Hoàn tất test AD + SQL + quyền trước khi báo hệ thống sẵn sàng.

## 7. Xử lý nhanh lỗi

| Hiện tượng | Kiểm tra |
|---|---|
| Server từ chối khởi động | Missing SQL connection, signing key, key ring path hoặc AD directory |
| 401 INVALID_CREDENTIALS | Alias đã provision chưa, đúng password chưa, SID có khớp không, account có bị khóa/expired trong AD không |
| 403 ACCESS_NOT_GRANTED | AppUser.IsActive, UserRole; không tự đổi whitelist FE |
| 503 DIRECTORY_UNAVAILABLE | DNS/DC/trust, quyền tìm OU, cơ chế Negotiate và chính sách domain; không tắt Signing/Sealing để lách lỗi |
| 503 DATABASE_UNAVAILABLE | SQL connection, certificate, quyền, bảng còn thiếu; dùng correlationId để lần log |
| 400 CSRF_INVALID | Cookie có gửi không, có đúng HTTPS/same origin không, key ring có đổi không |
| Site không hiện | UserSite, cfg.Site.IsActive, SourceScope/ScopeSite, source.IsActive |
| Tải lại trang bị logout ngay | Cookie Secure trên HTTPS, phiên hết hạn hoặc bị thu hồi, key/config giữa instance không đồng nhất |

Gói không chứa cơ chế reset mật khẩu AD. Người dùng đổi mật khẩu qua quy trình công ty. IT có thể khóa AppUser và thu hồi AuthSession bằng quy trình quản trị được duyệt; màn hình quản trị quyền là hạng mục tiếp theo, chưa nằm trong Identity slice này.
