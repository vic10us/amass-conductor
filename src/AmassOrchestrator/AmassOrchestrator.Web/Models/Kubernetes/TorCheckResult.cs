using System.Globalization;

namespace AmassOrchestrator.Web.Models.Kubernetes;

public record TorCheckResult(
    bool IsTor,
    string PublicIP,
    DateTime CheckedAtUtc,
    bool IsError = false,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Builds a TorCheckResult from pod annotations set by the ip-check sidecar.
    /// Returns null if no annotations are present.
    /// </summary>
    public static TorCheckResult? FromAnnotations(IDictionary<string, string>? annotations)
    {
        if (annotations is null)
            return null;

        if (!annotations.TryGetValue("amass.io/ip-check-status", out var status))
            return null;

        annotations.TryGetValue("amass.io/public-ip", out var ip);
        annotations.TryGetValue("amass.io/is-tor", out var isTorStr);
        annotations.TryGetValue("amass.io/ip-check-time", out var timeStr);

        var checkedAt = DateTime.TryParse(timeStr, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTime.UtcNow;

        if (status.StartsWith("error", StringComparison.OrdinalIgnoreCase))
        {
            return new TorCheckResult(false, ip ?? "", checkedAt, true, status);
        }

        var isTor = string.Equals(isTorStr, "true", StringComparison.OrdinalIgnoreCase);
        return new TorCheckResult(isTor, ip ?? "", checkedAt);
    }
}
