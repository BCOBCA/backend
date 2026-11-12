# Refresh rotation conflicts

- AuthService.RefreshAsync rejects reuse of a refresh token rotated within five seconds with 409 when a valid token still exists in its family. It returns no credentials and does not revoke the valid family for that short race. The window is fixed by RevokedAt and is not extended by retries.
- Older replay still revokes all active family tokens and returns 401. Explicit logout, expired tokens, unavailable accounts and password-reset revocation keep their existing checks. HttpOnly cookies, native token transport, access/refresh TTLs, rate limits and origin checks unchanged. No migration required.
- Frontend retries one 409 and renews access before expiry, with single-flight locking, lifecycle pauses, transient backoff and generation guards. Both sides must be deployed for full benefit.
- Validation: RefreshRotationTests reproduces the previous false logout (expected 409, got 401), then passes recent conflict/new-token continuation, older replay/family revocation and explicit logout. All 11 AuthCookieTests and core PostgresFlowTests pass too, using isolated scratch PostgreSQL databases. PostgresFlow moves reused token outside the five-second grace for its replay test; entity count updated from 68 to 67 for previous review-image migration.
- No database/schema/config changes, real logins, deployment or push.
