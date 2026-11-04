# Promotions and point redemption — 2026-11-04

## Approved scope
- Redesign promotions to match the supplied compact commerce layout: five category filters, featured voucher tickets and point-redemption cards. Retain the existing member benefits/check-in panel.
- Seed actual vouchers: fixed20000/min200000; percent10/cap50000; fixed50000/min500000/Lv3; point redemption fixed30000/cost500 and fixed50000/cost800. Add a short-running 15%/cap30000 flash voucher. No invented shipping discount or physical-gift fulfillment.
- Public guests can browse active offers; claims, redemption and personal points require login.
- Add PointCost, MinLevel and IsFlashSale to existing vouchers; extend admin DTOs/validation compatibly. Claim and redeem share existing endpoint; point claims are one per user, serializable/idempotent, write a point ledger debit and preserve lifetime points/level.
- Checkout must enforce current minimum lifetime level and ownership for redeemed vouchers, preventing entering a code to bypass point cost. Preserve normal free voucher code behavior.
- Point voucher issuance respects its overall limit, counting existing claims. Protect issued offers from admin rule changes; cancellation returns the already purchased entitlement, never a second point charge.
- Reuse existing return/discount/checkout behavior. No shipping/gift/redemption-refund feature is implied.

## Plan and acceptance
1. Add regression integration tests for public catalog, claim/redemption, insufficient points, debt/level gates, duplicate/concurrent requests and checkout bypass/cancellation.
2. Extend DAL/API/services with metadata and financial safeguards; generate migration plus finite, dated sample seed offers.
3. Implement responsive real-data filters/cards/actions/check-in and point exchange confirmation.
4. Test real PostgreSQL migration/finance, frontend checks and browser fixtures. Apply reviewed migration/data to configured DB only after checking pending migrations match this change; do not apply unrelated pending migrations automatically.
5. Record files/contracts/DB result/checks in handoff. No commit/push unless requested.
