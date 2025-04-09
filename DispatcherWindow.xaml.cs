using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
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
                Snackbar.MessageQueue?.Enqueue("Заказ успешно назначен!");
                LoadPendingOrders();
                LoadActiveOrders();
            }
            else
            {
                if (PendingOrdersGrid.SelectedItem == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Выберите заказ для назначения!");
                    ShakeElement(PendingOrdersGrid);
                }
                if (DriversCombo.SelectedItem == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Выберите водителя!");
                    ShakeElement(DriversCombo);
                }
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

        private void ShakeElement(UIElement element)
        {
            var shakeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 10,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            var transform = new TranslateTransform();
            element.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty, shakeAnimation);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}