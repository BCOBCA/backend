# Dealer card addresses

- Fix the listing projection in `BCOBCA.BLL/Services/DealerService.cs`: `DealerDto.LocationLabel` now contains the full address of the main active public branch; if that address is blank, use another active public branch with a nonblank address. Stable Id ordering breaks ties.
- Only branches enabled by StoreSettings.ShowBranches are eligible. Closed/hidden branches never expose addresses; missing address returns `Chưa công bố địa chỉ`.
- Province filtering and `/api/dealers/regions` continue to use ProvinceCode. No DTO shape, database schema or stored data changes.
- Frontend `DealerCard.tsx` renders LocationLabel directly without the province-code prefix or single-line truncation; text wraps fully and follow controls align at the card bottom.
- PostgreSQL regression tests cover the main address, whitespace-only addresses, fallback branch, closed branches and privacy settings. Frontend render tests cover public/private address labels without the incorrect prefix.
- Validation: 112 backend tests and 86 frontend tests pass; frontend typecheck and lint pass. Local HTTP listing returns all ten full addresses from the configured Neon data.
- Deployment note: `frontend/.env` currently points web requests to `https://backend-zqnb.onrender.com/api`, whose deployed listing still returns province codes. Deploy the updated backend to Render for that frontend configuration to display addresses. The env file is unchanged; local browser verification uses a process-only API override on a separate preview port.
- Verified local preview at `http://localhost:8086/dai-ly`: full Nguyễn Kim/GearVN street addresses render, no province-code prefix, no console warnings/errors and no overflow on desktop or 390px mobile. API runs at localhost:5000 with the updated binaries; no production deployment or Git push was performed.
