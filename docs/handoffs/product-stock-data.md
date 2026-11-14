# Production stock addition — 2026-11-09

User explicitly selected **20 additional units per product**, divided evenly among active variants and added to existing inventory.

- Applied to **93 products / 244 active variants**, total addition **1,860**. Physical stock **981 → 2,841**, reserved stock unchanged at **0**. All 72 previously zero-stock listings received 20 units each; original listings retained their existing stock plus 20.
- Distribution is deterministic by variant ID, quotient/remainder per product, guaranteeing exactly 20 units per listing. Recorded **244 ADJUSTMENT inventory movements** with positive per-variant deltas.
- Private executable/plan: `.local/product-stock/Stock.csproj`, `Program.cs`, `StockUpdate.cs`, `add-20-plan.json`. Exact database guard, serializable transaction, table locks/timeouts, optimistic before-quantity/reservation checks, parameterized bulk update/ledger insert and read-back validation. Reapplying the same plan verifies its movement IDs instead of adding stock again.
- Backup before writing: `.local/product-stock/before-20261109-072746-7708.json` (products, variants and existing inventory movements). Product rows and all non-stock variant fields verified identical. Prices, ownership, specifications, variant identities and reservations were preserved.
- Fresh read-only verification confirmed 2,841 stock and the exact per-variant plan/ledger entries. Public API checks compare both original iPhone 13 Pro and a separate store's listing against expected available quantities.
- No schema migration or deployment performed. This supersedes the ownership migration's initial zero-stock state; that migration policy remains historical and was not rerun.
