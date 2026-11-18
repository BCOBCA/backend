# GHN lifecycle and realtime notifications

## Scope
Continue using GHN staging only. Shop confirms; carrier controls pickup,
delivery/failure/return. Every carrier status event produces durable buyer
notifications and history, with authenticated in-app delivery and native push.
No production GHN requests or credential changes.

## Design and acceptance
- Snapshot the GHN request without credentials during checkout, per store order.
  Confirmation queues creation; a retryable worker creates the shipment with a
  stable client_order_code. Never substitute today's buyer address for a snapshot.
- Only CONFIRMED is accepted from seller. Never expose manual carrier handover.
- A secret-header webhook accepts known shipment events, checks shop/code, dedupes
  on provider code/type/time, rejects stale events, and updates shipment/history,
  order and notifications atomically. Deliveries require verified COD collection
  from GHN detail before financial completion; returns do not imply a refund.
- Store notification deliveries durably per device. Send via Expo, retain tickets,
  verify receipts, disable invalid tokens, retry transient errors. Revoke tokens on
  logout/account switch. No tokens or provider payloads in logs.
- In-app long polling uses existing cookie/bearer authentication, bounded waits,
  cursor replay, reconnect/backoff and account isolation. Invalidate order queries
  and show each newly received notification. Native notification permission is
  opt-in; unavailable EAS/FCM/APNs configuration is shown clearly.
- Mobile push covers background/terminated installed builds subject to OS
  permissions and provider delivery. Web runs live notifications while open;
  closed-browser Web Push is outside this native push integration.

## Plan
1. Add focused failing lifecycle/client tests; snapshot/queue schema migration.
2. Connect GHN creation, authenticated callbacks, transition policy and workers.
3. Add device registration, durable push dispatch/receipt and long polling.
4. Connect buyer/seller live updates and native opt-in/notification taps.
5. Run .NET tests on isolated PostgreSQL; frontend/seller tests/typecheck/lint/web
   builds, browser checks, and document external activation steps.

## Verification
Use local .tools/dotnet SDK 10, `dotnet test --filter` for affected tests;
`npm test`, `npm run typecheck`, `npm run lint`, `npm run build` in each client.
Preserve current commerce/financial rules; use existing DTO/envelope conventions.

## Sources
- https://developer.ghn.vn/vi/docs/order/create
- https://developer.ghn.vn/en/docs/webhook/callback-order-status
- https://developer.ghn.vn/vi/docs/master-data/order-status
- https://docs.expo.dev/versions/v58.0.0/sdk/notifications/
- https://docs.expo.dev/push-notifications/sending-notifications/
