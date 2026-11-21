# Published reviews and seller replies

## Behavior
- Buyers may review their own purchased item after verified shipment delivery or order completion. `CanReview` is added to buyer order list/detail responses. COD/payment/settlement logic is unchanged.
- New and edited reviews publish immediately. Migration `20261121075810_PublishedReviewsAndSellerReplies` adds nullable `ReplyContent`/`RepliedAt` and publishes existing pending reviews, changing their revision. Rejected reviews remain hidden. Downgrading drops replies; it does not restore pending status.
- Seller GET `/api/seller/stores/{storeId}/reviews` supports page/pageSize, rating, replied, productId, search, from/to (normalized to UTC). Data is scoped to the historical order-item store and active owner; no buyer phone/address is exposed.
- Seller review rows additionally return `orderId` so review-card actions open the exact seller order detail rather than guessing an identifier from orderCode. This additive DTO change requires backend deployment but no extra migration.
- Seller PUT `/api/seller/stores/{storeId}/reviews/{id}/reply` requires content (trimmed 1–2,000 chars) and expectedRevision. One editable reply, no seller editing/removal of buyer review. Stale revisions/concurrent updates return 409.
- Public review DTO appends nullable `reply: {content,storeName,updatedAt}`. Admin removal still exists for abuse, with no pre-publication approval requirement.
- New reviews enqueue `REVIEW_CREATED` for the store owner (`StoreReview` target = store ID). Reply changes enqueue `REVIEW_REPLY` for the buyer (`Review` target = order-item ID). Existing transactional inbox/push queue, unread bells and live feeds are reused. Repeated identical saves do not duplicate events.

## Clients
- Seller: Products → Đánh giá sản phẩm (`/reviews`). Rating chips; All/Awaiting/Replied tabs; product/buyer/order search; Vietnam date range; server pagination; reply/edit dialog. Notification routes to the notified owned store without assuming the currently selected shop. Live notifications refresh the list, with stale requests ignored.
- Buyer: order cards/detail honor canReview; editor says review is published, displays shop reply and preserves dirty drafts on live refresh. Public product review feed displays replies as plain escaped text. Inbox/native push reply taps open `/review/[orderItemId]`; live updates invalidate review caches.
- Reference for layout and interaction: [TikTok Shop Product Ratings](https://seller-us.tiktok.com/university/essay?identity=1&knowledge_id=6599730126833451). This implementation allows replies at all ratings.

## Rollout
1. Back up the target database through the normal deployment workflow.
2. Use .NET 10: `../.tools/dotnet/dotnet.exe ef database update --project BCOBCA.DAL --startup-project BCOBCA.DAL` (from backend). Select target connection through existing environment configuration.
3. Deploy backend and both clients together; the new clients need the added seller review endpoints. No live migration/deploy/commit was performed in this task.
4. Native push still requires registered device tokens and the existing push worker configuration. Tests verify inbox records and push queue creation; no real push messages or GHN deliveries were sent.

## Verification
- FE: 478 tests passed; seller: 248 tests passed. Both TypeScript and targeted ESLint passed.
- PostgreSQL service/migration regression covers delivered-but-unpaid eligibility, immediate publication/edits, buyer ownership, seller scoping, invalid content, stale revisions, filters/Vietnam time, public replies, inbox recipients/targets, idempotence and pending-vs-rejected migration. Prior related GHN/seller-read run: 13 passed.
- Isolated Chrome headless UI fixture exercised desktop 1440px/mobile 390px, draft entry/save, reply display and popup. Zero nested buttons, horizontal overflow or console errors. Fixtures removed. Screenshots: workspace `.local/reviews-*.png`; browser script `.local/reviews-browser-check.cjs` uses only local sample data.
- Full backend: **320 passed / 2 failed / 322 total**. Remaining failures are `PostgresFlowTests` (expects 68 model types, current model has 72) and `StoreOwnedProductsFlowTests` (historical fixture omits required `ShortDescription`). Both were reproduced against an isolated archive of unmodified HEAD with identical errors. Reports: workspace `.local/reviews-test-results/reviews-full-final.trx` and `baseline-two-fixtures.trx`.
- The existing review migration fixture now supplies the historical ShortDescription default locally, and FinanceFlow/review image expectations assert immediate publication instead of a moderation queue. No production product or finance behavior changed.
