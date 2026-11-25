# Store review rating

- ProductReviews.StoreId: nullable store FK with (StoreId, Status) index. Nullable supports legacy platform products without store ownership.
- ReviewService derives ownership from purchased OrderItem.StoreId, falling back to Product.StoreId; request DTO does not accept ownership.
- AddProductReviewStore migration backfills existing reviews by the same rule.
- DealerService projects averageRating (nullable double) and reviewCount from all individual Status=1 reviews for that store. Includes inactive products, excludes pending/rejected reviews and other stores.
- Frontend product seller card displays the average with one decimal and an accessible count; no reviews displays “Chưa có”, loading displays “—”. Review submission invalidates dealer/dealers queries.
- StoreReviewRatingTests verifies save ownership, legacy order fallback, purchase snapshot precedence, weighted aggregation, visibility, empty stores and migration backfill. PublishedReviewsTests now names its migration baseline so newer migrations do not break that regression check.
