# Seller order detail

- `GET /api/seller/stores/{storeId}/orders/{id}` keeps its route and existing DTO base fields. Detail adds note, payment status/method/paidAt, confirmedAt/completedAt/cancelledAt, shipments, history (including notes) and vouchers.
- Detail item fields: id, variantId, sku, variantName, imageUrl, discount. Order names/SKU/prices and recipient/address/note use stored order data; variant labels/images use current catalog metadata and may be null for removed metadata.
- List responses remain lightweight; extended detail data is loaded only by `SellerService.OrderAsync` after owner and single-store order validation. No migrations/auth changes. Never expose payment provider payloads/secrets or another store's order lines.
- Seller detail now requests this endpoint directly, validates the response and renders loading/error/retry states. Deploy API and seller web together.
- Validation: backend build and `SellerReadTests` passed with .NET SDK 10.404 and temporary PostgreSQL 18; test server stopped. Seller: 165 tests, typecheck, lint and web export passed. Browser verification unavailable due existing Chrome profile conflict.
