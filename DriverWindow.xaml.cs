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
    public partial class DriverWindow : System.Windows.Window
    {
        private readonly TaxiGoContext? _context;
        private readonly IServiceScope? _scope;
        private readonly int _userId;

        public DriverWindow(string userName, int userId, IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scope = scopeFactory.CreateScope();
            _context = _scope.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            _userId = userId;
            WelcomeText.Text = $"Добро пожаловать, {userName}!";

            LoadAvailableOrders();
            LoadMyOrders();
            LoadProfile();

            Closing += DriverWindow_Closing;
        }

        private void DriverWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _scope?.Dispose();
        }

        private void AcceptOrder_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableOrdersGrid.SelectedItem is Order selectedOrder)
            {
                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                selectedOrder.DriverId = _userId;
                selectedOrder.Status = "Accepted";
                _context.SaveChanges();
                Snackbar.MessageQueue?.Enqueue("Заказ успешно принят!");
                LoadAvailableOrders();
                LoadMyOrders();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Выберите заказ для принятия!");
                ShakeElement(AvailableOrdersGrid);
            }
        }

        private void CompleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (MyOrdersGrid.SelectedItem is Order selectedOrder)
            {
                if (selectedOrder.Status == "Completed")
                {
                    Snackbar.MessageQueue?.Enqueue("Этот заказ уже завершён!");
                    return;
                }

                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                selectedOrder.Status = "Completed";
                selectedOrder.OrderCompletionTime = System.DateTime.Now;
                _context.SaveChanges();
                Snackbar.MessageQueue?.Enqueue("Заказ успешно завершён!");
                LoadMyOrders();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Выберите заказ для завершения!");
                ShakeElement(MyOrdersGrid);
            }
        }

        private void LoadAvailableOrders()
        {
            if (_context?.Orders != null)
            {
                AvailableOrdersGrid.ItemsSource = _context.Orders.Where(o => o.Status == "Pending" && o.DriverId == null).ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки доступных заказов.");
            }
        }

        private void LoadMyOrders()
        {
            if (_context?.Orders != null)
            {
                MyOrdersGrid.ItemsSource = _context.Orders.Where(o => o.DriverId == _userId).ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки ваших заказов.");
            }
        }

        private void LoadProfile()
        {
            if (_context?.Users != null)
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == _userId);
                if (user == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Пользователь не найден.");
                    return;
                }

                var vehicle = _context.Vehicles.FirstOrDefault(v => v.DriverId == _userId);
                ProfileName.Text = $"Имя: {user.Name}";
                ProfilePhone.Text = $"Телефон: {user.Phone}";
                ProfileVehicle.Text = $"Машина: {vehicle?.Model ?? "Не назначена"}";
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки профиля.");
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