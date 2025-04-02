using System.Linq;
using System.Windows;
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
                System.Windows.MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            System.Windows.MessageBox.Show("Заказ успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}