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

## Chạy bằng Docker (local development)

Cài Docker Engine/Desktop với Linux containers và Docker Compose v2. Chạy trong
thư mục `backend`; không cần cài .NET SDK hoặc PostgreSQL trên máy:

```powershell
# Chỉ tạo file nếu chưa có .env; không ghi đè cấu hình hiện tại.
if (!(Test-Path .env)) { Copy-Item .env.example .env }
```

Điền `Jwt__SigningKey` và `Security__BankDataKey` như hướng dẫn phía trên.
Thêm `POSTGRES_PASSWORD` vào `.env` nếu file hiện tại chưa có biến này.
Dùng mật khẩu hex ngẫu nhiên, ví dụ tạo bằng PowerShell:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
[BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
```

Dùng một giá trị ngẫu nhiên riêng cho mỗi secret. JWT key có thể dùng hex như
trên; bank key dùng `[Convert]::ToBase64String($bytes)` với 32 byte mới.
Compose ghi đè connection string để kết nối tới `db:5432` bằng
`POSTGRES_PASSWORD`; không cần sửa `ConnectionStrings__Default` đang dùng local.
Các cấu hình nghiệp vụ, CORS, JWT và SMTP còn lại lấy từ `.env`.

```powershell
docker compose config --quiet
docker compose up --build -d
docker compose logs migrate api
Invoke-RestMethod http://localhost:5000/api/health
```

PostgreSQL phải healthy trước khi service `migrate` chạy EF migrations; API chỉ
khởi động khi migration thành công. Nếu migration lỗi, xem logs và sửa nguyên
nhân rồi chạy lại `docker compose up --build -d`.
API: `http://localhost:5000`, Swagger: `http://localhost:5000/swagger`.
Compose dùng Development và chỉ publish API trên loopback của máy host;
PostgreSQL chỉ mở trong mạng Compose. Cấu hình này dành cho phát triển local.

Provision SuperAdmin lần đầu sau khi điền Bootstrap env:

```powershell
docker compose run --rm api --provision-superadmin
```

Xóa Bootstrap env sau khi dùng, rồi chạy `docker compose up -d --force-recreate api`
để loại các biến này khỏi container API. Dừng bằng `docker compose down`;
database giữ trong named volume. `docker compose down -v` xóa toàn bộ database
Docker. Đổi `POSTGRES_PASSWORD` trong `.env` không đổi mật khẩu của database đã
khởi tạo; phải đổi role password trong PostgreSQL tương ứng.

Build image API riêng: `docker build --target final -t bcobca-api .`.
Image không chứa `.env`, source tests hoặc build outputs từ máy host; API chạy
bằng non-root user. Startup ordering theo [Docker Compose](https://docs.docker.com/compose/how-tos/startup-order/).

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

Phí sàn được lưu trong database và quản lý bằng `GET`/`PUT /api/admin/settings/platform-fee` (Admin/SuperAdmin), không lấy từ `Settlement__PlatformFeeRate` nữa. Migration khởi tạo 0%; xem [hướng dẫn cấu hình phí sàn](docs/handoffs/admin-platform-fee.md) để tích hợp màn hình admin và triển khai.
