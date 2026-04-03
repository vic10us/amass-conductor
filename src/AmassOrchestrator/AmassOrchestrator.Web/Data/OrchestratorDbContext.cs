using Microsoft.EntityFrameworkCore;

namespace AmassOrchestrator.Web.Data;

public class OrchestratorDbContext : DbContext
{
    public OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options) : base(options) { }

    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();
    public DbSet<LogRecord> Logs => Set<LogRecord>();
    public DbSet<SessionTemplateRecord> SessionTemplates => Set<SessionTemplateRecord>();
    public DbSet<ConductorHeartbeat> ConductorHeartbeats => Set<ConductorHeartbeat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionRecord>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.IsCompleted);
            entity.HasIndex(e => e.IsFailed);
            entity.HasIndex(e => e.IsCancelled);
            entity.HasIndex(e => e.InstanceId);
        });

        modelBuilder.Entity<LogRecord>(entity =>
        {
            entity.HasIndex(e => e.SessionToken);
            entity.HasIndex(e => e.TimestampUtc);
        });

        modelBuilder.Entity<SessionTemplateRecord>(entity =>
        {
            entity.HasIndex(e => e.InstanceId);
        });

        modelBuilder.Entity<ConductorHeartbeat>(entity =>
        {
            entity.HasKey(e => e.InstanceId);
        });
    }
}
