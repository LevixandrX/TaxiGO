using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using TaxiGO.Services;

namespace TaxiGO
{
    public partial class WelcomeWindow : Window
    {
        private readonly string _userName;
        private readonly int _userId;
        private readonly string _role;
        private readonly IServiceScopeFactory _scopeFactory;

        public WelcomeWindow(string userName, int userId, string role, IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();
            _userName = userName;
            _userId = userId;
            _role = role;
            _scopeFactory = scopeFactory;

            WelcomeText.Text = $"Добро пожаловать, {_userName}!";

            Loaded += WelcomeWindow_Loaded;
        }

        private void WelcomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления (прозрачность + легкое масштабирование)
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleInX = new DoubleAnimation(0.95, 1, TimeSpan.FromSeconds(1))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleInY = new DoubleAnimation(0.95, 1, TimeSpan.FromSeconds(1))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeIn);

            var transform = new ScaleTransform(1, 1, ActualWidth / 2, ActualHeight / 2);
            RenderTransform = transform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleInX);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleInY);

            // Анимация исчезновения через 2.5 секунды
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5))
            {
                BeginTime = TimeSpan.FromSeconds(2.5),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
            };
            var slideUp = new DoubleAnimation(0, -50, TimeSpan.FromSeconds(0.5))
            {
                BeginTime = TimeSpan.FromSeconds(2.5),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
            };
            var scaleOutX = new DoubleAnimation(1, 0.98, TimeSpan.FromSeconds(0.5))
            {
                BeginTime = TimeSpan.FromSeconds(2.5),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
            };
            var scaleOutY = new DoubleAnimation(1, 0.98, TimeSpan.FromSeconds(0.5))
            {
                BeginTime = TimeSpan.FromSeconds(2.5),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, ev) =>
            {
                // Открываем соответствующее окно в зависимости от роли
                Window nextWindow = _role switch
                {
                    "Client" => new ClientWindow(_userName, _userId, _scopeFactory, App.ServiceProvider.GetRequiredService<IGeocodingService>()),
                    "Driver" => new DriverWindow(_userName, _userId, _scopeFactory, App.ServiceProvider.GetRequiredService<IGeocodingService>()),
                    "Admin" => new AdminWindow(_userName, _userId, _scopeFactory),
                    "Dispatcher" => new DispatcherWindow(_userName, _scopeFactory),
                    _ => throw new NotSupportedException("Неизвестная роль пользователя")
                };

                nextWindow.Show();
                Close();
            };

            BeginAnimation(OpacityProperty, fadeOut);

            var translateTransform = new TranslateTransform();
            var combinedTransform = new TransformGroup();
            combinedTransform.Children.Add(transform);
            combinedTransform.Children.Add(translateTransform);
            RenderTransform = combinedTransform;

            translateTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOutX);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOutY);
        }
    }
}