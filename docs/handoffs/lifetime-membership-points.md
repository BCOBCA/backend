# Lifetime membership points — 2026-11-04

## Rules and implementation
- Supersedes earlier available-balance level policy. LevelInfo and reward/refund tier refresh now use `LifetimeEarnedPoints`; `AvailablePoints` remains the separate redeemable balance. Existing API fields and database schema are reused.
- Valid lifetime total excludes reversed refunded earnings. Spending points does not reduce lifetime/level; refund reversal can. Existing debt repayment/reversal ledger behavior remains unchanged.
- Purchases earn `floor((Subtotal - DiscountAmount) / Membership:SpendPerPoint)` only on completed paid orders. Configure **10000** for the requested rule: 100000 → 10, 95000 → 9, 100000 less 5000 discount → 9. Shipping is excluded, fractions discarded, repeated accrual is idempotent.
- `.env.example` and the local ignored `.env` now specify `Membership__SpendPerPoint=10000`. Deployment must set the same environment variable; OS/container environment takes precedence over local files. No deployed environment or external DB was changed.
- Existing RewardLedger credits both lifetime and available balances for purchases/check-in; authorized future minigame rewards can reuse it. No minigame, redemption endpoint, voucher-cost policy or historical point recalculation is introduced.
- No migration required. Membership reads derive the current level from lifetime and configurable thresholds immediately; the persisted tier cache still refreshes on reward/refund activity as before.

## Files
- `BCOBCA.BLL/Common/LoyaltyQueries.cs`, `BCOBCA.BLL/Services/MemberService.cs`: lifetime source for derived/persisted level.
- `.env.example`, `BCOBCA.Tests/LoyaltyFlowTests.cs`, `BCOBCA.Tests/MembershipLevelTests.cs`.
- Frontend `MemberLevelProgress.tsx`, member tab and membership-levels page: total-earned progress plus separate available points and corrected earning/spending descriptions.

## Verification
- Integration regression failed on the former balance-based implementation, then passed after the source change.
- **99 backend tests passed**, real temporary PostgreSQL databases. Expanded loyalty test covers exact purchase examples/discount/shipping/flooring/idempotency, reward credits to both balances, simulated spending without level loss, lifetime refund reversal and existing admin/migration behavior.
- **37 frontend tests**, TypeScript, targeted ESLint passed. Chrome API fixtures at 320/390/768/1024/1440px show lifetime1400/Lv5 separately from available125, correct rules, guest zeros and no horizontal overflow. Physical native runtime was not tested.
