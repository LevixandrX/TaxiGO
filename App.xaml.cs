using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                .SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddDbContext<TaxiGoContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("TaxiGoContext")));
            ServiceProvider = services.BuildServiceProvider();
        }
    }
}