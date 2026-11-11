# Shipping sandbox data — 2026-11-11

- Applied both checkout/shipping migrations to the configured database with user approval.
- User authorized filling shipping metadata and creating ten GHN sandbox pickup locations. Registered pickups were read back and verified before database binding.
- Filled missing package weight/dimensions for all 93 active products in ten active seller stores. Existing non-null metadata is preserved. Values are test estimates by product family, not measured specifications.
- Bound each seller to its own `GhnTestShopId`. Filled the national ward code on the selected existing pickup branch when blank; GHN legacy ward codes remain separate from national administrative codes.

| Store | GHN sandbox ShopId |
|---|---:|
| FPT Shop | 237900 |
| Thế Giới Di Động | 237901 |
| Phong Vũ | 237902 |
| Điện Máy Xanh | 237903 |
| CellphoneS | 237904 |
| Hoàng Hà Mobile | 237905 |
| An Khang Computer | 237906 |
| GearVN | 237907 |
| HACOM | 237908 |
| Nguyễn Kim | 237909 |

Pickup fixtures use existing main-branch addresses except CellphoneS, which uses its existing 55B Trần Quang Khải branch. Sandbox contact is the contact already registered on the user's GHN test account. No credentials or contact phone are recorded here.

Before-update snapshots and temporary seed/verification utilities are local under workspace `.tools/shipping-data`, outside the backend Git repository. Updates ran in serializable transactions. Prices, inventory, historical orders, payments and customer addresses were not changed.

Validation: re-read metadata found zero missing store/product shipping fields. The actual `GhnShippingQuoteService` successfully quoted one representative product from each of all ten stores to a test HCM destination, using GHN staging preview only. No orders or shipments were created. This verifies database configuration and local service execution; Render requires the valid sandbox token in its own environment as well.

Before production: replace estimated parcel measurements with measured packed dimensions/weights and configure production pickup/account integration separately.

## Recipient phone validation

The subsequent Nguyễn Kim/AirPods checkout failed because GHN rejects recipient numbers beginning `031`. Reproduced with synthetic `0310000000` (no customer's phone transmitted): staging preview returned HTTP 400 / `PHONE_INVALID`. The same parcel and Bình Chánh destination using the approved GHN test-account contact returned HTTP 200 with fee 42,900 VND. Do not modify customer address records automatically; the customer must supply a valid recipient phone. `GhnQuoteClient` now maps this provider code to an actionable HTTP 422; unrelated/invalid provider errors remain HTTP 503 and upstream details are never returned. Render needs deployment of this code to show the new message.
