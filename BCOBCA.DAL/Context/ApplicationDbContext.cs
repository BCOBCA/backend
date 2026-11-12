using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.DAL.Context;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreBranch> StoreBranches => Set<StoreBranch>();
    public DbSet<StoreVerification> StoreVerifications => Set<StoreVerification>();
    public DbSet<StoreFollower> StoreFollowers => Set<StoreFollower>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();

    private void SoftDeleteStores()
    {
        foreach (var entry in ChangeTracker.Entries<Store>().Where(x => x.State == EntityState.Deleted))
        {
            entry.State = EntityState.Unchanged;
            entry.Entity.DeletedAt = DateTimeOffset.UtcNow;
            entry.Entity.UpdatedAt = entry.Entity.DeletedAt.Value;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SoftDeleteStores();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SoftDeleteStores();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();
    public DbSet<SellerSettlementAdjustment> SellerSettlementAdjustments => Set<SellerSettlementAdjustment>();
    public DbSet<SellerSettlementRecovery> SellerSettlementRecoveries => Set<SellerSettlementRecovery>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandMember> BrandMembers => Set<BrandMember>();
    public DbSet<BrandCategory> BrandCategories => Set<BrandCategory>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductFavorite> ProductFavorites => Set<ProductFavorite>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<MembershipTier> MembershipTiers => Set<MembershipTier>();
    public DbSet<UserMembership> UserMemberships => Set<UserMembership>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<DailyCheckIn> DailyCheckIns => Set<DailyCheckIn>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<UserVoucher> UserVouchers => Set<UserVoucher>();
    public DbSet<VoucherUsage> VoucherUsages => Set<VoucherUsage>();
    public DbSet<VoucherTarget> VoucherTargets => Set<VoucherTarget>();
    public DbSet<AffiliateProfile> AffiliateProfiles => Set<AffiliateProfile>();
    public DbSet<AffiliateReferral> AffiliateReferrals => Set<AffiliateReferral>();
    public DbSet<AffiliateCommission> AffiliateCommissions => Set<AffiliateCommission>();
    public DbSet<AffiliateWallet> AffiliateWallets => Set<AffiliateWallet>();
    public DbSet<AffiliateWalletTransaction> AffiliateWalletTransactions => Set<AffiliateWalletTransaction>();
    public DbSet<AffiliateWithdrawal> AffiliateWithdrawals => Set<AffiliateWithdrawal>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ServiceQuotation> ServiceQuotations => Set<ServiceQuotation>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<PlatformFeeConfiguration> PlatformFeeConfigurations => Set<PlatformFeeConfiguration>();
    public DbSet<AccountRole> AccountRoles => Set<AccountRole>();
    public DbSet<BrandCategoryMapping> BrandCategoryMappings => Set<BrandCategoryMapping>();
    public DbSet<SellerOrder> SellerOrders => Set<SellerOrder>();
    public DbSet<SellerSettlement> SellerSettlements => Set<SellerSettlement>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<AffiliateLink> AffiliateLinks => Set<AffiliateLink>();
    public DbSet<AffiliateVisit> AffiliateVisits => Set<AffiliateVisit>();
    public DbSet<AffiliateAttribution> AffiliateAttributions => Set<AffiliateAttribution>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CheckInRule> CheckInRules => Set<CheckInRule>();
    public DbSet<PointExpiration> PointExpirations => Set<PointExpiration>();
    public DbSet<OrderVoucherApplication> OrderVoucherApplications => Set<OrderVoucherApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.Entity<MembershipTier>().HasData(LoyaltyDefaults.MembershipLevels());
        modelBuilder.Entity<AffiliateRank>().HasData(LoyaltyDefaults.AffiliateRanks());
        modelBuilder.Entity<CheckInRule>().HasData(new CheckInRule
        {
            Id = Guid.Parse("a704b6f3-5664-4428-93d1-668522026002"), Name = "Điểm danh hằng ngày", RewardPoints = 1, IsActive = true
        });
    }
}
