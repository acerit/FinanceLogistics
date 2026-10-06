# Hợp đồng frontend–backend · Identity V1.1

Trạng thái: đã có code tương ứng trong gói để review và chạy thử; chưa nghiệm thu tích hợp. Ngày 23/09/2026.

## Điều đã xác định từ E-Leave

Frontend gửi username/password đến AuthController. Backend dùng AD cho tài khoản IsAdaccount, hoặc BCrypt cho tài khoản local, rồi cấp JWT. Finance–Logistics V1.1 chọn nhánh AD.

Việc dùng cùng mật khẩu máy tính không đồng nghĩa trình duyệt đang dùng Windows SSO. Hai form Acer/HSN cùng gọi một login API; whitelist trong JavaScript không cấp/thu hồi quyền API.

## Quy ước chung

- API và frontend cùng HTTPS origin. API nằm tại `/api`; không cấu hình AllowAnyOrigin.
- JSON camelCase; timestamp là UTC ISO 8601; `userId` là chuỗi vì SQL bigint có thể vượt độ chính xác JavaScript Number.
- Các API nghiệp vụ chỉ nhận `Authorization: Bearer <token>`. Cookie không tự xác thực những endpoint đó.
- Auth endpoints `/api/auth/*` không dùng Bearer. Cookie chỉ dùng khôi phục/đăng xuất phiên.
- Dữ liệu nhạy cảm trả `Cache-Control: no-store`; password/token không vào log.
- Không nhận role, site, actorUserId từ client làm căn cứ cấp quyền.
- Không tạo/cấp quyền user tự động chỉ vì có email công ty.

## Endpoint

| Method + URL | Gửi gì | Thành công | Ghi chú |
|---|---|---|---|
| GET /api/auth/csrf | Cookie trình duyệt tự quản lý | 200 `{requestToken, headerName}` | Trước thao tác auth POST; không gửi Bearer |
| POST /api/auth/login | JSON `{username,password}` + X-CSRF-TOKEN | 200 AuthResponse + Set-Cookie | AD đúng + có tài khoản ứng dụng được cấp quyền |
| POST /api/auth/token | Cookie + X-CSRF-TOKEN, không cần body | 200 AuthResponse | Khôi phục phiên sau reload hoặc lấy access token mới |
| POST /api/auth/logout | Cookie + X-CSRF-TOKEN | 204, thu hồi phiên và xóa cookie | Idempotent nếu cookie thiếu/không hợp lệ; lỗi DB vẫn trả lỗi |
| GET /api/me | Bearer token | 200 UserProfile | Đọc user, role, site hiện tại từ DB |
| GET /api/me/source-access | Bearer token | 200 SourceAccessResponse[] | Chỉ liệt kê nguồn/phạm vi được phép |
| GET /api/source-access/{sourceCode}/{scopeCode} | Bearer token | 200 SourceAccessResponse | 403 nếu nguồn hợp lệ nhưng không có quyền; chưa upload file |
| GET /api/health/live | Không cần đăng nhập | 200 `{status:"alive"}` | Chỉ liveness; không chứng minh AD/SQL kết nối tốt |

`/api/auth/token` là endpoint khôi phục phiên nội bộ, không phải OAuth token endpoint. Giao diện chỉ gọi thông qua `api-client.mjs`.

## Request đăng nhập

```json
{
  "username": "user@example.test",
  "password": "<mật khẩu do người dùng nhập, giữ nguyên>"
}
```

Username tối đa 320 ký tự; password tối đa 1024. Username trim + uppercase để tìm alias đã đăng ký. Password không trim, không lowercase, không log, không lưu.

`sec.LoginAlias` quyết định chấp nhận cách gõ nào. Không tự đoán domain từ suffix email. Username không có domain chỉ dùng được khi alias đó không mơ hồ giữa các directory.

## AuthResponse

```json
{
  "token": "<access JWT>",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-09-23T10:10:00Z",
  "sessionExpiresAtUtc": "2026-09-23T12:00:00Z",
  "user": {
    "userId": "42",
    "username": "logistics.user",
    "upn": "logistics.user@example.test",
    "displayName": "Người dùng mẫu",
    "roles": ["LOGISTICS"],
    "sites": ["1000"],
    "permissions": ["sources.confirm", "sources.read", "sources.upload"]
  }
}
```

Ví dụ hoàn toàn giả; thời điểm thực tế do backend tạo. `/api/me` trả đúng object `user`, không bọc bằng AuthResponse. UPN có thể null và không được mặc định coi là email nhận thông báo.

`permissions` dùng để quyết định màn hình/nút có thể xuất hiện. Backend vẫn phải kiểm tra quyền trên từng tài nguyên; có `sources.upload` không có nghĩa upload được mọi site.

## Quyền

| Trường hợp | PO/AP/Payment/EX | Logistics 1000 + Pin test | Logistics 6000 | Quản trị kỹ thuật |
|---|---|---|---|---|
| FINANCE | Có | Có | Có | Chỉ khi có thêm SYSTEM_ADMIN |
| LOGISTICS + 1000 | Không | Có | Không | Không |
| LOGISTICS + 6000 | Không | Không | Có | Không |
| LOGISTICS + cả hai site | Không | Có | Có | Không |
| SYSTEM_ADMIN | Không mặc định | Không mặc định | Không mặc định | Có |

Scope chứa nhiều site yêu cầu Logistics có quyền trên **tất cả** site của scope. Source/scope/site bị vô hiệu hóa không được truy cập. Logistics chưa được gán site vẫn có thể đăng nhập nhưng không có nguồn để thao tác. Chỉ có role không được hệ thống biết đến thì từ chối cấp phiên.

Kiểm tra nguồn dựa trên catalog SQL, không dựa vào `site=1000` do frontend tự khai báo. Với file/batch ở bước sau, API phải tải batch → requirement → source/scope từ DB rồi kiểm tra.

## Phiên và đăng xuất

1. Login hợp lệ tạo session mới và secret ngẫu nhiên 256 bit. Database chỉ lưu SHA-256 của secret.
2. Secret được gửi trong cookie `__Host-fl-session`: Secure, HttpOnly, SameSite=Strict, Path=/, không có Domain.
3. JWT có issuer/audience/key riêng cho Finance–Logistics; frontend chỉ giữ trong bộ nhớ module.
4. JWT mặc định 10 phút; session mặc định 120 phút tuyệt đối. `/token` không kéo dài session.
5. Mỗi API có Bearer đều kiểm tra chữ ký, hạn, issuer, audience, session, user.IsActive và tải role/site mới từ DB. Quyền nhúng trong JWT không được dùng như snapshot bất biến.
6. Logout thu hồi session đang giữ cookie. Các JWT của session đó bị chặn ở request tiếp theo. Login lại trong cùng trình duyệt thu hồi session cũ nếu cookie cũ hợp lệ. Các trình duyệt khác có phiên riêng.
7. Chuyển user ở tab khác khiến client cũ yêu cầu tải lại trang, không âm thầm gửi request dưới user mới.
8. Hủy quyền trong DB ảnh hưởng request tiếp theo; request đã được cấp quyền và đang xử lý không thể bị rollback tự động bởi Identity. Upload/confirm cần kiểm tra lại trước commit.

Đổi mật khẩu hoặc khóa tài khoản **trong AD** không tự gửi sự kiện thu hồi vào database ứng dụng. Login mới kiểm tra AD; session đang tồn tại có thể tiếp tục tới hết hạn nếu IT chưa thu hồi session/khóa AppUser. Cần quyết định chính sách này trước production, nhất là khi yêu cầu thu hồi tức thời sau thay đổi AD.

## Lỗi

```json
{
  "code": "SOURCE_FORBIDDEN",
  "message": "Bạn không có quyền truy cập nguồn dữ liệu này.",
  "correlationId": "<mã do server tạo>"
}
```

| HTTP / code | Ý nghĩa và xử lý FE |
|---|---|
| 400 INVALID_REQUEST | Thiếu/sai dữ liệu input |
| 400 CSRF_INVALID | CSRF không khớp; client lấy lại và thử tối đa một lần, do action chưa chạy |
| 401 INVALID_CREDENTIALS | Sai AD credentials, SID không khớp hoặc alias chưa đăng ký; dùng thông báo chung |
| 401 AUTH_REQUIRED / SESSION_EXPIRED | Token/phiên không hợp lệ; khôi phục hoặc trở lại login |
| 403 ACCESS_NOT_GRANTED | Đã xác thực nhưng AppUser bị khóa hoặc chưa có role hợp lệ |
| 403 SOURCE_FORBIDDEN | Không đủ quyền trên nguồn/phạm vi này |
| 404 SOURCE_NOT_FOUND | Source + scope không có trong catalog |
| 429 RATE_LIMITED | Chờ; không lặp login liên tục |
| 503 DIRECTORY_UNAVAILABLE / DIRECTORY_CONFIGURATION | AD không truy cập được hoặc cấu hình chưa đầy đủ |
| 503 DATABASE_UNAVAILABLE | SQL không khả dụng hoặc lỗi lưu dữ liệu; IT xem correlationId |
| 500 SERVER_ERROR | Lỗi chưa phân loại; không trả stack trace ra FE |

FE phân biệt lỗi mạng với response HTML/JSON lỗi từ máy chủ. Lỗi framework/proxy trước khi tới middleware có thể không đúng envelope; client vẫn xử lý được response không phải JSON.

## Frontend sử dụng

```javascript
import { createIdentityClient } from '/js/api-client.mjs';
const api = createIdentityClient();
const me = await api.me();
const sources = await api.sources();
```

Không tự đọc JWT để lấy quyền, không lưu password/token vào localStorage. JavaScript vẫn cần phòng XSS; giữ CSP, dùng textContent khi hiển thị dữ liệu. HttpOnly không thay thế bảo vệ XSS/CSRF.

GET có thể được client thử lại một lần sau 401. POST upload/confirm không được tự replay trong module này; sẽ bổ sung idempotency ở hợp đồng upload riêng.
