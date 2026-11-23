# My offers API

- `GET /api/vouchers/mine` uses MemberController's existing authorization and account-to-user lookup. No caller-supplied user ID.
- Paginated OwnedVoucherDto includes claim/voucher IDs, code and discount conditions, collection/use/expiry dates, point cost/minimum level and computed status.
- `status`: omitted = all; AVAILABLE/USED/EXPIRED filter current state; COLLECTED/REDEEMED filter original free/point-cost source, including history. Unknown filters return 400.
- USED precedes EXPIRED; otherwise inactive/unknown claims are UNAVAILABLE and future-start vouchers UPCOMING. All history remains visible under all/source filters.
- Reuses UserVoucher and Voucher entities, no migration. Existing redemption, cancellation and checkout rules remain authoritative.
- Frontend `/my-offers` requires this endpoint; deploy backend alongside frontend.
- Regression assertions live in VoucherRedemptionFlowTests. Test uses an isolated temporary PostgreSQL database and the original environment connection string to retain its password after admin connection opens.
- Validation: .NET 10 build and the PostgreSQL VoucherRedemptionFlowTests passed, including ownership, filters, pagination and lifecycle assertions. Default machine SDK is .NET 9; validation used a .NET 10 SDK under the temporary directory.
