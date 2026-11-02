# ADR-001 — Runtime, cấu hình và phiên đăng nhập

Accepted — 2026-11-02.

Giữ công nghệ user yêu cầu: .NET 10, PostgreSQL/EF Code First, Expo/React Native một codebase. Ba layer API → BLL → DAL; controller không xử lý tính giá/sổ cái. Store interface giữ query EF composable, tránh generic repository CRUD dư thừa.

Theo yêu cầu bổ sung, cấu hình môi trường chỉ từ env. DotNetEnv NoClobber chạy trước WebApplicationBuilder và trong design-time factory; OS/container env thắng file local. JSON không làm nguồn cấu hình môi trường. Secret không đi vào Git.

JWT có subject account, role, jti và session family sid. Mỗi request xác minh account active và family còn phiên sống. Refresh token chỉ hash trong DB, rotation và replay revoke family. Web giữ refresh trong HttpOnly/SameSite Strict cookie; native giữ trong SecureStore. Access token nằm memory.

Frontend serialize login/register/refresh/logout; Web Locks phối hợp nhiều tab. Refresh khác identity không retry thao tác cũ; cache/profile được cập nhật hoặc xóa. Request giữ owner identity lúc bắt đầu và loại response/retry từ phiên cũ. Mất mạng không xóa refresh token hợp lệ.

AccountActionTokens là bảng bổ sung cho reset/verify; không dùng JWT dài hạn làm reset token. Reset/change password thu hồi sessions; login và các thao tác đó dùng Serializable để tránh race.

Quyền quản trị nhạy cảm kiểm tra ở BLL; role changes thu hồi sessions. SuperAdmin authority check và invariant còn ít nhất một active SuperAdmin nằm trong transaction.
