using AmassOrchestrator.Web.Data.Amass;
using AmassOrchestrator.Web.Models.Amass;

namespace AmassOrchestrator.Web.Services;

public interface IAmassDataService
{
    Task<bool> IsAvailableAsync();
    Task<Dictionary<string, long>> GetAssetCountsAsync();

    Task<(List<FqdnAsset> Items, int TotalCount)> GetFqdnsAsync(int skip, int take, string? search = null);
    Task<(List<IpAddressAsset> Items, int TotalCount)> GetIpAddressesAsync(int skip, int take, string? search = null);
    Task<(List<NetblockAsset> Items, int TotalCount)> GetNetblocksAsync(int skip, int take, string? search = null);
    Task<(List<AutonomousSystemAsset> Items, int TotalCount)> GetAutonomousSystemsAsync(int skip, int take, string? search = null);
    Task<(List<AutnumRecordAsset> Items, int TotalCount)> GetAutnumRecordsAsync(int skip, int take, string? search = null);
    Task<(List<DomainRecordAsset> Items, int TotalCount)> GetDomainRecordsAsync(int skip, int take, string? search = null);
    Task<(List<TlsCertificateAsset> Items, int TotalCount)> GetTlsCertificatesAsync(int skip, int take, string? search = null);
    Task<(List<ServiceAsset> Items, int TotalCount)> GetServicesAsync(int skip, int take, string? search = null);

    Task<List<RelatedIpAddress>> GetRelatedIpsForFqdnAsync(long fqdnId);
    Task<List<RelatedFqdn>> GetRelatedFqdnsForIpAsync(long ipId);
    Task<List<RelatedNetblock>> GetRelatedNetblocksForIpAsync(long ipId);
    Task<List<RelatedFqdn>> GetRelatedFqdnsForCertAsync(long certId);
    Task<List<RelatedNetblock>> GetRelatedNetblocksForAsnAsync(long asnId);
}
