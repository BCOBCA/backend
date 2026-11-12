# Affiliate withdrawal fees and bank accounts

## API
- Authenticated GET `/api/affiliate/withdrawal-settings`: `{ feeAmount, minimumAmount, maximumAmount, revision, updatedAt, updatedByAccountId }`. Default fee 0, minimum 50,000, maximum 50,000,000 VND.
- Admin/SuperAdmin GET/PUT `/api/admin/settings/affiliate-withdrawal`. PUT body `{ feeAmount, minimumAmount, maximumAmount, expectedRevision }`. Values have at most two decimals, `0 <= fee < minimum <= maximum`; update requires active admin account, current revision and writes an audit entry. Concurrent/stale updates return 409.
- Authenticated GET/POST `/api/affiliate/bank-accounts`. POST `{ bankName, accountNumber, accountHolder }`; own-user list only, at most ten accounts, duplicate bank/number rejected. Account numbers returned masked; holder returned only to the owning user.
- Existing POST `/api/affiliate/withdrawals` now accepts `{ amount, bankAccountId, expectedConfigurationRevision }` with required Idempotency-Key. Direct bank fields remain supported; nonzero fees require a reviewed revision. Saved bank ownership is checked server-side. Client fee/net values are never trusted.
- Withdrawal list DTO adds `feeAmount`, `netAmount`; admin bank-detail access remains separately authorized/audited.

## Money and storage
- Amount is gross debit. Net = amount - fee; snapshot both fee/net on creation. Rejection restores full gross debit. Payout consumes gross commission backing as before; transfer amount is net. Policy changes affect new requests only. Idempotent retries return the original request before checking current policy.
- Reused AES-256-GCM `v1:` envelope and existing `Security:BankDataKey`; shared BankDataProtection validates/protects bank details. No plaintext full account number/holder is stored on bank-account rows. No new encryption dependency/key.
- Migration `20261112084310_AffiliateWithdrawalFeesAndBanks`: creates settings and saved-bank tables, adds fee/net columns/constraint, backfills existing requests with fee 0 and net = amount before enforcing constraint. Model migration state verified in affected flow test.
- Migration has NOT been applied to the application's configured database. Apply through the normal migration workflow before running this feature. No live withdrawals or real transfers performed.

## Validation
- FinanceFlowTests and ProductAffiliateFlowTests passed in isolated scratch DBs: ownership, encryption/masking, admin permission/validation/revision/audit, default limits, stale/unreviewed fee rejection, fee snapshot stability, net, gross debit/refund, payout/idempotency and existing finance/affiliate lifecycle.
- Existing low-value finance fixtures explicitly configure small test minimums; production defaults remain 50,000/50,000,000.
