# Nghiệm thu Identity và chuyển sang upload PO

## Điều kiện nghiệm thu trên môi trường công ty

| Nhóm | Trường hợp cần kiểm tra | Kết quả phải có |
|---|---|---|
| Build | Restore/build API + chạy C# checks | Không lỗi build; checks qua |
| SQL | Chạy DDL/catalog/provision trong database dev | Không lỗi constraint; catalog có tám source-scope theo phạm vi ban đầu |
| AD | User đúng password, sai password, locked/disabled, SID không khớp | Chỉ người hợp lệ được cấp session |
| Alias | UPN, username ngắn, DOMAIN\username đã đăng ký | Đúng một user; không tự nối hoặc xóa suffix domain |
| Password | Khoảng trắng đầu/cuối nếu chính sách domain cho phép | FE/BE không tự cắt; adapter nhận nguyên chuỗi |
| Cấp quyền | Có AD account nhưng chưa provision; AppUser khóa; chưa có role | Không được dùng API nghiệp vụ |
| Finance | /me/source-access | Đủ tám nguồn/phạm vi đang hoạt động |
| Logistics 1000 | /me/source-access và gọi thẳng API 6000/PO | Chỉ LOGISTICS_1000 và PIN_TEST_1000; 403 khi gọi chéo |
| Logistics 6000 | /me/source-access và gọi thẳng API 1000/PO | Chỉ LOGISTICS_6000; 403 khi gọi chéo |
| Admin | Chỉ role SYSTEM_ADMIN | Đăng nhập được; không tự có quyền Finance |
| Nhiều role/site | Finance + Admin hoặc Logistics hai site | Không mất role do FirstOrDefault; đúng phạm vi được cấp |
| Phiên | Reload, hết JWT, hết session, logout rồi dùng JWT cũ | Reload/renew đúng; hết session yêu cầu login; logout chặn JWT cũ |
| Đổi quyền | Đổi UserRole/UserSite/IsActive trong DB rồi gọi API | Áp dụng ở request sau; không phụ thuộc localStorage hoặc JWT role cũ |
| Cookie/CSRF | Thiếu header CSRF, cross-site POST, cookie trên HTTPS | Auth POST bị chặn khi CSRF sai; cookie đúng HttpOnly/Secure/Strict |
| Hạ tầng | SQL/AD tạm không truy cập được | Lỗi có mã rõ; không giả thành login thành công |
| Trình duyệt | Edge/Chrome công ty, refresh/multi-tab, redirect URL lạ | Không chuyển ra ngoài; không âm thầm đổi user trên tab cũ |

Phải kiểm tra trên API, không chỉ nhìn nút ẩn/hiện. Đặc biệt lấy một token Logistics 1000 và gọi `/api/source-access/LOGISTICS_6000/SITE_6000`; kết quả phải là 403. Tương tự gọi `/api/source-access/PO_UPDATED/FINANCE_SHARED` bằng Logistics phải bị chặn.

Các file/prototype trước đây không chứng minh những test này đã qua. Người thực hiện nên ghi ngày, môi trường, người test, expected/actual, lỗi và cách sửa.

## Những gì đã có và chưa có

| Đã có code | Chưa triển khai ở đợt này |
|---|---|
| Login AD, API JWT, phiên cookie, CSRF, logout | Browser Integrated Windows SSO / Entra SSO |
| AppUser, role, site, alias, session, audit | UI quản trị user/role/site và đồng bộ AD group tự động |
| Kiểm tra quyền trên nguồn/phạm vi | Upload, đọc CSV/Excel, preview, confirm |
| Shared API client, form login, trang kiểm tra quyền | Dashboard nghiệp vụ, báo cáo, email deadline |
| Template provisioning SQL | Reset password/OTP/local account |

Không đánh dấu task “Identity hoàn thành” chỉ vì đã có code ZIP. Chốt hoàn thành sau khi build, chạy SQL và thử AD/quyền trên môi trường thật đạt các tiêu chí bên trên.

## Thứ tự bước kế tiếp: PO upload

1. Giữ nguyên Identity contract và gắn người đăng nhập vào các API nghiệp vụ. Khi ghi UploadedByUserId lấy từ CurrentUser.UserId, không lấy từ body.
2. Chốt header/template PO_updated: tên chính xác 17 cột, kiểu dữ liệu, cột bắt buộc, quy tắc ngày/số/thập phân, sheet nào cần đọc, đơn vị tiền. Chưa cần mapping 42 cột còn lại để làm upload.
3. Chốt nghĩa của một file tháng: snapshot hay delta; upload lại có thay bản trước không; ngày nghiệp vụ thuộc tháng nào; xử lý thiếu hoặc trùng dòng.
4. Dùng V1 ReportingPeriod → SubmissionRequirement → ImportBatch → StoredFile → RawRow. Lưu file gốc, raw data, kết quả validation và bản preview trước khi confirm.
5. Khi POST upload, load requirement từ SQL và kiểm tra SourceCode/ScopeCode bằng AccessRules với operation="upload". Khi preview/download, load batch/file rồi kiểm tra lại nguồn/phạm vi. Không tin source/site/userId tự khai trong request.
6. Sau khi user confirm, kiểm tra lại active user/quyền + kỳ OPEN + version/rowversion trong transaction. Chỉ lúc này dữ liệu mới trở thành nguồn đang dùng; chưa gọi upload là đã hoàn tất nộp.
7. Sau khi upload/preview/confirm PO chạy ổn mới thêm AP/Payment/EX/Logistics và tính PO receipt bằng mapping đã được Finance xác nhận.

## Những câu cần chốt trước PO

- Một dòng PO có khóa duy nhất là gì, và có thể xuất hiện nhiều lần trong cùng tháng hay nhiều tháng không?
- File tháng là toàn bộ dữ liệu tới hiện tại hay chỉ phát sinh tháng đó?
- Mốc deadline theo giờ Việt Nam, ai được upload trễ, ai được mở lại kỳ?
- Cột thiếu là lỗi chặn, cảnh báo hay nguồn chưa đủ? N/A chỉ là hiển thị; DB tiền/ngày không lưu chuỗi N/A.
- Ai được confirm dữ liệu Logistics: chính Logistics trong site hay Finance? Quyền kỹ thuật V1.1 có sources.confirm cho cả hai, nhưng policy nghiệp vụ cuối cùng có thể siết ở endpoint confirm.

Những câu này ảnh hưởng luồng nhập dữ liệu, không ngăn việc triển khai Identity trước.
