using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Umayor.Dynamics.DeletePoc.Models;
using Umayor.Dynamics.DeletePoc.Services;
using Umayor.Dynamics.DeletePoc.Shared.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, builder) =>
    {
        builder.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Cargar AppSettings
        var settings = new AppSettings();
        context.Configuration.Bind(settings);
        services.AddSingleton(settings);

        // Registrar Servicios Compartidos
        services.AddSingleton<LogService>();
        services.AddSingleton<DataverseConnectionFactory>();
        services.AddSingleton<BlobStorageBackupService>();
    })
    .Build();

host.Run();
