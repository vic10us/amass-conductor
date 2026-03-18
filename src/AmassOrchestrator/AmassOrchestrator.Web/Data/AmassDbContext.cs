using System.Net;
using AmassOrchestrator.Web.Data.Amass;
using Microsoft.EntityFrameworkCore;

namespace AmassOrchestrator.Web.Data;

public class AmassDbContext : DbContext
{
    public AmassDbContext(DbContextOptions<AmassDbContext> options) : base(options) { }

    public DbSet<FqdnAsset> Fqdns => Set<FqdnAsset>();
    public DbSet<IpAddressAsset> IpAddresses => Set<IpAddressAsset>();
    public DbSet<NetblockAsset> Netblocks => Set<NetblockAsset>();
    public DbSet<AutonomousSystemAsset> AutonomousSystems => Set<AutonomousSystemAsset>();
    public DbSet<AutnumRecordAsset> AutnumRecords => Set<AutnumRecordAsset>();
    public DbSet<DomainRecordAsset> DomainRecords => Set<DomainRecordAsset>();
    public DbSet<TlsCertificateAsset> TlsCertificates => Set<TlsCertificateAsset>();
    public DbSet<ServiceAsset> Services => Set<ServiceAsset>();

    public override int SaveChanges() =>
        throw new InvalidOperationException("Amass database is read-only.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("Amass database is read-only.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Amass database is read-only.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Amass database is read-only.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FqdnAsset>(entity =>
        {
            entity.ToTable("fqdn");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Fqdn).HasColumnName("fqdn");
            entity.Property(e => e.ReverseFqdn).HasColumnName("reverse_fqdn");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<IpAddressAsset>(entity =>
        {
            entity.ToTable("ipaddress");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<NetblockAsset>(entity =>
        {
            entity.ToTable("netblock");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.NetblockCidr).HasColumnName("netblock_cidr");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<AutonomousSystemAsset>(entity =>
        {
            entity.ToTable("autonomoussystem");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Asn).HasColumnName("asn");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<AutnumRecordAsset>(entity =>
        {
            entity.ToTable("autnumrecord");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Handle).HasColumnName("handle");
            entity.Property(e => e.Asn).HasColumnName("asn");
            entity.Property(e => e.RecordName).HasColumnName("record_name");
            entity.Property(e => e.WhoisServer).HasColumnName("whois_server");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<DomainRecordAsset>(entity =>
        {
            entity.ToTable("domainrecord");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Domain).HasColumnName("domain");
            entity.Property(e => e.RecordName).HasColumnName("record_name");
            entity.Property(e => e.Punycode).HasColumnName("punycode");
            entity.Property(e => e.Extension).HasColumnName("extension");
            entity.Property(e => e.WhoisServer).HasColumnName("whois_server");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TlsCertificateAsset>(entity =>
        {
            entity.ToTable("tlscertificate");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SerialNumber).HasColumnName("serial_number");
            entity.Property(e => e.SubjectCommonName).HasColumnName("subject_common_name");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ServiceAsset>(entity =>
        {
            entity.ToTable("service");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UniqueId).HasColumnName("unique_id");
            entity.Property(e => e.ServiceType).HasColumnName("service_type");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        // Graph tables - keyless, used via Dapper only
        modelBuilder.Entity<AmassEntity>(entity =>
        {
            entity.ToTable("entity");
            entity.HasNoKey();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.EtypeId).HasColumnName("etype_id");
            entity.Property(e => e.NaturalKey).HasColumnName("natural_key");
            entity.Property(e => e.TableName).HasColumnName("table_name");
            entity.Property(e => e.RowId).HasColumnName("row_id");
        });

        modelBuilder.Entity<AmassEdge>(entity =>
        {
            entity.ToTable("edge");
            entity.HasNoKey();
            entity.Property(e => e.EdgeId).HasColumnName("edge_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.EtypeId).HasColumnName("etype_id");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.FromEntityId).HasColumnName("from_entity_id");
            entity.Property(e => e.ToEntityId).HasColumnName("to_entity_id");
        });
    }
}
