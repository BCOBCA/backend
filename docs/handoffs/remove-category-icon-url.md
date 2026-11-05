# Remove Categories.IconUrl

- Removed only `Categories.IconUrl` from entity/configuration, public CategoryDto, CategoryInput and catalog/admin mappings. `ImageUrl` and `BrandCategories.IconUrl` remain.
- Migration `20261105032258_RemoveCategoryIconUrl`: Up drops the column; Down recreates non-null text with empty default. Restoring previous icon values additionally requires the local backup.
- Applied to configured Neon DB after confirming this was the only pending migration. Backup: workspace `.local/category-column/category-icons-backup.json` (10 IDs/names/image/icon URLs; ignored by Git). Post-check: 10 categories and all original images preserved; 7 remaining columns; zero pending migrations.
- Frontend Category type/fixtures/rendering no longer use iconUrl; category photos/names and selection remain.
- Verification: CategorySchemaTests plus core PostgreSQL flow passed (3 tests); 59 frontend tests, TypeScript and targeted ESLint passed. API/schema removal requires deploying the updated backend; earlier backend versions still reference the dropped column.
