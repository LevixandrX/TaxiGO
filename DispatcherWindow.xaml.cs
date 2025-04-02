using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TaxiGO.Models;

namespace TaxiGO
{
    public partial class DispatcherWindow : System.Windows.Window
    {
        private readonly TaxiGoContext _context;

        public DispatcherWindow(string userName)
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetService<TaxiGoContext>()!;
            WelcomeText.Text = $"Добро пожаловать, {userName}!";

            DriversCombo.ItemsSource = _context.Users.Where(u => u.Role == "Driver" && u.IsActive).ToList();
            LoadPendingOrders();
            LoadActiveOrders();
        }

        private void AssignOrder_Click(object sender, RoutedEventArgs e)
        {
            if (PendingOrdersGrid.SelectedItem is Order selectedOrder && DriversCombo.SelectedItem is User selectedDriver)
            {
                selectedOrder.DriverId = selectedDriver.UserId;
                selectedOrder.Status = "Assigned";
                _context.SaveChanges();
                LoadPendingOrders();
                LoadActiveOrders();
            }
        }

        private void LoadPendingOrders()
        {
            PendingOrdersGrid.ItemsSource = _context.Orders.Where(o => o.Status == "Pending" && o.DriverId == null).ToList();
        }

        private void LoadActiveOrders()
        {
            ActiveOrdersGrid.ItemsSource = _context.Orders.Where(o => o.Status != "Completed").ToList();
        }
    }
}