# ADR-002 — COD, sổ cái và các khoản thu hồi

Accepted — 2026-11-02. User chọn COD trước; online payment hoãn.

## Giá và kho
Server tính giá biến thể, voucher có phạm vi, phí giao hàng và total. Preview không ghi dữ liệu. ExpectedTotal chỉ dùng để yêu cầu xác nhận lại khi giá đổi, không làm nguồn tính giá. Idempotency key gắn owner + request hash. Voucher phân bổ theo largest remainder cents, không dòng nào âm.

Checkout giữ kho, tạo Orders/OrderItems/SellerOrders/Payment/usage trong Serializable transaction. xmin trên projections tài chính/kho và unique indexes chặn double processing. Worker chỉ tự hủy đơn PENDING/UNPAID quá hạn; đơn đã xác nhận không bị worker giải phóng kho giữa vận chuyển.

COD chỉ PAID khi Admin xác nhận đã thu tiền; người mua không tự xác nhận. Các thao tác chi trả/hoàn tiền là ghi nhận xác nhận thủ công có mã giao dịch thực, không gọi ngân hàng.

## Điểm và Affiliate
Chỉ hoàn thành đơn đã trả tiền mới tích điểm/hoa hồng. Cơ sở tính là tiền hàng sau giảm giá, không gồm vận chuyển. Rate/đơn vị quy đổi lấy env, mặc định tắt cho tới khi có chính sách thực.

Point transaction ORDER_EARN, kể cả 0 điểm, là marker idempotency cho xử lý hoàn thành. Hoàn đơn ghi ORDER_REVERSE và điều chỉnh doanh số đủ điều kiện/tier. Nếu điểm đã dùng, ghi PointDebt; phần thưởng tiếp theo cấn trừ, không làm AvailablePoints âm.

Wallet ledger tách bằng Type:
- CREDIT/DEBIT/RECOVERY_DEBIT: AvailableBalance.
- PENDING_CREDIT/PENDING_DEBIT: PendingBalance.
- DEBT_CREDIT/DEBT_DEBIT: DebtBalance.

BalanceAfter thuộc bucket tương ứng. Duyệt commission chuyển pending → available; không duyệt khi đơn có refund chờ xử lý. Withdrawal trừ available ngay để giữ tiền; rejection credit trả lại và cấn trừ debt trước. PAID cần mã chuyển tiền thực và không còn debt. Unique event/reference và trạng thái terminal ngăn double credit/reversal.

Refund sau payout không sửa/xóa lịch sử payout: thu hồi phần available còn lại, tạo DebtBalance cho phần thiếu. Các credit sau tự cấn trừ. BankName lưu được để hiển thị; account number/holder lưu masked, payload đầy đủ AES-256-GCM với random nonce, key từ env. Chỉ Admin đọc plaintext qua endpoint có audit và no-store.

## Đối soát thương hiệu
Mỗi SellerOrder có settlement: gross là tiền dòng hàng sau discount, fee lấy rate đã cấu hình, net là phần merchant được hưởng. Affiliate là chi phí của nền tảng; không tự trừ lần nữa vào merchant ngoài platform fee đã khai báo.

Settlement PENDING đợi hold và không có refund mở; PAID ghi mã chuyển tiền và PaidAmount. Refund hủy settlement chưa chi. Với settlement đã chi, thêm SellerSettlementAdjustment, giữ nguyên snapshot gốc. Lần đối soát sau cấn trừ vào RemainingAmount và ghi SellerSettlementRecovery.

Nếu net bị cấn trừ hết, PaidAmount=0, reference dạng OFFSET:id là bút toán cấn trừ, không phải chuyển tiền ngân hàng. Refund một settlement từng bị offset vẫn tạo lại khoản cần thu hồi bằng NetAmount (không chỉ PaidAmount), để không mất khoản phải thu cũ.

Giai đoạn này hỗ trợ hoàn toàn bộ đơn; hoàn một phần cần thiết kế phân bổ lại items, phí, điểm, commission và settlement trước khi mở API. Không suy diễn partial refund từ thao tác full refund hiện tại.

## Các bảng bổ sung
AccountActionTokens (auth); SellerSettlementAdjustments và SellerSettlementRecoveries (receivable/recovery ledger). Các quan hệ Word vẫn được giữ. PointDebt, DebtBalance và các trường idempotency/encryption/version là bổ sung kỹ thuật cần cho tính đúng đắn; không nhét quan hệ nghiệp vụ vào JSON. JSON bank ciphertext không thay thế FK.
