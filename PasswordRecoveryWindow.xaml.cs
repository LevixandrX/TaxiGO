using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using TaxiGO.Models;
using BCrypt.Net;
using MaterialDesignThemes.Wpf;
using System.Windows.Shapes;

namespace TaxiGO
{
    public partial class PasswordRecoveryWindow : Window
    {
        private readonly TaxiGoContext _context;
        private string? _tempCode;
        private User? _user;
        private int _currentStep = 1;
        private bool _isPasswordVisible = false;
        private Grid? _mainGrid;
        private Grid? _innerGrid;
        private Border? _mainBorder;

        public PasswordRecoveryWindow(TaxiGoContext context)
        {
            InitializeComponent();
            _context = context ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            Loaded += PasswordRecoveryWindow_Loaded;
        }

        private void PasswordRecoveryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _mainGrid = FindName("MainGrid") as Grid;
            _innerGrid = FindName("InnerGrid") as Grid;
            _mainBorder = FindName("MainBorder") as Border;
            if (_mainGrid == null || _innerGrid == null || _mainBorder == null)
            {
                MessageBox.Show("Не удалось найти MainGrid, InnerGrid или MainBorder. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Инициализация индикатора прогресса
            UpdateProgressIndicator();
        }

        private void UpdateProgressIndicator()
        {
            var activeColor = Color.FromRgb(76, 175, 80); // #FF4CAF50 (зелёный)
            var inactiveColor = Color.FromRgb(97, 97, 97); // #FF616161 (серый)

            // Обновляем круги
            Step1Indicator.Fill = new SolidColorBrush(_currentStep >= 1 ? activeColor : inactiveColor);
            Step2Indicator.Fill = new SolidColorBrush(_currentStep >= 2 ? activeColor : inactiveColor);
            Step3Indicator.Fill = new SolidColorBrush(_currentStep >= 3 ? activeColor : inactiveColor);

            // Обновляем линии
            Line1to2.Stroke = new SolidColorBrush(_currentStep >= 2 ? activeColor : inactiveColor);
            Line2to3.Stroke = new SolidColorBrush(_currentStep >= 3 ? activeColor : inactiveColor);

            // Анимация пульсации для активного шага
            if (_currentStep == 1)
            {
                AnimatePulse(Step1Pulse, activeColor);
            }
            else if (_currentStep == 2)
            {
                AnimatePulse(Step2Pulse, activeColor);
            }
            else if (_currentStep == 3)
            {
                AnimatePulse(Step3Pulse, activeColor);
            }

            // Управление видимостью кнопки "Назад"
            BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AnimatePulse(Ellipse pulseEllipse, Color color)
        {
            pulseEllipse.Fill = new SolidColorBrush(color);
            var scaleAnimation = new DoubleAnimation
            {
                From = 1,
                To = 2,
                Duration = TimeSpan.FromSeconds(1),
                AutoReverse = false,
                RepeatBehavior = RepeatBehavior.Forever
            };
            var opacityAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 0,
                Duration = TimeSpan.FromSeconds(1),
                AutoReverse = false,
                RepeatBehavior = RepeatBehavior.Forever
            };
            pulseEllipse.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            pulseEllipse.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            pulseEllipse.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
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
            if (_currentStep == 1) // Шаг 1: Проверка логина
            {
                string login = LoginTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(login))
                {
                    MessageBox.Show("Введите логин.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(LoginTextBox);
                    return;
                }

                if (_context.Users == null)
                {
                    MessageBox.Show("Ошибка: база данных недоступна.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var users = _context.Users;
                if (users == null)
                {
                    MessageBox.Show("Ошибка: коллекция пользователей недоступна.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _user = users.FirstOrDefault(u => u.Login == login);
                if (_user == null)
                {
                    MessageBox.Show("Пользователь с таким логином не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShakeElement(LoginTextBox);
                    return;
                }

                // Генерируем временный код (в реальном приложении отправляем по email/SMS)
                _tempCode = new Random().Next(100000, 999999).ToString();
                MessageBox.Show($"Ваш код восстановления: {_tempCode}\n(В реальном приложении это будет отправлено на ваш email или телефон)", "Код", MessageBoxButton.OK, MessageBoxImage.Information);

                // Переходим к шагу 2
                _currentStep = 2;
                UpdateProgressIndicator();
                LoginPanel.Visibility = Visibility.Collapsed;
                CodePanel.Visibility = Visibility.Visible;
                ActionButton.Content = "Подтвердить код";
            }
            else if (_currentStep == 2) // Шаг 2: Проверка кода
            {
                string enteredCode = CodeTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(enteredCode))
                {
                    MessageBox.Show("Введите код восстановления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(CodeTextBox);
                    return;
                }

                if (enteredCode != _tempCode)
                {
                    MessageBox.Show("Неверный код восстановления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShakeElement(CodeTextBox);
                    return;
                }

                // Переходим к шагу 3
                _currentStep = 3;
                UpdateProgressIndicator();
                CodePanel.Visibility = Visibility.Collapsed;
                NewPasswordPanel.Visibility = Visibility.Visible;
                ActionButton.Content = "Сменить пароль";
            }
            else if (_currentStep == 3) // Шаг 3: Смена пароля
            {
                string newPassword = (_isPasswordVisible ? NewPasswordTextBox.Text : NewPasswordBox.Password).Trim();
                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    MessageBox.Show("Введите новый пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(_isPasswordVisible ? NewPasswordTextBox : NewPasswordBox);
                    return;
                }

                if (newPassword.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать минимум 6 символов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShakeElement(_isPasswordVisible ? NewPasswordTextBox : NewPasswordBox);
                    return;
                }

                if (_user == null)
                {
                    MessageBox.Show("Ошибка: пользователь не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Обновляем пароль пользователя
                _user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                try
                {
                    _context.SaveChanges();
                    MessageBox.Show("Пароль успешно изменён! Теперь вы можете войти.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при смене пароля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 2)
            {
                _currentStep = 1;
                CodePanel.Visibility = Visibility.Collapsed;
                LoginPanel.Visibility = Visibility.Visible;
                ActionButton.Content = "Далее";
            }
            else if (_currentStep == 3)
            {
                _currentStep = 2;
                NewPasswordPanel.Visibility = Visibility.Collapsed;
                CodePanel.Visibility = Visibility.Visible;
                ActionButton.Content = "Подтвердить код";
            }
            UpdateProgressIndicator();
        }

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            if (_isPasswordVisible)
            {
                NewPasswordTextBox.Text = NewPasswordBox.Password;
                NewPasswordBox.Visibility = Visibility.Collapsed;
                NewPasswordTextBox.Visibility = Visibility.Visible;
                TogglePasswordIcon.Kind = PackIconKind.EyeOff;
            }
            else
            {
                NewPasswordBox.Password = NewPasswordTextBox.Text;
                NewPasswordBox.Visibility = Visibility.Visible;
                NewPasswordTextBox.Visibility = Visibility.Collapsed;
                TogglePasswordIcon.Kind = PackIconKind.Eye;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not Button)
            {
                DragMove();
            }
        }
    }
}