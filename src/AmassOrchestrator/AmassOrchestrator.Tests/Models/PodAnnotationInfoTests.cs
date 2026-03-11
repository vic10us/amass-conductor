using AmassOrchestrator.Web.Models.Kubernetes;

namespace AmassOrchestrator.Tests.Models;

public class PodAnnotationInfoTests
{
    [Fact]
    public void FromAnnotations_NullAnnotations_ReturnsNull()
    {
        var result = PodAnnotationInfo.FromAnnotations(null);
        Assert.Null(result);
    }

    [Fact]
    public void FromAnnotations_NoAmassAnnotations_ReturnsNull()
    {
        var annotations = new Dictionary<string, string>
        {
            ["kubernetes.io/name"] = "test"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);
        Assert.Null(result);
    }

    [Fact]
    public void FromAnnotations_EmptyAnnotations_ReturnsNull()
    {
        var result = PodAnnotationInfo.FromAnnotations(new Dictionary<string, string>());
        Assert.Null(result);
    }

    [Fact]
    public void FromAnnotations_AllAnnotations_ParsesCorrectly()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "1.2.3.4",
            ["amass.io/is-tor"] = "true",
            ["amass.io/ip-check-status"] = "ok",
            ["amass.io/ip-check-time"] = "2026-03-11T12:00:00Z"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.Equal("1.2.3.4", result!.PublicIP);
        Assert.True(result.IsTor);
        Assert.Equal(IpCheckStatus.Ok, result.CheckStatus);
        Assert.NotNull(result.CheckedAtUtc);
        Assert.Null(result.CheckError);
    }

    [Fact]
    public void FromAnnotations_IsTorFalse_ParsesCorrectly()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "5.6.7.8",
            ["amass.io/is-tor"] = "false",
            ["amass.io/ip-check-status"] = "ok",
            ["amass.io/ip-check-time"] = "2026-03-11T12:00:00Z"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.False(result!.IsTor);
        Assert.Equal("5.6.7.8", result.PublicIP);
    }

    [Fact]
    public void FromAnnotations_PublicIpOnly_NoTorAnnotation_IsTorIsNull()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "5.6.7.8",
            ["amass.io/ip-check-status"] = "ok",
            ["amass.io/ip-check-time"] = "2026-03-11T12:00:00Z"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.Equal("5.6.7.8", result!.PublicIP);
        Assert.Null(result.IsTor);
        Assert.Equal(IpCheckStatus.Ok, result.CheckStatus);
    }

    [Fact]
    public void FromAnnotations_ErrorStatus_ReturnsError()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/ip-check-status"] = "error:timeout",
            ["amass.io/ip-check-time"] = "2026-03-11T12:00:00Z"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.Equal(IpCheckStatus.Error, result!.CheckStatus);
        Assert.Equal("error:timeout", result.CheckError);
        Assert.Null(result.IsTor);
        Assert.Null(result.PublicIP);
    }

    [Fact]
    public void FromAnnotations_ErrorStatus_WithPublicIp_PreservesIp()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/ip-check-status"] = "error:unreachable",
            ["amass.io/public-ip"] = "1.2.3.4"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.Equal(IpCheckStatus.Error, result!.CheckStatus);
        Assert.Equal("1.2.3.4", result.PublicIP);
    }

    [Fact]
    public void FromAnnotations_NoCheckStatus_ReturnsUnknown()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "1.2.3.4"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.Equal(IpCheckStatus.Unknown, result!.CheckStatus);
        Assert.Equal("1.2.3.4", result.PublicIP);
    }

    [Fact]
    public void FromAnnotations_NoCheckTime_CheckedAtUtcIsNull()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "1.2.3.4",
            ["amass.io/ip-check-status"] = "ok"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.Null(result!.CheckedAtUtc);
    }

    [Fact]
    public void FromAnnotations_InvalidTimeFormat_CheckedAtUtcIsNull()
    {
        var annotations = new Dictionary<string, string>
        {
            ["amass.io/public-ip"] = "1.2.3.4",
            ["amass.io/ip-check-status"] = "ok",
            ["amass.io/ip-check-time"] = "not-a-date"
        };

        var result = PodAnnotationInfo.FromAnnotations(annotations);

        Assert.NotNull(result);
        Assert.Null(result!.CheckedAtUtc);
    }
}
