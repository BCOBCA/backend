using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class MergeProductReviewImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "ProductReviews",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql("""
                UPDATE "ProductReviews" AS review
                SET "ImageUrls" = images.urls
                FROM (
                    SELECT "ProductReviewId", jsonb_agg("ImageUrl" ORDER BY "SortOrder", "Id") AS urls
                    FROM "ProductReviewImages"
                    GROUP BY "ProductReviewId"
                ) AS images
                WHERE review."Id" = images."ProductReviewId";
                """);

            migrationBuilder.DropTable(
                name: "ProductReviewImages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductReviews_ImageUrls_Array",
                table: "ProductReviews",
                sql: "jsonb_typeof(\"ImageUrls\") = 'array'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductReviewImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    ProductReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReviewImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductReviewImages_ProductReviews_ProductReviewId",
                        column: x => x.ProductReviewId,
                        principalTable: "ProductReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviewImages_ProductReviewId",
                table: "ProductReviewImages",
                column: "ProductReviewId");

            migrationBuilder.Sql("""
                INSERT INTO "ProductReviewImages" ("Id", "ProductReviewId", "ImageUrl", "SortOrder")
                SELECT gen_random_uuid(), review."Id", image.url, (image.ordinality - 1)::integer
                FROM "ProductReviews" AS review
                CROSS JOIN LATERAL jsonb_array_elements_text(review."ImageUrls") WITH ORDINALITY AS image(url, ordinality);
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductReviews_ImageUrls_Array",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "ProductReviews");
        }
    }
}
