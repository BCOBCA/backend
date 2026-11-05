# Remove category IconUrl

- Remove `Categories.IconUrl` from EF entity/configuration, category public/admin DTOs and save/read mappings. Keep `BrandCategories.IconUrl` and category `ImageUrl` intact.
- Remove the frontend category icon field/rendering; cards retain their stored image and category name.
- Add a reversible EF migration dropping only this column. Before applying to the configured DB, back up category IDs/icon URLs and verify the only pending migration is this change. Down recreates the column with empty values; original URLs are retained in the local backup.
- Verify schema/DTO regression tests, PostgreSQL category schema in the existing core flow, frontend tests/typecheck/lint and browser category cards. Deploy backend/frontend together because older backend queries reference the removed column.
