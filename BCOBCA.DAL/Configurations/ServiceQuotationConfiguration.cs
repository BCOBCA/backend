using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ServiceQuotationConfiguration : IEntityTypeConfiguration<ServiceQuotation>
{
    public void Configure(EntityTypeBuilder<ServiceQuotation> builder)
    {
        builder.ToTable("ServiceQuotations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestId).HasColumnType("uuid").IsRequired();
        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.QuoteCode).HasColumnType("varchar(40)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.ValidUntil).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
