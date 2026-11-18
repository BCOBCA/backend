# GHN lifecycle and notifications

- Staging only. Checkout snapshots a token-free GHN request into Shipment.
  Seller CONFIRMED requires the snapshot; SHOP SHIPPING requests are rejected.
- `CommerceDeliveryWorker` runs separate retry loops for GHN creation and Expo
  push. Enable explicitly with Shipping__Ghn__FulfillmentEnabled / Push__Enabled.
- GHN callback: POST /api/shipping/ghn/callback, secret header
  X-GHN-Webhook-Secret, minimum 32 chars, fixed-time comparison, size/rate bounds.
  Shop/order binding, event idempotency, stale/terminal guards, normalized UTC.
- Status/history/notifications commit atomically. GHN pickup moves CONFIRMED →
  READY → SHIPPING. Delivered is verified through staging detail; COD must be
  collected and equal checkout total before existing completion/accrual runs.
  Return/failure events notify without inventing refunds.
- Push devices are account/session-family bound, hash indexed and Data Protection
  encrypted. Logout/revocation/account switching prevents pending old deliveries.
  PushDeliveries is transactional outbox; worker SKIP LOCKED, ticket/receipt checks,
  bounded retries, invalid-token disabling. Delivery is at least once on crashes.
- GET /notifications/live replays latest 100 account notifications with 10s long
  polling; clients deduplicate IDs, invalidate order/inbox caches and reconnect
  with backoff. Full historical inbox remains paginated. No SSE/native dependency.
- Native buyer push is opt-in and opens Order links; web has live in-page updates.
  EAS/FCM/APNs activation is pending by user choice. Closed-browser Web Push is
  not included. Persist/share backend Data Protection key ring across deployments.
- Migrations: GhnLifecycleAndPushNotifications, PushDeviceSessionBinding.
  No migration applied to a real database; old orders lack a GHN snapshot.
- Full setup: [GHN and push setup](../../../frontend/docs/setup/ghn-and-push-notifications.md).
- Verification: 52 focused .NET tests passed on isolated PostgreSQL 18, including
  GHN binding, duplicate/stale callbacks, COD verification/accrual, encrypted
  devices, session/account revocation and Expo ticket/receipt workflow. GHN/Expo
  HTTP is mocked; no real shipment or device push was sent.
- Clients: 432 buyer tests and 183 seller tests; typecheck/lint, both web exports
  and Android Hermes export passed. Browser fixture verified confirmation removes
  manual handover and live notifications/history update without reload; no console
  errors. Real device push awaits EAS/FCM/APNs setup per user's instruction.
