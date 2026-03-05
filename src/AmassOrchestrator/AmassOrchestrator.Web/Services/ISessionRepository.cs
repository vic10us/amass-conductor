using AmassOrchestrator.Web.Data;
using AmassOrchestrator.Web.Models.Kubernetes;

namespace AmassOrchestrator.Web.Services;

public interface ISessionRepository
{
    Task CreateAsync(string enginePodName, string token, IEnumerable<string> domains, string configJson);
    Task UpsertAsync(string enginePodName, SessionInfo session);
    Task<List<SessionRecord>> GetAllAsync();
    Task<List<SessionRecord>> GetActiveAsync();
    Task<SessionRecord?> GetByTokenAsync(string token);
    Task DeleteAsync(string token);
    Task<string?> GetConfigJsonAsync(string token);
    Task AddLogAsync(string sessionToken, string enginePodName, string message);
    Task<List<LogRecord>> GetLogsAsync(string sessionToken, int? limit = null);
    Task<List<LogRecord>> GetRecentLogsAsync(int limit = 100);
    Task DeleteLogsAsync(string sessionToken);
}
