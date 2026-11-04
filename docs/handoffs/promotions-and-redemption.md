# Promotions and point redemption — 2026-11-04

## Delivered
- Existing Voucher/UserVoucher/VoucherUsage/PointTransaction entities and claim endpoint are reused. New voucher metadata: `PointCost` (long, default0), `MinLevel` (1..10, default1), `IsFlashSale` (defaultfalse).
- Admin VoucherInput adds these optional fields; negative costs, invalid levels and point offers with PerUserLimit other than1 are rejected. Issued/used voucher rules cannot be rewritten; create a new code to change terms.
- Public GET `/api/vouchers?page=1&pageSize=50` returns active available offers. Anonymous responses never expose another user's claimed state. Signed-in responses include the caller's claimed flag. Existing paging contract remains.
- Authorized POST `/api/vouchers/{id}/claim` receives free vouchers or redeems point offers. Serializable transaction checks lifetime-derived level, available points, debt and issuance limit. Point debit + UserVoucher are saved atomically; repeat claims return without another debit.
- PointTransaction type `VOUCHER_REDEEM`, negative Points, ReferenceType `UserVoucher`, ReferenceId matching the unique claim. LifetimeEarnedPoints and level remain unchanged.
- Checkout/preview checks minimum level and requires ownership of an AVAILABLE redeemed voucher. Entering a public code cannot bypass its point cost. Existing cancellation restores the same voucher entitlement; it does not refund or re-charge redeemed points. Existing completed-order refund policy is unchanged.
- Paid-offer global limit counts issued UserVouchers, avoiding spending points for an oversubscribed offer. Unique user/voucher index and membership xmin protect concurrent requests; serialization conflicts return retryable409.
- Restricted point/level offers use `MEMBER_OFFER` status. Older API releases only accept ACTIVE, so they cannot accidentally issue/apply these offers without enforcing the new restrictions. Updated API accepts ACTIVE and eligible MEMBER_OFFER; free unrestricted offers remain ACTIVE.

## Seed migration
`20261104160000_AddVoucherRedemptionOffers` adds metadata/check constraint and six dated, finite offers. Does not alter existing voucher codes (`ON CONFLICT (Code) DO NOTHING`). No new entity/table.

| Code | Benefit | Minimum order | Point cost / level | Limit |
|---|---|---:|---|---:|
| BCA20K | Fixed20,000 | 200,000 | Free, Lv1 | 1000 |
| BCA10PHANTRAM | 10%, cap50,000 | 200,000 | Free, Lv1 | 1000 |
| BCALV350K | Fixed50,000 | 500,000 | Free, Lv3 | 1000 |
| BCADOI30K | Fixed30,000 | 200,000 | 500points, Lv1 | 500 |
| BCADOI50K | Fixed50,000 | 300,000 | 800points, Lv1 | 500 |
| BCAFLASH15 | 15%, cap30,000 | 150,000 | Free, Lv1 | 100 |

Each account uses a code once. Normal offers expire90days after migration; flash expires3days after migration. Start is one minute before migration. Discounts apply to eligible merchandise, not shipping. No shipping or physical gift feature was invented.

Rollback deactivates seeded offer IDs and drops new metadata; claim/order/point history is preserved. Review rollback financially before reverting a deployed redemption feature. Reapplying does not reactivate existing codes or overwrite admin changes.

## Configured DB status
- Read-only check found exactly one pending migration: `20261104160000_AddVoucherRedemptionOffers`.
- Applying it to the DB selected by `backend/.env` was **blocked by automatic approval review** because the request did not explicitly confirm that shared/deployed DB as the intended target. The apply command did not run; no data/schema there was changed.
- Await explicit target confirmation before running database update. Do not silently apply unrelated pending migrations. Updated API must be deployed for guests/point/level offers; older APIs intentionally ignore restricted offer statuses.

## Files
- DAL: Voucher entity/configuration, migration/designer/snapshot.
- BLL: RewardLedger, VoucherEligibility, MemberService, OrderService, AdminCatalogService; IMemberService and voucher DTOs.
- API: MemberController makes only GET vouchers anonymous; claim remains authorized.
- Tests: VoucherRedemptionFlowTests.
- Frontend: sibling `frontend/docs/handoffs/promotions-and-redemption.md`.

## Verification
- **100 backend tests passed** with temporary real PostgreSQL databases; migration/model match, seed definitions, catalog privacy, insufficient points/debt/level, checkout bypass, concurrent same-user claim and global last-offer races, cancellation reuse, lifetime preservation, admin protection.
- **41 frontend tests passed**, TypeScript/full ESLint passed; web export generated dist.
- Chrome fixtures at320/390/768/1440px: retained benefits/check-in, filters, confirmation/cancel, balance refresh, code copy, free claim, insufficient-point/level hints, public browsing/login redirect, no runtime exceptions or horizontal overflow. Screenshots visually inspected at320/1440px. Native physical devices were not tested.
