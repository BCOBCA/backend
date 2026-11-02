# Backend Docker

## Scope and acceptance

Run the existing .NET 10 API and PostgreSQL locally using Linux containers.
Keep application code and database schema unchanged. Reuse `.env`, exclude
secrets/build outputs from images, run the API as the image's non-root user,
and persist PostgreSQL data. API startup must wait for successful migrations.

## Plan

1. Add a cached multi-stage `Dockerfile` and `.dockerignore`; verify project
   paths, Release publishing and migration tool configuration.
2. Add `compose.yaml`: PostgreSQL health check, one-shot migration service,
   API on localhost:5000, named database volume. Verify Compose configuration.
3. Document environment setup, startup, health verification and provisioning
   in `README.md`; review the final diff for secrets and unrelated changes.

## Commands and boundaries

From `backend`: `docker compose config --quiet`, `docker compose up --build -d`,
`docker compose logs migrate api`, `curl http://localhost:5000/api/health`.
Use `docker compose down` to stop while retaining data.

Configuration-only change: validate configuration and container startup rather
than adding application unit tests. Existing tests live in `BCOBCA.Tests`.
Use existing naming and JSON-array Docker entrypoints, e.g.
`ENTRYPOINT ["dotnet", "BCOBCA.API.dll"]`.
Never copy `.env` into an image or modify existing local secrets. Database
schema/API changes and production deployment require separate scope.

## Verification

- Implemented Dockerfile targets `final` (non-root ASP.NET runtime) and `migrate`
  (SDK + pinned repository EF tool). Compose uses PostgreSQL 17 with persistent
  storage, health-gated migrations and localhost-only API publishing.
- Release publish verified with workspace .NET SDK 10.0.403 through single-node
  MSBuild: zero warnings/errors. Default parallel CLI publish exited without
  diagnostics on this workstation; single-node publishing succeeded.
- YAML parsed with PyYAML; dependency conditions, connection hostname/port,
  host binding, volume and Docker COPY paths checked. `git diff --check` passed.
- Reviewed image context exclusions, env overrides and migration configuration.
- Docker CLI is unavailable: image build, `docker compose config --quiet`,
  database migrations in containers and HTTP smoke test still need runtime
  verification on a Docker host. No production deployment performed.
