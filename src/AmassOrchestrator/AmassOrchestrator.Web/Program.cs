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
        config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
    }
    return new Kubernetes(config);
});

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
