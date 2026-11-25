# Store review rating

- Save the purchased order item's StoreId on each new review; fall back to the product's store for legacy order items. Clients cannot supply this value.
- Backfill existing reviews using the same rule. Keep nullable ownership for legacy platform products without a store.
- Public dealer data includes nullable averageRating and reviewCount, calculated across individual published reviews (Status = 1) for that store, including inactive products. Never average product averages.
- Product detail shows the average to one decimal, or “Chưa có” when there are no published reviews. Refresh dealer queries after submission.
- Verify persisted ownership, migration backfill, cross-product weighting, other-store exclusion, moderation visibility and empty/loading UI.
