# Product review image consolidation

Store ordered review image URLs as a JSON array in non-null `ProductReviews.ImageUrls` (`jsonb`, default `[]`). Follow existing string-to-jsonb mapping conventions. API `ReviewInput.ImageUrls` / `ReviewDto.ImageUrls` remain arrays; no frontend change. Preserve validation (up to five URLs), ordering, revision checks and no-op saves.

Plan:
1. Add regression coverage for JSON persistence, ordered readback, replacement/removal, no-op moderation state and migration data preservation/rollback.
2. Replace separate image entity/configuration/DbSet with the JSON column; update review service.
3. Add migration: aggregate old rows by `SortOrder, Id`, backfill, then drop image table; Down recreates rows with their ordered URLs. Update EF snapshot/designer.
4. Run targeted tests, model consistency and build; document rollout and verification limits.

Commands (backend): `dotnet test BCOBCA.Tests --filter "FullyQualifiedName~ReviewImagesJson|FullyQualifiedName~FinanceFlowTests"`, `dotnet build BCOBCA.sln`, `dotnet ef migrations has-pending-model-changes --project BCOBCA.DAL --startup-project BCOBCA.API`.

Boundaries: preserve existing data and API contracts; keep unrelated edits out; test migrations against isolated PostgreSQL only. Do not apply to a shared/live database without an explicit deployment request. Rollout requires stopping old API instances before migration, because old binaries reference the removed table. Rollback preserves URLs/order but regenerates image row IDs; no external consumers reference those IDs in this repository.
