# Render backend deployment

- Use the backend repository Dockerfile and its default final target.
- Set `ASPNETCORE_ENVIRONMENT=Production` and supply the required application variables from `.env.example` through Render Environment. Never upload `.env`.
- Render supplies `PORT` (normally `10000`). The API binds to `http://0.0.0.0:$PORT`, taking precedence over local URL defaults. Without `PORT`, `ASPNETCORE_URLS` remains effective (Docker defaults to `http://+:8080`).
- Configure the health check path as `/api/health`. It returns 503 when PostgreSQL is unavailable; the database must be reachable and migrations applied separately.
- If PostgreSQL uses password authentication and TLS, add `GSS Encryption Mode=Disable` to `ConnectionStrings__Default` to suppress the optional Kerberos library warning. Keep the existing TLS settings. This warning is separate from the port scan timeout.

Expected startup log: `Now listening on: http://0.0.0.0:10000` (or the configured `PORT`).

References: [Render port binding](https://render.com/docs/web-services#port-binding), [Npgsql GSSAPI fallback](https://www.npgsql.org/doc/security.html).
