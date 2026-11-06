using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class StoreVerificationConfiguration : IEntityTypeConfiguration<StoreVerification>
{
    public void Configure(EntityTypeBuilder<StoreVerification> b)
    {
        b.ToTable("StoreVerifications", t =>
        {
            t.HasCheckConstraint("CK_StoreVerifications_Type", "\"VerificationType\" BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_StoreVerifications_Status", "\"Status\" BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_StoreVerifications_Documents", "jsonb_typeof(\"Documents\") = 'array'");
        });
        b.HasKey(x => x.Id);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.ClientNoAction);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.ReviewedByAccountId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.LegalName).HasMaxLength(255).IsRequired();
        b.Property(x => x.TaxCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.RepresentativeName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Documents).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.RejectionReason).HasColumnType("text");
        b.HasIndex(x => new { x.StoreId, x.CreatedAt });
    }
}
