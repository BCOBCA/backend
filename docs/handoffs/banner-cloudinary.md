# Banner and Cloudinary handoff

- Banner retains `TargetUrl`; removed `Title`, `Position`, `SortOrder` from entity/mapping/DTOs. Catalog order is StartsAt descending then Id. FE uses generic accessible labels instead of Title.
- Migration: `20261103065935_SimplifyBannerFields`. Generated/tested locally, NOT applied to deployment DB. Back up removed fields before applying; rollback recreates columns with defaults, not their old values.
- Upload: `POST /api/admin/uploads/images`, multipart `file`, JWT required plus current Admin/SuperAdmin database role. Returns envelope `{url,publicId}`; use url as ImageUrl in existing banner create/update. Upload alone does not create a banner.
- Configure backend `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret`. No frontend secrets. Server-side HTTPS Basic Auth, fixed endpoint, redirects disabled, JPEG/PNG/WebP signatures, 5 MiB max, 20 attempts/account/minute.
- 43 backend tests passed including real local PostgreSQL migration/persistence/role revocation; API Release publish and FE typecheck/ESLint/web export passed. No real Cloudinary upload was performed; live credentials are still required.
- Full contract, setup, errors and deployment procedure: [banner-cloudinary.md](../banner-cloudinary.md).
