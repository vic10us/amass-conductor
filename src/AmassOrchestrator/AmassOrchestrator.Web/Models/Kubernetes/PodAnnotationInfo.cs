using System.Globalization;

namespace AmassOrchestrator.Web.Models.Kubernetes;

public record PodAnnotationInfo(
    string? PublicIP,
    bool? IsTor,
    IpCheckStatus CheckStatus,
    DateTime? CheckedAtUtc,
    string? CheckError)
{
    /// <summary>
    /// Builds a PodAnnotationInfo from pod annotations set by the ip-check sidecar.
    /// Returns null if no amass.io annotations are present.
    /// </summary>
    public static PodAnnotationInfo? FromAnnotations(IDictionary<string, string>? annotations)
    {
        if (annotations is null)
            return null;

        var hasAny = false;
        foreach (var key in annotations.Keys)
        {
            if (key.StartsWith("amass.io/", StringComparison.Ordinal))
            {
                hasAny = true;
                break;
            }
        }

        if (!hasAny)
            return null;

        annotations.TryGetValue("amass.io/public-ip", out var ip);
        annotations.TryGetValue("amass.io/is-tor", out var isTorStr);
        annotations.TryGetValue("amass.io/ip-check-status", out var status);
        annotations.TryGetValue("amass.io/ip-check-time", out var timeStr);

        var checkedAt = DateTime.TryParse(timeStr, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : (DateTime?)null;

        if (status is not null && status.StartsWith("error", StringComparison.OrdinalIgnoreCase))
        {
            return new PodAnnotationInfo(ip, null, IpCheckStatus.Error, checkedAt, status);
        }

        bool? isTor = isTorStr is not null
            ? string.Equals(isTorStr, "true", StringComparison.OrdinalIgnoreCase)
            : null;

        var checkStatus = status is not null ? IpCheckStatus.Ok : IpCheckStatus.Unknown;

        return new PodAnnotationInfo(ip, isTor, checkStatus, checkedAt, null);
    }
}

public enum IpCheckStatus { Unknown, Ok, Error }
