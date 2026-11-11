# Order detail display metadata

- Add optional StoreName/StoreSlug/ImageUrl/VariantName to OrderItemDto and PaymentMethod to OrderDto. Existing constructors/contracts remain compatible; no migration.
- Owned-order query still validates user ownership before reading lines, history and shipments. Metadata uses correlated selects in the existing item query, avoiding per-item API calls. Snapshot names/SKU/monetary fields remain from OrderItem; display enrichment uses current seller/variant/image metadata.
- Historical seller names remain available after soft deletion. StoreSlug explicitly requires public store type, active status and DeletedAt null; rely on the explicit predicate because IgnoreQueryFilters in another subquery can suppress global filters for this projection.
- Existing history/shipment loading unchanged. Frontend preserves their visible sections while redesigning the detail page.
- Isolated PostgreSQL CollectedCheckoutVoucherFlowTests now verifies display fields, real payment method, order history, access denied for another user and preserved snapshot/history after product/store soft deletion. Existing amount/idempotency/voucher/cancellation assertions still pass. No real business data changed; temporary DB removed.
