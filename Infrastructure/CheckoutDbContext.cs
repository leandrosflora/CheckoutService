using CheckoutService.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckoutService.Infrastructure;

public sealed class CheckoutDbContext : DbContext
{
    public CheckoutDbContext(DbContextOptions<CheckoutDbContext> options)
        : base(options)
    {
    }

    public DbSet<CheckoutSession> Checkouts => Set<CheckoutSession>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ShippingPromiseProjection> ShippingPromiseProjections => Set<ShippingPromiseProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CheckoutSession>(entity =>
        {
            entity.ToTable("checkouts");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.ShippingPromiseId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.ShippingMode)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Carrier)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.IdempotencyKey)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.ConfirmationIdempotencyKey)
                .HasMaxLength(200);

            entity.Property(x => x.PaymentIntentId)
                .HasMaxLength(200);

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.HasIndex(x => x.ConfirmationIdempotencyKey)
                .IsUnique()
                .HasFilter("\"ConfirmationIdempotencyKey\" IS NOT NULL");

            entity.OwnsMany(x => x.Items, item =>
            {
                item.ToTable("checkout_items");
                item.WithOwner().HasForeignKey("CheckoutId");
                item.HasKey(x => x.Id);
                item.Property(x => x.UnitPrice).HasPrecision(18, 2);
            });

            entity.Property(x => x.ItemsTotal).HasPrecision(18, 2);
            entity.Property(x => x.ShippingCost).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventType)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Payload)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.HasIndex(x => x.ProcessedAt);
        });

        modelBuilder.Entity<ShippingPromiseProjection>(entity =>
        {
            entity.ToTable("shipping_promise_projections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PromiseId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Carrier).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Cost).HasPrecision(18, 2);
            entity.HasIndex(x => x.EventId).IsUnique();
            entity.HasIndex(x => new { x.CorrelationId, x.CheckoutId }).IsUnique();
        });
    }
}
