# Affiliate policy disclosure API

- Added anonymous, read-only `GET /api/affiliate/policy`, standard `ApiResponse<AffiliatePolicyDto>`. No DB migration, secrets, configuration enumeration, business-service mutations or approval action.
- `AffiliatePolicyConfiguration.Read` returns an explicit allowlist. Existing runtime keys `Affiliate:AttributionDays` (1–90, explicit configuration only) and `Affiliate:ApprovalHoldDays` (nonnegative) supply disclosed timing. Missing/invalid values remain null; tracking's execution fallback is not presented as a published promise.
- Metadata keys under `Affiliate:Policy`: `Approved`, `Version`, `UpdatedOn`, `ApprovedOn`, `EffectiveOn`, `ComplaintEmail`, `OperatorName`, `OperatorAddress`. Dates are strict YYYY-MM-DD; email is validated. `.env.example` documents empty fields and approval defaults false.
- Status is DRAFT unless Approved=true plus a version and UpdatedOn <= ApprovedOn <= EffectiveOn. Frontend additionally checks whether approval/effective dates have arrived. No metadata was filled or approved in the live configuration.
- Fee, minimum withdrawal, payout deadline, attribution-priority text and complaint SLA are null: current execution services have no corresponding configurable published terms. Do not fill these with arbitrary metadata that does not affect execution.
- Actual HTTP smoke test: DRAFT; no update/effective dates; 14 approval-hold days; other undisclosed parameters null. Existing rank API returns Đồng 0/2%, Bạc 10/3%, Vàng 30/5%, Bạch kim 60/7%, Kim cương 100/10%.
- Verified 20 focused tests (policy configuration and existing affiliate rank rules); API project built by the test build. Public HTTP responses verified without authentication; no DB records changed.
- Deployment: ship API before frontend. Without the endpoint, frontend shows an unavailable/unverified state and does not assert legal approval.
- Publication remains an operator responsibility: review the actual deployed text, configured business rules, legal identity and applicable legal obligations before approving metadata. This endpoint is disclosure, not an Admin approval workflow.
