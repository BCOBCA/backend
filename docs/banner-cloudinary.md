# Banner simplification and image uploads

## Contract and scope
- Remove `Title`, `Position`, `SortOrder` from the Banner entity/table and request/response DTOs.
- Keep `Id`, `ImageUrl`, `TargetUrl`, `StartsAt`, `EndsAt`, `IsActive`. Public `GET /api/banners` returns `{id,imageUrl,targetUrl}`, active/in-window only, ordered by start time descending then ID.
- Existing admin banner create/update accepts `{imageUrl,targetUrl,startsAt,endsAt,isActive}` and persists the uploaded URL.
- Add authenticated `POST /api/admin/uploads/images`, multipart field `file`, current Admin/SuperAdmin role required. Return the normal envelope with `{url,publicId}`.
- Accept JPEG/PNG/WebP, maximum 5 MiB; validate content signatures. No remote URL imports, SVG or arbitrary files. Limit upload attempts by account.
- Use Cloudinary server-side HTTPS upload with Basic Authentication; cloud name/API key/API secret stay in backend environment variables. No new dependency required.
- FE consumes the reduced banner contract and retains clickable targets with generic accessible labels instead of removed titles.

## Implementation plan
1. Add focused failing upload boundary/transport tests and banner model/contract checks.
2. Simplify banner model/services; generate migration and verify SQL without touching deployment DB.
3. Implement upload controller/service, environment configuration and failure handling.
4. Update FE banner display; run tests, typecheck, lint, API publish and web export.

## Deployment notes
This is a coordinated DB/API/FE contract change. Migration drops three columns; their values cannot be restored by rolling back without a backup. Deploy the matching backend and frontend together. Do not apply the migration automatically during API startup.

## Configuration
Set backend/Render variables (leave the service unconfigured locally if uploads are not needed):
```dotenv
Cloudinary__CloudName=your-cloud-name
Cloudinary__ApiKey=your-api-key
Cloudinary__ApiSecret=your-api-secret
```
Missing settings return 503 only when uploading; the rest of the API can start normally. No Cloudinary credential goes into frontend `.env`.

## Upload, then save the returned URL
`POST /api/admin/uploads/images` with `Authorization: Bearer <admin access token>` and multipart field `file`.

Successful response:
```json
{"success":true,"message":"Thành công","data":{"url":"https://res.cloudinary.com/your-cloud-name/image/upload/v1/bcobca/uploads/image-id.png","publicId":"bcobca/uploads/image-id"},"errors":null}
```

Use `data.url` in `POST /api/admin/banners` (or `PUT /api/admin/banners/{id}`):
```json
{
  "imageUrl":"https://res.cloudinary.com/your-cloud-name/image/upload/v1/bcobca/uploads/image-id.png",
  "targetUrl":"https://example.com/campaign",
  "startsAt":"2026-11-03T00:00:00Z",
  "endsAt":"2026-12-03T00:00:00Z",
  "isActive":true
}
```
`targetUrl` may be empty when the banner has no link. Uploading an image alone does not create a banner. If a later save is abandoned or fails, its uploaded image remains in Cloudinary for manual cleanup; no external resource is deleted automatically. Existing non-Cloudinary image URLs continue to work.

The upload endpoint allows 20 attempts per authenticated account per minute. Service errors use the existing API envelope: invalid image 422, file over 5 MiB 413, missing configuration 503, invalid/provider response 502, provider timeout 504. Larger request bodies can be rejected earlier by ASP.NET form/body limits with 400 or 413. Caller cancellation is propagated. Permission checks read current database roles.

## Migration/deployment
Back up banner data, then review/apply `SimplifyBannerFields` through the existing migration workflow:
```powershell
dotnet tool run dotnet-ef database update --project BCOBCA.DAL --startup-project BCOBCA.API
```
Run from `backend` after configuring the intended database. This command is a deployment step; it is not run automatically by the API. Deploy the API using the reduced model after migrating; update FE to remove references to banner `title`. The original imported Word schema in `docs/database-schema.json` is historical; the EF model and migrations define the current schema.

## Verification
- All 43 backend tests passed, including upload transport/validation/failures and real PostgreSQL migration from a populated legacy banner table.
- The isolated PostgreSQL test verified ImageUrl/TargetUrl/scheduling preservation, the new column set, public reads, admin banner persistence and current-role upload authorization after revocation. No deployment database was changed; the local test cluster was stopped afterward.
- API Release publish, FE typecheck/ESLint and web export passed. EF reports no pending model changes; generated SQL drops only Title/Position/SortOrder.
- Provider tests use an in-memory HTTP handler; no live Cloudinary upload has been performed because credentials are not configured.

References: [server-side upload authentication](https://cloudinary.com/documentation/authentication_signatures), [Upload API](https://cloudinary.com/documentation/image_upload_api_reference).
