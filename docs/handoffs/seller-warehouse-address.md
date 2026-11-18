# Seller pickup/return address creation

- Branch POST/PUT accepts optional `setAsPickup` / `setAsReturn`, default false. True replaces the requested warehouse role; false preserves existing roles. Existing clients keep their original behavior.
- `SaveBranchAsync` validates ownership/version/contact/national address, ensures a pickup's GHN binding, clears prior warehouse flags and saves the branch/new roles in one serializable transaction. Carrier failure preserves the old branch list and flags. New-role tests cover pickup, return, both and rollback.
- `ShippingContact.NormalizePhone` is shared by seller branch persistence/selection and GHN registration verification. Strip common separators; normalize +84 / 84 mobile and landline formats to local 0. Branch validator requires 10–11 digits starting with 0; GHN checks carrier-specific validity.
- Seller `StoreBranchDialog` has matching normalization/validation, required-field guidance and pickup/return checkboxes. Selected roles cannot be unset there; choosing another address replaces them. Warehouse pages open this dialog in place with the requested role selected and locked. Confirming the already selected pickup retries activation instead of silently closing.
- State changes and errors keep entered data on failure. The existing branch endpoint returns the updated profile, so one request creates/selects/activates the address; no second role-selection request is needed. Deploy backend and seller together so the new flags are honored.

## Verification
- Backend: 56 related tests passed, including new role-aware create/rollback and contact-format tests, existing branch operations and GHN client/registration flows. PostgreSQL test data was disposable and isolated on localhost.
- Seller: all 148 tests passed, TypeScript and ESLint clean; web export succeeded.
- Browser: isolated Chrome/CDP with synthetic fixture verified in-place pickup creation at 320/768/1440px, normalized phone, both role flags, required fields, carrier error/data retention/retry, same-pickup activation retry and return-only defaults; no runtime exceptions. The configured DevTools MCP profile was already in use; used a separate temporary Chrome profile. Fixture source/screenshots are workspace-local at `.local/seller-address-check`.
- QQ: GHN sandbox shop239384 street repaired and ID persisted through the real seller service; actual `GhnShippingQuoteService` preview succeeded at20,900 VND for a synthetic destination at the shop. No shipment was created and no other carrier shop modified.

No schema migration. Backend/seller source changes have not been deployed. See [specification](../specs/seller-warehouse-address.md).
