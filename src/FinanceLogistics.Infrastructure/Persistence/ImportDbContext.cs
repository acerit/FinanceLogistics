using FinanceLogistics.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace FinanceLogistics.Infrastructure.Persistence;

// SQL-FIRST example: the DDL scripts own the schema. No EnsureCreated or EF migrations.
// Add the other import entities/configurations while implementing the A-Z guide.
public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<PoUpdateLine> PoUpdateLines => Set<PoUpdateLine>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new PoUpdateLineConfiguration());
}

public sealed class PoUpdateLineConfiguration : IEntityTypeConfiguration<PoUpdateLine>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<PoUpdateLine> b)
    {
        b.ToTable("PoUpdateLine", "src");
        b.HasKey(x => x.SourceLineId);
        b.Property(x => x.SourceLineId).ValueGeneratedOnAdd();
        b.Property(x => x.SourceCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        b.Property(x => x.SourceCodeText).HasMaxLength(100);
        b.Property(x => x.ReceiptNumber).HasMaxLength(120);
        b.Property(x => x.PoNumber).HasMaxLength(120);
        b.Property(x => x.PoLineReference).HasMaxLength(50);
        b.Property(x => x.PoCurrency).HasMaxLength(10);
        b.Property(x => x.InvoiceNumber).HasMaxLength(200);
        b.Property(x => x.LineDescription).HasMaxLength(2000);
        b.Property(x => x.ReceiptQuantity).HasPrecision(28, 6);
        b.Property(x => x.PoUnitPrice).HasPrecision(28, 8);
        b.Property(x => x.PoRate).HasPrecision(28, 10);
        b.Property(x => x.ReceiptRate).HasPrecision(28, 10);
        b.Property(x => x.PurchaseFunctionalAmount).HasPrecision(28, 8);
        b.Property(x => x.InvoiceFunctionalAmount).HasPrecision(28, 8);
        b.Property(x => x.InvoiceAmount).HasPrecision(28, 8);
        b.HasIndex(x => x.RawRowId).IsUnique();
        b.HasIndex(x => x.BatchId);
        // Composite foreign keys are enforced by database/004. The other entities
        // are intentionally not included in this first mapping example.
    }
}
