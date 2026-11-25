# Public store product reviews

DealerDto additionally exposes productCount for active public-catalog products owned by the dealer; reviewCount continues counting published reviews. Both appear alongside followers in the store header. Counts require no schema change.

`GET /api/dealers/{id:guid}/reviews` accepts the existing CatalogQuery pagination and returns PageResult<ReviewDto>.
Only active, undeleted dealers (StoreType = 1) are public. Return 404 otherwise.
Select published reviews by persisted StoreId, including inactive products. Order by CreatedAt descending, then Id ascending, before pagination. Preserve buyer images and seller replies. Do not expose order information or buyer account IDs.
Verify cross-store isolation, pending/hidden exclusion, pagination, replies and unavailable dealer handling in StoreReviewRatingTests. No schema changes.
