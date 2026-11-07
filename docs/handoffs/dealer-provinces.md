# Dealer operating provinces

- `Stores.Provinces` is nullable `text`: `Toàn quốc`, or canonical names joined by `, `. Migration: `20261107014205_AddStoreProvinces`. Existing rows remain NULL; never infer scope from branch addresses.
- `DealerProvinces` owns the 34-name catalog, validation and deduplication. Names use short labels such as `Hồ Chí Minh`; catalog source: https://datafiles.chinhphu.vn/cpp/files/vbpq/2025/7/19ttg.signed.pdf. Geographic catalog maintenance is independent from shipping-provider address mapping.
- GET `/api/dealers/regions` returns selectable province names. The existing `provinceCode` query parameter accepts names and current codes for compatibility; filtering includes nationwide dealers and matches whole comma-delimited names.
- GET `/api/dealers/mine` and PUT `/api/dealers/{id}/provinces` require authentication. Updates accept `{ "provinces": "Hà Nội, Hồ Chí Minh" }` and enforce store ownership/type/not-deleted. Empty, unknown and mixed nationwide/province selections fail validation.
- Public DTO adds `provinces`; `locationLabel` also returns scope for older clients. Branch detail addresses and shipping fields remain unchanged.
- `provinceCode=Toàn quốc` filters exclusively to nationwide dealers. Selecting an individual province still includes nationwide dealers; other search/sort/pagination options compose normally. No schema change required.
- Verified: DealerProvinceTests + DealerFlowTests (9 cases), including ownership, persistence, filtering, privacy independence, migrations up/down. Frontend verification is documented in its handoff.
- Migration was applied only to the isolated local QA database `bcobca_provinces_preview` on port 55433. Configured backend/.env points to a remote database; it was not modified. Deploy migration before the updated backend. No remote deployment or commits performed.
