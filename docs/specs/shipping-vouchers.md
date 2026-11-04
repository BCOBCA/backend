# Shipping vouchers — 2026-11-04

- Approved offers: free claim and 1000-point redemption; both reduce shipping by at most30000 on merchandise subtotal at least300000, one use per account. Two actual DB seed definitions, not frontend fixtures.
- Existing voucher type `SHIPPING` uses DiscountValue as the shipping amount cap. Keep the merchandise discount separate: Order.ShippingDiscountAmount default0, gross ShippingFee retained, total=subtotal-merchandiseDiscount+shipping-shippingDiscount.
- Cap reduction at actual shipping; refuse shipping voucher when fee is already zero. Leave item allocations, purchase point base, commission base and seller settlement unchanged. Usage/application store the actual benefit including shipping.
- Reuse claim/redemption/minimum-level/ownership/limits; restricted MEMBER_OFFER status keeps older APIs from issuing shipping offers they cannot apply.
- Update preview/order DTOs, checkout/details and voucher cards/details with accurate shipping labels/icon. A single voucher per order remains the current rule.
- Plan: extend schema/DTO contract, generate migration/seeds, reproduce via PostgreSQL flow test, implement shipping-only calculation/admin validation, update UI, verify finance/refund/idempotency/browser and document.
- Do not apply migrations to configured external DB: prior auto-review rejected that target until explicitly confirmed. No new authorization for external DB writes is inferred from approval of voucher amounts.
