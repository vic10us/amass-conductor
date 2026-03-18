using AmassOrchestrator.Web.Data;
using AmassOrchestrator.Web.Data.Amass;
using AmassOrchestrator.Web.Models.Amass;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace AmassOrchestrator.Web.Services;

public class AmassDataService : IAmassDataService
{
    private readonly IDbContextFactory<AmassDbContext> _contextFactory;
    private readonly ILogger<AmassDataService> _logger;

    public AmassDataService(IDbContextFactory<AmassDbContext> contextFactory, ILogger<AmassDataService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync();
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            await conn.ExecuteScalarAsync<int>("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Amass database is not available");
            return false;
        }
    }

    public async Task<Dictionary<string, long>> GetAssetCountsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        // Use pg_class reltuples for fast approximate counts (instant, no table scan)
        const string sql = """
            SELECT c.relname AS name, GREATEST(c.reltuples, 0)::bigint AS cnt
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relname IN ('fqdn','ipaddress','netblock','autonomoussystem','autnumrecord','domainrecord','tlscertificate','service')
            """;

        var rows = await conn.QueryAsync<(string name, long cnt)>(sql);

        var labelMap = new Dictionary<string, string>
        {
            ["fqdn"] = "FQDNs",
            ["ipaddress"] = "IP Addresses",
            ["netblock"] = "Netblocks",
            ["autonomoussystem"] = "ASNs",
            ["autnumrecord"] = "Autnum Records",
            ["domainrecord"] = "Domains",
            ["tlscertificate"] = "Certificates",
            ["service"] = "Services"
        };

        return rows.ToDictionary(r => labelMap.GetValueOrDefault(r.name, r.name), r => r.cnt);
    }

    /// <summary>
    /// Fast approximate row count from pg_class (no table scan).
    /// Used for unfiltered pagination totals on large tables.
    /// </summary>
    private static async Task<int> GetApproximateCountAsync(System.Data.Common.DbConnection conn, string tableName)
    {
        const string sql = "SELECT GREATEST(c.reltuples, 0)::bigint FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relname = @Table";
        return (int)await conn.ExecuteScalarAsync<long>(sql, new { Table = tableName });
    }

    public async Task<(List<FqdnAsset> Items, int TotalCount)> GetFqdnsAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.Fqdns.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(f => EF.Functions.ILike(f.Fqdn, $"%{search}%"));
            var count = await query.CountAsync();
            var items = await query.OrderByDescending(f => f.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
        else
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            var count = await GetApproximateCountAsync(conn, "fqdn");
            var items = await query.OrderByDescending(f => f.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    public async Task<(List<IpAddressAsset> Items, int TotalCount)> GetIpAddressesAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var countSql = "SELECT count(*) FROM ipaddress WHERE host(ip_address) ILIKE @Search";
            var dataSql = "SELECT id AS Id, host(ip_address) AS IpAddress, created_at AS CreatedAt, updated_at AS UpdatedAt FROM ipaddress WHERE host(ip_address) ILIKE @Search ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";

            var count = await conn.ExecuteScalarAsync<int>(countSql, new { Search = $"%{search}%" });
            var items = (await conn.QueryAsync<IpAddressDapperDto>(dataSql, new { Search = $"%{search}%", Skip = skip, Take = take }))
                .Select(d => new IpAddressAsset
                {
                    Id = d.Id,
                    IpAddress = System.Net.IPAddress.Parse(d.IpAddress),
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                }).ToList();
            return (items, count);
        }
        else
        {
            var count = await GetApproximateCountAsync(conn, "ipaddress");
            var items = await db.IpAddresses.OrderByDescending(i => i.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    public async Task<(List<NetblockAsset> Items, int TotalCount)> GetNetblocksAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var countSql = "SELECT count(*) FROM netblock WHERE netblock_cidr::text ILIKE @Search";
            var dataSql = "SELECT id AS Id, netblock_cidr::text AS NetblockCidr, created_at AS CreatedAt, updated_at AS UpdatedAt FROM netblock WHERE netblock_cidr::text ILIKE @Search ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";

            var count = await conn.ExecuteScalarAsync<int>(countSql, new { Search = $"%{search}%" });
            var items = (await conn.QueryAsync<NetblockDapperDto>(dataSql, new { Search = $"%{search}%", Skip = skip, Take = take }))
                .Select(d => new NetblockAsset
                {
                    Id = d.Id,
                    NetblockCidr = System.Net.IPNetwork.Parse(d.NetblockCidr),
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                }).ToList();
            return (items, count);
        }
        else
        {
            var count = await GetApproximateCountAsync(conn, "netblock");
            var items = await db.Netblocks.OrderByDescending(n => n.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    public async Task<(List<AutonomousSystemAsset> Items, int TotalCount)> GetAutonomousSystemsAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.AutonomousSystems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            if (int.TryParse(search, out var asnSearch))
                query = query.Where(a => a.Asn == asnSearch);
            else
                query = query.Where(a => EF.Functions.ILike(a.Asn.ToString(), $"%{search}%"));

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
        else
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            var count = await GetApproximateCountAsync(conn, "autonomoussystem");
            var items = await query.OrderByDescending(a => a.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    public async Task<(List<AutnumRecordAsset> Items, int TotalCount)> GetAutnumRecordsAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.AutnumRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => EF.Functions.ILike(a.Handle, $"%{search}%")
                || (a.RecordName != null && EF.Functions.ILike(a.RecordName, $"%{search}%")));
            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
        else
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            var count = await GetApproximateCountAsync(conn, "autnumrecord");
            var items = await query.OrderByDescending(a => a.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    public async Task<(List<DomainRecordAsset> Items, int TotalCount)> GetDomainRecordsAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.DomainRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => EF.Functions.ILike(d.Domain, $"%{search}%")
                || (d.RecordName != null && EF.Functions.ILike(d.RecordName, $"%{search}%")));
            var count = await query.CountAsync();
            var items = await query.OrderByDescending(d => d.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
        else
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            var count = await GetApproximateCountAsync(conn, "domainrecord");
            var items = await query.OrderByDescending(d => d.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    public async Task<(List<TlsCertificateAsset> Items, int TotalCount)> GetTlsCertificatesAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.TlsCertificates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => EF.Functions.ILike(t.SubjectCommonName, $"%{search}%")
                || EF.Functions.ILike(t.SerialNumber, $"%{search}%"));
            var count = await query.CountAsync();
            var items = await query.OrderByDescending(t => t.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
        else
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            var count = await GetApproximateCountAsync(conn, "tlscertificate");
            var items = await query.OrderByDescending(t => t.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    public async Task<(List<ServiceAsset> Items, int TotalCount)> GetServicesAsync(int skip, int take, string? search = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.Services.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => EF.Functions.ILike(s.ServiceType, $"%{search}%")
                || EF.Functions.ILike(s.UniqueId, $"%{search}%"));
            var count = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
        else
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            var count = await GetApproximateCountAsync(conn, "service");
            var items = await query.OrderByDescending(s => s.CreatedAt).Skip(skip).Take(take).ToListAsync();
            return (items, count);
        }
    }

    // Relationship queries via Dapper - traverse entity/edge graph

    public async Task<List<RelatedIpAddress>> GetRelatedIpsForFqdnAsync(long fqdnId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        const string sql = """
            SELECT ip.id AS Id, host(ip.ip_address) AS IpAddress, ip.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity src
            JOIN edge e ON e.from_entity_id = src.entity_id
            JOIN entity tgt ON tgt.entity_id = e.to_entity_id
            JOIN ipaddress ip ON ip.id = tgt.row_id AND tgt.table_name = 'ipaddress'
            WHERE src.table_name = 'fqdn' AND src.row_id = @FqdnId
            UNION
            SELECT ip.id AS Id, host(ip.ip_address) AS IpAddress, ip.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity tgt
            JOIN edge e ON e.to_entity_id = tgt.entity_id
            JOIN entity src ON src.entity_id = e.from_entity_id
            JOIN ipaddress ip ON ip.id = src.row_id AND src.table_name = 'ipaddress'
            WHERE tgt.table_name = 'fqdn' AND tgt.row_id = @FqdnId
            ORDER BY CreatedAt DESC
            LIMIT 100
            """;

        return (await conn.QueryAsync<RelatedIpAddress>(sql, new { FqdnId = fqdnId })).ToList();
    }

    public async Task<List<RelatedFqdn>> GetRelatedFqdnsForIpAsync(long ipId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        const string sql = """
            SELECT f.id AS Id, f.fqdn AS Fqdn, f.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity src
            JOIN edge e ON e.from_entity_id = src.entity_id
            JOIN entity tgt ON tgt.entity_id = e.to_entity_id
            JOIN fqdn f ON f.id = tgt.row_id AND tgt.table_name = 'fqdn'
            WHERE src.table_name = 'ipaddress' AND src.row_id = @IpId
            UNION
            SELECT f.id AS Id, f.fqdn AS Fqdn, f.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity tgt
            JOIN edge e ON e.to_entity_id = tgt.entity_id
            JOIN entity src ON src.entity_id = e.from_entity_id
            JOIN fqdn f ON f.id = src.row_id AND src.table_name = 'fqdn'
            WHERE tgt.table_name = 'ipaddress' AND tgt.row_id = @IpId
            ORDER BY CreatedAt DESC
            LIMIT 100
            """;

        return (await conn.QueryAsync<RelatedFqdn>(sql, new { IpId = ipId })).ToList();
    }

    public async Task<List<RelatedNetblock>> GetRelatedNetblocksForIpAsync(long ipId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        const string sql = """
            SELECT nb.id AS Id, nb.netblock_cidr::text AS Cidr, nb.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity src
            JOIN edge e ON e.from_entity_id = src.entity_id
            JOIN entity tgt ON tgt.entity_id = e.to_entity_id
            JOIN netblock nb ON nb.id = tgt.row_id AND tgt.table_name = 'netblock'
            WHERE src.table_name = 'ipaddress' AND src.row_id = @IpId
            UNION
            SELECT nb.id AS Id, nb.netblock_cidr::text AS Cidr, nb.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity tgt
            JOIN edge e ON e.to_entity_id = tgt.entity_id
            JOIN entity src ON src.entity_id = e.from_entity_id
            JOIN netblock nb ON nb.id = src.row_id AND src.table_name = 'netblock'
            WHERE tgt.table_name = 'ipaddress' AND tgt.row_id = @IpId
            ORDER BY CreatedAt DESC
            LIMIT 100
            """;

        return (await conn.QueryAsync<RelatedNetblock>(sql, new { IpId = ipId })).ToList();
    }

    public async Task<List<RelatedFqdn>> GetRelatedFqdnsForCertAsync(long certId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        const string sql = """
            SELECT f.id AS Id, f.fqdn AS Fqdn, f.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity src
            JOIN edge e ON e.from_entity_id = src.entity_id
            JOIN entity tgt ON tgt.entity_id = e.to_entity_id
            JOIN fqdn f ON f.id = tgt.row_id AND tgt.table_name = 'fqdn'
            WHERE src.table_name = 'tlscertificate' AND src.row_id = @CertId
            UNION
            SELECT f.id AS Id, f.fqdn AS Fqdn, f.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity tgt
            JOIN edge e ON e.to_entity_id = tgt.entity_id
            JOIN entity src ON src.entity_id = e.from_entity_id
            JOIN fqdn f ON f.id = src.row_id AND src.table_name = 'fqdn'
            WHERE tgt.table_name = 'tlscertificate' AND tgt.row_id = @CertId
            ORDER BY CreatedAt DESC
            LIMIT 100
            """;

        return (await conn.QueryAsync<RelatedFqdn>(sql, new { CertId = certId })).ToList();
    }

    public async Task<List<RelatedNetblock>> GetRelatedNetblocksForAsnAsync(long asnId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        const string sql = """
            SELECT nb.id AS Id, nb.netblock_cidr::text AS Cidr, nb.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity src
            JOIN edge e ON e.from_entity_id = src.entity_id
            JOIN entity tgt ON tgt.entity_id = e.to_entity_id
            JOIN netblock nb ON nb.id = tgt.row_id AND tgt.table_name = 'netblock'
            WHERE src.table_name = 'autonomoussystem' AND src.row_id = @AsnId
            UNION
            SELECT nb.id AS Id, nb.netblock_cidr::text AS Cidr, nb.created_at AS CreatedAt, e.label AS EdgeLabel
            FROM entity tgt
            JOIN edge e ON e.to_entity_id = tgt.entity_id
            JOIN entity src ON src.entity_id = e.from_entity_id
            JOIN netblock nb ON nb.id = src.row_id AND src.table_name = 'netblock'
            WHERE tgt.table_name = 'autonomoussystem' AND tgt.row_id = @AsnId
            ORDER BY CreatedAt DESC
            LIMIT 100
            """;

        return (await conn.QueryAsync<RelatedNetblock>(sql, new { AsnId = asnId })).ToList();
    }

    private class IpAddressDapperDto
    {
        public long Id { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private class NetblockDapperDto
    {
        public long Id { get; set; }
        public string NetblockCidr { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
