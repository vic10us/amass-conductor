using AmassOrchestrator.Web.Components;
using AmassOrchestrator.Web.Configuration;
using AmassOrchestrator.Web.Services;
using k8s;
using Radzen;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .WriteTo.File("logs/orchestrator-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

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

builder.Services.AddHttpClient(AmassEngineClient.HttpClientName)
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10))
    .AddStandardResilienceHandler();

builder.Services.AddHostedService<EngineMonitorService>();

var app = builder.Build();

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
