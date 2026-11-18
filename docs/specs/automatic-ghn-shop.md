# Automatic GHN sandbox shop registration

## Objective and scope
Seller pickup selection must prepare shipping without an admin assigning a shop ID. Keep the existing sandbox account and quote behavior. Do not book shipments or change verification requirements. Existing shop IDs remain intact; explicit pickup/return overrides already supply the selected addresses to quotes.

## Behavior and contracts
- On selecting a pickup, or saving an already selected pickup, ensure a missing `Store.GhnTestShopId` is registered and persisted before reporting success. Existing sellers can save their pickup to activate shipping.
- Authorize ownership and validate branch/address/version before any carrier calls. Serialize branch mutations using the store row lock inside the existing transaction.
- Resolve verified national province/ward names against GHN's active two-level catalogue; never pass national codes as GHN IDs or guess a legacy district.
- Use a deterministic remote shop name scoped to the store and pickup data. Search all pages before registration to recover from an earlier successful remote call whose local transaction failed. Verify recovered/created routing and contact fields before binding.
- Reuse an existing positive local shop ID. Selecting return/main branches alone does not create shops.
- Missing credentials, provider errors, timeout or invalid response produce safe actionable errors. No local selection or ID changes commit on failure; retry uses the same existing seller actions. Never expose the token or raw provider errors.

## Acceptance criteria
Tests cover creation, recovery, pagination, address matching, malformed/error responses, cancellation, no-call reuse, ownership/stale-version rejection, rollback/retry, and concurrent selections. Build and focused backend tests pass with disposable databases and fake HTTP. Full-suite failures must be compared with unmodified HEAD to distinguish existing fixture failures from regressions.

## Boundaries
Use existing server-side credentials and staging endpoints. No secrets or personal contact data in logs/docs. No schema, authentication, checkout pricing or frontend contract changes. Live sandbox verification may register the user's selected QQ pickup and invoke a quote preview only.

## Plan and verification
1. Add HTTP boundary tests, demonstrate failure, implement registration/catalogue matching and response validation; run focused tests.
2. Add seller integration tests, demonstrate failure, wire pickup select/save with locking; verify persistence, rollback, ownership and recovery.
3. Run backend suite/build, inspect diff for correctness/security, document setup and handoff. Verify QQ via the same seller service where the configured database/provider are reachable.

## Sources and assumptions
GHN two-level catalogue: https://developer.ghn.vn/vi/docs/master-data/get-province-new and https://developer.ghn.vn/vi/docs/master-data/get-ward-new . Shop listing/recovery: https://developer.ghn.vn/vi/docs/shop/get-shop . Registration: https://developer.ghn.vn/vi/docs/shop/create-shop . The published registration example still uses legacy district/ward fields. Direct staging verification and the official sandbox frontend established that two-level shop fields are `is_new_address`, `address_v2`, `province_id_v2`, `ward_id_v2`; include legacy `address` as well. See the handoff for the pending bounded repair of the already-created QQ sandbox shop.
