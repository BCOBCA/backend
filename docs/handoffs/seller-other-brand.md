# Seller other-brand support

- Product options append `SupportsOtherBrand = true`. Create input appends `UseOtherBrand = false`; true requires empty BrandId, false preserves active real-brand checks. Reserved `seller-other-` brands are hidden from ordinary options and rejected for direct ordinary selection.
- `ProductInput` carries the flag to the existing catalog transaction. Authorize StoreId before resolving a per-store unverified `Khác` Brand, with actual owner account. Reject deleted/inactive/foreign-owner/verified reserved-slug collisions. Resolve the real FK before persisting products. Product and fallback creation roll back together. No schema change, role grant or production writes.
- Focused regression tests cover JSON HTTP binding, validation, ownership, transactional rollback, retries, reuse across products and separation across stores. Run with localhost:55433 scratch PostgreSQL; deploy backend first so seller can advertise the option.
- Public brand directory omits reserved placeholders; product listings remain visible and product brand detail still resolves to `Khác`.
