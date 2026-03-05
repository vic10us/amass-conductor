using System.Text.Json.Serialization;

namespace AmassOrchestrator.Web.Models;

public class AmassConfig
{
    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    [JsonPropertyName("rigid_boundaries")]
    public bool? RigidBoundaries { get; set; }

    [JsonPropertyName("scope")]
    public AmassScope? Scope { get; set; }

    [JsonPropertyName("seed")]
    public AmassScope? Seed { get; set; }

    [JsonPropertyName("resolvers")]
    public List<string>? Resolvers { get; set; }

    [JsonPropertyName("trusted_resolvers")]
    public List<string>? TrustedResolvers { get; set; }

    [JsonPropertyName("options")]
    public AmassOptions? Options { get; set; }

    [JsonPropertyName("transformations")]
    public Dictionary<string, AmassTransformation>? Transformations { get; set; }
}

public class AmassOptions
{
    [JsonPropertyName("brute_force")]
    public BruteForceOptions? BruteForce { get; set; }

    [JsonPropertyName("alterations")]
    public AlterationsOptions? Alterations { get; set; }

    [JsonPropertyName("default_transform_values")]
    public AmassTransformation? DefaultTransformValues { get; set; }

    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }

    [JsonPropertyName("datasources")]
    public DataSourceConfig? Datasources { get; set; }
}

public class BruteForceOptions
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("wordlists")]
    public List<string>? Wordlists { get; set; }
}

public class AlterationsOptions
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("wordlists")]
    public List<string>? Wordlists { get; set; }
}

public class AmassScope
{
    [JsonPropertyName("domains")]
    public List<string>? Domains { get; set; }

    [JsonPropertyName("asns")]
    public List<int>? Asns { get; set; }

    [JsonPropertyName("cidrs")]
    public List<IpNet>? Cidrs { get; set; }

    [JsonPropertyName("ips")]
    public List<byte[]>? Ips { get; set; }

    [JsonPropertyName("ports")]
    public List<int>? Ports { get; set; }

    [JsonPropertyName("blacklist")]
    public List<string>? Blacklist { get; set; }
}

public class IpNet
{
    [JsonPropertyName("ip")]
    public byte[]? Ip { get; set; }

    [JsonPropertyName("mask")]
    public byte[]? Mask { get; set; }
}

public class AmassDatabase
{
    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("primary")]
    public bool? Primary { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("host")]
    public string? Host { get; set; }

    [JsonPropertyName("port")]
    public string? Port { get; set; }

    [JsonPropertyName("db_name")]
    public string? DbName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("options")]
    public string? Options { get; set; }
}

public class DataSourceConfig
{
    [JsonPropertyName("datasources")]
    public List<DataSource>? Datasources { get; set; }

    [JsonPropertyName("global_options")]
    public Dictionary<string, int>? GlobalOptions { get; set; }
}

public class DataSource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; }

    [JsonPropertyName("creds")]
    public Dictionary<string, DataSourceCredentials>? Creds { get; set; }
}

public class DataSourceCredentials
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("apikey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("secret")]
    public string? Secret { get; set; }
}

public class AmassTransformation
{
    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("confidence")]
    public int? Confidence { get; set; }

    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; }

    [JsonPropertyName("exclude")]
    public List<string>? Exclude { get; set; }
}
