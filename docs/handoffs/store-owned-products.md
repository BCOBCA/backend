# Store-owned product listings

User confirmed: manufacturer/brand may be shared; each marketplace listing must belong to one store/dealer. This replaces the previous shared `StoreProducts` design.

## Implemented

- `Products.StoreId` FK owns each listing. DB check requires ownership on non-archived products. Nullable ownership is reserved for archived historical records. Brand remains manufacturer metadata. Migration `20261109065944_StoreOwnedProducts` removes the many-to-many join table/entity.
- New admin product input requires `storeId`; omission preserves ownership on updates. Only store owner or admin can create/edit/archive. Brand ownership alone grants no listing write access. Transfers between stores are rejected. Variants/images/specifications/prices/inventory belong to their listing.
- Public catalog/favorites/affiliate/reviews/cart/checkout require active public owner. Dealer storefronts filter by direct ownership. Detail returns one seller; listings/detail include storeId, catalog supports `?storeId=`, and brand filters still identify manufacturer.
- Cart returns storeId/storeName; frontend uses the store label. Related products filter by the same store. Frontend does not hide shared sellers to simulate single ownership.
- New order lines snapshot StoreId; seller orders split by `(StoreId,BrandId)`. Settlements inherit store identity. Store owners see their settlements; refund debt recovery cannot cross stores. Brand-scoped permissions/recovery remain only for historical financial snapshots with null StoreId. Old financial records are not assigned a guessed store.

## Accepted data policy

User chose to retain existing stock at one store; other stores enter inventory again. Retained owner prefers a linked store with the manufacturer owner's account, otherwise active/non-deleted, then deterministic slug/ID order. Original product/variant IDs, SKU, stock, reservations, reviews, favorites, cart/order/affiliate references and inventory history remain there.

Other links become independent listings with deterministic new product/variant/image IDs and unique SKU/slug. Description/specifications/images/starting prices are copied. Stock/reservations and affiliate budget start at **0**. Engagement, order references and financial history are never copied. Unlinked historical products are archived, not deleted.

Migration backfill is transactional and locks catalog tables. Downgrade restores one-to-one links only before new store-attributed commerce; it refuses to discard store snapshots after such activity. Independent listings are never merged during downgrade.

## Preview / deployment

Private read-only helper `.local/store-owned-products/Preview.csproj` and `Program.cs` guard the exact configured Neon host/database and save `.local/store-owned-products/preview.json` under a read-only repeatable-read transaction.

Online preview: **21 products → 93 listings** (72 new), **52 → 244 variants**, total stock **981 → 981**, reserved **0**. Original phone/audio/accessory inventory stays at CellphoneS, laptop inventory at An Khang Computer, kitchen inventory at Điện Máy Xanh. Exact mapping is in the private preview; new listings have zero stock.

**Not applied online / not deployed.** Back up the DB, verify mapping and pending migrations, and coordinate backend deployment with migration in a maintenance window: old backend queries the removed join table. Do not rerun the old shared-product seeder.

## Verification

- Full backend suite: 140 passed on isolated PostgreSQL; final projection/locking changes also passed focused tests. Migration flow covers independent identities, stock/reservation conservation, original history preservation, single seller, owner/brand authorization, immutable ownership, two stores in one checkout and separate settlements. Additional payout test confirms store B refund debt does not reduce store A payout; guarded downgrade preserves snapshots after commerce activity.
- Frontend: 145 tests, TypeScript, lint and web export passed. Existing seller layout was browser-tested earlier; online single-owner behavior requires new backend plus migration. No online inventory changed.
- Preserve unrelated deleted dealer-categories handoff and existing frontend work.
