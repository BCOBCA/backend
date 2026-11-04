# Product Affiliate implementation

Implemented and verified 2026-11-04. Completion report: `../handoffs/product-affiliate.md` (99 backend tests, 36 frontend tests, typecheck/lint, web export and isolated browser checks). External DB migration/deployment is not performed by this implementation task.

## Contract
- Reuse existing AffiliateProfile/Referral/Rank/Link/Attribution/Commission/Wallet/Withdrawal and ledgers.
- Rank minima become 0/10/30/60/100; final rates remain 2/3/5/7/10%. Distinct referred customers qualify only through completed, paid orders.
- Restore optional manual referral input and prefill `register?ref=CODE`. Backend owns code validation and immutable referral assignment.
- Product link creation requires an active authenticated affiliate and an active product. Share URLs use App:PublicUrl, never a placeholder/request Host.
- Attribution: last valid click, 30 days by default (Affiliate:AttributionDays), one affiliate per order. Match only the linked product. Reject self-attribution. Bind an anonymous visit to its first purchasing user.
- Browser source uses an HttpOnly cookie; native source uses opaque random tokens persisted with SecureStore. Store only token hashes in the database. Expired/invalid source cannot earn product-link commission.
- Valid product attribution takes precedence over legacy registration referrals. Preserve legacy referral commission when no valid product-link attribution exists. No multi-level payouts.
- Freeze source at checkout and rank/rate at order accrual. Only completed paid orders accrue, once per order/item. Merchandise is net of allocated discounts; shipping is excluded.
- Admin may disable affiliate per product, cap rate or set a lifetime commission budget. Serializable transactions and wallet concurrency protect allocations. Reversed commissions release budget.
- Preserve existing approval hold and refund/debt recovery. Snapshot rank names. Record paid allocations when withdrawal is paid; partially paid commissions remain approved until fully paid. Refunds reverse pending/approved/paid commissions exactly once.
- Keep anti-abuse controls: registration/IP limits, duplicate contact rejection, active-code checks, distinct qualifying paid customers, no self-orders. Do not claim SMS/email verification or bot detection that does not exist.

## Ordered implementation
1. Extend existing DAL entities plus visits, commission payout allocation state, policies and rank seed migration.
2. Add link/visit/invitation/policy APIs and server-side attribution; integrate checkout and net item commission/budget snapshots.
3. Restore registration referral flow; build responsive Affiliate overview with code/link copy/share, progress, five-rank benefits, commission history.
4. Add product create/share and capture source; propagate native source in checkout, web via cookie. Keep checkout idempotency stable.
5. Cover attribution expiry/multi-click/self/refund/retry/rank boundaries/caps/budgets, run full tests/build/migration checks and isolated browser checks.

## Commands and boundaries
- Backend SDK: `../.tools/dotnet/dotnet.exe`; single-node build/test with `/p:UseSharedCompilation=false`.
- EF tool pinned in backend/.config/dotnet-tools.json; use workspace DOTNET_ROOT.
- Frontend: `npm run typecheck`, `node_modules/.bin/eslint .`, `node_modules/.bin/vitest run --pool=threads`, Expo web export.
- Use a dedicated loopback PostgreSQL test cluster; never migrate the configured external database during implementation.
- No unrelated refactors, new payment integration or automatic deployment. Public tracking/rank definitions never expose personal balances.
