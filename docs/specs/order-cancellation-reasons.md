# Order confirmation and cancellation reasons

## Objective and acceptance
- Confirmation notifications say `Đơn hàng của bạn đã được xác nhận.`
- Buyer and seller may cancel only PENDING, UNPAID orders. Existing paid-order refund rules remain in force. Recheck state and ownership inside a serializable transaction to prevent confirm/cancel races.
- Buyer selects a predefined reason. Seller selects a preset or enters another reason (1–500 trimmed characters). Reject missing/invalid reasons on the server; never accept the cancellation actor from the client.
- Buyer POST `/api/orders/{id}/cancel` accepts `{ reason: string }`. Seller PUT `/api/seller/stores/{storeId}/orders/{id}/status` accepts `{ status: "CANCELLED", reason: string }`; confirmation retains its existing contract.
- Store authenticated actor in `OrderStatusHistory.ChangedByAccountId` and actor/reason in `Note`. Buyer notification: `Bạn đã hủy đơn thành công với lý do: ...`. Seller cancellation: `Cửa hàng đã hủy đơn của bạn với lý do: ...`.
- Reuse inventory/voucher/shipment cancellation and transaction handling. Do not alter historical notifications whose actor/reason was not recorded; do not infer it from ambiguous status text.

## Implementation plan
1. Backend: reason rules, DTO/interface/controller, customer/seller cancellation paths, notification/history contents. Prove validation, ownership, lifecycle and resource release with focused xUnit tests.
2. FE: reason form before cancellation from both order list and detail; submit selected reason and refresh orders, detail, vouchers and notifications. Verify Vitest route/form tests.
3. Seller: pending-order cancellation form with preset/custom reason; refresh list/detail on success. Verify Vitest behavior and view tests.
4. Run backend tests/build, frontend and seller tests/typecheck/lint, inspect responsive forms in a browser, record handoff.

## Structure and conventions
Use existing BLL/DTOs, BLL/Common, BLL/Services and BCOBCA.Tests; client forms live in existing order component folders. Follow existing explicit DTOs, BusinessException status codes and React Native components. No schema migration or dependencies required.

## Commands
- Backend: `dotnet test BCOBCA.sln --no-restore`.
- Clients (each workspace): `node node_modules/vitest/vitest.mjs run --pool=threads`; `node node_modules/typescript/bin/tsc --noEmit`; `node node_modules/eslint/bin/eslint.js .` (seller: `app src tests`).

## Boundaries
Preserve unrelated working changes. No production data rewrites, deployment or real customer test cancellations. User explicitly chose seller cancellation only before confirmation.
