using Microsoft.JSInterop;

namespace AmassOrchestrator.Web.Services;

public class ThemeService
{
    private readonly IJSRuntime _js;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public bool IsDark { get; private set; } = true;

    public async Task InitializeAsync()
    {
        var saved = await _js.InvokeAsync<string?>("themeInterop.getTheme");
        if (saved is "light" or "dark")
        {
            IsDark = saved == "dark";
        }
        else
        {
            var system = await _js.InvokeAsync<string>("themeInterop.getSystemPreference");
            IsDark = system == "dark";
        }

        await _js.InvokeVoidAsync("themeInterop.setTheme", IsDark ? "dark" : "light");
    }

    public async Task ToggleAsync()
    {
        IsDark = !IsDark;
        await _js.InvokeVoidAsync("themeInterop.setTheme", IsDark ? "dark" : "light");
    }
}
