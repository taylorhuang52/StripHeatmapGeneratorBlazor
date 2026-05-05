using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using StripHeatmapGeneratorBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<StripHeatmapGeneratorBlazor.App>("#app");
builder.RootComponents.Add<Microsoft.AspNetCore.Components.Web.HeadOutlet>("head::after");

// Register services
builder.Services.AddScoped<CsvParserService>();
builder.Services.AddScoped<StripRendererService>();
builder.Services.AddScoped<XlsConverterService>();
builder.Services.AddScoped<YieldCalculatorService>();

await builder.Build().RunAsync();
