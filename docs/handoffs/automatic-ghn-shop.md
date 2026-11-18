# Automatic GHN sandbox shop registration

## Behavior
- `SellerService.SelectPickupBranchAsync` and editing an already selected pickup register a missing `GhnTestShopId` using `GhnQuoteClient.GetOrRegisterShopAsync`. Existing positive IDs are reused. Return/main selection and ordinary branch creation do not provision shops.
- Ownership, branch validation, address verification and optimistic version checks precede registration. Store row locking under the existing serializable transaction prevents concurrent requests from both registering. Registration failure leaves the database unchanged.
- Match the verified national province/ward names to active GHN two-level names/aliases. National administrative codes are never sent as GHN routing IDs. The selected pickup/return address overrides in quote requests remain in effect.
- A store-scoped deterministic remote name includes a pickup-data hash. Paginated lookup recovers successful remote registrations after a lost response/local failure; verify phone, address, active status and routing before binding. External effects cannot be rolled back with the database.

## Operation
Keep `Shipping__Ghn__TestToken` backend-only; the same sandbox account must have an active existing shop to authorize registration. No new configuration or migration. Admin binding remains a manual override. Deploy the backend, then select a pickup or edit/save an existing selected pickup to activate an old shop with no ID. GET profile/checkout do not perform external registration.

## Verification and remaining work
HTTP-faked tests cover new registration, response loss/recovery, pagination, malformed results, address/phone normalization, cancellation and safe provider errors. Disposable PostgreSQL tests cover pickup select/save persistence, existing-ID reuse, return-only behavior, rollback/retry, authorization/version checks and concurrent requests.

Validation 2026-11-18: build succeeded as part of `dotnet test`; all 27 new tests passed. Full suite: 271 passed / 3 failed / 0 skipped (274 total). Re-ran the three failures against an isolated archive of unmodified HEAD: identical failure messages in all three. `PostgresFlowTests` expects 68 entity types while HEAD has 70; `ReviewImagesJsonTests` and `StoreOwnedProductsFlowTests` populate an old schema without its required `ShortDescription`. These unrelated fixtures were left unchanged. TRX reports and baseline archive are under workspace `.local/ghn-test-results` and `.local/ghn-baseline`.

Inspection of the configured business database confirmed QQ has an active selected pickup/return, complete parcel metadata and no GHN shop ID. No business records have been changed. Staging catalogue reads confirmed two-level master data and shop fields `address_v2`, `ward_id_v2`, `province_id_v2`, `is_new_address`.

## Live validation follow-up (2026-11-18)
- User explicitly approved sending QQ's pickup contact/address to GHN sandbox, registering a shop, binding its ID and preview verification.
- Live registration rejected unsuffixed `ward_id` with "new ward does not exist: 0". Corrected the production request and regression tests to `province_id_v2` / `ward_id_v2`.
- Created exactly one sandbox shop for QQ: **239384**. Phone, active status and routing match; new routing is province `1000001`, ward `1003580`. The provider did not retain the desired street in `address_v2` when only legacy `address` was supplied. Readback correctly refused to bind this shop.
- Corrected production registration to send both `address` and `address_v2`. Source: the official sandbox frontend bundle at https://cdn.ghn.tech/online-static/fe-5sao/CM-5555/1814d940/1.43.161/assets/index-342cl7oa.js exposes the `_v2` shop fields and `POST /v2/shop/update` with `id`, `version_no` and retained shop metadata. Public source snapshot is in workspace `.local/shipping-readiness-check/ghn-public-index.js`.
- Demonstrated the new field expectations fail before each correction. After corrections, all **42 related tests passed**, including all 27 feature tests. Latest report: workspace `.local/ghn-test-results/ghn-live-contract-green.trx`. Prior full-suite/baseline results above remain applicable; only registration JSON fields changed.
- Actual staging preview with shop 239384, QQ's parcel and explicit selected pickup/return overrides succeeded: **20,900 VND** to a synthetic receiver at the shop address, empty preview order code, no shipment created. No customer's delivery data was used.

**Pending explicit approval: repair the existing external shop, then bind.** Auto-review rejected a subsequent registration diagnostic for duplicate-resource risk, so switched to GET-only inspection. Auto-review also rejected the bounded `POST /v2/shop/update` repair because the approval covered registration/verification but did not explicitly authorize modifying the already-created shop. Do not repeat either rejected operation without the relevant permission. Never create another shop as a workaround.

The prepared `.local/shipping-readiness-check` utility has `--repair-existing-ghn`: GET shop list, require ID 239384 and its QQ identity/phone/routing, retain existing metadata/version and update only `address_v2` to the selected local street. It cannot call `shop/register` (the handler blocks registration in repair/inspect/activation/preview modes). After permission and repair success, run `--inspect-ghn`, then `--activate-existing-ghn` to invoke the actual seller pickup selection and persist the verified binding. The activation fails if another registration would be necessary. Re-read DB and quote again as appropriate. No deployment has been performed.

Specification: [automatic-ghn-shop](../specs/automatic-ghn-shop.md).
