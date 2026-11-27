# Admin users API

- `GET /api/admin/users/summary`, `GET /api/admin/users`, `GET /api/admin/users/{id}`; all no-store. `PUT /api/admin/users/{id}/status` accepts `active`, mandatory `reason` (<=1000), `updatedAt`.
- Admin/SuperAdmin authorization plus active administrator check in service. Nondeleted non-admin targets only. DTOs exclude password/token fields. Accounts without profiles supported.
- Search name/nickname/email/phone; status, contact verification, joined week/month, sort newest/oldest/name. List page size <=50; independent order/store/review/audit pages <=20. Pages capped at last page.
- Purchased orders scoped by User.Id; owned commerce stores by Account.Id; reviews include hidden with explicit flag; account audit scoped by entity/id. No schema migration.
- Serializable transaction: conditional account status update with timestamp, pause owned shops, revoke all refresh tokens, account/store audits. Unlock only account. Stale/no-op ->409 with rollback. Admin/self mutation ->403.
- Service: `BCOBCA.BLL/Services/AdminUserService.cs`; contract: `AdminUserDtos.cs`, `IAdminUserService.cs`; controller: `AdminUsersController.cs`; DI in Program.cs.
- `AdminUsersTests`: isolated PostgreSQL migrations/fixtures; role guard, scope/filters/paging, missing profile, hidden review, protected targets, invalid input, stale token, transactional lock/unlock/audit and no credential leakage.
