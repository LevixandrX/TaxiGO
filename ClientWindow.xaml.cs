using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using TaxiGO.Models;

namespace TaxiGO
{
    public partial class ClientWindow : System.Windows.Window
    {
        private readonly TaxiGoContext _context;
        private readonly int _userId;

        public ClientWindow(string userName, int userId)
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetService<TaxiGoContext>()!;
            _userId = userId;
            WelcomeText.Text = $"Добро пожаловать, {userName}!";

            TariffCombo.ItemsSource = _context.Tariffs.ToList();
            LoadOrderHistory();
            LoadProfile();
        }

        private void OrderTaxi_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(StartPoint.Text) || string.IsNullOrEmpty(EndPoint.Text) || TariffCombo.SelectedItem == null)
            {
                Snackbar.MessageQueue?.Enqueue("Заполните все поля!");
                ShakeElement(StartPoint);
                ShakeElement(EndPoint);
                ShakeElement(TariffCombo);
                return;
            }

            var order = new Order
            {
                ClientId = _userId,
                StartPoint = StartPoint.Text,
                EndPoint = EndPoint.Text,
                TariffId = (TariffCombo.SelectedItem as Tariff)!.TariffId,
                Status = "Pending",
                OrderTime = System.DateTime.Now
            };
            _context.Orders.Add(order);
            _context.SaveChanges();
            Snackbar.MessageQueue?.Enqueue("Заказ успешно создан!");
            LoadOrderHistory();
        }

        private void LoadOrderHistory()
        {
            OrderHistoryGrid.ItemsSource = _context.Orders.Where(o => o.ClientId == _userId).ToList();
        }

        private void LoadProfile()
        {
            var user = _context.Users.First(u => u.UserId == _userId);
            ProfileName.Text = $"Имя: {user.Name}";
            ProfilePhone.Text = $"Телефон: {user.Phone}";
            ProfileEmail.Text = $"Email: {user.Email ?? "Не указан"}";
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