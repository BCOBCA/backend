# Dealer directory data — 2026-11-06

## Imported
Added ten real public retailer profiles to the configured application PostgreSQL database (`neondb`): FPT Shop, Thế Giới Di Động, Phong Vũ, Điện Máy Xanh, CellphoneS, Hoàng Hà Mobile, An Khang Computer, GearVN, HACOM and Nguyễn Kim.

- 10 Stores, 29 StoreBranches (10 main headquarters + 19 other published locations), 10 StoreSettings.
- Each dealer has its real website, description, public support phone, logo/avatar and cover image. Published support emails included where found; no guessed email addresses.
- Twenty image URLs are hosted on the configured Cloudinary service and were fetched/decoded successfully. Source images come from retailer websites/CDNs; provenance is in the manifest. Logo sources are delivered as PNG for cross-platform compatibility.
- Public settings enable branches and support phones; email is shown only when published. Messaging disabled.
- Profiles remain unverified (`IsVerified=false`, `VerifiedAt=null`); no fabricated verification documents or followers.
- Reused the existing disabled, role-free `catalog-holder@catalog.invalid` account to satisfy OwnerAccountId. No new accounts or login permissions, no changes to existing Accounts, Brands or Products.
- Import runs inside a transaction with an advisory lock, stable UUIDs and unique-slug checks. Existing merchants are not overwritten. Counts/settings/owner checks run before commit; all ten records were read back through DealerService after commit.

## Source manifest
`docs/data/dealers-2026-11-06.json` contains source URLs, original and hosted image URLs, headquarters, branch addresses and contact data. Data is persisted in PostgreSQL, never hardcoded into the frontend.

These are selected published locations, **not every national branch** of each chain. Exact addresses are retained from the cited sources, including older administrative wording where a source has not been updated. Unknown ward codes and coordinates are left empty/null rather than invented. Support phone may be the central hotline when the official source does not publish a local line.

The one-time import preparation/tooling is under workspace `.local/dealer-import/` and is excluded from Git. The application schema was already migrated when this task started; no schema changes were made.

## Verification
- Validated ten unique slugs, one main location per dealer, at least one additional branch, public source URLs and twenty Cloudinary image URLs.
- Post-import DealerService listing returns totalItems=10; detail queries return all 29 active locations and the correct main headquarters.
- Existing record counts preserved: Accounts=1, Brands=20, Products=0.
- No application source code changes were required for this data import.
- HTTP verification: `/api/dealers` returns ten profiles; FPT detail returns three locations; verified-only listing correctly returns zero.
- Browser at `/dai-ly`: all twenty cover/avatar images loaded, no console warnings/errors or horizontal overflow. Replaced thin header logos for FPT, Hoàng Hà and GearVN with their official square icons after visual QA.
