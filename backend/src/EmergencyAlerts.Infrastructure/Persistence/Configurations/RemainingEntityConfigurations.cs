using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EmergencyAlerts.Domain.Entities;

namespace EmergencyAlerts.Infrastructure.Persistence.Configurations;

public class ApprovalRecordConfiguration : IEntityTypeConfiguration<ApprovalRecord>
{
    public void Configure(EntityTypeBuilder<ApprovalRecord> builder)
    {
        builder.ToTable("approval_records", schema: "emergency_alerts");
        builder.HasKey(ar => ar.ApprovalId);

        builder.Property(ar => ar.ApprovalId).HasColumnName("approval_id").ValueGeneratedNever();
        builder.Property(ar => ar.AlertId).HasColumnName("alert_id").IsRequired();
        builder.Property(ar => ar.ApproverId).HasColumnName("approver_id").HasMaxLength(255).IsRequired();
        builder.Property(ar => ar.Decision).HasColumnName("decision").HasConversion<string>().IsRequired();
        builder.Property(ar => ar.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(ar => ar.DecidedAt).HasColumnName("decided_at").HasColumnType("timestamptz").HasDefaultValueSql("NOW()").IsRequired();

        // Unique constraint: one approval per alert
        builder.HasIndex(ar => ar.AlertId).IsUnique();
    }
}

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("delivery_attempts", schema: "emergency_alerts");
        builder.HasKey(da => da.AttemptId);

        builder.Property(da => da.AttemptId).HasColumnName("attempt_id").ValueGeneratedNever();
        builder.Property(da => da.AlertId).HasColumnName("alert_id").IsRequired();
        builder.Property(da => da.RecipientId).HasColumnName("recipient_id").IsRequired();
        builder.Property(da => da.AttemptNumber).HasColumnName("attempt_number").HasDefaultValue(1).IsRequired();
        builder.Property(da => da.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(AttemptStatus.Pending).IsRequired();
        builder.Property(da => da.FailureReason).HasColumnName("failure_reason");
        builder.Property(da => da.AcsOperationId).HasColumnName("acs_operation_id").HasMaxLength(100);
        builder.Property(da => da.AttemptedAt).HasColumnName("attempted_at").HasColumnType("timestamptz").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(da => da.AlertId);
        builder.HasIndex(da => da.Status).HasFilter("status = 'Pending'");
    }
}

public class RecipientConfiguration : IEntityTypeConfiguration<Recipient>
{
    public void Configure(EntityTypeBuilder<Recipient> builder)
    {
        builder.ToTable("recipients", schema: "emergency_alerts");
        builder.HasKey(r => r.RecipientId);

        builder.Property(r => r.RecipientId).HasColumnName("recipient_id").ValueGeneratedNever();
        builder.Property(r => r.EmailAddress).HasColumnName("email_address").HasMaxLength(255).IsRequired();
        builder.Property(r => r.DisplayName).HasColumnName("display_name").HasMaxLength(255);
        builder.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(r => r.EmailAddress).IsUnique();
    }
}

public class CorrelationEventConfiguration : IEntityTypeConfiguration<CorrelationEvent>
{
    public void Configure(EntityTypeBuilder<CorrelationEvent> builder)
    {
        builder.ToTable("correlation_events", schema: "emergency_alerts");
        builder.HasKey(ce => ce.EventId);

        builder.Property(ce => ce.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(ce => ce.PatternType).HasColumnName("pattern_type").HasConversion<string>().IsRequired();
        // Store AlertIds as JSON array (PostgreSQL supports this natively)
        builder.Property(ce => ce.AlertIds).HasColumnName("alert_ids").HasColumnType("uuid[]").IsRequired();
        builder.Property(ce => ce.DetectionTimestamp).HasColumnName("detection_timestamp").HasColumnType("timestamptz").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(ce => ce.ClusterSeverity).HasColumnName("cluster_severity").HasConversion<string>();
        builder.Property(ce => ce.RegionCode).HasColumnName("region_code").HasMaxLength(20);
        builder.Property(ce => ce.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(ce => ce.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamptz");

        builder.HasIndex(ce => ce.DetectionTimestamp);
    }
}

public class AdminBoundaryConfiguration : IEntityTypeConfiguration<AdminBoundary>
{
    public void Configure(EntityTypeBuilder<AdminBoundary> builder)
    {
        builder.ToTable("admin_boundaries", schema: "emergency_alerts");
        builder.HasKey(ab => ab.BoundaryId);

        builder.Property(ab => ab.BoundaryId).HasColumnName("boundary_id").ValueGeneratedNever();
        builder.Property(ab => ab.RegionCode).HasColumnName("region_code").HasMaxLength(20).IsRequired();
        builder.Property(ab => ab.RegionName).HasColumnName("region_name").HasMaxLength(255).IsRequired();
        builder.Property(ab => ab.BoundaryPolygonWkt).HasColumnName("boundary_polygon_wkt").IsRequired();
        builder.Property(ab => ab.AdminLevel).HasColumnName("admin_level").IsRequired();

        builder.HasIndex(ab => ab.RegionCode).IsUnique();
    }
}
