using System.Text.Json;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Data;
using AmassOrchestrator.Web.Models.Kubernetes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class SessionRepository : ISessionRepository
{
    private readonly IDbContextFactory<OrchestratorDbContext> _contextFactory;
    private readonly ILogger<SessionRepository> _logger;
    private readonly string _instanceId;

    public SessionRepository(
        IDbContextFactory<OrchestratorDbContext> contextFactory,
        ILogger<SessionRepository> logger,
        IOptionsMonitor<OrchestratorOptions> options)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _instanceId = options.CurrentValue.InstanceId;
    }

    public async Task CreateAsync(string enginePodName, string token, IEnumerable<string> domains, string configJson)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.Sessions.FirstOrDefaultAsync(s => s.Token == token);
        if (existing != null) return;

        db.Sessions.Add(new SessionRecord
        {
            Token = token,
            EnginePodName = enginePodName,
            InstanceId = _instanceId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Domains = JsonSerializer.Serialize(domains),
            ConfigJson = configJson
        });

        await db.SaveChangesAsync();
        _logger.LogDebug("Created session record for {Token} on {Engine}", token, enginePodName);
    }

    public async Task UpsertAsync(string enginePodName, SessionInfo session)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.Sessions.FirstOrDefaultAsync(s => s.Token == session.Token);

        if (record == null)
        {
            // Session exists on the engine but was not created by this cluster — ignore it
            return;
        }

        record.WorkItemsCompleted = session.WorkItemsCompleted;
        record.WorkItemsTotal = session.WorkItemsTotal;
        record.ConsecutiveCompletionPolls = session.ConsecutiveCompletionPolls;
        record.UpdatedAtUtc = DateTime.UtcNow;

        if (session.IsCompleted && !record.IsCompleted)
        {
            record.IsCompleted = true;
            record.CompletedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<SessionRecord>> GetAllAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Sessions
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<List<SessionRecord>> GetActiveAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Sessions
            .Where(s => !s.IsCompleted)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<SessionRecord?> GetByTokenAsync(string token)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Sessions.FirstOrDefaultAsync(s => s.Token == token);
    }

    public async Task DeleteAsync(string token)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.Sessions.FirstOrDefaultAsync(s => s.Token == token);
        if (record != null)
        {
            db.Sessions.Remove(record);
            await db.SaveChangesAsync();
            _logger.LogDebug("Deleted session record for {Token}", token);
        }
    }

    public async Task<string?> GetConfigJsonAsync(string token)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Sessions.Where(s => s.Token == token).Select(s => s.ConfigJson).FirstOrDefaultAsync();
    }

    public async Task AddLogAsync(string sessionToken, string enginePodName, string message)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Logs.Add(new LogRecord
        {
            SessionToken = sessionToken,
            EnginePodName = enginePodName,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<LogRecord>> GetLogsAsync(string sessionToken, int? limit = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.Logs.Where(l => l.SessionToken == sessionToken).OrderBy(l => l.TimestampUtc);

        if (limit.HasValue)
            return await query.Take(limit.Value).ToListAsync();

        return await query.ToListAsync();
    }

    public async Task<List<LogRecord>> GetRecentLogsAsync(int limit = 100)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Logs.OrderByDescending(l => l.TimestampUtc).Take(limit).OrderBy(l => l.TimestampUtc).ToListAsync();
    }

    public async Task<List<LogRecord>> GetFilteredLogsAsync(string? sessionToken = null, string? enginePodName = null, int limit = 500)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        IQueryable<LogRecord> query = db.Logs;

        if (!string.IsNullOrEmpty(sessionToken))
            query = query.Where(l => l.SessionToken == sessionToken);
        if (!string.IsNullOrEmpty(enginePodName))
            query = query.Where(l => l.EnginePodName == enginePodName);

        return await query.OrderByDescending(l => l.TimestampUtc).Take(limit).OrderBy(l => l.TimestampUtc).ToListAsync();
    }

    public async Task DeleteLogsAsync(string sessionToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var logs = await db.Logs.Where(l => l.SessionToken == sessionToken).ToListAsync();
        db.Logs.RemoveRange(logs);
        await db.SaveChangesAsync();
    }

    public async Task CreateFailedAsync(IEnumerable<string> domains, string configJson, string errorMessage)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var token = $"failed-{Guid.NewGuid():N}";
        db.Sessions.Add(new SessionRecord
        {
            Token = token,
            EnginePodName = string.Empty,
            InstanceId = _instanceId,
            IsFailed = true,
            ErrorMessage = errorMessage,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Domains = JsonSerializer.Serialize(domains),
            ConfigJson = configJson
        });
        await db.SaveChangesAsync();
        _logger.LogDebug("Created failed session record {Token}: {Error}", token, errorMessage);
    }

    public async Task MarkCancelledAsync(string token)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.Sessions.FirstOrDefaultAsync(s => s.Token == token);
        if (record != null)
        {
            record.IsCancelled = true;
            record.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            _logger.LogDebug("Marked session {Token} as cancelled", token);
        }
    }

    public async Task MarkFailedAsync(string token, string errorMessage)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.Sessions.FirstOrDefaultAsync(s => s.Token == token);
        if (record != null)
        {
            record.IsFailed = true;
            record.ErrorMessage = errorMessage;
            record.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            _logger.LogDebug("Marked session {Token} as failed: {Error}", token, errorMessage);
        }
    }

    public async Task<HashSet<string>> GetOwnedActiveTokensAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var tokens = await db.Sessions
            .Where(s => s.InstanceId == _instanceId && !s.IsCompleted && !s.IsFailed && !s.IsCancelled)
            .Select(s => s.Token)
            .ToListAsync();
        return tokens.ToHashSet();
    }

    public async Task UpsertHeartbeatAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.ConductorHeartbeats.FindAsync(_instanceId);
        if (existing == null)
            db.ConductorHeartbeats.Add(new Data.ConductorHeartbeat { InstanceId = _instanceId, LastHeartbeatUtc = DateTime.UtcNow });
        else
            existing.LastHeartbeatUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<int> ClaimAbandonedSessionsAsync(TimeSpan staleThreshold)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow - staleThreshold;

        var deadInstanceIds = await db.ConductorHeartbeats
            .Where(h => h.InstanceId != _instanceId && h.LastHeartbeatUtc < cutoff)
            .Select(h => h.InstanceId)
            .ToListAsync();

        if (deadInstanceIds.Count == 0) return 0;

        return await db.Sessions
            .Where(s => deadInstanceIds.Contains(s.InstanceId) && !s.IsCompleted && !s.IsFailed && !s.IsCancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.InstanceId, _instanceId)
                .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow));
    }
}
