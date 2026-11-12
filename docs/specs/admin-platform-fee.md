# Admin-managed platform fee

Replace Settlement:PlatformFeeRate environment configuration with a singleton PlatformFeeConfigurations row. Initial rate remains 0 (current .env value). Store fraction as numeric(7,4), revision UUID, updated-at and optional updating account. Constrain singleton ID and rate [0,1]. Existing seller settlements retain stored amounts.

Authenticated Admin/SuperAdmin GET and PUT /api/admin/settings/platform-fee. Both enforce active account and current database role. GET returns platformFeeRate, revision, updatedAt, updatedByAccountId. PUT requires platformFeeRate and expectedRevision, rejects absent/invalid input, negative/>1 values and >4 fraction decimals; 0.05 means 5%. Stale revision -> 409. Valid update changes revision and writes AuditLog; no-op preserves revision. No public write access.

FinanceService reads the database rate inside the existing completion/accrual transaction and keeps current money rounding. Changes apply at accrual time to new completed/paid orders, including orders placed earlier; already accrued orders remain idempotent and unchanged. No per-product/per-store overrides or admin UI in this change; APIs prepare the future admin screen. Missing/invalid settings must fail transaction rather than silently charge a fallback rate.

Plan: (1) focused rules + PostgreSQL configuration/accrual regression tests; (2) entity/config, DTO/interface/service/controller and DI; (3) EF migration seed 0%, update financial reader and remove env requirement/example; (4) verify migration/model consistency, admin authorization, conflict/no-op/audit, rate application/rounding and historical settlements, build and relevant existing finance/core tests.

Commands from backend: dotnet test BCOBCA.Tests --filter "FullyQualifiedName~PlatformFee|FullyQualifiedName~FinanceFlowTests|FullyQualifiedName~PostgresFlowTests"; dotnet ef migrations has-pending-model-changes --project BCOBCA.DAL --startup-project BCOBCA.API. Follow current record DTO, transactional service and ApiResponse conventions.

Do not edit secrets, apply migration to configured shared database, or deploy. Migration rollback removes settings only and preserves settlements. Deploy with a coordinated stop/migrate/start; environments historically charging nonzero fees must set their intended rate before processing completed orders. Keep unrelated working-tree edits.
