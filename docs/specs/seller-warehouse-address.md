# Seller warehouse address creation

## Objective
Create a usable pickup/return address in the seller form without a separate role-selection trip. Preserve current addresses if validation or GHN activation fails.

## Contract and behavior
- Extend branch POST/PUT input with optional `setAsPickup` and `setAsReturn` action flags, default false. False preserves an existing role; selecting another address replaces that role. Existing clients remain compatible.
- Require branch/contact name, Vietnamese contact phone, two-level province/ward selection and street address. Normalize common phone separators and +84 on both client and server. GHN remains authoritative for carrier-specific validity.
- Save pickup address, register/reuse GHN and update warehouse roles in one serializable transaction. Carrier failure preserves the old roles and branch list. Never accept provider routing IDs from the client.
- Dialog shows required fields, address guidance and pickup/return checkboxes. Warehouse pages create the desired role directly. A selected pickup can be confirmed again to retry activation. Errors preserve entered data.

## Plan and verification
1. Failing backend tests for role-aware creation, rollback and contact validation; implement compatible input/transaction handling.
2. Failing seller validation tests; matching validation and in-place role-aware creation.
3. Focused backend tests, seller tests/typecheck/lint/build; browser desktop/mobile and error/retry with isolated fixtures.

## QQ repair
User approved correcting GHN sandbox shop 239384. Updated only `address_v2`, verified contact/routing and used the real seller pickup selection to persist `GhnTestShopId=239384`. Read-only DB confirmation succeeded. Do not modify other sandbox shops or create shipments.
