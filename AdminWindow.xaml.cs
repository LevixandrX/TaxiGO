using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TaxiGO.Models;

namespace TaxiGO
{
    public partial class AdminWindow : System.Windows.Window
    {
        private readonly TaxiGoContext _context;

        public AdminWindow(string userName)
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetService<TaxiGoContext>()!;
            WelcomeText.Text = $"Добро пожаловать, {userName}!";

            LoadUsers();
            LoadTariffs();
            LoadPromoCodes();
        }

        private void ToggleUserActive_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User selectedUser)
            {
                selectedUser.IsActive = !selectedUser.IsActive;
                _context.SaveChanges();
                LoadUsers();
            }
        }

        private void AddTariff_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TariffName.Text) || !decimal.TryParse(BasePrice.Text, out decimal price))
            {
                System.Windows.MessageBox.Show("Введите корректные данные!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _context.Tariffs.Add(new Tariff { Name = TariffName.Text, BasePrice = price, PricePerKm = 10m, WaitingPenaltyPerMin = 5m });
            _context.SaveChanges();
            LoadTariffs();
        }

        private void AddPromoCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PromoCodeText.Text) || !int.TryParse(Discount.Text, out int discount))
            {
                System.Windows.MessageBox.Show("Введите корректные данные!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _context.PromoCodes.Add(new PromoCode { Code = PromoCodeText.Text, DiscountPercent = discount, ExpiryDate = DateTime.Now.AddMonths(1), IsActive = true });
            _context.SaveChanges();
            LoadPromoCodes();
        }

        private void LoadUsers()
        {
            UsersGrid.ItemsSource = _context.Users.ToList();
        }

        private void LoadTariffs()
        {
            TariffsGrid.ItemsSource = _context.Tariffs.ToList();
        }

        private void LoadPromoCodes()
        {
            PromoCodesGrid.ItemsSource = _context.PromoCodes.ToList();
        }
    }
}