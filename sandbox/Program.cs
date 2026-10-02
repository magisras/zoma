using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace TwentyTons.Sandbox
{
    /// <summary>
    /// Boots the .NET runtime in the browser. There is no Blazor UI at all: the page is plain HTML,
    /// the JavaScript in wwwroot/app.js owns the canvas and calls into <see cref="SandboxApi"/>.
    /// </summary>
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            await builder.Build().RunAsync();
        }
    }
}
