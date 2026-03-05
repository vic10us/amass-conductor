using AmassOrchestrator.Web.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AmassOrchestrator.Web.Services;

public static class DatasourceConfigParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DataSourceConfig? Parse(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return null;

        return Deserializer.Deserialize<DataSourceConfig>(yaml);
    }
}
