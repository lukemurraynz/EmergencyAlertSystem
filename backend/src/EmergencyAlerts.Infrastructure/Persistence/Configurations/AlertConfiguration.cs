using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EmergencyAlerts.Domain.Entities;

namespace EmergencyAlerts.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Alert entity.
/// </summary>
public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts", schema: "emergency_alerts");

        builder.HasKey(a => a.AlertId);
        builder.Property(a => a.AlertId)
            .HasColumnName("alert_id")
            .ValueGeneratedNever(); // Guid is generated in domain

        builder.Property(a => a.Headline)
            .HasColumnName("headline")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(a => a.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.ChannelType)
            .HasColumnName("channel_type")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasDefaultValue(AlertStatus.PendingApproval)
            .IsRequired();

        builder.Property(a => a.LanguageCode)
            .HasColumnName("language_code")
            .HasMaxLength(5)
            .HasDefaultValue("en-GB")
            .IsRequired();

        builder.Property(a => a.DeliveryStatus)
            .HasColumnName("delivery_status")
            .HasConversion<string>()
            .HasDefaultValue(DeliveryStatus.Pending)
            .IsRequired();

        builder.Property(a => a.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("timestamptz");

        builder.Property(a => a.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(a => a.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(255)
            .IsRequired();

        // Relationships
        builder.HasMany(a => a.Areas)
            .WithOne(a => a.Alert)
            .HasForeignKey(a => a.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ApprovalRecord)
            .WithOne(ar => ar.Alert)
            .HasForeignKey<ApprovalRecord>(ar => ar.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.DeliveryStatus).HasFilter("delivery_status = 'Pending'");
        builder.HasIndex(a => a.ExpiresAt);
    }
}
