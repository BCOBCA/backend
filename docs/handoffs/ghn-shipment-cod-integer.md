# GHN shipment creation: integer COD

- Confirmed orders could remain at `Shipment.Status=PENDING` while the worker retried. The stored request serialized a PostgreSQL decimal total as `cod_amount:61900.00`; GHN create rejects this with HTTP 400 (`expected=int`, `ShiipCreate.cod_amount`). Preview had succeeded.
- `OrderService` now snapshots COD as an integer after its existing whole-dong/range checks. `GhnQuoteClient.CreateShipmentAsync` also normalizes existing snapshots before sending, rejecting fractional, negative, nonnumeric or over-limit amounts without contacting GHN. No amount rounding, migration, or customer data correction.
- Regression: the three existing-snapshot integer cases failed before the fix. All 36 quote/lifecycle/webhook tests pass after the fix; build and diff checks pass.
- Bounded live staging retry for confirmed order `BCA368A693DB5E64A99BF8F` succeeded through `GhnFulfillmentService`; tracking code `LYCUTR`, status `ready_to_pick`. Independent database read confirmed persistence. COD remained 61,900 VND. No additional purchase order was created.
- Deploy the changed backend to Render to fix future background retries. This session repaired the existing order using the updated service but did not deploy or push code. GHN remains staging only.
