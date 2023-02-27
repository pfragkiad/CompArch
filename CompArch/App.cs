using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CompArch;

public static class App
{
    public static IHost GetApp(string[]? args =null )
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices( (context,services) =>
        {
            services
            .AddSingleton<BranchPredictor>()
            .AddSingleton<Tomasulo.Tomasulo>()
            .AddSingleton<ScenarioReader>()
            .AddSingleton<Scenarios>()

            .Configure<TomasuloOptions>(context.Configuration.GetSection(TomasuloOptions.Header))
            ;

        }).UseSerilog((context, configuration) =>
        {
            //NUGET: Serial.Settings.Configuration
            //for configuration see: https://github.com/serilog/serilog-settings-configuration
            //configuration.ReadFrom.Configuration(context.Configuration); //read Serilog options from appsettings.json

            configuration.MinimumLevel.Debug();
            configuration.WriteTo.Console(restrictedToMinimumLevel:Serilog.Events.LogEventLevel.Debug);
            //configuration.WriteTo.File(path: "logs/myapp.txt", rollingInterval: RollingInterval.Hour);
        }); //IHostBuilder;
        return host.Build();
    }

    public static BranchPredictor GetBranchPredictor(this IServiceProvider provider) =>
        provider.GetRequiredService<BranchPredictor>();

    public static Scenarios GetTomasuloScenarios(this IServiceProvider provider) =>
        provider.GetRequiredService<Scenarios>();

}
