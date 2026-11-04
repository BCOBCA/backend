# Product Affiliate — 2026-11-04

## Delivered
- Extends existing referral, rank, link, attribution, commission, wallet, withdrawal and refund services. No parallel wallet or payout system.
- Defaults: Đồng 0/2%, Bạc 10/3%, Vàng 30/5%, Bạch kim 60/7%, Kim cương 100/10%. These supersede the earlier 70/150 thresholds. Admin database policies remain authoritative.
- A successful referral is a distinct registered referred customer with at least one completed, paid order. Multiple orders do not add multiple people. Refunded orders no longer qualify; another qualifying order can retain qualification.
- Optional signup referral is validated against an active affiliate/account, rejects matching contacts and cannot be reassigned through profile updates. Both email and phone registration remain supported.
- Product links use the configured public website and the user's actual referral code, including when a legacy AffiliateProfile code differs.
- Last valid click wins, default 30 days. A product source covers only that linked product and takes precedence over the legacy registration referrer. Without a valid product source, existing one-level member referral behavior remains. No self-purchase or network payouts.
- Checkout freezes attribution and binds an anonymous visit to its first purchasing user. Native requests retain an opaque source across checkout retries; browser cookies do not alter the checkout idempotency hash.
- On completed, paid orders, calculate commission on each eligible line after allocated discounts, excluding shipping. Snapshot rank/rate at accrual; apply product rate cap and remaining lifetime commission budget. Reversal releases budget.
- Pending → approved after configured hold/admin approval → paid via actual withdrawal confirmation. FIFO paid allocations support partial withdrawals. Refunds reverse pending/approved/paid amounts once; insufficient available funds become debt and offset future credits.
- Serializable transactions, unique order-item commissions and wallet/xmin concurrency prevent duplicate payouts and budget overrun. Database conflicts wrapped by Npgsql map to HTTP 409 for retry.

## API contract
All paths below start with `/api`; responses retain `ApiResponse<T>`.

| Method/path | Access | Request/result |
|---|---|---|
| GET `/affiliate/invitation` | Signed in | Own code, real invite URL, total/successful invite counts |
| POST `/affiliate/links` | Signed in | `{ productId }` → own reusable product link and click count |
| GET `/affiliate/links` | Signed in | Latest 100 own product links and click counts |
| POST `/affiliate/visits` | Public, IP limited | `{ code, previousToken? }` → expiry; browser gets HttpOnly cookie, native gets opaque token |
| GET `/affiliate/products/{id}/policy` | Admin/SuperAdmin | `{ enabled, maxRate, budget }` |
| PUT `/affiliate/products/{id}/policy` | Admin/SuperAdmin | Same object; rate 0..1, budget nullable/nonnegative; audited |
| GET `/affiliate/ranks` | Public | Five configurable rank definitions |
| PUT `/admin/affiliate/ranks` | Admin/SuperAdmin | Existing rank configuration API |
| GET `/affiliate/commissions` | Signed in, own data | Existing paged API extended with order/product, rank snapshot and paid amount |

Existing `/affiliate`, referrals, leaderboard, wallet transactions, withdrawals, admin commission approval and withdrawal status APIs are reused. Product policy and rank administration are available through protected APIs; this change does not introduce an admin web console.

`POST /orders/checkout` adds optional `affiliateVisitToken` for native clients with `X-Client-Platform: native`; browser requests use only the server cookie. Existing checkout clients continue working.

## Configuration and attribution security
- `App__PublicUrl`: actual absolute frontend origin/base URL, HTTP(S), no credentials/query/fragment. Sharing returns 503 until valid configuration exists; no fabricated domain fallback.
- `Affiliate__AttributionDays`: default 30, allowed 1..90. UI describes the 30-day default; this is independently configurable.
- Existing `Affiliate__ApprovalHoldDays` controls the approval delay; configure to cover the return/settlement policy. Approval remains an explicit authorized action.
- Existing CORS origins must include the real frontend origin. Production browser cookie is Secure/HttpOnly/SameSite=None, Path `/api`; development uses Lax. Cross-site cookie restrictions still depend on browser policy/deployment domains.
- Native token uses SecureStore; database stores only SHA-256 hashes. Browser tracking does not expose the token to JavaScript. Tracking validates origin, active product/affiliate/account, rejects self-source and limits IP requests. Repeat visits within one minute reuse the same valid source.
- Registration retains existing authentication rate limits and unique contact constraints. Referral rank counts require paid completed use, not raw registrations/clicks. SMS verification, device fingerprinting and automated bank transfers were not added.

## Database / deployment
New migration: `BCOBCA.DAL/Migrations/20261104120000_CompleteProductAffiliateTracking.cs` plus designer/snapshot.

- Adds `AffiliateVisits` with link/user foreign keys, unique token hash and visit/expiry indexes.
- Extends Products with enable/rate-cap/budget and check constraint.
- Extends AffiliateCommissions with rank snapshot, paid allocation, paid amount constraint and xmin concurrency.
- Changes previous seeded 70/150 rank thresholds only where still equal to those defaults, preserving admin customizations. Existing financial records/links remain; historical unknown rank snapshots stay blank rather than inventing past ranks. Previously paid legacy withdrawals are not retroactively allocated to individual commissions.

Run from `backend` after selecting the intended DB connection and taking the normal backup:

```powershell
$env:DOTNET_ROOT = (Resolve-Path ..\.tools\dotnet).Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet tool restore
dotnet ef database update --project BCOBCA.DAL --startup-project BCOBCA.API
```

Uses workspace .NET 10; EF tool is pinned in `.config/dotnet-tools.json`. Apply migrations before deploying updated API. No external/deployed database was migrated during implementation. Down drops the new tracking/snapshot columns; preserve a backup if rollback must retain those records.

## Files
- API: `Controllers/AffiliateLinksController.cs`, OrdersController, LoyaltyConfigurationController, Program, ExceptionMiddleware.
- BLL: `Services/AffiliateLinkService.cs`, AuthService, MemberService, OrderService, FinanceService, RefundSettlementService; `Common/AffiliateSources.cs`, DatabaseConflicts; AffiliateDtos, FinanceDtos, OrderDtos; IAffiliateLinkService/IOrderService.
- DAL: AffiliateVisit entity/configuration; Product and AffiliateCommission entities/configurations; ApplicationDbContext, LoyaltyDefaults; migration/designer/snapshot.
- Tests: ProductAffiliateFlowTests, ExceptionMiddlewareTests, AffiliateRankTests, PostgresFlowTests.
- Frontend: see `frontend/docs/handoffs/product-affiliate.md` in the sibling repository.

## Verification
- Backend build + all **99 tests passed**, including a temporary real PostgreSQL database with migration/model alignment checks.
- End-to-end finance test covers signup/referral validation, code alias compatibility, link reuse, source replacement/retry/expiry/ownership/self rejection, checkout idempotency, per-product discounted base/cap/budget, cancellation, approval, paid withdrawal, repeat refund/debt recovery and released budget.
- A synchronized two-transaction test reads the same remaining budget for distinct buyers/affiliates/variants: one transaction conflicts, retry succeeds, total commission stays at the budget.
- Rank boundaries cover 10/30/60/100. Middleware tests cover direct and wrapped serialization/deadlock conflicts without leaking database details.
- Frontend: **36 tests passed**, TypeScript and ESLint passed; Expo web export generated `dist`. Physical iOS/Android runtime was not tested.
- Isolated Chrome checks passed at 320/390/768/1440px: no horizontal overflow, Gold progress at 42 referrals, code clipboard feedback, product visit/link creation, signup referral prefill, guest policy access and private balance isolation. Browser APIs used fixtures for layout/interaction; backend financial tests used real PostgreSQL. Screenshots were visually inspected at 320px and 1440px. No runtime exceptions were observed.
