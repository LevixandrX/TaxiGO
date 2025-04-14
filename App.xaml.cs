using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using TaxiGO.Models;

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
            string? connectionString = Configuration.GetConnectionString("TaxiGoContext");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'TaxiGoContext' not found in appsettings.json.");
            }

            services.AddDbContext<TaxiGoContext>(options =>
                options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

            // Регистрируем IServiceScopeFactory для создания новых scope
            services.AddSingleton<IServiceScopeFactory>(provider => provider.GetRequiredService<IServiceProvider>().GetRequiredService<IServiceScopeFactory>());

            // Регистрируем окна как Transient, чтобы каждый раз создавался новый экземпляр
            services.AddTransient<MainWindow>();
            services.AddTransient<ClientWindow>();
            services.AddTransient<DriverWindow>();
            services.AddTransient<DispatcherWindow>();
            services.AddTransient<AdminWindow>();
        }
    }
}