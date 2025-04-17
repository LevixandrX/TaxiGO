using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TaxiGO.Models;
using System.Windows.Data;
using System.Windows.Media.Effects;

namespace TaxiGO
{
    public partial class AdminWindow : Window
    {
        private readonly TaxiGoContext? _context;
        private readonly IServiceScope? _scope;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly int _userId;
        private Grid? mainGrid;
        private Border? mainBorder;
        private bool _isEditingUser = false;
        private bool _isEditingTariff = false;
        private bool _isEditingPromoCode = false;
        private bool _isTariffSelected = false;
        private bool _isPromoCodeSelected = false;
        private string _originalUserLogin = string.Empty;
        private string _originalUserName = string.Empty;
        private string _originalUserPhone = string.Empty;
        private string _originalUserEmail = string.Empty;
        private string _originalUserAddress = string.Empty;
        private string _originalUserRole = string.Empty;
        private string _originalTariffName = string.Empty;
        private decimal _originalBasePrice;
        private decimal _originalPricePerKm;
        private decimal _originalWaitingPenalty;
        private string _originalPromoCode = string.Empty;
        private int _originalDiscountPercent;
        private DateTime _originalExpiryDate;
        private string _currentSearchText = string.Empty;
        private string _currentRoleFilter = "Все";

        public AdminWindow(string userName, int userId, IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
            _scope = scopeFactory.CreateScope();
            _context = _scope.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            _userId = userId;
            WelcomeText.Text = "Пользователи";

            if (Snackbar != null)
            {
                Snackbar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
            }

            LoadUsers();
            LoadTariffs();
            LoadPromoCodes();

            Loaded += AdminWindow_Loaded;
            StateChanged += AdminWindow_StateChanged;
            SizeChanged += AdminWindow_SizeChanged;
            Closing += AdminWindow_Closing;
        }

        private void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mainGrid = FindName("MainGrid") as Grid;
            mainBorder = FindName("MainBorder") as Border;
            if (mainGrid == null || mainBorder == null)
            {
                MessageBox.Show("Не удалось найти MainGrid или MainBorder. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            UpdateLayoutForWindowState();
            UpdateMaximizeRestoreIcon();
        }

        private void AdminWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayoutForWindowState();
        }

        private void AdminWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateLayoutForWindowState();
            UpdateMaximizeRestoreIcon();
        }

        private void AdminWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _scope?.Dispose();
        }

        private void UpdateMaximizeRestoreIcon()
        {
            if (MaximizeRestoreIcon != null)
            {
                MaximizeRestoreIcon.Kind = WindowState == WindowState.Maximized ? PackIconKind.WindowRestore : PackIconKind.WindowMaximize;
            }
        }

        private void UpdateLayoutForWindowState()
        {
            if (mainBorder == null || mainGrid == null) return;

            double windowWidth = ActualWidth;
            double windowHeight = ActualHeight;

            var windowClip = mainGrid.Clip as RectangleGeometry;
            if (windowClip == null) return;

            windowClip.Rect = new Rect(0, 0, windowWidth, windowHeight);

            var sidebarGrid = mainBorder.Child as Grid;
            if (sidebarGrid == null) return;

            var sidebarBorder = sidebarGrid.Children?.OfType<Border>().FirstOrDefault();
            if (sidebarBorder == null) return;

            var sidebarContentGrid = sidebarBorder.Child as Grid;
            if (sidebarContentGrid == null) return;

            if (WindowState == WindowState.Maximized)
            {
                mainBorder.CornerRadius = new CornerRadius(0);
                mainBorder.Effect = null;
                windowClip.RadiusX = 0;
                windowClip.RadiusY = 0;

                SidebarColumn.Width = new GridLength(Math.Min(windowWidth * 0.1, 100));

                sidebarContentGrid.Visibility = Visibility.Visible;
                var titleTextBlock = sidebarContentGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (titleTextBlock != null)
                {
                    titleTextBlock.FontSize = 20;
                    titleTextBlock.Margin = new Thickness(0, 20, 0, 20);
                    titleTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                }

                var navGrid = sidebarContentGrid.Children.OfType<Grid>().FirstOrDefault();
                if (navGrid != null)
                {
                    navGrid.HorizontalAlignment = HorizontalAlignment.Center;
                    foreach (var button in navGrid.Children.OfType<Button>())
                    {
                        button.Width = 60;
                        button.Height = 60;
                        button.Margin = new Thickness(0, 10, 0, 10);
                        button.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                }

                var logoutButton = sidebarContentGrid.Children.OfType<Button>().LastOrDefault();
                if (logoutButton != null)
                {
                    logoutButton.Width = 60;
                    logoutButton.Height = 60;
                    logoutButton.Margin = new Thickness(0, 10, 0, 10);
                    logoutButton.HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
            else
            {
                mainBorder.CornerRadius = new CornerRadius(20);
                mainBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.5,
                    Color = Color.FromRgb(66, 165, 245)
                };

                windowClip.RadiusX = 20;
                windowClip.RadiusY = 20;

                SidebarColumn.Width = new GridLength(80);

                sidebarContentGrid.Visibility = Visibility.Visible;
                var titleTextBlock = sidebarContentGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (titleTextBlock != null)
                {
                    titleTextBlock.FontSize = 18;
                    titleTextBlock.Margin = new Thickness(0, 20, 0, 20);
                    titleTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                }

                var navGrid = sidebarContentGrid.Children.OfType<Grid>().FirstOrDefault();
                if (navGrid != null)
                {
                    navGrid.HorizontalAlignment = HorizontalAlignment.Center;
                    foreach (var button in navGrid.Children.OfType<Button>())
                    {
                        button.Width = 50;
                        button.Height = 50;
                        button.Margin = new Thickness(0, 5, 0, 5);
                        button.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                }

                var logoutButton = sidebarContentGrid.Children.OfType<Button>().LastOrDefault();
                if (logoutButton != null)
                {
                    logoutButton.Width = 50;
                    logoutButton.Height = 50;
                    logoutButton.Margin = new Thickness(0, 5, 0, 5);
                    logoutButton.HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                if (UsersPanel.Visibility == Visibility.Visible)
                {
                    ResetUsersTab(sender, e);
                }
                else if (TariffsPanel.Visibility == Visibility.Visible)
                {
                    ResetTariffsTab(sender, e);
                }
                else if (PromoCodesPanel.Visibility == Visibility.Visible)
                {
                    ResetPromoCodesTab(sender, e);
                }

                UsersNavButton.Tag = null;
                TariffsNavButton.Tag = null;
                PromoCodesNavButton.Tag = null;
                clickedButton.Tag = "Active";

                var slideOutAnimation = Resources["SlideOutLeftAnimation"] as Storyboard;
                var slideInAnimation = Resources["SlideInRightAnimation"] as Storyboard;

                if (slideOutAnimation == null || slideInAnimation == null) return;

                ResetPanelState(UsersPanel);
                ResetPanelState(TariffsPanel);
                ResetPanelState(PromoCodesPanel);

                if (clickedButton == UsersNavButton)
                {
                    SwitchPanel(TariffsPanel, PromoCodesPanel, UsersPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Пользователи";
                    Title = "TaxiGO - Пользователи";
                    LoadUsers();
                }
                else if (clickedButton == TariffsNavButton)
                {
                    SwitchPanel(UsersPanel, PromoCodesPanel, TariffsPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Тарифы";
                    Title = "TaxiGO - Тарифы";
                    LoadTariffs();
                }
                else if (clickedButton == PromoCodesNavButton)
                {
                    SwitchPanel(UsersPanel, TariffsPanel, PromoCodesPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Промокоды";
                    Title = "TaxiGO - Промокоды";
                    LoadPromoCodes();
                }
            }
        }

        private void ResetPanelState(Grid panel)
        {
            panel.Visibility = Visibility.Hidden;
            if (panel.RenderTransform == null || panel.RenderTransform.GetType() != typeof(TranslateTransform))
                panel.RenderTransform = new TranslateTransform();
            ((TranslateTransform)panel.RenderTransform).X = 0;
        }

        private void SwitchPanel(Grid fromPanel1, Grid fromPanel2, Grid toPanel, Storyboard slideOutAnimation, Storyboard slideInAnimation)
        {
            if (fromPanel1.Visibility == Visibility.Visible)
            {
                Storyboard.SetTarget(slideOutAnimation, fromPanel1);
                slideOutAnimation.Completed += (s, ev) =>
                {
                    fromPanel1.Visibility = Visibility.Hidden;
                    toPanel.Opacity = 0.2;
                    toPanel.Visibility = Visibility.Visible;
                    Storyboard.SetTarget(slideInAnimation, toPanel);
                    slideInAnimation.Begin();
                };
                slideOutAnimation.Begin();
            }
            else if (fromPanel2.Visibility == Visibility.Visible)
            {
                Storyboard.SetTarget(slideOutAnimation, fromPanel2);
                slideOutAnimation.Completed += (s, ev) =>
                {
                    fromPanel2.Visibility = Visibility.Hidden;
                    toPanel.Opacity = 0.2;
                    toPanel.Visibility = Visibility.Visible;
                    Storyboard.SetTarget(slideInAnimation, toPanel);
                    slideInAnimation.Begin();
                };
                slideOutAnimation.Begin();
            }
            else
            {
                toPanel.Opacity = 0.2;
                toPanel.Visibility = Visibility.Visible;
                Storyboard.SetTarget(slideInAnimation, toPanel);
                slideInAnimation.Begin();
            }
        }

        private void LogAction(string action)
        {
            if (_context == null) return;

            _context.Logs?.Add(new Log
            {
                UserId = _userId,
                Action = action,
                Timestamp = DateTime.Now
            });
            _context.SaveChanges();
        }

        private void ShakeElement(Control element)
        {
            var shakeAnimation = Resources["ShakeAnimation"] as Storyboard;
            if (shakeAnimation != null)
            {
                Storyboard.SetTarget(shakeAnimation, element);
                shakeAnimation.Begin();
            }
        }

        #region Users Tab
        private void LoadUsers()
        {
            if (_context?.Users != null)
            {
                try
                {
                    var query = _context.Users
                        .Include(u => u.OrderClients)
                        .Include(u => u.OrderDrivers)
                        .Include(u => u.Vehicle)
                        .AsQueryable();

                    if (_currentRoleFilter != "Все")
                    {
                        query = query.Where(u => u.Role == _currentRoleFilter);
                    }

                    if (!string.IsNullOrEmpty(_currentSearchText))
                    {
                        var searchText = _currentSearchText.ToLower();
                        query = query.AsEnumerable().Where(u =>
                            (u.Name != null && u.Name.ToLower().Contains(searchText)) ||
                            (u.Phone != null && u.Phone.Contains(searchText)) ||
                            (u.Email != null && u.Email.ToLower().Contains(searchText)))
                            .AsQueryable();
                    }

                    UsersGrid.ItemsSource = query.ToList();
                }
                catch (Exception ex)
                {
                    Snackbar?.MessageQueue?.Enqueue($"Ошибка загрузки пользователей: {ex.Message}");
                }
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Ошибка загрузки списка пользователей.");
            }
        }

        private void ToggleUserActive_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User selectedUser)
            {
                if (_context == null)
                {
                    Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                selectedUser.IsActive = !selectedUser.IsActive;
                _context.SaveChanges();
                LogAction($"Изменена активность пользователя {selectedUser.Name} (ID: {selectedUser.UserId}) на {selectedUser.IsActive}");
                Snackbar?.MessageQueue?.Enqueue($"Активность пользователя {selectedUser.Name} изменена!");
                LoadUsers();
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Выберите пользователя!");
                ShakeElement(UsersGrid);
            }
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User selectedUser)
            {
                if (!_isEditingUser)
                {
                    _isEditingUser = true;
                    _originalUserLogin = selectedUser.Login ?? string.Empty;
                    _originalUserName = selectedUser.Name ?? string.Empty;
                    _originalUserPhone = selectedUser.Phone ?? string.Empty;
                    _originalUserEmail = selectedUser.Email ?? string.Empty;
                    _originalUserAddress = selectedUser.Address ?? string.Empty;
                    _originalUserRole = selectedUser.Role ?? string.Empty;

                    UsersGrid.Columns.Clear();
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "ID", Binding = new Binding("UserId"), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Логин", Binding = new Binding("Login"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Имя", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Роль", Binding = new Binding("Role"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Телефон", Binding = new Binding("Phone"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Email", Binding = new Binding("Email"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Адрес", Binding = new Binding("Address"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Дата регистрации", Binding = new Binding("RegistrationDate") { StringFormat = "{0:dd.MM.yyyy}" }, IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Аватар", Binding = new Binding("AvatarPath"), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Транспорт", Binding = new Binding("Vehicle.Model") { Converter = new NullToTextConverter(), ConverterParameter = "Нет транспорта" }, IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                    UsersGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Статус", Binding = new Binding("IsActive") { Converter = new BooleanToTextConverter { TrueText = "Активен", FalseText = "Неактивен" } }, IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

                    UpdateUsersButtonVisibility(true);
                    Snackbar?.MessageQueue?.Enqueue("Редактирование пользователя включено.");
                }
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Выберите пользователя!");
                ShakeElement(UsersGrid);
            }
        }

        private void UpdateUsersButtonVisibility(bool isEditing)
        {
            EditUserButton.Visibility = _isEditingUser ? Visibility.Collapsed : (_isTariffSelected ? Visibility.Visible : Visibility.Collapsed);
            SaveUserButton.Visibility = _isEditingUser ? Visibility.Visible : Visibility.Collapsed;
            ResetPasswordButton.Visibility = _isEditingUser ? Visibility.Collapsed : (_isTariffSelected ? Visibility.Visible : Visibility.Collapsed);
            ToggleActiveButton.Visibility = _isEditingUser ? Visibility.Collapsed : (_isTariffSelected ? Visibility.Visible : Visibility.Collapsed);
            CancelButton.Visibility = _isEditingUser ? Visibility.Visible : Visibility.Collapsed;
            RefreshButton.Visibility = _isEditingUser ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SaveUserEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingUser)
            {
                if (UsersGrid.SelectedItem is User selectedUser)
                {
                    string? login = selectedUser.Login?.Trim();
                    string? name = selectedUser.Name?.Trim();
                    string? phone = selectedUser.Phone?.Trim();
                    string? email = selectedUser.Email?.Trim();
                    string? address = selectedUser.Address?.Trim();
                    string? role = selectedUser.Role?.Trim();

                    if (string.IsNullOrEmpty(login))
                    {
                        Snackbar?.MessageQueue?.Enqueue("Логин не может быть пустым!");
                        ShakeElement(UsersGrid);
                        return;
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        Snackbar?.MessageQueue?.Enqueue("Имя не может быть пустым!");
                        ShakeElement(UsersGrid);
                        return;
                    }

                    if (string.IsNullOrEmpty(phone) || !Regex.IsMatch(phone, @"^\+?\d{10,15}$"))
                    {
                        Snackbar?.MessageQueue?.Enqueue("Введите корректный номер телефона!");
                        ShakeElement(UsersGrid);
                        return;
                    }

                    if (!string.IsNullOrEmpty(email) && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        Snackbar?.MessageQueue?.Enqueue("Введите корректный email!");
                        ShakeElement(UsersGrid);
                        return;
                    }

                    if (string.IsNullOrEmpty(role) || !new[] { "Client", "Driver", "Dispatcher", "Admin" }.Contains(role))
                    {
                        Snackbar?.MessageQueue?.Enqueue("Роль должна быть: Client, Driver, Dispatcher или Admin!");
                        ShakeElement(UsersGrid);
                        return;
                    }

                    if (_context != null)
                    {
                        var user = _context.Users?.FirstOrDefault(u => u.UserId == selectedUser.UserId);
                        if (user != null)
                        {
                            user.Login = login;
                            user.Name = name;
                            user.Phone = phone;
                            user.Email = string.IsNullOrEmpty(email) ? null : email;
                            user.Address = string.IsNullOrEmpty(address) ? null : address;
                            user.Role = role;
                            _context.SaveChanges();

                            LogAction($"Отредактирован пользователь {user.Name} (ID: {user.UserId})");
                            Snackbar?.MessageQueue?.Enqueue("Данные пользователя обновлены!");

                            _isEditingUser = false;
                            LoadUsers();
                            UpdateUsersButtonVisibility(false);
                        }
                        else
                        {
                            Snackbar?.MessageQueue?.Enqueue("Ошибка: Пользователь не найден.");
                        }
                    }
                    else
                    {
                        Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    }
                }
            }
        }

        private void ResetUserPassword_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User selectedUser)
            {
                if (_context == null)
                {
                    Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                var resetPasswordWindow = new ResetPasswordWindow(_context, selectedUser)
                {
                    Owner = this
                };
                if (resetPasswordWindow.ShowDialog() == true)
                {
                    LogAction($"Сброшен пароль пользователя {selectedUser.Name} (ID: {selectedUser.UserId})");
                    Snackbar?.MessageQueue?.Enqueue("Пароль пользователя успешно сброшен!");
                }
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Выберите пользователя!");
                ShakeElement(UsersGrid);
            }
        }

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isTariffSelected = UsersGrid.SelectedItem != null;
            UpdateUsersButtonVisibility(_isEditingUser);
        }

        private void UserRoleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserRoleFilter.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content is string role)
            {
                _currentRoleFilter = role == "Клиент" ? "Client" :
                                    role == "Водитель" ? "Driver" :
                                    role == "Диспетчер" ? "Dispatcher" :
                                    role == "Админ" ? "Admin" : "Все";
                LoadUsers();
            }
        }

        private void UserSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchText = UserSearchBox.Text.Trim();
            LoadUsers();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            UserRoleFilter.SelectedIndex = 0; // "Все"
            UserSearchBox.Text = string.Empty;
            _currentRoleFilter = "Все";
            _currentSearchText = string.Empty;
            LoadUsers();
            Snackbar?.MessageQueue?.Enqueue("Фильтры очищены.");
        }

        private void RefreshUsers_Click(object sender, RoutedEventArgs e)
        {
            LoadUsers();
            Snackbar?.MessageQueue?.Enqueue("Список пользователей обновлен.");
        }

        #endregion

        #region Tariffs Tab
        private void LoadTariffs()
        {
            if (_context?.Tariffs != null)
            {
                TariffsGrid.ItemsSource = _context.Tariffs.ToList();
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Ошибка загрузки тарифов.");
            }
        }

        private void AddTariff_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TariffName.Text) ||
                !decimal.TryParse(BasePrice.Text, out decimal basePrice) ||
                !decimal.TryParse(PricePerKm.Text, out decimal pricePerKm) ||
                !decimal.TryParse(WaitingPenalty.Text, out decimal waitingPenalty))
            {
                Snackbar?.MessageQueue?.Enqueue("Введите корректные данные!");
                if (string.IsNullOrEmpty(TariffName.Text))
                    ShakeElement(TariffName);
                if (!decimal.TryParse(BasePrice.Text, out _))
                    ShakeElement(BasePrice);
                if (!decimal.TryParse(PricePerKm.Text, out _))
                    ShakeElement(PricePerKm);
                if (!decimal.TryParse(WaitingPenalty.Text, out _))
                    ShakeElement(WaitingPenalty);
                return;
            }

            if (_context == null)
            {
                Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            if (_context.Tariffs == null)
            {
                Snackbar?.MessageQueue?.Enqueue("Ошибка: коллекция тарифов недоступна.");
                return;
            }

            // Проверка на уникальность имени тарифа
            if (_context.Tariffs.Any(t => t.Name == TariffName.Text))
            {
                Snackbar?.MessageQueue?.Enqueue("Тариф с таким названием уже существует!");
                ShakeElement(TariffName);
                return;
            }

            try
            {
                var newTariff = new Tariff
                {
                    Name = TariffName.Text.Trim(),
                    BasePrice = basePrice,
                    PricePerKm = pricePerKm,
                    WaitingPenaltyPerMin = waitingPenalty
                };

                _context.Tariffs.Add(newTariff);
                _context.SaveChanges();
                LogAction($"Добавлен тариф {newTariff.Name} (ID: {newTariff.TariffId})");
                Snackbar?.MessageQueue?.Enqueue("Тариф успешно добавлен!");
                LoadTariffs();
                ClearTariffFields();
            }
            catch (DbUpdateException ex)
            {
                Snackbar?.MessageQueue?.Enqueue($"Ошибка при добавлении тарифа: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void EditTariff_Click(object sender, RoutedEventArgs e)
        {
            if (TariffsGrid.SelectedItem is Tariff selectedTariff)
            {
                if (!_isEditingTariff)
                {
                    _isEditingTariff = true;
                    _originalTariffName = selectedTariff.Name;
                    _originalBasePrice = selectedTariff.BasePrice;
                    _originalPricePerKm = selectedTariff.PricePerKm;
                    _originalWaitingPenalty = selectedTariff.WaitingPenaltyPerMin;

                    TariffName.Text = selectedTariff.Name;
                    BasePrice.Text = selectedTariff.BasePrice.ToString();
                    PricePerKm.Text = selectedTariff.PricePerKm.ToString();
                    WaitingPenalty.Text = selectedTariff.WaitingPenaltyPerMin.ToString();

                    UpdateTariffsButtonVisibility(true);
                    Snackbar?.MessageQueue?.Enqueue("Редактирование тарифа включено.");
                }
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Выберите тариф!");
                ShakeElement(TariffsGrid);
            }
        }

        private void UpdateTariffsButtonVisibility(bool isEditing)
        {
            EditTariffButton.Visibility = _isEditingTariff ? Visibility.Collapsed : (_isTariffSelected ? Visibility.Visible : Visibility.Collapsed);
            SaveTariffButton.Visibility = _isEditingTariff ? Visibility.Visible : Visibility.Collapsed;
            AddTariffButton.Visibility = _isEditingTariff ? Visibility.Collapsed : Visibility.Visible;
            CancelTariffButton.Visibility = _isEditingTariff ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TariffsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isTariffSelected = TariffsGrid.SelectedItem != null;
            UpdateTariffsButtonVisibility(_isEditingTariff);
        }

        private void SaveTariffEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingTariff)
            {
                if (TariffsGrid.SelectedItem is Tariff selectedTariff)
                {
                    if (!decimal.TryParse(BasePrice.Text, out decimal basePrice) ||
                        !decimal.TryParse(PricePerKm.Text, out decimal pricePerKm) ||
                        !decimal.TryParse(WaitingPenalty.Text, out decimal waitingPenalty))
                    {
                        Snackbar?.MessageQueue?.Enqueue("Введите корректные числовые данные!");
                        if (!decimal.TryParse(BasePrice.Text, out _))
                            ShakeElement(BasePrice);
                        if (!decimal.TryParse(PricePerKm.Text, out _))
                            ShakeElement(PricePerKm);
                        if (!decimal.TryParse(WaitingPenalty.Text, out _))
                            ShakeElement(WaitingPenalty);
                        return;
                    }

                    if (_context != null)
                    {
                        var tariff = _context.Tariffs?.FirstOrDefault(t => t.TariffId == selectedTariff.TariffId);
                        if (tariff != null)
                        {
                            tariff.BasePrice = basePrice;
                            tariff.PricePerKm = pricePerKm;
                            tariff.WaitingPenaltyPerMin = waitingPenalty;
                            try
                            {
                                _context.SaveChanges();
                                LogAction($"Отредактирован тариф {tariff.Name} (ID: {tariff.TariffId})");
                                Snackbar?.MessageQueue?.Enqueue("Тариф обновлен!");
                            }
                            catch (DbUpdateException ex)
                            {
                                Snackbar?.MessageQueue?.Enqueue($"Ошибка при сохранении тарифа: {ex.InnerException?.Message ?? ex.Message}");
                                return;
                            }

                            _isEditingTariff = false;
                            UpdateTariffsButtonVisibility(false);
                            LoadTariffs();
                            ClearTariffFields();
                        }
                        else
                        {
                            Snackbar?.MessageQueue?.Enqueue("Ошибка: Тариф не найден.");
                        }
                    }
                    else
                    {
                        Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    }
                }
            }
        }

        private void ClearTariffFields()
        {
            TariffName.Text = string.Empty;
            BasePrice.Text = string.Empty;
            PricePerKm.Text = string.Empty;
            WaitingPenalty.Text = string.Empty;
        }

        #endregion

        #region PromoCodes Tab
        private void LoadPromoCodes()
        {
            if (_context?.PromoCodes != null)
            {
                PromoCodesGrid.ItemsSource = _context.PromoCodes.ToList();
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Ошибка загрузки промокодов.");
            }
        }

        private void AddPromoCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PromoCodeText.Text) ||
                !int.TryParse(Discount.Text, out int discount) ||
                !ExpiryDate.SelectedDate.HasValue)
            {
                Snackbar?.MessageQueue?.Enqueue("Введите корректные данные!");
                if (string.IsNullOrEmpty(PromoCodeText.Text))
                    ShakeElement(PromoCodeText);
                if (!int.TryParse(Discount.Text, out _))
                    ShakeElement(Discount);
                if (!ExpiryDate.SelectedDate.HasValue)
                    ShakeElement(ExpiryDate);
                return;
            }

            if (_context == null)
            {
                Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            if (_context.PromoCodes == null)
            {
                Snackbar?.MessageQueue?.Enqueue("Ошибка: коллекция промокодов недоступна.");
                return;
            }

            // Проверка на уникальность промокода
            if (_context.PromoCodes.Any(p => p.Code == PromoCodeText.Text.ToUpper()))
            {
                Snackbar?.MessageQueue?.Enqueue("Промокод с таким кодом уже существует!");
                ShakeElement(PromoCodeText);
                return;
            }

            try
            {
                var newPromoCode = new PromoCode
                {
                    Code = PromoCodeText.Text.ToUpper().Trim(),
                    DiscountPercent = discount,
                    ExpiryDate = ExpiryDate.SelectedDate.Value,
                    IsActive = true
                };

                _context.PromoCodes.Add(newPromoCode);
                _context.SaveChanges();
                LogAction($"Добавлен промокод {newPromoCode.Code} (ID: {newPromoCode.PromoCodeId})");
                Snackbar?.MessageQueue?.Enqueue("Промокод успешно добавлен!");
                LoadPromoCodes();
                ClearPromoCodeFields();
            }
            catch (DbUpdateException ex)
            {
                Snackbar?.MessageQueue?.Enqueue($"Ошибка при добавлении промокода: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void EditPromoCode_Click(object sender, RoutedEventArgs e)
        {
            if (PromoCodesGrid.SelectedItem is PromoCode selectedPromoCode)
            {
                if (!_isEditingPromoCode)
                {
                    _isEditingPromoCode = true;
                    _originalPromoCode = selectedPromoCode.Code;
                    _originalDiscountPercent = selectedPromoCode.DiscountPercent;
                    _originalExpiryDate = selectedPromoCode.ExpiryDate;

                    PromoCodeText.Text = selectedPromoCode.Code;
                    Discount.Text = selectedPromoCode.DiscountPercent.ToString();
                    ExpiryDate.SelectedDate = selectedPromoCode.ExpiryDate;

                    UpdatePromoCodesButtonVisibility(true);
                    Snackbar?.MessageQueue?.Enqueue("Редактирование промокода включено.");
                }
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Выберите промокод!");
                ShakeElement(PromoCodesGrid);
            }
        }

        private void UpdatePromoCodesButtonVisibility(bool isEditing)
        {
            EditPromoCodeButton.Visibility = _isEditingPromoCode ? Visibility.Collapsed : (_isPromoCodeSelected ? Visibility.Visible : Visibility.Collapsed);
            SavePromoCodeButton.Visibility = _isEditingPromoCode ? Visibility.Visible : Visibility.Collapsed;
            TogglePromoCodeActiveButton.Visibility = _isEditingPromoCode ? Visibility.Collapsed : (_isPromoCodeSelected ? Visibility.Visible : Visibility.Collapsed);
            AddPromoCodeButton.Visibility = _isEditingPromoCode ? Visibility.Collapsed : Visibility.Visible;
            CancelPromoCodeButton.Visibility = _isEditingPromoCode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PromoCodesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isPromoCodeSelected = PromoCodesGrid.SelectedItem != null;
            UpdatePromoCodesButtonVisibility(_isEditingPromoCode);
        }

        private void SavePromoCodeEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingPromoCode)
            {
                if (PromoCodesGrid.SelectedItem is PromoCode selectedPromoCode)
                {
                    if (!int.TryParse(Discount.Text, out int discount) || !ExpiryDate.SelectedDate.HasValue)
                    {
                        Snackbar?.MessageQueue?.Enqueue("Введите корректные данные!");
                        if (!int.TryParse(Discount.Text, out _))
                            ShakeElement(Discount);
                        if (!ExpiryDate.SelectedDate.HasValue)
                            ShakeElement(ExpiryDate);
                        return;
                    }

                    if (_context != null)
                    {
                        var promoCode = _context.PromoCodes?.FirstOrDefault(p => p.PromoCodeId == selectedPromoCode.PromoCodeId);
                        if (promoCode != null)
                        {
                            promoCode.DiscountPercent = discount;
                            promoCode.ExpiryDate = ExpiryDate.SelectedDate.Value;
                            try
                            {
                                _context.SaveChanges();
                                LogAction($"Отредактирован промокод {promoCode.Code} (ID: {promoCode.PromoCodeId})");
                                Snackbar?.MessageQueue?.Enqueue("Промокод обновлен!");
                            }
                            catch (DbUpdateException ex)
                            {
                                Snackbar?.MessageQueue?.Enqueue($"Ошибка при сохранении промокода: {ex.InnerException?.Message ?? ex.Message}");
                                return;
                            }

                            _isEditingPromoCode = false;
                            UpdatePromoCodesButtonVisibility(false);
                            LoadPromoCodes();
                            ClearPromoCodeFields();
                        }
                        else
                        {
                            Snackbar?.MessageQueue?.Enqueue("Ошибка: Промокод не найден.");
                        }
                    }
                    else
                    {
                        Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    }
                }
            }
        }

        private void TogglePromoCodeActive_Click(object sender, RoutedEventArgs e)
        {
            if (PromoCodesGrid.SelectedItem is PromoCode selectedPromoCode)
            {
                if (_context == null)
                {
                    Snackbar?.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                selectedPromoCode.IsActive = !selectedPromoCode.IsActive;
                try
                {
                    _context.SaveChanges();
                    LogAction($"Изменена активность промокода {selectedPromoCode.Code} (ID: {selectedPromoCode.PromoCodeId}) на {selectedPromoCode.IsActive}");
                    Snackbar?.MessageQueue?.Enqueue($"Активность промокода {selectedPromoCode.Code} изменена!");
                    LoadPromoCodes();
                }
                catch (DbUpdateException ex)
                {
                    Snackbar?.MessageQueue?.Enqueue($"Ошибка при изменении активности промокода: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            else
            {
                Snackbar?.MessageQueue?.Enqueue("Выберите промокод!");
                ShakeElement(PromoCodesGrid);
            }
        }

        private void ClearPromoCodeFields()
        {
            PromoCodeText.Text = string.Empty;
            Discount.Text = string.Empty;
            ExpiryDate.SelectedDate = null;
        }

        #endregion

        private void ResetUsersTab(object sender, RoutedEventArgs e)
        {
            if (_isEditingUser)
            {
                CancelAction_Click(sender, e);
            }
            LoadUsers();
            UsersPanel.Children.OfType<ScrollViewer>().FirstOrDefault()?.ScrollToTop();
        }

        private void ResetTariffsTab(object sender, RoutedEventArgs e)
        {
            if (_isEditingTariff)
            {
                CancelAction_Click(sender, e);
            }
            LoadTariffs();
            ClearTariffFields();
            TariffsPanel.Children.OfType<ScrollViewer>().FirstOrDefault()?.ScrollToTop();
        }

        private void ResetPromoCodesTab(object sender, RoutedEventArgs e)
        {
            if (_isEditingPromoCode)
            {
                CancelAction_Click(sender, e);
            }
            LoadPromoCodes();
            ClearPromoCodeFields();
            PromoCodesPanel.Children.OfType<ScrollViewer>().FirstOrDefault()?.ScrollToTop();
        }

        private void CancelAction_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingUser)
            {
                if (UsersGrid.SelectedItem is User selectedUser)
                {
                    selectedUser.Login = _originalUserLogin;
                    selectedUser.Name = _originalUserName;
                    selectedUser.Phone = _originalUserPhone;
                    selectedUser.Email = _originalUserEmail;
                    selectedUser.Address = _originalUserAddress;
                    selectedUser.Role = _originalUserRole;

                    _isEditingUser = false;
                    LoadUsers();
                    UpdateUsersButtonVisibility(false);
                    Snackbar?.MessageQueue?.Enqueue("Изменения пользователя отменены.");
                }
            }
            else if (_isEditingTariff)
            {
                _isEditingTariff = false;
                UpdateTariffsButtonVisibility(false);
                LoadTariffs();
                ClearTariffFields(); // Очищаем поля сразу
                Snackbar?.MessageQueue?.Enqueue("Изменения тарифа отменены.");
            }
            else if (_isEditingPromoCode)
            {
                _isEditingPromoCode = false;
                UpdatePromoCodesButtonVisibility(false);
                LoadPromoCodes();
                ClearPromoCodeFields(); // Очищаем поля сразу
                Snackbar?.MessageQueue?.Enqueue("Изменения промокода отменены.");
            }
            else
            {
                ClearTariffFields();
                ClearPromoCodeFields();
                Snackbar?.MessageQueue?.Enqueue("Поля очищены.");
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите выйти?", "Выход", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var loginWindow = new MainWindow(_scopeFactory);
                loginWindow.Show();
                Close();
            }
        }
    }
}