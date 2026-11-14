# Seller verification submission

## HTTP contract
- Authenticated owner: `POST /api/seller/stores/{storeId}/verifications`, multipart fields `submissionId` (UUID), `updatedAt` (current profile timestamp), `legalName`, `taxCode`, `representativeName`, `license`, `identityFront`, `identityBack`.
- Returns the refreshed `SellerStoreProfileDto` in the existing API envelope. Verification summary adds `id`, `submittedAt`, `documents[{kind,fileName,contentType,size}]`; existing fields remain compatible. No public IDs, private URLs or file contents in JSON.
- `GET /api/seller/stores/{storeId}/verifications/{verificationId}/documents/{kind}` authorizes the current active owner, proxies bytes as an attachment, and sets `Cache-Control: no-store`.
- Validation errors: 400 (multipart/model binding), 422 (business/input/file validation), 413 (file size), 404 (ownership/not found), 409 (pending/verified/stale), 502/503/504 (storage). Reuses account-scoped `image-upload` policy.

## Persistence and storage
- Reuse existing StoreVerifications table/JSON; no migration. Pending=0, approved=1, rejected=2. Submission never sets `Store.IsVerified`. Retain old rejected applications and their documents for history; no admin review UI in this change.
- Validate all three files before external I/O: exact kinds, nonempty, 10 MiB each, safe filenames <=255, matching extension/MIME/signature. Legal fields are trimmed; tax code has 10 or 13 digits (optional branch hyphen).
- `CloudinaryVerificationStorage` reuses `Cloudinary:CloudName/ApiKey/ApiSecret`. REST upload goes to `/raw/authenticated`, never public image uploads; filenames sent to Cloudinary are generic. Validate returned ID, raw/authenticated types and byte length. Disable redirects and HTTP client loggers.
- Paths have a server-generated attempt namespace. Sign raw downloads server-side and proxy bytes; signed URLs never reach clients. External response bodies/errors are not logged or exposed.
- Upload outside DB transaction; recheck ownership/version/status in a serializable transaction and update the Store row to serialize concurrent submissions. Existing submission UUID returns the saved profile without new uploads. Independent attempt namespaces make concurrent retries safe to clean up.
- Failures before COMMIT attempt trigger bounded best-effort deletion of all attempted paths. A failed cleanup logs only exception type. Uncertain COMMIT outcomes retain files to avoid corrupting a possibly saved application; retry the same UUID. Residual uploads may require operational cleanup after storage outages.

## Verification / limits
- Tests: `VerificationValidationTests`, `CloudinaryVerificationStorageTests` (fake HTTP), `SellerVerificationTests` (isolated localhost PostgreSQL, including concurrency), `SellerVerificationHttpTests` (real localhost multipart/read requests and test-only authentication).
- Use workspace `.tools/dotnet/dotnet.exe` (.NET 10); system `dotnet` is .NET 9. Local PostgreSQL tests use scratch DBs on 127.0.0.1:55433 only. No live document uploads performed.
- Full suite additionally exposes existing unrelated failures: PostgresFlowTests table count 68 vs 70, StoreOwnedProductsFlowTests historical fixture missing ProductReviews.ImageUrls. No changes made to those tests.
- Cloudinary sources: [REST upload types](https://cloudinary.com/documentation/upload_parameters), [delivery signatures](https://cloudinary.com/documentation/delivery_url_signatures).
