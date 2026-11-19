using JanitorialManagementDashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Overview;

internal static class OverviewTargetModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OverviewTargetRevision>(e =>
        {
            e.ToTable("overview_target_revisions");
            e.Property(x => x.EffectiveAt).HasColumnType("datetime(6)");
            e.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.Payload).HasColumnType("json").IsRequired();
            e.HasIndex(x => new { x.EffectiveAt, x.Id });
        });

        modelBuilder.Entity<OverviewTargetState>(e =>
        {
            e.ToTable("overview_target_state");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne(x => x.LatestRevision).WithMany().HasForeignKey(x => x.LatestRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasData(new OverviewTargetState { Id = 1 });
        });
    }
}
