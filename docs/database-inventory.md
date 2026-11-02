# Database inventory

43 core tables extracted from supplied Word; 14 designed extensions. Additions and nullable lifecycle timestamps are recorded in schema JSON and spec.

| Table | Fields | Source |
|---|---:|---|
| Accounts | 12 | Word |
| Users | 13 | Word |
| UserAddresses | 11 | Word |
| RefreshTokens | 9 | Word |
| Brands | 16 | Word |
| BrandMembers | 6 | Word |
| BrandCategories | 6 | Word |
| Categories | 8 | Word |
| Products | 16 | Word |
| ProductImages | 5 | Word |
| ProductVariants | 10 | Word |
| ProductFavorites | 4 | Word |
| ProductReviews | 8 | Word |
| Carts | 4 | Word |
| CartItems | 7 | Word |
| Orders | 19 | Word |
| OrderItems | 11 | Word |
| OrderStatusHistories | 7 | Word |
| Shipments | 7 | Word |
| Payments | 9 | Word |
| PaymentTransactions | 9 | Word |
| Refunds | 8 | Word |
| MembershipTiers | 7 | Word |
| UserMemberships | 9 | Word |
| PointTransactions | 9 | Word |
| DailyCheckIns | 6 | Word |
| Vouchers | 13 | Word |
| UserVouchers | 6 | Word |
| VoucherUsages | 7 | Word |
| VoucherTargets | 5 | Word |
| AffiliateProfiles | 6 | Word |
| AffiliateReferrals | 7 | Word |
| AffiliateCommissions | 9 | Word |
| AffiliateWallets | 6 | Word |
| AffiliateWalletTransactions | 8 | Word |
| AffiliateWithdrawals | 10 | Word |
| Services | 11 | Word |
| ServiceRequests | 9 | Word |
| ServiceQuotations | 8 | Word |
| Banners | 9 | Word |
| Articles | 9 | Word |
| Notifications | 9 | Word |
| Feedbacks | 9 | Word |
| AccountRoles | 3 | Designed extension |
| BrandCategoryMappings | 3 | Designed extension |
| SellerOrders | 8 | Designed extension |
| SellerSettlements | 9 | Designed extension |
| InventoryMovements | 6 | Designed extension |
| InventoryReservations | 7 | Designed extension |
| AffiliateLinks | 5 | Designed extension |
| AffiliateAttributions | 5 | Designed extension |
| DeviceTokens | 5 | Designed extension |
| AuditLogs | 6 | Designed extension |
| CheckInRules | 4 | Designed extension |
| PointExpirations | 6 | Designed extension |
| OrderVoucherApplications | 4 | Designed extension |
| ProductReviewImages | 4 | Designed extension |

## Bổ sung sau schema nguồn

Inventory phía trên và JSON mô tả 57 bảng nguồn Word. Model EF hiện tại có 60 bảng (chưa tính EF history): thêm `AccountActionTokens`, `SellerSettlementAdjustments`, `SellerSettlementRecoveries`. Các migrations bổ sung point/wallet debt, bank ciphertext, request hashes, xmin concurrency, review revision và constraint ledger/tracking. Migrations và EF configurations là nguồn chính xác cho schema chạy hiện tại.
