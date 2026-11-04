# Shipping vouchers — 2026-11-04

## Rules
- `BCASHIP30K`: free claim, maximum30000 shipping discount, minimum300000 merchandise, one use/account, global1000 uses.
- `BCADOISHIP30K`: same discount/minimum, costs1000 available points, one redemption/use/account, global500 issued offers. Lifetime points/level unchanged by redemption.
- Both expire90days after migration, status MEMBER_OFFER. Older releases cannot issue/apply a shipping type they do not support. Existing claimed/point-limit/concurrency protections apply.
- New voucher DiscountType `SHIPPING`; DiscountValue is fixed shipping cap, optionally further capped by MaxDiscount. Admin create/update accepts the new type.
- New Order.ShippingDiscountAmount is separate from goods DiscountAmount. Original gross ShippingFee is retained; TotalAmount = Subtotal - DiscountAmount + ShippingFee - ShippingDiscountAmount.
- Discount cannot exceed actual shipping. Already-zero shipping rejects voucher application without consuming it. Existing shipping-fee configuration was not changed.
- Item allocations, purchase point base, Affiliate commission and seller settlement remain based on merchandise. VoucherUsage/OrderVoucherApplication record the actual voucher benefit including shipping.
- Single voucher per checkout remains. Cancellation restores the same voucher entitlement; completed-order refunds use actual paid TotalAmount. No extra point refunds/redemptions are introduced.

## API / UI
- Existing voucher/claim/checkout/order endpoints reused, no new route.
- Preview/order DTO append optional/default0 ShippingDiscountAmount, preserving previous clients/checkout request hashes.
- Public/member voucher availability and admin status selection support shipping MEMBER_OFFERs.
- Cards use truck graphics and actual cap/threshold; details explain shipping-only terms. Checkout/order details show merchandise discount separately from shipping discount.

## Database and files
- `20261104170000_AddShippingVouchers` adds numeric18,2 ShippingDiscountAmount (default0), check constraint0..ShippingFee and two non-overwriting seed codes. Existing orders/benefits remain unchanged.
- Apply after `20261104160000_AddVoucherRedemptionOffers`. No configured external DB migration was attempted: previous automatic approval rejected that shared/deployed target until explicitly confirmed. Local integration databases were migrated/tested and removed.
- Down deactivates seeded IDs and drops the new field/constraint; financial history remains but shipping breakdown is lost on rollback, so review a production rollback before running it.
- BLL: OrderService, MemberService, AdminCatalogService, OrderDtos. DAL: Order/configuration/migration/designer/snapshot. Tests: ShippingVoucherFlowTests, VoucherRedemptionFlowTests. PostgresFlowTests' existing role-race catch now uses DatabaseConflicts to recognize wrapped Npgsql conflicts consistently.
- Frontend: OfferCard, VoucherTicket, promotions details, checkout, order details, API types, offers.test.ts.

## Verification
- **101 backend tests passed**, including real PostgreSQL migration/model checks,20k full shipping reduction,50k capped reduction, zero shipping/minimum-order rejection, point ownership/double claim, checkout retries/cancellation, unchanged merchandise points/commission and exact paid refund amount.
- **42 frontend tests**, TypeScript/targeted ESLint passed; web export generated dist.
- Isolated Chrome fixtures at320/390/768/1440px verified truck cards, free claim,1000-point confirmation/debit, lifetime preservation, applying ship code in checkout and shipping breakdown/order total; no console errors or overflow. Screenshots visually inspected at320/1440px. Physical native device runtime not tested.
