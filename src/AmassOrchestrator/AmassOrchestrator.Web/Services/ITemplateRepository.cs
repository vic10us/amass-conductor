using AmassOrchestrator.Web.Data;

namespace AmassOrchestrator.Web.Services;

public interface ITemplateRepository
{
    Task<List<SessionTemplateRecord>> GetAllAsync();
    Task<SessionTemplateRecord> CreateAsync(string name, string? description, string configJson);
    Task UpdateAsync(int id, string name, string? description, string configJson);
    Task DeleteAsync(int id);
}
