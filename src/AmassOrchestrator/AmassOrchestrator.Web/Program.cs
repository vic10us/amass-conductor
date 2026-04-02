using AmassOrchestrator.Web.Components;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Data;
using AmassOrchestrator.Web.Services;
using k8s;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Radzen;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var orchestratorConfig = builder.Configuration.GetSection(OrchestratorOptions.SectionName).Get<OrchestratorOptions>() ?? new OrchestratorOptions();

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
});

builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<AmassOrchestrator.Web.Services.ThemeService>();

builder.Services.Configure<OrchestratorOptions>(
    builder.Configuration.GetSection(OrchestratorOptions.SectionName));

// Kubernetes client: in-cluster first, then kubeconfig fallback
builder.Services.AddSingleton<IKubernetes>(_ =>
{
    KubernetesClientConfiguration config;
    try
    {
        config = KubernetesClientConfiguration.InClusterConfig();
    }
    catch
    {
        var kubeConfigPath = orchestratorConfig.KubeConfigPath;
        var kubeContext = orchestratorConfig.KubeContext;
        config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigPath, kubeContext);
    }
    return new Kubernetes(config);
});

builder.Services.AddSingleton<KubernetesContextService>();
builder.Services.AddSingleton<IKubernetesDiscoveryService, KubernetesDiscoveryService>();
builder.Services.AddSingleton<IAmassEngineClient, AmassEngineClient>();
builder.Services.AddSingleton<EngineStateStore>();
builder.Services.AddSingleton<IEnumerationService, EnumerationService>();

// SQLite database
var dbPath = orchestratorConfig.DatabasePath;
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir))
    Directory.CreateDirectory(dbDir);

builder.Services.AddDbContextFactory<OrchestratorDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton<ISessionRepository, SessionRepository>();

// Amass PostgreSQL database (optional, read-only)
var amassConnStr = orchestratorConfig.AmassDbConnectionString;
if (!string.IsNullOrEmpty(amassConnStr))
{
    // Convert URI format (postgres://user:pass@host:port/db) to key-value format
    if (amassConnStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        amassConnStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(amassConnStr);
        var userInfo = uri.UserInfo.Split(':');
        var csb = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        };
        amassConnStr = csb.ConnectionString;
    }

    builder.Services.AddDbContextFactory<AmassDbContext>(options =>
        options.UseNpgsql(amassConnStr)
               .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
    builder.Services.AddSingleton<IAmassDataService, AmassDataService>();
}

builder.Services.AddSingleton<DefaultsLoaderService>();

builder.Services.AddHttpClient(AmassEngineClient.HttpClientName)
    .ConfigureHttpClient((sp, c) =>
    {
        var opts = sp.GetRequiredService<IOptions<OrchestratorOptions>>().Value;
        c.Timeout = TimeSpan.FromSeconds(opts.HttpClientTimeoutSeconds);
    })
    .AddStandardResilienceHandler();

builder.Services.AddHostedService<EngineMonitorService>();

builder.Services.AddSingleton<LogAggregatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogAggregatorService>());

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OrchestratorDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();

    // Safe schema migration for existing databases
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sessions ADD COLUMN IsFailed INTEGER NOT NULL DEFAULT 0"); } catch { /* column already exists */ }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sessions ADD COLUMN ErrorMessage TEXT"); } catch { /* column already exists */ }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sessions ADD COLUMN IsCancelled INTEGER NOT NULL DEFAULT 0"); } catch { /* column already exists */ }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sessions ADD COLUMN InstanceId TEXT NOT NULL DEFAULT ''"); } catch { /* column already exists */ }

    // Backfill existing sessions to claim them for this instance
    var instanceId = orchestratorConfig.InstanceId;
    await db.Database.ExecuteSqlRawAsync(
        "UPDATE Sessions SET InstanceId = {0} WHERE InstanceId = ''", instanceId);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseSerilogRequestLogging();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
