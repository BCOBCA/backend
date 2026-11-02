# BCOBCA Backend

ASP.NET Core 10, PostgreSQL, EF Core Code First. Repository: https://github.com/BCOBCA/backend. Frontend: https://github.com/BCOBCA/frontend.

## Chạy độc lập

Cài .NET SDK 10 và PostgreSQL. Clone repo vào thư mục `backend` theo tên mặc định, sau đó chạy trong thư mục đó:

```powershell
Copy-Item .env.example .env
# Điền connection string, JWT signing key và bank encryption key trong .env.
dotnet restore BCOBCA.sln
dotnet tool restore
dotnet tool run dotnet-ef database update --project BCOBCA.DAL --startup-project BCOBCA.API
dotnet run --project BCOBCA.API --no-launch-profile
```

Chỉ sao chép env mẫu khi chưa có `.env`; không ghi đè cấu hình đang dùng. JWT signing key cần ngẫu nhiên ít nhất 32 byte; bank key là base64 của 32 byte ngẫu nhiên. API và EF CLI tự nạp `.env`; biến OS/container được ưu tiên. `appsettings.json` rỗng. Không commit secrets.

Mẫu listen `http://localhost:5000`; health `/api/health`, Swagger `/swagger`, OpenAPI `/openapi/v1.json` trong Development. Cấu hình URL, CORS, auth, SMTP và chính sách tài chính qua env. Chưa có SuperAdmin thì đặt Bootstrap env và chạy:

```powershell
dotnet run --project BCOBCA.API --no-launch-profile -- --provision-superadmin
```

Xóa các biến Bootstrap sau khi dùng; không có tài khoản/mật khẩu admin mặc định.

## Source và verification

- `BCOBCA.API`: controllers, auth, middleware, OpenAPI, expiry worker.
- `BCOBCA.BLL`: DTOs, validators, business rules và services.
- `BCOBCA.DAL`: 60 bảng, configurations, migrations và repositories.
- `BCOBCA.Tests`: unit rules và PostgreSQL integration; 15 tests đã đạt trong workspace phát triển.

Migrations: `InitialCreate`, `AddAccountActionTokens`, `SecureAffiliateLedger`, `RefundRecoveryAndSettlements`, `GuardReviewRevisionAndShipmentIdentity`.

```powershell
dotnet build BCOBCA.sln
dotnet test BCOBCA.Tests
```

Integration tests cần PostgreSQL development và role có CREATEDB; tự tạo/xóa database test riêng. Không dùng connection production.

COD trước; báo giá chỉ để khách xem, nhân viên xử lý tiếp. Có cart/checkout, voucher, kho, multibrand orders, reviews/moderation, membership, affiliate, withdrawals, full refunds và settlements. Không seed dữ liệu kinh doanh giả. Thưởng theo đơn mặc định tắt cho đến khi đặt chính sách thực trong env.

Chưa xác minh SMTP thật, payment/push/carrier integrations hoặc production deployment. Các khoản thu/chi COD hiện do nhân viên xác nhận giao dịch thực tế.
