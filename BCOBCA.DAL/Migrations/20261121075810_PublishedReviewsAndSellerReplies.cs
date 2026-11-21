using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class PublishedReviewsAndSellerReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RepliedAt",
                table: "ProductReviews",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"ProductReviews\" SET \"Status\" = 1, \"Revision\" = gen_random_uuid() WHERE \"Status\" = 0");

            migrationBuilder.AddColumn<string>(
                name: "ReplyContent",
                table: "ProductReviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepliedAt",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "ReplyContent",
                table: "ProductReviews");
        }
    }
}
