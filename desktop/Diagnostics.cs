using System.Text.Json;
using MiniInfos.Providers;
using MiniInfos.Infrastructure;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Sources;

namespace MiniInfos.Desktop;

internal static class Diagnostics
{
    public static async Task<int> RunAsync()
    {
        try
        {
            await UsageSources.Shared.RefreshAsync();
            var usage = new List<object>();
            foreach (var provider in UsageProviders.Create())
            {
                var page = await provider.ReadAsync(DateTimeOffset.Now, false, false);
                usage.Add(new { provider.Id, Installed = provider.IsInstalled(), Today = page.Today.Total, Total = page.Total.Total });
                provider.Flush();
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Version = AppInfo.Version, OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), Data = AppPaths.Data, Providers = usage }));
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
