using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using TaxiGO.Models;
using TaxiGO.Services;

namespace TaxiGO
{
    public partial class App : Application
    {
        public static IConfiguration Configuration { get; private set; } = null!;
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Создаём новый scope для MainWindow
            using (var scope = ServiceProvider.CreateScope())
            {
                var mainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Регистрируем IConfiguration в контейнере
            services.AddSingleton<IConfiguration>(Configuration);

            string? connectionString = Configuration.GetConnectionString("TaxiGoContext");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'TaxiGoContext' not found in appsettings.json.");
            }

            services.AddDbContext<TaxiGoContext>(options =>
                options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

            // Регистрируем IServiceScopeFactory
            services.AddSingleton<IServiceScopeFactory>(provider => provider.GetRequiredService<IServiceProvider>().GetRequiredService<IServiceScopeFactory>());

            // Добавляем MemoryCache
            services.AddMemoryCache();

            // Регистрируем IGeocodingService
            services.AddSingleton<IGeocodingService, YandexGeocodingService>();

            // Регистрируем окна как Transient
            services.AddTransient<MainWindow>();
            services.AddTransient<ClientWindow>();
            services.AddTransient<DriverWindow>();
            services.AddTransient<DispatcherWindow>();
            services.AddTransient<AdminWindow>();
        }
    }
}