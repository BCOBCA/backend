# Review revisions và nhiều vận đơn

Đánh giá chỉ do khách đã mua gửi. Khi sửa rating/content/images, server tạo revision UUID mới và chuyển về chờ duyệt. `ReviewDto.revision` là phiên bản nội dung nhân viên đã đọc; endpoint moderation bắt buộc nhận `expectedRevision`. Sai phiên bản trả 409 để đọc lại. `xmin` bảo vệ thao tác sửa/duyệt diễn ra đồng thời; các lần gửi nội dung giống hệt giữ nguyên revision và trạng thái.

Đơn nhiều thương hiệu có thể có nhiều tracking. Vận đơn đầu tiên chuyển READY → SHIPPING trong cùng transaction; bổ sung vận đơn khi đang SHIPPING không phát sinh chuyển trạng thái lặp. Unique `(OrderId, Carrier, TrackingCode)` ngăn tạo trùng; retry tuần tự trả lại cùng ID. Race có thể trả 409 và cần retry. Hoàn thành/hủy cập nhật toàn bộ vận đơn cùng transaction đơn hàng. Đây là tracking do nhân viên nhập; chưa gọi API hãng vận chuyển.

Migration `GuardReviewRevisionAndShipmentIdentity` chỉ thêm revision/concurrency và index, không đổi bảng nghiệp vụ gốc. PostgreSQL integration kiểm tra hai tracking, retry identity và từ chối duyệt bản nội dung đã cũ.
