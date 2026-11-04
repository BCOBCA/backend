# Membership levels and affiliate ranks

Status: membership levels and affiliate ranks implemented. Level-specific benefit content is awaiting the user's policy table.

## Requirements
- Membership has exactly 10 levels, Lv.1–Lv.10, based on the current available point balance. Spending or reversing points can lower a level.
- Each level has its own point threshold and benefits.
- Display the current level at the right of the membership heading, plus progress, accumulated points and points remaining to the next level.
- Affiliate ranks are separate: Đồng, Bạc, Vàng, Bạch kim, Kim cương.
- Affiliate ranks add a configurable benefit to successful-referral commission; they do not determine membership level.
- Prepare protected admin APIs/configuration for all thresholds and benefits. A full admin interface is a later feature.

## Required business input
The user chose to provide the specific rules rather than use sample defaults.
- Ten rows: level, minimum points, benefits (including scope, limits and expiry if monetary).
- Confirmed: five ranks use distinct successfully referred customers, with no time window. A successful customer currently has at least one completed, paid order.
- Confirmed: commission percentages are the final rates on eligible order-item merchandise value, not additions to the old base rate.
- Confirmed: levels use current spendable point balance; spending points can lower a level.
- Membership uses the post-transaction available balance, including point-debt repayments. Affiliate rank excludes a customer if all their qualifying orders are refunded; subsequent commissions use the resulting current rank.

Do not invent benefit content while it is pending; empty lists remain editable through admin configuration.

## Confirmed thresholds
- Membership minimum points, Lv.1–Lv.10: 0, 100, 300, 600, 1000, 1600, 2500, 4000, 6000, 10000.
- Affiliate minimum referrals, Đồng–Kim cương: 0, 10, 30, 70, 150.
- Affiliate final commission percentages: 2%, 3%, 5%, 7%, 10%.
- Level benefits have not been supplied; preserve empty editable benefit lists rather than invent monetary entitlements.

## Existing implementation
- `MembershipTier`: name, minimum spend, minimum points, JSON benefits, sort order and active state. Only the default “Thành viên” tier is seeded.
- `UserMembership` tracks available points, lifetime earned points, qualifying spend and tier ID.
- `RewardLedger` increases lifetime points on earning and reverses them on refunds; spending points is separate.
- `FinanceService.AccrueOrderAsync` currently selects tiers using both spend and lifetime points and calculates commission using a configured base rate.
- `RefundSettlementService` reverses points/commission and recalculates membership tier.
- `MemberService.CheckInAsync` grants points but does not update stored tier ID. The new design must handle every point source consistently.
- Membership API returns a tier name but no level/progress/benefits metadata.
- Affiliate referral status distinguishes pending versus qualified referrals. Total registrations must not be silently treated as successful referrals.
- Existing commission rows snapshot their rate and amount. Future configuration edits must not rewrite earned commission.

## Implementation plan
1. Finalize the rules table and API DTOs. Keep existing fields compatible while adding level/progress and affiliate-rank details.
2. Implement focused rules tests first: exact thresholds, level cap, points spending/refunds, distinct successful referrals, rank boundaries and commission rounding/idempotency.
3. Add database configuration/migrations, protected admin updates, boundary validation and audit logging. Preserve existing users and financial records.
4. Integrate member level calculation with earning, check-in and refunds; integrate rank bonuses at commission accrual after the financial formula is confirmed.
5. Add the member-card level badge/progress, member-benefit details and separate affiliate-rank presentation. Guests show dashes; loading/errors must not show fabricated levels.
6. Verify frontend typecheck/lint/tests, backend tests/migrations and responsive browser behavior. Document deployment and rollback requirements.

## Verification environment
- Frontend: Expo 58, React Native 0.88, TypeScript, Vitest; run `npm run typecheck`, `node_modules/.bin/eslint .`, `node_modules/.bin/vitest run --pool=threads`.
- Backend: .NET 10, EF Core 10, PostgreSQL, xUnit; run `dotnet test BCOBCA.Tests/BCOBCA.Tests.csproj` with a .NET 10 SDK and development PostgreSQL connection.
- A .NET 10.0.403 SDK is available at `../.tools/dotnet/dotnet.exe` despite .NET 9 being first on PATH.
- All 91 backend tests passed with a dedicated temporary local PostgreSQL 18 cluster on loopback port 55434. Tests create/drop only their own databases.
- Use existing engineering conventions, no new dependencies without a demonstrated need, no production deployment or policy changes before the rules are supplied.
