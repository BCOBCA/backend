# Seller warehouse branches

- Seller branch create/update endpoints are independent of profile editing.
  Owner/store/branch access, phone/address validation, province/ward membership
  and store UpdatedAt protect writes in serializable transactions.
- PUT pickup-branch and return-branch independently select existing active
  branches. A branch can serve both; main-branch identity is unaffected.
  Unique indexes and check constraints enforce one valid default per purpose.
- GHN staging preview receives selected from_* and return_* fields with new
  administrative-address flags. Unselected stores retain configured GHN defaults;
  existing per-store GHN ShopId/token configuration remains required.
- Apply SellerBranchPickup and SellerReturnBranch migrations before running the
  updated API. They add four branch fields and constraints without rewriting
  existing addresses or automatically choosing defaults.
- Verified: 20 focused backend tests on isolated local PostgreSQL cover access,
  stale versions, invalid locations, independent/shared defaults, database
  constraints, GHN payloads, checkout and storefront regression behavior.
- Carrier contract: https://developer.ghn.vn/vi/docs/order/create and
  https://developer.ghn.vn/vi/docs/order/preview (verified 2026-11-13).
