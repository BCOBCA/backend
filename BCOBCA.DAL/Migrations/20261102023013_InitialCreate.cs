using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "varchar(255)", nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "varchar(30)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    PhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Banners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<string>(type: "varchar(50)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(180)", nullable: false),
                    IconUrl = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "varchar(150)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(180)", nullable: false),
                    IconUrl = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckInRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", nullable: false),
                    RewardPoints = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", nullable: false),
                    MinSpend = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MinPoints = table.Column<int>(type: "int", nullable: false),
                    Benefits = table.Column<string>(type: "jsonb", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipTiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DiscountType = table.Column<string>(type: "varchar(20)", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UsageLimit = table.Column<int>(type: "int", nullable: true),
                    PerUserLimit = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "varchar(30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRoles_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", nullable: false),
                    EntityType = table.Column<string>(type: "varchar(100)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(220)", nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: false),
                    CoverUrl = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "varchar(255)", nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Brands_Accounts_OwnerAccountId",
                        column: x => x.OwnerAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "varchar(20)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTokens_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    TargetType = table.Column<string>(type: "varchar(50)", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Accounts_RecipientAccountId",
                        column: x => x.RecipientAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "varchar(150)", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "varchar(150)", nullable: false),
                    Nickname = table.Column<string>(type: "varchar(100)", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: false),
                    CoverUrl = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<short>(type: "smallint", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferralCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    ReferredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Users_ReferredByUserId",
                        column: x => x.ReferredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "varchar(250)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(280)", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrandCategoryMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandCategoryMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandCategoryMappings_BrandCategories_BrandCategoryId",
                        column: x => x.BrandCategoryId,
                        principalTable: "BrandCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrandCategoryMappings_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrandMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "varchar(30)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandMembers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrandMembers_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(250)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(280)", nullable: false),
                    SKU = table.Column<string>(type: "varchar(100)", nullable: false),
                    ShortDescription = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ProductType = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(250)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(280)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: false),
                    PriceFrom = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PricingType = table.Column<string>(type: "varchar(20)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateCode = table.Column<string>(type: "varchar(40)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    TotalReferrals = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateReferrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferredUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCode = table.Column<string>(type: "varchar(40)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    QualifiedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateReferrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateReferrals_Users_ReferredUserId",
                        column: x => x.ReferredUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AffiliateReferrals_Users_ReferrerUserId",
                        column: x => x.ReferrerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Carts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyCheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RewardPoints = table.Column<int>(type: "int", nullable: false),
                    StreakDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCheckIns", x => x.Id);
                    table.CheckConstraint("CK_DailyCheckIns_Valid", "\"RewardPoints\" >= 0");
                    table.ForeignKey(
                        name: "FK_DailyCheckIns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "varchar(30)", nullable: false),
                    Subject = table.Column<string>(type: "varchar(200)", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    AdminResponse = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feedbacks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderCode = table.Column<string>(type: "varchar(40)", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "varchar(30)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShippingFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RecipientName = table.Column<string>(type: "varchar(150)", nullable: false),
                    RecipientPhone = table.Column<string>(type: "varchar(20)", nullable: false),
                    ShippingAddress = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientName = table.Column<string>(type: "varchar(150)", nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", nullable: false),
                    ProvinceCode = table.Column<string>(type: "varchar(20)", nullable: false),
                    WardCode = table.Column<string>(type: "varchar(20)", nullable: false),
                    AddressLine = table.Column<string>(type: "text", nullable: false),
                    AddressType = table.Column<short>(type: "smallint", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAddresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TierId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberCode = table.Column<string>(type: "varchar(50)", nullable: false),
                    AvailablePoints = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeEarnedPoints = table.Column<long>(type: "bigint", nullable: false),
                    TotalQualifiedSpend = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    TierExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemberships", x => x.Id);
                    table.CheckConstraint("CK_UserMemberships_Valid", "\"AvailablePoints\" >= 0");
                    table.ForeignKey(
                        name: "FK_UserMemberships_MembershipTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "MembershipTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserVouchers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserVouchers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductFavorites_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SKU = table.Column<string>(type: "varchar(100)", nullable: false),
                    VariantName = table.Column<string>(type: "varchar(150)", nullable: false),
                    Attributes = table.Column<string>(type: "jsonb", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.CheckConstraint("CK_ProductVariants_Valid", "\"StockQuantity\" >= 0 AND \"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"StockQuantity\" AND \"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoucherTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherTargets_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherTargets_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherTargets_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherTargets_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "varchar(150)", nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", nullable: false),
                    Email = table.Column<string>(type: "varchar(255)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateLinks_AffiliateProfiles_AffiliateProfileId",
                        column: x => x.AffiliateProfileId,
                        principalTable: "AffiliateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AffiliateLinks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PendingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateWallets", x => x.Id);
                    table.CheckConstraint("CK_AffiliateWallets_Valid", "\"AvailableBalance\" >= 0 AND \"PendingBalance\" >= 0");
                    table.ForeignKey(
                        name: "FK_AffiliateWallets_AffiliateProfiles_AffiliateProfileId",
                        column: x => x.AffiliateProfileId,
                        principalTable: "AffiliateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<string>(type: "varchar(30)", nullable: false),
                    NewStatus = table.Column<string>(type: "varchar(30)", nullable: false),
                    ChangedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistories_Accounts_ChangedByAccountId",
                        column: x => x.ChangedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistories_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderVoucherApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderVoucherApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderVoucherApplications_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderVoucherApplications_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethod = table.Column<string>(type: "varchar(30)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    ProviderReference = table.Column<string>(type: "varchar(150)", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellerOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellerOrders_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerOrders_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Carrier = table.Column<string>(type: "varchar(100)", nullable: false),
                    TrackingCode = table.Column<string>(type: "varchar(100)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoucherUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "varchar(30)", nullable: false),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfter = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceType = table.Column<string>(type: "varchar(30)", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointTransactions_UserMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "UserMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    IsSelected = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.Id);
                    table.CheckConstraint("CK_CartItems_Valid", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CartItems_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartItems_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(30)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservations", x => x.Id);
                    table.CheckConstraint("CK_InventoryReservations_Valid", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InventoryReservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "varchar(250)", nullable: false),
                    SKU = table.Column<string>(type: "varchar(100)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Valid", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0 AND \"TotalPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceQuotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteCode = table.Column<string>(type: "varchar(40)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceQuotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceQuotations_ServiceRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateAttributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateAttributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateAttributions_AffiliateLinks_AffiliateLinkId",
                        column: x => x.AffiliateLinkId,
                        principalTable: "AffiliateLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AffiliateAttributions_AffiliateProfiles_AffiliateProfileId",
                        column: x => x.AffiliateProfileId,
                        principalTable: "AffiliateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AffiliateAttributions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "varchar(30)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReferenceType = table.Column<string>(type: "varchar(30)", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateWalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateWalletTransactions_AffiliateWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "AffiliateWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BankName = table.Column<string>(type: "varchar(150)", nullable: false),
                    AccountNumber = table.Column<string>(type: "varchar(50)", nullable: false),
                    AccountHolder = table.Column<string>(type: "varchar(150)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    TransactionReference = table.Column<string>(type: "varchar(150)", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateWithdrawals", x => x.Id);
                    table.CheckConstraint("CK_AffiliateWithdrawals_Valid", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_AffiliateWithdrawals_AffiliateWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "AffiliateWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "varchar(30)", nullable: false),
                    TransactionCode = table.Column<string>(type: "varchar(150)", nullable: false),
                    EventId = table.Column<string>(type: "varchar(150)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    ResponseData = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    ProviderRefundCode = table.Column<string>(type: "varchar(150)", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellerSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellerSettlements_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerSettlements_SellerOrders_SellerOrderId",
                        column: x => x.SellerOrderId,
                        principalTable: "SellerOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointExpirations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointExpirations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointExpirations_PointTransactions_PointTransactionId",
                        column: x => x.PointTransactionId,
                        principalTable: "PointTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointExpirations_UserMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "UserMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_AffiliateProfiles_AffiliateProfileId",
                        column: x => x.AffiliateProfileId,
                        principalTable: "AffiliateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<short>(type: "smallint", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReviews", x => x.Id);
                    table.CheckConstraint("CK_ProductReviews_Valid", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_ProductReviews_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductReviews_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductReviewImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
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

            migrationBuilder.InsertData(
                table: "CheckInRules",
                columns: new[] { "Id", "IsActive", "Name", "RewardPoints" },
                values: new object[] { new Guid("a704b6f3-5664-4428-93d1-668522026002"), true, "Điểm danh hằng ngày", 1 });

            migrationBuilder.InsertData(
                table: "MembershipTiers",
                columns: new[] { "Id", "Benefits", "IsActive", "MinPoints", "MinSpend", "Name", "SortOrder" },
                values: new object[] { new Guid("a704b6f3-5664-4428-93d1-668522026001"), "{}", true, 0, 0m, "Thành viên", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRoles_AccountId_Role",
                table: "AccountRoles",
                columns: new[] { "AccountId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Phone",
                table: "Accounts",
                column: "Phone",
                unique: true,
                filter: "\"Phone\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateAttributions_AffiliateLinkId",
                table: "AffiliateAttributions",
                column: "AffiliateLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateAttributions_AffiliateProfileId",
                table: "AffiliateAttributions",
                column: "AffiliateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateAttributions_OrderId",
                table: "AffiliateAttributions",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_AffiliateProfileId_OrderItemId",
                table: "AffiliateCommissions",
                columns: new[] { "AffiliateProfileId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_OrderItemId",
                table: "AffiliateCommissions",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_AffiliateProfileId",
                table: "AffiliateLinks",
                column: "AffiliateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_Code",
                table: "AffiliateLinks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_ProductId",
                table: "AffiliateLinks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateProfiles_AffiliateCode",
                table: "AffiliateProfiles",
                column: "AffiliateCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateProfiles_UserId",
                table: "AffiliateProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateReferrals_ReferredUserId",
                table: "AffiliateReferrals",
                column: "ReferredUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateReferrals_ReferrerUserId",
                table: "AffiliateReferrals",
                column: "ReferrerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWallets_AffiliateProfileId",
                table: "AffiliateWallets",
                column: "AffiliateProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWalletTransactions_WalletId",
                table: "AffiliateWalletTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWithdrawals_WalletId",
                table: "AffiliateWithdrawals",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_BrandId",
                table: "Articles",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AccountId",
                table: "AuditLogs",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandCategoryMappings_BrandCategoryId",
                table: "BrandCategoryMappings",
                column: "BrandCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandCategoryMappings_BrandId_BrandCategoryId",
                table: "BrandCategoryMappings",
                columns: new[] { "BrandId", "BrandCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandMembers_AccountId",
                table: "BrandMembers",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandMembers_BrandId_AccountId",
                table: "BrandMembers",
                columns: new[] { "BrandId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_OwnerAccountId",
                table: "Brands",
                column: "OwnerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                table: "Brands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId",
                table: "CartItems",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductId",
                table: "CartItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_VariantId",
                table: "CartItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId",
                table: "Carts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckIns_UserId_CheckInDate",
                table: "DailyCheckIns",
                columns: new[] { "UserId", "CheckInDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_AccountId",
                table: "DeviceTokens",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_TokenHash",
                table: "DeviceTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_UserId",
                table: "Feedbacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_OrderId",
                table: "InventoryMovements",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_VariantId",
                table: "InventoryMovements",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId",
                table: "InventoryReservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_VariantId",
                table: "InventoryReservations",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientAccountId",
                table: "Notifications",
                column: "RecipientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_BrandId",
                table: "OrderItems",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_VariantId",
                table: "OrderItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderCode",
                table: "Orders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_ChangedByAccountId",
                table: "OrderStatusHistories",
                column: "ChangedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_OrderId",
                table: "OrderStatusHistories",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderVoucherApplications_OrderId_VoucherId",
                table: "OrderVoucherApplications",
                columns: new[] { "OrderId", "VoucherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderVoucherApplications_VoucherId",
                table: "OrderVoucherApplications",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentId",
                table: "PaymentTransactions",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_EventId",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointExpirations_MembershipId",
                table: "PointExpirations",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_PointExpirations_PointTransactionId",
                table: "PointExpirations",
                column: "PointTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_MembershipId",
                table: "PointTransactions",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductFavorites_ProductId",
                table: "ProductFavorites",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductFavorites_UserId_ProductId",
                table: "ProductFavorites",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviewImages_ProductReviewId",
                table: "ProductReviewImages",
                column: "ProductReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_OrderItemId",
                table: "ProductReviews",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_ProductId",
                table: "ProductReviews",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_UserId_OrderItemId",
                table: "ProductReviews",
                columns: new[] { "UserId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_SKU",
                table: "ProductVariants",
                column: "SKU",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_AccountId",
                table: "RefreshTokens",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_BrandId",
                table: "SellerOrders",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_OrderId_BrandId",
                table: "SellerOrders",
                columns: new[] { "OrderId", "BrandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlements_BrandId",
                table: "SellerSettlements",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlements_SellerOrderId",
                table: "SellerSettlements",
                column: "SellerOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceQuotations_RequestId",
                table: "ServiceQuotations",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ServiceId",
                table: "ServiceRequests",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_UserId",
                table: "ServiceRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_BrandId",
                table: "Services",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_UserId",
                table: "UserAddresses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemberships_MemberCode",
                table: "UserMemberships",
                column: "MemberCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMemberships_TierId",
                table: "UserMemberships",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemberships_UserId",
                table: "UserMemberships",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccountId",
                table: "Users",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferredByUserId",
                table: "Users",
                column: "ReferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_UserId_VoucherId",
                table: "UserVouchers",
                columns: new[] { "UserId", "VoucherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_VoucherId",
                table: "UserVouchers",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherTargets_BrandId",
                table: "VoucherTargets",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherTargets_CategoryId",
                table: "VoucherTargets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherTargets_ProductId",
                table: "VoucherTargets",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherTargets_VoucherId",
                table: "VoucherTargets",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_OrderId",
                table: "VoucherUsages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_UserId",
                table: "VoucherUsages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_VoucherId_OrderId",
                table: "VoucherUsages",
                columns: new[] { "VoucherId", "OrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRoles");

            migrationBuilder.DropTable(
                name: "AffiliateAttributions");

            migrationBuilder.DropTable(
                name: "AffiliateCommissions");

            migrationBuilder.DropTable(
                name: "AffiliateReferrals");

            migrationBuilder.DropTable(
                name: "AffiliateWalletTransactions");

            migrationBuilder.DropTable(
                name: "AffiliateWithdrawals");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Banners");

            migrationBuilder.DropTable(
                name: "BrandCategoryMappings");

            migrationBuilder.DropTable(
                name: "BrandMembers");

            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropTable(
                name: "CheckInRules");

            migrationBuilder.DropTable(
                name: "DailyCheckIns");

            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "InventoryReservations");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OrderStatusHistories");

            migrationBuilder.DropTable(
                name: "OrderVoucherApplications");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "PointExpirations");

            migrationBuilder.DropTable(
                name: "ProductFavorites");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "ProductReviewImages");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "SellerSettlements");

            migrationBuilder.DropTable(
                name: "ServiceQuotations");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "UserAddresses");

            migrationBuilder.DropTable(
                name: "UserVouchers");

            migrationBuilder.DropTable(
                name: "VoucherTargets");

            migrationBuilder.DropTable(
                name: "VoucherUsages");

            migrationBuilder.DropTable(
                name: "AffiliateLinks");

            migrationBuilder.DropTable(
                name: "AffiliateWallets");

            migrationBuilder.DropTable(
                name: "BrandCategories");

            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropTable(
                name: "PointTransactions");

            migrationBuilder.DropTable(
                name: "ProductReviews");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "SellerOrders");

            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "AffiliateProfiles");

            migrationBuilder.DropTable(
                name: "UserMemberships");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "MembershipTiers");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
