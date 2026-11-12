# Affiliate dashboard total

- GET `/api/affiliate` adds `totalCommission` to AffiliateDto without changing existing fields or requiring a migration.
- MemberService aggregates all commission amounts belonging to the requesting user's affiliate profiles, excluding `REVERSED`. Includes pending, approved and paid commissions; withdrawals do not reduce lifetime total. Returns zero for no commission history.
- ProductAffiliateFlowTests passed with assertions for account isolation, initial pending commission, unchanged total after payout and zero after refund reversal. Existing cap/budget/order/refund regression retained; test database isolated from configured application data.
- Frontend `/affiliate` dashboard uses this field; older backend responses display an unknown total rather than deriving a false lifetime sum from wallet balances.
