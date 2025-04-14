using System;
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
    public partial class AdminWindow : System.Windows.Window
    {
        private readonly TaxiGoContext? _context;
        private readonly IServiceScope? _scope;

        public AdminWindow(string userName, IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scope = scopeFactory.CreateScope();
            _context = _scope.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            WelcomeText.Text = $"Добро пожаловать, {userName}!";

            LoadUsers();
            LoadTariffs();
            LoadPromoCodes();

            Closing += AdminWindow_Closing;
        }

        private void AdminWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _scope?.Dispose();
        }

        private void ToggleUserActive_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User selectedUser)
            {
                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                selectedUser.IsActive = !selectedUser.IsActive;
                _context.SaveChanges();
                Snackbar.MessageQueue?.Enqueue($"Активность пользователя {selectedUser.Name} изменена!");
                LoadUsers();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Выберите пользователя!");
                ShakeElement(UsersGrid);
            }
        }

        private void AddTariff_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TariffName.Text) || !decimal.TryParse(BasePrice.Text, out decimal price))
            {
                Snackbar.MessageQueue?.Enqueue("Введите корректные данные!");
                if (string.IsNullOrEmpty(TariffName.Text))
                    ShakeElement(TariffName);
                if (!decimal.TryParse(BasePrice.Text, out _))
                    ShakeElement(BasePrice);
                return;
            }

            if (_context == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            _context.Tariffs.Add(new Tariff { Name = TariffName.Text, BasePrice = price, PricePerKm = 10m, WaitingPenaltyPerMin = 5m });
            _context.SaveChanges();
            Snackbar.MessageQueue?.Enqueue("Тариф успешно добавлен!");
            LoadTariffs();
            TariffName.Text = string.Empty;
            BasePrice.Text = string.Empty;
        }

        private void AddPromoCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PromoCodeText.Text) || !int.TryParse(Discount.Text, out int discount))
            {
                Snackbar.MessageQueue?.Enqueue("Введите корректные данные!");
                if (string.IsNullOrEmpty(PromoCodeText.Text))
                    ShakeElement(PromoCodeText);
                if (!int.TryParse(Discount.Text, out _))
                    ShakeElement(Discount);
                return;
            }

            if (_context == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            _context.PromoCodes.Add(new PromoCode { Code = PromoCodeText.Text, DiscountPercent = discount, ExpiryDate = DateTime.Now.AddMonths(1), IsActive = true });
            _context.SaveChanges();
            Snackbar.MessageQueue?.Enqueue("Промокод успешно добавлен!");
            LoadPromoCodes();
            PromoCodeText.Text = string.Empty;
            Discount.Text = string.Empty;
        }

        private void LoadUsers()
        {
            if (_context?.Users != null)
            {
                UsersGrid.ItemsSource = _context.Users.ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки списка пользователей.");
            }
        }

        private void LoadTariffs()
        {
            if (_context?.Tariffs != null)
            {
                TariffsGrid.ItemsSource = _context.Tariffs.ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки тарифов.");
            }
        }

        private void LoadPromoCodes()
        {
            if (_context?.PromoCodes != null)
            {
                PromoCodesGrid.ItemsSource = _context.PromoCodes.ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки промокодов.");
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