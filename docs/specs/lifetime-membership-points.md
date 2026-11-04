# Lifetime membership points — 2026-11-04

- Level and member-card progress use LifetimeEarnedPoints; AvailablePoints is the separate redeemable balance. Spending does not lower level. Existing refund reversals remove invalid earned points and may lower level.
- Completed paid orders earn floor((Subtotal - DiscountAmount) / 10000), shipping excluded. Preserve configurable SpendPerPoint; configure the requested value 10000 locally and in the environment example/deployment instructions. Preserve past order accruals and idempotency.
- Existing RewardLedger adds both balances for purchases/check-in and can support authorized minigame rewards later; do not implement a game or redemption API in this change.
- Reuse existing API fields/tables. Membership reads derive level from the current lifetime total and policy; existing tier cache refreshes on reward/refund activity, as before.
- Plan: update integration regression, confirm failure against balance-based level, switch backend level source/configuration, update member card/details/labels, verify real PostgreSQL tests and responsive browser fixtures, record handoff.
