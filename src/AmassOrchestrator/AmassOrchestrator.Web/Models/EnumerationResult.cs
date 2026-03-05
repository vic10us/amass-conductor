namespace AmassOrchestrator.Web.Models;

public record EnumerationResult
{
    public bool Success { get; init; }
    public string? SessionToken { get; init; }
    public string? EnginePodName { get; init; }
    public string? ErrorMessage { get; init; }

    public static EnumerationResult Ok(string sessionToken, string enginePodName) =>
        new() { Success = true, SessionToken = sessionToken, EnginePodName = enginePodName };

    public static EnumerationResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
