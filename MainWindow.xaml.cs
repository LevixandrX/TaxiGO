using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using TaxiGO.Models;
using BCrypt.Net;
using MaterialDesignThemes.Wpf;
using System.Windows.Media.Effects;

namespace TaxiGO
{
    public partial class MainWindow : Window
    {
        private readonly TaxiGoContext _context;
        private bool isLoginMode = true;
        private bool isPasswordVisible = false;
        private bool isRegPasswordVisible = false;
        private Grid? mainGrid;
        private Grid? innerGrid;
        private Border? mainBorder;

        public MainWindow()
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
            SizeChanged += MainWindow_SizeChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mainGrid = FindName("MainGrid") as Grid;
            innerGrid = FindName("InnerGrid") as Grid;
            mainBorder = FindName("MainBorder") as Border;
            if (mainGrid == null || innerGrid == null || mainBorder == null)
            {
                MessageBox.Show("Не удалось найти MainGrid, InnerGrid или MainBorder. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            UpdateLayoutForWindowState();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayoutForWindowState();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateLayoutForWindowState();
        }

        private void UpdateLayoutForWindowState()
        {
            if (mainBorder == null || innerGrid == null || LoginPanel == null || RegisterPanel == null) return;

            double windowWidth = ActualWidth;
            double windowHeight = ActualHeight;

            // Находим RectangleGeometry для обрезки
            var windowClip = MainGrid.Clip as RectangleGeometry;
            if (windowClip == null) return;

            if (WindowState == WindowState.Maximized)
            {
                // Убираем скругление углов и тень в полноэкранном режиме
                mainBorder.CornerRadius = new CornerRadius(0);
                mainBorder.Effect = null;

                // Обновляем Clip: убираем скругление углов
                windowClip.Rect = new Rect(0, 0, windowWidth, windowHeight);
                windowClip.RadiusX = 0;
                windowClip.RadiusY = 0;

                // Убираем внешние отступы
                innerGrid.Margin = new Thickness(40);

                // Адаптируем размеры элементов
                double contentWidth = Math.Min(windowWidth * 0.3, 450); // 30% ширины окна, но не более 450
                double tabWidth = Math.Min(windowWidth * 0.2, 300); // 20% ширины окна, но не более 300

                // Панели ввода
                LoginInputPanel.Width = contentWidth;
                PasswordInputPanel.Width = contentWidth;
                RegNamePanel.Width = contentWidth;
                RegLoginPanel.Width = contentWidth;
                RegPasswordPanel.Width = contentWidth;
                RegPhonePanel.Width = contentWidth;
                RegEmailPanel.Width = contentWidth;

                // Кнопки
                LoginButton.Width = contentWidth * 0.4; // 40% от ширины панели
                LoginButton.MaxWidth = 250;
                RegisterButton.Width = contentWidth * 0.4;
                RegisterButton.MaxWidth = 250;

                // Вкладки
                TabGridInner.Width = tabWidth;
                TabGridInner.Height = 36; // Уменьшаем высоту вкладок
                TabGridInner.MaxWidth = 300;

                // Отступы для вкладок
                TabGrid.Margin = new Thickness(0, 40, 0, 20);

                // Динамический отступ сверху для панелей в полноэкранном режиме
                double loginTopMargin = Math.Max((windowHeight - 600) / 4, 30);
                double registerTopMargin = Math.Max((windowHeight - 600) / 6, 20);

                LoginPanel.Margin = new Thickness(0, loginTopMargin, 0, 0);
                RegisterPanel.Margin = new Thickness(0, registerTopMargin, 0, 0);
            }
            else
            {
                // Восстанавливаем скругление углов и тень
                mainBorder.CornerRadius = new CornerRadius(20);
                mainBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.5,
                    Color = Colors.Black
                };

                // Обновляем Clip: восстанавливаем скругление углов
                windowClip.Rect = new Rect(0, 0, windowWidth, windowHeight);
                windowClip.RadiusX = 20;
                windowClip.RadiusY = 20;

                // Восстанавливаем внешние отступы
                innerGrid.Margin = new Thickness(20);

                // Возвращаем стандартные размеры
                double contentWidth = 300; // Стандартная ширина
                double tabWidth = 300;

                // Панели ввода
                LoginInputPanel.Width = contentWidth;
                PasswordInputPanel.Width = contentWidth;
                RegNamePanel.Width = contentWidth;
                RegLoginPanel.Width = contentWidth;
                RegPasswordPanel.Width = contentWidth;
                RegPhonePanel.Width = contentWidth;
                RegEmailPanel.Width = contentWidth;

                // Кнопки
                LoginButton.Width = 200;
                LoginButton.MaxWidth = 300;
                RegisterButton.Width = 200;
                RegisterButton.MaxWidth = 300;

                // Вкладки
                TabGridInner.Width = tabWidth;
                TabGridInner.Height = 40; // Стандартная высота
                TabGridInner.MaxWidth = 400;

                // Отступы для вкладок
                TabGrid.Margin = new Thickness(0, 0, 0, 20);

                // Сбрасываем отступы для панелей в оконном режиме
                LoginPanel.Margin = new Thickness(0);
                RegisterPanel.Margin = new Thickness(0);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null || LoginPanel == null || RegisterPanel == null)
                return;

            if (tabControl.SelectedIndex == 0)
            {
                isLoginMode = true;
                LoginPanel.Visibility = Visibility.Visible;
                RegisterPanel.Visibility = Visibility.Collapsed;
                Title = "TaxiGO - Авторизация";
            }
            else
            {
                isLoginMode = false;
                RegisterPanel.Visibility = Visibility.Visible;
                LoginPanel.Visibility = Visibility.Collapsed;
                Title = "TaxiGO - Регистрация";
            }
        }

        private void LoginTabButton_Click(object sender, RoutedEventArgs e)
        {
            TabControl.SelectedIndex = 0;
        }

        private void RegisterTabButton_Click(object sender, RoutedEventArgs e)
        {
            TabControl.SelectedIndex = 1;
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

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoginMode)
            {
                string login = LoginTextBox.Text.Trim();
                string password = (isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password).Trim();

                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(LoginTextBox);
                    ShakeElement(isPasswordVisible ? PasswordTextBox : PasswordBox);
                    return;
                }

                if (_context.Users == null)
                {
                    MessageBox.Show("Ошибка: база данных недоступна.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var user = _context.Users.FirstOrDefault(u => u.Login == login);
                if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    if (!user.IsActive)
                    {
                        MessageBox.Show("Ваш аккаунт заблокирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    Window nextWindow = user.Role switch
                    {
                        "Client" => new ClientWindow(user.Name, user.UserId),
                        "Driver" => new DriverWindow(user.Name, user.UserId),
                        "Admin" => new AdminWindow(user.Name),
                        "Dispatcher" => new DispatcherWindow(user.Name),
                        _ => throw new NotSupportedException("Неизвестная роль пользователя")
                    };

                    nextWindow.Show();
                    Close();
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShakeElement(LoginTextBox);
                    ShakeElement(isPasswordVisible ? PasswordTextBox : PasswordBox);
                }
            }
            else
            {
                string name = RegNameTextBox.Text.Trim();
                string login = RegLoginTextBox.Text.Trim();
                string password = (isRegPasswordVisible ? RegPasswordTextBox.Text : RegPasswordBox.Password).Trim();
                string phone = RegPhoneTextBox.Text.Trim();
                string email = RegEmailTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(login) ||
                    string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(phone))
                {
                    MessageBox.Show("Заполните все обязательные поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (string.IsNullOrWhiteSpace(name)) ShakeElement(RegNameTextBox);
                    if (string.IsNullOrWhiteSpace(login)) ShakeElement(RegLoginTextBox);
                    if (string.IsNullOrWhiteSpace(password)) ShakeElement(isRegPasswordVisible ? RegPasswordTextBox : RegPasswordBox);
                    if (string.IsNullOrWhiteSpace(phone)) ShakeElement(RegPhoneTextBox);
                    return;
                }

                if (password.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать минимум 6 символов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(isRegPasswordVisible ? RegPasswordTextBox : RegPasswordBox);
                    return;
                }

                if (_context.Users == null)
                {
                    MessageBox.Show("Ошибка: база данных недоступна.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_context.Users.Any(u => u.Login == login))
                {
                    MessageBox.Show("Пользователь с таким логином уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShakeElement(RegLoginTextBox);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(email) && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    MessageBox.Show("Введите корректный адрес электронной почты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(RegEmailTextBox);
                    return;
                }

                phone = Regex.Replace(phone, "[^0-9]", "");
                if (phone.Length == 11 && phone.StartsWith("8"))
                {
                    var newUser = new User
                    {
                        Name = name.Length > 100 ? name.Substring(0, 100) : name,
                        Login = login.Length > 50 ? login.Substring(0, 50) : login,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        Phone = phone,
                        Role = "Client",
                        IsActive = true,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                        RegistrationDate = DateTime.Now
                    };

                    try
                    {
                        _context.Users.Add(newUser);
                        _context.SaveChanges();

                        MessageBox.Show("Регистрация успешна! Теперь вы можете войти.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        TabControl.SelectedIndex = 0;
                    }
                    catch (Exception ex)
                    {
                        string errorMessage = ex.Message;
                        if (ex.InnerException != null)
                        {
                            errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                            if (ex.InnerException.InnerException != null)
                            {
                                errorMessage += $"\nInner Inner Exception: {ex.InnerException.InnerException.Message}";
                            }
                        }
                        MessageBox.Show($"Ошибка при регистрации: {errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Номер телефона должен начинаться с 8 и содержать 11 цифр.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(RegPhoneTextBox);
                }
            }
        }

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;
            if (isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                TogglePasswordIcon.Kind = PackIconKind.EyeOff;
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                TogglePasswordIcon.Kind = PackIconKind.Eye;
            }
        }

        private void ToggleRegPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            isRegPasswordVisible = !isRegPasswordVisible;
            if (isRegPasswordVisible)
            {
                RegPasswordTextBox.Text = RegPasswordBox.Password;
                RegPasswordBox.Visibility = Visibility.Collapsed;
                RegPasswordTextBox.Visibility = Visibility.Visible;
                ToggleRegPasswordIcon.Kind = PackIconKind.EyeOff;
            }
            else
            {
                RegPasswordBox.Password = RegPasswordTextBox.Text;
                RegPasswordBox.Visibility = Visibility.Visible;
                RegPasswordTextBox.Visibility = Visibility.Collapsed;
                ToggleRegPasswordIcon.Kind = PackIconKind.Eye;
            }
        }

        private void RegPhoneTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string currentText = RegPhoneTextBox.Text;
            string newText = currentText + e.Text;

            string digitsOnly = Regex.Replace(newText, "[^0-9]", "");

            if (digitsOnly.Length > 11)
            {
                e.Handled = true;
                return;
            }

            if (digitsOnly.Length <= 11)
            {
                string formatted = "8";
                if (digitsOnly.Length > 1) formatted += " (" + digitsOnly.Substring(1, Math.Min(3, digitsOnly.Length - 1));
                if (digitsOnly.Length > 4) formatted += ") " + digitsOnly.Substring(4, Math.Min(3, digitsOnly.Length - 4));
                if (digitsOnly.Length > 7) formatted += "-" + digitsOnly.Substring(7, Math.Min(2, digitsOnly.Length - 7));
                if (digitsOnly.Length > 9) formatted += "-" + digitsOnly.Substring(9, Math.Min(2, digitsOnly.Length - 9));

                RegPhoneTextBox.Text = formatted;
                RegPhoneTextBox.CaretIndex = formatted.Length;
                e.Handled = true;
            }
        }

        private void ForgotPassword_Click(object sender, MouseButtonEventArgs e)
        {
            var recoveryWindow = new PasswordRecoveryWindow(_context)
            {
                Owner = this
            };
            recoveryWindow.ShowDialog();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is not Button)
                {
                    if (e.ClickCount == 2)
                    {
                        if (WindowState == WindowState.Normal)
                        {
                            WindowState = WindowState.Maximized;
                        }
                        else if (WindowState == WindowState.Maximized)
                        {
                            WindowState = WindowState.Normal;
                        }
                    }
                    else
                    {
                        DragMove();
                    }
                }
            }
        }
    }
}