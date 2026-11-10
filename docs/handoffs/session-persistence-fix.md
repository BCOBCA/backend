# Session persistence and refresh recovery

## Root causes and changes

- The frontend uses `https://backend-zqnb.onrender.com/api` from localhost. The API issued `SameSite=Strict` refresh cookies, which cannot support that cross-site session flow. `AuthController` now uses HttpOnly/Secure/SameSite=None/Partitioned cookies on HTTPS, with the existing configured lifetime. Plain-HTTP development uses Lax. Native body/SecureStore transport remains unchanged.
- Login now applies the same exact Origin validation as refresh, restore and logout. No allowed origins or JWT/refresh-token lifetimes were broadened. No credentials are persisted to web localStorage.
- Legacy unpartitioned cookies expire before replacement. Logout and rejected restore expire both cookie variants with matching scope/security attributes. Refresh-token rotation, reuse revocation and family validation are unchanged.
- Frontend coalesces restore/refresh calls and replays late 401 responses with the latest access token. Refresh rejection cleanup runs once inside the shared session lock. Network errors, 5xx and Origin-related 403 do not clear a valid session; confirmed 401 still does.
- Startup connectivity failures now show a retry screen, not anonymous navigation. Reconnection, web visibility and native foreground events retry failed recovery. Successful retry clears the error; confirmed invalid sessions clear stale user/cache state.

## Verification

- Frontend: 180 tests across 39 files passed; TypeScript and full ESLint passed. New regressions failed before fixes (including late 401, concurrent restore, duplicate invalidation, 403 and offline startup).
- Backend: 11 AuthCookieTests passed without a database, including cross-site flags, reload rotation, cookie cleanup, rejected origins and native transport. Seven initially failed against the previous controller.
- Browser: at the isolated development origin, blocked recovery showed the retry screen; clicking retry showed recovery progress and returned a connection error rather than a false logged-out app. Real signed-in refresh/reload was not exercised; no account or saved session was modified.
- Public CORS preflight against Render confirmed HTTP 204, allowed origin `http://localhost:8082` and credentials enabled. No tokens or cookies were sent by the probe.

## Deployment

The hosted API still needs this backend version deployed. Local frontend changes cannot update its cookie policy. After deployment, sign in once, reload, wait for/force access expiry in an isolated test, and confirm one refresh plus a successful replay. Keep the existing exact frontend Origin allowlist. Browsers without partitioned-cookie support may require same-site frontend/API hosting; do not weaken token storage or replay detection to work around browser policy.

Sources: [ASP.NET Core SameSite guidance](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0), [MDN partitioned cookies](https://developer.mozilla.org/en-US/docs/Web/Privacy/Guides/Third-party_cookies/Partitioned_cookies).
