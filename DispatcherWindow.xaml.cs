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
        private readonly TaxiGoContext? _context;
        private readonly IServiceScope? _scope;

        public DispatcherWindow(string userName, IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scope = scopeFactory.CreateScope();
            _context = _scope.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            WelcomeText.Text = $"Добро пожаловать, {userName}!";

            if (_context.Users != null)
            {
                DriversCombo.ItemsSource = _context.Users.Where(u => u.Role == "Driver" && u.IsActive).ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки списка водителей.");
            }

            LoadPendingOrders();
            LoadActiveOrders();

            Closing += DispatcherWindow_Closing;
        }

        private void DispatcherWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _scope?.Dispose();
        }

        private void AssignOrder_Click(object sender, RoutedEventArgs e)
        {
            if (PendingOrdersGrid.SelectedItem is Order selectedOrder && DriversCombo.SelectedItem is User selectedDriver)
            {
                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

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
            if (_context?.Orders != null)
            {
                PendingOrdersGrid.ItemsSource = _context.Orders.Where(o => o.Status == "Pending" && o.DriverId == null).ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки ожидающих заказов.");
            }
        }

        private void LoadActiveOrders()
        {
            if (_context?.Orders != null)
            {
                ActiveOrdersGrid.ItemsSource = _context.Orders.Where(o => o.Status != "Completed").ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки активных заказов.");
            }
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