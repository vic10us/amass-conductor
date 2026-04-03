using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmassOrchestrator.Web.Services;

public class TemplateRepository : ITemplateRepository
{
    private readonly IDbContextFactory<OrchestratorDbContext> _contextFactory;
    private readonly ILogger<TemplateRepository> _logger;
    private readonly string _instanceId;

    public TemplateRepository(
        IDbContextFactory<OrchestratorDbContext> contextFactory,
        ILogger<TemplateRepository> logger,
        IOptionsMonitor<OrchestratorOptions> options)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _instanceId = options.CurrentValue.InstanceId;
    }

    public async Task<List<SessionTemplateRecord>> GetAllAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.SessionTemplates
            .Where(t => t.InstanceId == _instanceId)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .ToListAsync();
    }

    public async Task<SessionTemplateRecord> CreateAsync(string name, string? description, string configJson)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var record = new SessionTemplateRecord
        {
            Name = name,
            Description = description,
            InstanceId = _instanceId,
            ConfigJson = configJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.SessionTemplates.Add(record);
        await db.SaveChangesAsync();
        _logger.LogDebug("Created session template {Name} (id={Id})", name, record.Id);
        return record;
    }

    public async Task UpdateAsync(int id, string name, string? description, string configJson)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.SessionTemplates.FirstOrDefaultAsync(t => t.Id == id && t.InstanceId == _instanceId);
        if (record == null) return;

        record.Name = name;
        record.Description = description;
        record.ConfigJson = configJson;
        record.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        _logger.LogDebug("Updated session template {Id} -> {Name}", id, name);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.SessionTemplates.FirstOrDefaultAsync(t => t.Id == id && t.InstanceId == _instanceId);
        if (record == null) return;

        db.SessionTemplates.Remove(record);
        await db.SaveChangesAsync();
        _logger.LogDebug("Deleted session template {Id}", id);
    }
}
