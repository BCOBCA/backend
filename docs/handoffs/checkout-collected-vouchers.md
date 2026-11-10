# Collected checkout vouchers

## Contract

- CheckoutRequest adds optional `voucherCodes` (0–2). Null is omitted from internal serialization so pre-existing single-code request hashes remain stable. Existing `voucherCode` remains supported; mixing contracts or duplicate codes is rejected.
- CheckoutPreviewRequest adds `voucherCodes` and `autoSelectVouchers`. Preview returns owned options (`vouchers`) with actual savings, eligibility/reason and `appliedVoucherCodes`.
- Array/automatic flow requires AVAILABLE ownership. Legacy public free single-code behavior remains compatible; paid vouchers always require ownership. Authentication remains controller-derived.

## Rules and accounting

- `CheckoutVoucherRules` validates lists, dates, type/status, ownership, membership, total/per-user uses, brand/product/category targets, minimum and caps. At most one merchandise and one shipping voucher. Auto selection maximizes actual independent savings; deterministic ties by expiry/code.
- Batch-load ownership, vouchers, targets, usage aggregates, categories and membership once. No per-candidate preview/API calls. Both categories evaluate original eligible merchandise subtotals; only merchandise discounts allocate to order lines. Shipping discount is capped by shipping fee.
- Checkout rechecks within the existing serializable transaction. Each voucher gets its own usage/application amount and status update; existing cancellation loop restores both. Existing affiliate, settlement and refund monetary paths retained.

## Validation and rollout

- Pure voucher/commerce tests plus PostgreSQL isolated-database flow: only owned options, auto pair, explicit none, invalid/unowned/same-category rejection, legacy compatibility, total/hash, idempotent retry, both ledger records/payment and cancellation restoration.
- Legacy shipping flow including points, affiliate commission and refund checks passes. Fixed that fixture to construct the test DB connection from the original environment value: Npgsql's open connection string strips its password.
- No schema changes/migrations. Backend must be updated with frontend; no deployment or commit made. No real orders or production tables changed by verification; temporary databases are removed by tests.
