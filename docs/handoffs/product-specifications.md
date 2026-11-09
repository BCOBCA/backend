# Category-driven product specifications

Implemented 2026-11-09. Online database changes are **pending explicit approval**: automatic approval review rejected applying the migration/seed to the live Neon catalog. No online writes or deployment occurred.

## Storage and contracts

- `Products.Specifications`: JSONB object containing typed common properties. `Categories.AttributeDefinitions`: JSONB array defining key, label, group, type, unit, required, scope and select options. Migration `20261109032814_AddProductSpecifications` adds only these two columns, defaults and JSON shape checks.
- Descendants inherit ancestor definitions; child overrides match `(scope, key)`. Local definitions max 100, merged max 200. Existing products are not retroactively rejected when category definitions change; new/explicit specification writes and category changes validate against the current schema.
- Common properties use scope `product`; purchasable color/storage/RAM/size remain in existing `ProductVariant.Attributes`, scope `variant`. IDs, SKU, price and stock contracts stay intact.
- Category admin input accepts optional `attributeDefinitions`; product admin input accepts optional `specifications`. Omission preserves existing metadata; `{}` explicitly clears optional product values. Required fields, JSON types, select options and unknown configured keys are validated. Existing variant objects remain compatible where no variant schema exists.
- `GET /api/categories/{id}/attributes`: inherited definitions for an active category. Product detail `specifications`: grouped `{ name, items: [{ key, label, value, unit }] }`. Listings keep this optional field empty.
- Product detail also returns `sku` from `Product.SKU`. The “Mã sản phẩm” row uses this value and remains unchanged when switching variants; variant `sku` remains available for variant-level operations. Older API responses without product `sku` omit the row rather than substituting a variant code. Regression tests cover both data mapping and display.
- Current scope implements backend admin write contracts and customer-facing rendering. A visual category/schema editor is not included.

Example clothing definitions and values:

```json
[
  { "key": "material", "label": "Chất liệu", "group": "Đặc điểm", "type": "text", "scope": "product", "required": true },
  { "key": "color", "label": "Màu sắc", "group": "Phân loại", "type": "text", "scope": "variant" },
  { "key": "size", "label": "Kích thước", "group": "Phân loại", "type": "select", "scope": "variant", "options": ["S", "M", "L", "XL"] }
]
```

Product `specifications`: `{"material":"Cotton"}`; individual variant `attributes`: `{"color":"Đen","size":"M"}`. Electronics instead define chip, camera, battery, screen, audio and connection fields. Appliances can define wattage, voltage and capacity. No database column is needed for each new property.

## Frontend

`product-specifications.ts` formats grouped values, units and booleans; selected variant fields replace matching common rows. `ProductDetailInfo` shows six preview rows and a full specifications tab. No hardcoded model-specific facts in the frontend.

Until the new backend is deployed, readable description sections beginning with `\n\nTHÔNG SỐ KỸ THUẬT\n` are supported as a legacy API fallback. They are hidden from the normal description and rendered as groups. Structured API values take precedence. This fallback has not been written to the online DB yet.

## Prepared catalog update

Private preparation scripts/data are under ignored `.local/product-specifications/` at workspace root; the deleted full product JSON exports are not required or recreated.

- `prepare.py` builds `manifest.json`: 10 category schemas, 21 products, 99 common values. Clothing/footwear schemas are prepared despite no clothing products in the current catalog.
- Unknown exact models/configurations receive only established product identity information. Never infer specific power, capacity, processor or battery values for generic appliances/accessories.
- `Program.cs` / `Specs.csproj`: exact Neon host/database guard, category existence checks, reject unexpected pending migrations, raw backup before migration, serializable metadata transaction, refusal to overwrite unexpected metadata, read-back checks. Commerce snapshot checks preserve product identity/prices/status, all variant fields and images.
- Pending action after approval: run this helper with `--apply`, then `--verify`, using the workspace .NET 10 SDK and `DOTNET_CLI_HOME=.local/dotnet-home`. The only permitted pending migration is the migration named above. Local backup is created before applying it. Then deploy the backend separately when requested.
- `serve-preview.py` is a **local fixture preview** on port 18107. It reads public product data and adds prepared specification values to local responses only. It does not prove that online API/DB metadata has changed.

Primary source provenance is recorded per product in the private manifest. Sources include [Apple iPhone 13 Pro](https://support.apple.com/en-us/111871), [iPhone X](https://support.apple.com/en-gb/111864), [MacBook Pro 14](https://support.apple.com/en-by/111902), [iPad mini](https://support.apple.com/en-au/111886), [AirPods Max](https://support.apple.com/en-ca/111858), [HomePod mini](https://support.apple.com/en-ie/111914), [Watch Series 4](https://support.apple.com/en-gb/111984), [Samsung Galaxy S10](https://www.samsungmobilepress.com/articles/samsung-raises-the-bar-with-galaxy-s10-more-screen-cameras-and-choices), [Tab S8 comparison](https://www.samsung.com/ie/support/mobile-devices/galaxy-tab-s8-vs-s8-vs-s8-ultra-comparison/) and [Lenovo Yoga 920 PSREF](https://psref.lenovo.com/syspool/Sys/PDF/Lenovo_Laptops/Yoga_920_13IKB/Yoga_920_13IKB_Spec.PDF).

## Validation

- Full backend suite: 139 passed on isolated PostgreSQL 18, including migrated scratch DB, model snapshot consistency, typed grouped reads, inheritance, permissions, preserved omitted metadata, invalid required values and category changes from phone to clothing.
- Full frontend suite: 140 passed; TypeScript, lint and web export passed. Includes typed values/units, false/zero/null, category-specific fields, selected RAM override and legacy description fallback.
- Browser: Samsung detail renders grouped fixture specifications and switches to the full table; at 390 px viewport the page has no horizontal overflow and displays camera, battery and selected RAM correctly. Authentication endpoints are unavailable in the read-only preview; checkout/authentication was not tested here.
- Backend regression assertions updated for the new category column and the existing 69-entity model (the prior 67 count predated dealer entities). No unrelated deleted handoff or frontend work was reverted.
