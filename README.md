**FinanceLogistics_DEV — Workspace mốc đầu tiên**

Workspace này được khởi tạo từ Identity V1.1 cùng các ví dụ Clean Architecture cho PO. Đọc docs/Huong_dan_AZ.md theo thứ tự.

| Thành phần | Trạng thái |
|---|---|
| Identity login/phiên/quyền | Có source; cần cấu hình và kiểm tra AD/SQL thật |
| Domain/Application/Infrastructure | Có project, rule/entity/port/Fluent Mapping mẫu |
| Database import | Có script 004 cho schema import chưa cài ở DEV |
| API import, reader, worker, màn hình PO | Cần triển khai theo hướng dẫn; chưa có chức năng giả |

API target net10.0-windows vì AD adapter hiện có. Các class library mới target net10.0. Script khởi tạo chọn SDK 10 stable đang có trên máy qua global.json và cập nhật các package Microsoft của bản sao lên 10.0.12; các tài liệu baseline có thể còn ghi 10.0.10 là phiên bản bàn giao Identity trước đây.

```powershell
dotnet restore FinanceLogistics.sln
dotnet build FinanceLogistics.sln --no-restore
dotnet run --project tests/Workflow.Checks
dotnet run --project tests/Identity.Checks
node --test tests/frontend/identity.test.mjs
```

Sau khi cấu hình SQL/AD/secrets:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/FinanceLogistics.Api --launch-profile https
```

Mở https://localhost:7243/login.html. Chưa có trang po-upload.html trước khi bạn thực hiện các bước viết frontend/import trong hướng dẫn.
