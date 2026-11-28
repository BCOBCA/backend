# Admin voucher scopes

- VoucherManagementQuery adds trailing optional Scope="ALL"; accepted values ALL/PLATFORM/SHOP. Existing admin/frontend/seller query defaults stay compatible. Sellers can only use ALL within their existing authorized shop scope.
- VoucherManagementService filters StoreId null/non-null before statistics and server pagination. Admin tab counts now correspond to selected issuer scope. Description is included in text search alongside code and shop name.
- No schema changes; existing VoucherInput/managed DTO Name compatibility remains unchanged. Name is intentionally not a persisted voucher column; new admin program title uses existing Description.
- Existing creation/detail/usage history/disable routes are reused. No changes to redemption logic, shop ownership or mutation authorization.
- Added AdminVoucherScopeTests for platform/shop isolation and statistics, description search, ALL default and seller scope rejection. Requires .NET 10 and isolated PostgreSQL; installed SDK 9.0.305 prevents backend verification here.
- Admin frontend tests/typecheck/lint/export and isolated browser fixtures passed for platform/shop tabs, issuer text-only, selection/filter/detail/create/disable and mobile. Deploy this backend change alongside admin, which sends Scope on list requests.
