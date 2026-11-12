# Affiliate product catalog

- New authenticated `GET /api/affiliate/products`, accepting existing CatalogQuery paging/search/category/brand/store/sort parameters.
- Active affiliate/account required. Catalog limits products to active affiliate-enabled items with eligible brands/stores; category selection includes active descendants and search matches category names.
- Reuses catalog product/variant/image projection. Sorts product query before DTO projection for EF translation.
- Response adds approved rating/count, effective commission rate (current rank capped by product policy), and one-unit estimate from current price rounded like finance settlement and limited by remaining product budget. Reviews, policies and spent commission are batch queried.
- Registered IAffiliateCatalogService; no schema migration or shared-database change.
- ProductAffiliateFlowTests passed: rank changes, discounted price, cap/budget/exhaustion, approved reviews, category/search/sort and inactive affiliate rejection alongside existing attribution/payout/refund flow.
- Test fixture captures supplied PostgreSQL connection before opening admin connection so password stripping cannot break scratch-database setup. Tests use isolated scratch databases.
