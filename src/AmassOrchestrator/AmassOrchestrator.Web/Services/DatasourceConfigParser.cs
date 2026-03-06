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

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public static DataSourceConfig? Parse(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return null;

        return Deserializer.Deserialize<DataSourceConfig>(yaml);
    }

    public static string? Serialize(DataSourceConfig? config)
    {
        if (config is null)
            return null;

        return Serializer.Serialize(config);
    }
}
