# Voucher form simplification

- Removed seller program-name input, draft validation and request field; vouchers display/search by code.
- Removed `Voucher.Name` and its EF mapping. Migration `20261125012810_RemoveVoucherProgramName` drops `Vouchers.Name`; applied to the configured database on 2026-11-25. No pending model changes.
- Existing response DTO `name` fields return the voucher code for client compatibility. Legacy admin input `Name` is ignored rather than stored.
- Only percentage vouchers show/require a maximum discount. Fixed seller/admin saves discard any supplied cap and persist null. Shipping behavior is preserved.
- Validation: seller 267 tests, typecheck, lint and web export passed. Backend API build passed; 26 existing voucher/checkout tests passed, and the new database regression passed after correcting its fixture query to select the created voucher by ID.
- Browser: verified both discount modes and created `FIXED_BROWSER_QA` on an isolated local database after switching from percent with a cap. SQL confirmed FIXED / 50000 / MaxDiscount null. No console errors during the form checks. Screenshot capture timed out.
- Historical migrations remain unchanged. Unrelated client worktree changes were preserved.
