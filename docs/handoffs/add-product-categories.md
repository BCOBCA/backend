# Additional product categories

- Added the 15 requested categories to the configured database on 2026-11-05: Nội thất & trang trí; Nhà cửa & đời sống; Ô tô & xe máy; Sách & văn phòng phẩm; Gaming & giải trí; Máy ảnh & quay phim; Đồng hồ & trang sức; Thú cưng; Hoa & quà tặng; Nông nghiệp & làm vườn; Công cụ & thiết bị; Thiết bị công nghiệp; Voucher & dịch vụ; Du lịch & khách sạn; Sức khỏe & chăm sóc.
- New records are active root categories ordered after existing records. ImageUrl is empty until category images are supplied; frontend uses its fallback icon. No fabricated product/image associations.
- Executed ignored helper `.local/add-categories/AddCategories.csproj` with workspace .NET 10 SDK. Duplicate detection by name/slug, serializable transaction, and post-write verification before commit.
- Result: 15 inserted, 0 skipped, 25 total; all 10 prior records and their images/ordering/activation preserved. No application code or schema changes.

## Category images

- Follow-up workflow: `.local/add-category-images/prepare.py` downloads 15 category-specific Unsplash photos at 960×520, creates a visual QA contact sheet, uploads to existing Cloudinary storage, and verifies each uploaded URL decodes as an image. Source URLs/checkpoint are retained locally in `sources.json` and `uploaded.json`.
- Database helper: `.local/add-category-images/CategoryImages.csproj`; backs up category metadata locally, uses a serializable transaction, updates only missing ImageUrl values for the 15 targets, and verifies unrelated fields/records before commit. No frontend or schema changes.
- Completed: 15 uploaded images fetched/decoded successfully; 15 ImageUrl values committed and read back. All 25 categories now have images; existing category images and all other metadata preserved. Contact sheet visually inspected; no app-code tests required for this data-only update.
