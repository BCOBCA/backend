# Store product reviews

- Store header statistics: DealerDto adds optional ProductCount (default 0), projected using ProductCatalogScope.Available and the store ID. Existing ReviewCount remains the published-review total. Regression coverage checks store isolation, inactive products and empty stores.

- Public `GET /api/dealers/{id:guid}/reviews` uses CatalogQuery and existing PageResult<ReviewDto>; rejects unavailable dealers with 404.
- Selects published reviews by persisted StoreId, preserving purchase ownership even after product reassignment and including inactive products. Orders by CreatedAt descending then Id before pagination.
- Reuses buyer display-name privacy fallback, images and seller replies. Reply store name prefers persisted review ownership, retaining the legacy order-item fallback.
- Coverage in StoreReviewRatingTests: store isolation, hidden/pending exclusion, newest-first pagination across all pages, replies, legacy-order reply names, inactive products and missing/inactive stores. PublishedReviewsTests covers seller reply behavior.
- Uses isolated PostgreSQL at 127.0.0.1:55433 and workspace .NET 10 SDK. No database migration required.
