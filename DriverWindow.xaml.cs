using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TaxiGO.Models;

namespace TaxiGO
{
    public partial class DriverWindow : System.Windows.Window
    {
        private readonly TaxiGoContext _context;
        private readonly int _userId;

        public DriverWindow(string userName, int userId)
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetService<TaxiGoContext>()!;
            _userId = userId;
            WelcomeText.Text = $"Добро пожаловать, {userName}!";

            LoadAvailableOrders();
            LoadMyOrders();
            LoadProfile();
        }

        private void AcceptOrder_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableOrdersGrid.SelectedItem is Order selectedOrder)
            {
                selectedOrder.DriverId = _userId;
                selectedOrder.Status = "Accepted";
                _context.SaveChanges();
                LoadAvailableOrders();
                LoadMyOrders();
            }
        }

        private void CompleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (MyOrdersGrid.SelectedItem is Order selectedOrder && selectedOrder.Status != "Completed")
            {
                selectedOrder.Status = "Completed";
                selectedOrder.OrderCompletionTime = System.DateTime.Now;
                _context.SaveChanges();
                LoadMyOrders();
            }
        }

        private void LoadAvailableOrders()
        {
            AvailableOrdersGrid.ItemsSource = _context.Orders.Where(o => o.Status == "Pending" && o.DriverId == null).ToList();
        }

        private void LoadMyOrders()
        {
            MyOrdersGrid.ItemsSource = _context.Orders.Where(o => o.DriverId == _userId).ToList();
        }

        private void LoadProfile()
        {
            var user = _context.Users.First(u => u.UserId == _userId);
            var vehicle = _context.Vehicles.FirstOrDefault(v => v.DriverId == _userId);
            ProfileName.Text = $"Имя: {user.Name}";
            ProfilePhone.Text = $"Телефон: {user.Phone}";
            ProfileVehicle.Text = $"Машина: {vehicle?.Model ?? "Не назначена"}";
        }
    }
}