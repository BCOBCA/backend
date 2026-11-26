# Single account role

## Requirements
- `Accounts.Role` is the sole role source. Preserve its existing values exactly; do not merge/promote roles from the legacy table.
- Remove AccountRole entity/configuration/DbSet and add an EF migration dropping AccountRoles. Historical migrations remain intact. Revoke pre-migration refresh sessions so old JWT claims cannot retain obsolete privileges.
- Login, refresh and profile return `roles: [account.Role]` for client compatibility. JWT has exactly one role. All service authorization checks read the active Account directly.
- Existing role-management request remains compatible: granting replaces the role, revoking the current role falls back to Customer, revoking another role is a no-op. Prevent self/last-active-SuperAdmin demotion on either grant or revoke. Revoke sessions on actual changes and retain audit logs.
- Customer-to-seller onboarding sets Seller and never overwrites Admin/SuperAdmin/brand roles; store ownership continues to govern seller operations.
- First-superadmin provisioning writes Accounts.Role. Buyer, seller and admin keep their current profile envelope; test single-role login/refresh and permissions.

## Work and checks
1. Regression test model and Accounts-based token/profile behavior; update auth/provisioning.
2. Move service authorization and role mutation to Accounts, update test fixtures and concurrency protection.
3. Remove legacy model, generate migration and test upgrade/downgrade in an isolated PostgreSQL database. Build/test backend and typecheck/lint relevant clients; document deployment order and handoff.

## Deployment boundary
The configured database is used by the deployed Render API. Deploy updated backend before applying the destructive migration; do not drop the table while the old backend still queries it. Prepare the complete migration and verification locally first. Retain a role backup before applying on the shared database.
