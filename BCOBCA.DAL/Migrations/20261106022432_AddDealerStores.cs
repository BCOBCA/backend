using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreType = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: false),
                    CoverUrl = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.CheckConstraint("CK_Stores_Status", "\"Status\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Stores_Type", "\"StoreType\" IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_Stores_Accounts_OwnerAccountId",
                        column: x => x.OwnerAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreBranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    ProvinceCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WardCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreBranches", x => x.Id);
                    table.CheckConstraint("CK_StoreBranches_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_StoreBranches_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_StoreBranches_Accounts_ManagerAccountId",
                        column: x => x.ManagerAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreBranches_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "StoreFollowers",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreFollowers", x => new { x.StoreId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_StoreFollowers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreFollowers_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowMessages = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPhone = table.Column<bool>(type: "boolean", nullable: false),
                    ShowEmail = table.Column<bool>(type: "boolean", nullable: false),
                    ShowBranches = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.StoreId);
                    table.ForeignKey(
                        name: "FK_StoreSettings_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "StoreVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationType = table.Column<short>(type: "smallint", nullable: false),
                    LegalName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RepresentativeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Documents = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    ReviewedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreVerifications", x => x.Id);
                    table.CheckConstraint("CK_StoreVerifications_Documents", "jsonb_typeof(\"Documents\") = 'array'");
                    table.CheckConstraint("CK_StoreVerifications_Status", "\"Status\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_StoreVerifications_Type", "\"VerificationType\" BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_StoreVerifications_Accounts_ReviewedByAccountId",
                        column: x => x.ReviewedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreVerifications_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreBranches_Main",
                table: "StoreBranches",
                column: "StoreId",
                unique: true,
                filter: "\"IsMain\"");

            migrationBuilder.CreateIndex(
                name: "IX_StoreBranches_ManagerAccountId",
                table: "StoreBranches",
                column: "ManagerAccountId",
                unique: true,
                filter: "\"ManagerAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoreBranches_StoreId_IsActive_ProvinceCode",
                table: "StoreBranches",
                columns: new[] { "StoreId", "IsActive", "ProvinceCode" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreFollowers_AccountId",
                table: "StoreFollowers",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_OwnerAccountId",
                table: "Stores",
                column: "OwnerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Slug",
                table: "Stores",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_StoreType_Status_CreatedAt",
                table: "Stores",
                columns: new[] { "StoreType", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreVerifications_ReviewedByAccountId",
                table: "StoreVerifications",
                column: "ReviewedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreVerifications_StoreId_CreatedAt",
                table: "StoreVerifications",
                columns: new[] { "StoreId", "CreatedAt" });

            // Keep personal-store invariants even when rows are written outside EF.
            migrationBuilder.Sql("""
                CREATE FUNCTION store_no_hard_delete() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Stores must be soft deleted using DeletedAt' USING ERRCODE = '23514';
                END $$;
                CREATE TRIGGER store_no_hard_delete BEFORE DELETE ON "Stores" FOR EACH ROW EXECUTE FUNCTION store_no_hard_delete();

                CREATE FUNCTION store_personal_branch_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE kind smallint;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        SELECT "StoreType" INTO kind FROM "Stores" WHERE "Id" = OLD."StoreId" FOR UPDATE;
                        IF kind = 0 THEN RAISE EXCEPTION 'Personal stores require their default branch' USING ERRCODE = '23514'; END IF;
                        RETURN OLD;
                    END IF;
                    SELECT "StoreType" INTO kind FROM "Stores" WHERE "Id" = NEW."StoreId" FOR UPDATE;
                    IF kind = 0 AND (NOT NEW."IsMain" OR EXISTS (SELECT 1 FROM "StoreBranches" WHERE "StoreId" = NEW."StoreId" AND "Id" <> NEW."Id")) THEN
                        RAISE EXCEPTION 'Personal stores have one main branch' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP = 'UPDATE' AND OLD."StoreId" <> NEW."StoreId" AND EXISTS (SELECT 1 FROM "Stores" WHERE "Id" = OLD."StoreId" AND "StoreType" = 0) THEN
                        RAISE EXCEPTION 'Cannot move the personal default branch' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER store_personal_branch_guard BEFORE INSERT OR UPDATE OR DELETE ON "StoreBranches" FOR EACH ROW EXECUTE FUNCTION store_personal_branch_guard();

                CREATE FUNCTION store_personal_default() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."StoreType" = 0 THEN
                        IF (SELECT count(*) FROM "StoreBranches" WHERE "StoreId" = NEW."Id") > 1 THEN
                            RAISE EXCEPTION 'Personal stores have one main branch' USING ERRCODE = '23514';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM "StoreBranches" WHERE "StoreId" = NEW."Id") THEN
                            INSERT INTO "StoreBranches" ("Id", "StoreId", "Name", "Phone", "Address", "ProvinceCode", "WardCode", "IsMain", "IsActive", "CreatedAt", "UpdatedAt")
                            VALUES (gen_random_uuid(), NEW."Id", NEW."Name", '', '', '', '', true, true, NEW."CreatedAt", NEW."UpdatedAt");
                        ELSE
                            UPDATE "StoreBranches" SET "IsMain" = true WHERE "StoreId" = NEW."Id" AND NOT "IsMain";
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER store_personal_default AFTER INSERT OR UPDATE OF "StoreType" ON "Stores" FOR EACH ROW EXECUTE FUNCTION store_personal_default();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS store_no_hard_delete ON \"Stores\"; DROP TRIGGER IF EXISTS store_personal_default ON \"Stores\"; DROP TRIGGER IF EXISTS store_personal_branch_guard ON \"StoreBranches\"; DROP FUNCTION IF EXISTS store_no_hard_delete(); DROP FUNCTION IF EXISTS store_personal_default(); DROP FUNCTION IF EXISTS store_personal_branch_guard();");
            migrationBuilder.DropTable(
                name: "StoreBranches");

            migrationBuilder.DropTable(
                name: "StoreFollowers");

            migrationBuilder.DropTable(
                name: "StoreSettings");

            migrationBuilder.DropTable(
                name: "StoreVerifications");

            migrationBuilder.DropTable(
                name: "Stores");
        }
    }
}
