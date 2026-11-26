START TRANSACTION;
UPDATE "RefreshTokens" SET "RevokedAt" = CURRENT_TIMESTAMP WHERE "RevokedAt" IS NULL;

DROP TABLE "AccountRoles";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261126025829_UseSingleAccountRole', '10.0.14');

COMMIT;
