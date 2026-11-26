# Single account role

- Source of truth: Accounts.Role only. Removed AccountRole entity, configuration and DbSet; all live BLL/API authorization queries use active Accounts. Historical migrations and the existing `/api/admin/account-roles` request name remain for compatibility.
- Auth login/refresh/profile expose `roles: [account.Role]`; JWT has a single role claim. Customer registration sets Customer; seller registration/onboarding promotes Customer to Seller while preserving other roles and ownership checks. First-SuperAdmin provisioning writes Accounts.Role.
- SetRoleAsync: granting replaces the role; revoking the current role sets Customer; unrelated revokes are no-ops. Serializable transaction protects self/last-active-SuperAdmin demotion, including replacement grants. Actual changes revoke sessions and write audit logs.
- Migration `20261126025829_UseSingleAccountRole` leaves Accounts.Role untouched, revokes old session families, drops AccountRoles. Down recreates one legacy row per Accounts.Role; original multi-role assignments require the pre-deployment backup.
- Regression reproduces mxh account's mismatch (Accounts=Admin, legacy=Customer) on an isolated database and verifies upgrade, login/JWT/refresh/profile, seller onboarding, replacement/revocation and downgrade. Client regression tests cover single-role profiles and refresh without merging obsolete permissions. Production frontend/seller transport needs no contract change.
- Verification: backend build passes. Full local suite passed 334/335; the remaining historical-schema fixture was repaired (ShortDescription and pre-ImageUrls review insert) and passed on rerun. Frontend session/lifecycle tests: 32; seller session/API/lifecycle tests: 39; admin session tests: 12. TypeScript and lint pass for all three clients. SQL rollout artifact: `docs/migrations/single-account-role.sql`.

## Deployment order
1. Back up AccountRoles and Accounts role assignments from the shared database.
2. Deploy this backend revision. It works while the unused legacy table still exists.
3. Apply the migration using the matching revision. Existing sessions are revoked and users sign in again. Verify `/api/auth/me` returns the Accounts role and admin APIs authorize it.
4. Rollback: migrate to `20261125070018_AddProductReviewStore` before deploying the prior backend; restore original role rows from backup only if required.

Local implementation does not change the currently running Render binary. Do not drop the live table while that binary still depends on it.
