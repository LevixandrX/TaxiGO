using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using Microsoft.Extensions.DependencyInjection;
using TaxiGO.Models;
using System.Text.RegularExpressions;

namespace TaxiGO
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly TaxiGoContext _context;
        private bool isLoginMode = true;
        private bool isPasswordVisible = false;
        private bool isRegPasswordVisible = false;

        public MainWindow()
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tabControl = sender as HandyControl.Controls.TabControl;
            if (tabControl == null || LoginPanel == null || RegisterPanel == null)
                return;

            if (tabControl.SelectedIndex == 0)
            {
                isLoginMode = true;
                LoginPanel.Visibility = Visibility.Visible;
                RegisterPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                isLoginMode = false;
                LoginPanel.Visibility = Visibility.Collapsed;
                RegisterPanel.Visibility = Visibility.Visible;
            }
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
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

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoginMode)
            {
                string login = LoginTextBox.Text;
                string password = isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;

                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                {
                    System.Windows.MessageBox.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(LoginTextBox);
                    ShakeElement(isPasswordVisible ? PasswordTextBox : PasswordBox);
                    return;
                }

                // Пароли в базе данных хранятся в виде текста, поэтому не хешируем
                var user = _context.Users.FirstOrDefault(u => u.Login == login && u.PasswordHash == password);

                if (user != null)
                {
                    if (!user.IsActive)
                    {
                        System.Windows.MessageBox.Show("Ваш аккаунт заблокирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    System.Windows.Window nextWindow = user.Role switch
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
                    System.Windows.MessageBox.Show("Неверный логин или пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShakeElement(LoginTextBox);
                    ShakeElement(isPasswordVisible ? PasswordTextBox : PasswordBox);
                }
            }
            else
            {
                string name = RegNameTextBox.Text;
                string login = RegLoginTextBox.Text;
                string password = isRegPasswordVisible ? RegPasswordTextBox.Text : RegPasswordBox.Password;
                string phone = RegPhoneTextBox.Text;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(login) ||
                    string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(phone))
                {
                    System.Windows.MessageBox.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(RegNameTextBox);
                    ShakeElement(RegLoginTextBox);
                    ShakeElement(isRegPasswordVisible ? RegPasswordTextBox : RegPasswordBox);
                    ShakeElement(RegPhoneTextBox);
                    return;
                }

                if (password.Length < 6)
                {
                    System.Windows.MessageBox.Show("Пароль должен содержать минимум 6 символов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(isRegPasswordVisible ? RegPasswordTextBox : RegPasswordBox);
                    return;
                }

                if (_context.Users.Any(u => u.Login == login))
                {
                    System.Windows.MessageBox.Show("Пользователь с таким логином уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShakeElement(RegLoginTextBox);
                    return;
                }

                // Убираем все нечисловые символы из номера телефона
                phone = Regex.Replace(phone, "[^0-9]", "");
                if (phone.Length == 11 && phone.StartsWith("8"))
                {
                    var newUser = new User
                    {
                        Name = name,
                        Login = login,
                        PasswordHash = password, // Сохраняем пароль как текст, без хеширования
                        Phone = phone,
                        Role = "Client",
                        IsActive = true,
                        Email = "" // Устанавливаем пустую строку вместо NULL
                    };

                    try
                    {
                        _context.Users.Add(newUser);
                        _context.SaveChanges();

                        System.Windows.MessageBox.Show("Регистрация успешна! Теперь вы можете войти.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        TabControl.SelectedIndex = 0;
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Ошибка при регистрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Номер телефона должен начинаться с 8 и содержать 11 цифр.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                TogglePasswordImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/eye-off.png"));
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                TogglePasswordImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/eye.png"));
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
                ToggleRegPasswordImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/eye-off.png"));
            }
            else
            {
                RegPasswordBox.Password = RegPasswordTextBox.Text;
                RegPasswordBox.Visibility = Visibility.Visible;
                RegPasswordTextBox.Visibility = Visibility.Collapsed;
                ToggleRegPasswordImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/eye.png"));
            }
        }

        private void RegPhoneTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string currentText = RegPhoneTextBox.Text;
            string newText = currentText + e.Text;

            // Удаляем все нечисловые символы для проверки длины
            string digitsOnly = Regex.Replace(newText, "[^0-9]", "");

            // Ограничиваем ввод до 11 цифр
            if (digitsOnly.Length > 11)
            {
                e.Handled = true;
                return;
            }

            // Форматирование номера телефона
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
            System.Windows.MessageBox.Show("Функция восстановления пароля пока в разработке.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
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