# Render backend deployment

- Use the backend repository Dockerfile and its default final target.
- Set `ASPNETCORE_ENVIRONMENT=Production` and supply the required application variables from `.env.example` through Render Environment. Never upload `.env`.
- Set `AllowedHosts` to the backend's public hostname, without `https://`, a port or `/api`. For this service: `AllowedHosts=backend-zqnb.onrender.com;localhost;127.0.0.1`. The `.env.example` value is for local development and rejects the public Render hostname with HTTP 400 `Invalid Hostname` if copied unchanged. Save the environment change and redeploy on Render; editing the local `.env` does not update Render.
- `Cors__Origins__0`, `Cors__Origins__1`, etc. must contain the frontend origins allowed to call the API, including scheme and port, without a trailing slash (for local Expo web: `http://localhost:8081` and `http://localhost:8082`). Add the deployed frontend's exact origin when publishing it. CORS origins are separate from backend `AllowedHosts`.
- Web session persistence across frontend/API sites requires the updated `AuthController`: HTTPS refresh cookies use `HttpOnly; Secure; SameSite=None; Partitioned`, with the existing configured refresh lifetime. Plain-HTTP development uses `SameSite=Lax`. Deploy the backend change before testing reload/expired-token recovery; frontend-only updates cannot change cookies issued by Render. Log in once after deployment to obtain the new cookie. Login, refresh, restore and logout all validate the existing exact Origin allowlist. Logout/invalid restore clear both the partitioned cookie and the legacy unpartitioned cookie. Native clients continue using SecureStore body credentials.
- Render supplies `PORT` (normally `10000`). The API binds to `http://0.0.0.0:$PORT`, taking precedence over local URL defaults. Without `PORT`, `ASPNETCORE_URLS` remains effective (Docker defaults to `http://+:8080`).
- Configure the health check path as `/api/health`. It returns 503 when PostgreSQL is unavailable; the database must be reachable and migrations applied separately.
- If PostgreSQL uses password authentication and TLS, add `GSS Encryption Mode=Disable` to `ConnectionStrings__Default` to suppress the optional Kerberos library warning. Keep the existing TLS settings. This warning is separate from the port scan timeout.

Expected startup log: `Now listening on: http://0.0.0.0:10000` (or the configured `PORT`).

After deploying, check `https://backend-zqnb.onrender.com/api/health`: HTTP 200 indicates that the API and database are reachable; HTTP 503 indicates a database connectivity problem; HTTP 400 `Invalid Hostname` means the backend host allowlist still rejects the request. The root URL `/` has no application endpoint and is not the health check.

References: [Render port binding](https://render.com/docs/web-services#port-binding), [Npgsql GSSAPI fallback](https://www.npgsql.org/doc/security.html).
