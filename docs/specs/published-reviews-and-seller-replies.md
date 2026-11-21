# Published reviews and seller replies

## Objective and contract
- A buyer may review their purchased order item after verified delivery (`Shipment.DeliveredAt` or completed order), independent of COD settlement. Never accept cancelled or undelivered items.
- New/edited reviews publish immediately (`Status=1`). Existing pending reviews become published in migration; rejected reviews remain hidden. Keep admin removal for abuse, without a pre-publication queue.
- One review per buyer/order item and one editable seller reply per review. Existing 1–5 stars, 5,000 character buyer content and five-image rules remain.
- Reply: trimmed plain text, 1–2,000 characters, owner-only store access, expected review revision required; concurrent edits return 409. Seller cannot modify buyer content or ratings.
- GET `/seller/stores/{storeId}/reviews`: paginated, rating, reply status, product, search and date filters. Include product/order context and published reviews only.
- PUT `/seller/stores/{storeId}/reviews/{id}/reply`: `{content, expectedRevision}`. Public review responses append nullable `{content, storeName, updatedAt}` reply.
- Buyer lists/detail expose `canReview` from server; delivery does not mark COD paid.
- New review notifies the owning shop; reply/edit notifies the buyer. Both use existing transactional inbox/push queues and bell refresh. Identical repeated submissions do not duplicate notifications. Shop notification opens the notified shop?s reviews; buyer notification opens the buyer?s review and reply.

## UI
Seller Products links to Product reviews; responsive overview, rating chips, All / Awaiting reply / Replied tabs, search, date filters, review cards with buyer/product/order context and reply dialog. Public product reviews and buyer editor show seller replies. Match existing red/white design and 44px mobile controls.
Reference: TikTok Shop Academy Product Ratings, https://seller-us.tiktok.com/university/essay?identity=1&knowledge_id=6599730126833451. Use its feed, filters and reply popup pattern; allow replies at all ratings per this project's requirements.

## Plan and verification
1. Backend eligibility + immediate publication: PostgreSQL service tests, additive order contracts, regression checks.
2. Reply persistence/migration + scoped APIs: test other-shop denial, invalid input, concurrent revision, filters and public reply.
3. Buyer UI and seller management/dialog: focused Vitest checks and TypeScript/lint for both clients.
4. Real browser desktop/mobile flow using isolated fixtures; remove fixtures. Record handoff and migration instructions.

## Boundaries and style
.NET/EF/PostgreSQL backend, React Native/Expo TypeScript clients. Reuse existing DTO/service ownership checks, plain React text rendering and API clients; no new dependencies. Example: `await CheckStoreAccess(accountId, storeId, ct);` before reading or updating reviews.
Run `../.tools/dotnet/dotnet.exe test BCOBCA.Tests --filter FullyQualifiedName~PublishedReviewsTests` with an isolated local PostgreSQL connection. Clients: `node node_modules/vitest/vitest.mjs run --pool=threads`, `node node_modules/typescript/bin/tsc --noEmit`, `node node_modules/eslint/bin/eslint.js .`.
Generate migration for review reply fields; do not apply to a live database or send messages externally. Do not change COD accounting or unrelated product code. Verify before completion; no commit/deploy requested.
