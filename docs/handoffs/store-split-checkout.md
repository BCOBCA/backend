# Store checkout and GHN sandbox

- One checkout creates one Order per StoreId, with separate code, payment, reservations, seller order, history, notification and existing refund/finance effects. Nullable CheckoutGroupId links children. Historic combined orders remain unchanged. All writes/cart removal occur in one serializable transaction.
- Checkout/recovery retain first-order fields and return checkoutGroupId/checkoutOrders. GET /orders filters by checkoutGroupId under user ownership. ExpectedTotal/cart hash and original idempotency key prevent stale pricing and duplicate groups.
- Voucher.StoreId separates seller codes from platform codes. Allow one seller merchandise code per store plus one platform and one shipping code. Seller reductions stay within their own store. Platform minimum uses original eligible merchandise across the entire checkout; discount applies only to the largest remaining eligible product line, capped at that line. Ties use ProductId then VariantId. Shipping savings are redeemed once and capped per eligible quoted fee.
- Persist one VoucherUsage per voucher/checkout and per-child OrderVoucherApplication amounts. Release usage only when all benefiting children cancel. Surviving orders keep accepted prices; refund policy unchanged. Point/level coupon creation remains admin-only.
- Preview adds storeOrders, seller/platform totals, issuer/application store IDs, ETA and pricingPolicy STORE_PRODUCT_MAX_V1. Deploy the frontend that understands these fields with this backend.

## Schema and setup

1. Requires .NET 10. Apply reviewed migrations SplitCheckoutByStore and StoreVoucherAndGhnTestShipping using `dotnet tool run dotnet-ef database update --project BCOBCA.DAL --startup-project BCOBCA.API`. Implementation tests migrated disposable databases only; business databases were not migrated.
2. Set Shipping__Ghn__TestToken in backend/.env to a token from the GHN sandbox account at https://developer.ghn.dev. Keep the token backend-only and out of git. Restart the backend after configuration.
3. As admin, bind each registered sandbox pickup warehouse using `PUT /api/admin/stores/{id}/shipping-test` with `{ "ghnTestShopId": 123 }`. Verify the pickup address matches the store. Ordinary sellers cannot rebind warehouse IDs.
4. Product endpoints accept optional shippingWeightGrams (1-50,000), shippingLengthCm/shippingWidthCm/shippingHeightCm (1-200). Omitted values preserve existing metadata. Package estimate sums weights, uses max length/width and stacked heights. Missing/oversized parcels cannot be quoted.
5. Existing voucher endpoint accepts storeId. Owners may issue ordinary seller coupons only within their store; platform/point/level rules require admin. Existing claimed/used-code edit restrictions remain.

## Carrier boundary

GhnQuoteClient calls only `https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/preview`. No production switch, shipment booking or create-order call. Resolve existing Vietnamese two-level province/ward codes to names before sending is_new_to_address=true. The registered GHN test ShopId supplies the pickup origin; quote each store's parcel and after-discount merchandise COD separately.

Validate HTTP/API status, fee, empty preview order code and ETA. Token goes only to GHN; redirects disabled. Missing credentials/metadata or invalid quotes fail closed. OrderService requires IShippingQuoteService; fixed quotes exist only in TestOrders fixtures.

The supplied credential was confirmed to belong to production after staging rejected it with HTTP 401 Token is not valid. It was removed from sandbox configuration; a valid staging token is still needed for direct provider verification. No production request was attempted.

Official contracts: [GHN preview](https://developer.ghn.vn/vi/docs/order/preview), [request/address fields](https://developer.ghn.vn/vi/docs/order/create), [separate staging token](https://developer.ghn.vn/vi/docs/token/get-token).

## Validation

- 179 backend tests passed, including disposable PostgreSQL checkout/cancel/COD/refund/ownership/retry flows, stacking shop+platform+shipping codes, minimum/cap rules and HTTP-faked GHN boundary tests.
- Build passes; EF reports no pending model changes. Generated migrations were inspected and unsafe downgrade is guarded after multi-store/seller scope is used.
- Frontend counterpart passed 286 tests, TypeScript, lint and web export; browser fixture verified scoped selectors, combined summary and mobile footer.
