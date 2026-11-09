# Store-owned marketplace listings

## Requirement and accepted data policy

Each listing belongs to exactly one store. Brand is manufacturer metadata, shared between listings. Two stores selling the same manufacturer/model have distinct product IDs, variants/SKUs, prices, stock, images, reviews and affiliate budgets. Store owners can manage only their listings; brand ownership grants no listing write access.

Existing shared rows must split into independent listings per store. User chose: retain original inventory at one store; additional stores enter inventory again (new variant stock/reserved zero). Keep original product/variant IDs and historical references at the retained store. No stock multiplication or copying reviews/orders/favorites/affiliate links to new listings. Retained owner prefers a linked store with the brand owner's account, otherwise deterministic store slug/ID order. Document the exact resulting mapping before any online write.

## Implementation plan

1. Add nullable `Products.StoreId` FK for legacy archived records; new writes require it, updates cannot transfer it. Public/catalog/cart/checkout require active public owner. Replace StoreProducts many-to-many usage with direct ownership, migrate existing links into distinct listings and remove the join table. Archive legacy unassigned products without rewriting historical references.
2. Admin creation/edit/archive authorize store owner or admin. Append storeId to existing DTOs. Expose one seller in detail; storefront and related-store links use ownership, never manufacturer as seller.
3. Snapshot storeId on new order items, seller orders and settlements. Keep legacy snapshots nullable. Split seller orders by store plus brand; isolate settlement visibility and refund recovery by store. Preserve old brand-scoped history when store snapshot is absent.
4. Migration tests: shared products split, immutable original stock/reservations, independent zero-stock clones, no copied engagement, one public seller, authorization boundaries, correct store order/settlement grouping and rollback safety. Full regression suite and frontend typecheck/lint/build.
5. Read-only online preview/backup and concrete mapping first; apply only reviewed migration with authorization, verify counts/inventory/history. No deploy/commit unless requested. All live operations must guard the configured host/database and reject unexpected pending migrations.

## Code and verification

Use existing .NET 10 / EF Core PostgreSQL / DTO / CommerceStore conventions in `BCOBCA.DAL`, `BCOBCA.BLL`, `BCOBCA.Tests`; Expo React Native frontend. Example: `product.StoreId = owningStore.Id; product.BrandId = manufacturer.Id;`.

Backend tests: workspace `.tools/dotnet/dotnet.exe test backend/BCOBCA.Tests --no-restore --verbosity quiet`, with DOTNET_CLI_HOME `.local/dotnet-home` and isolated PostgreSQL port55439. Frontend: `npm test`, `npm run typecheck`, `npm run lint`, `npm run build`. Never execute tests against the online DB or print credentials. Preparation/backup files live under ignored `.local/store-owned-products/`.
