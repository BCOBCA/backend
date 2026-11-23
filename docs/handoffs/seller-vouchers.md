# Seller voucher management

## Existing schema reused

Audited the live information_schema and EF models before implementation. Reused Vouchers, VoucherTargets, UserVouchers, VoucherUsages, OrderVoucherApplications and AuditLogs. No tables added. VoucherType is inferred from StoreId; scope from VoucherTargets; counts from APPLIED usages. UserVoucher ownership and unique voucher codes retain existing constraints.

Migration `20261123092117_AddSellerVoucherManagement` adds Vouchers.CreatedAt/UpdatedAt/DisabledAt/DisabledByAccountId/DisabledReason and AuditLogs.Details, an account FK and store/time index. Historical creation/update timestamps backfill to migration time. Migration only tested on temporary databases; deployment DB is unchanged.

## API

- Seller base: `/api/seller/stores/{storeId}/vouchers`. GET paged list/statistics, GET `/{id}?page=...` detail/usage history, POST create, PUT `/{id}` update, POST `/{id}/duplicate` with fresh complete input, POST `/{id}/disable` with reason.
- Admin: GET `/api/admin/vouchers` (shop/search/status/time filters), GET `/stores` shop options, GET `/{id}` detail/history, POST `/{id}/disable`. Controller roles and service DB role checks both enforce Admin/SuperAdmin. Legacy admin voucher writes now require admin roles to prevent lifecycle bypass.
- Buyer: existing GET `/api/vouchers` adds optional storeId scope; existing claim and `/vouchers/mine` reused. Voucher/wallet DTOs append optional StoreId/StoreName. Unavailable shops and globally exhausted vouchers cannot appear as available in the wallet. Existing catalog/history API behavior remains compatible.

## Rules

- Seller owner/active-store scope checked on every call, no StoreId accepted in input body. Seller publishes ACTIVE without approval, FIXED or PERCENT only; percent requires a cap. ProductIds empty = all shop, otherwise max 100 distinct active products belonging to this shop. Existing platform pricing is unchanged.
- UPCOMING/ACTIVE/EXPIRED/EXHAUSTED/DISABLED computed from timestamps, status and APPLIED usage count on each query. Edits only before start and before claims/usages. Copy creates a new code/identity. Disable idempotently logs actor/reason and blocks claims/new checkout; existing order discounts remain snapshots.
- Existing serializable claim/checkout/cancellation transactions retain global/per-user limits and idempotency. Shop vouchers require ownership even through legacy code entry. Each shop gets one voucher, then existing platform/shipping policy. Shop discount affects only eligible shop merchandise. Unpaid reservation expiry and cancellation release usage without double release. No payment gateway added; current checkout supports COD.

## UI and operation

Seller routes `/vouchers`, `/voucher/new?edit=...|copy=...`, `/voucher/{id}` replace dashboard promotion placeholder. Buyer storefront product/promotion panels show shop vouchers; existing wallet/card/checkout reused. `/admin-vouchers` in buyer app is linked from member menu for admin roles (no separate admin frontend exists here).

Apply deployment migration using .NET 10: `dotnet ef database update --project BCOBCA.DAL --startup-project BCOBCA.API`, then restart/deploy API and both frontends. No automatic application to live database, commits or deployment performed.

## Validation

SellerVoucherFlowTests covers store isolation, foreign products, cap validation, future editing, copy, claims/idempotency, selected-product multi-shop pricing, disable/audit/admin role, independent-product races for the final use and unpaid expiry release. VoucherAuthorizationTests locks legacy routes; lifecycle and existing pricing/checkout/regression tests cover platform compatibility. Browser QA used an isolated migrated database, actual HTTP APIs and temporary seller/admin/buyer sessions. Verified create with product selection, end with confirmation, admin shop filter/disable history, claim to wallet and 390px seller layout. QA database was removed; main database untouched.

Final checks: 24 targeted backend tests passed (including StoreSplitCheckoutFlowTests and existing redemption/pricing tests); 264 seller and 520 buyer tests passed. Both frontends passed typecheck/lint and final web exports. Migration model matches snapshot. Browser screenshot verification and interactive HTTP flows passed.
