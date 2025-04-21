using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TaxiGO.Models;
using MaterialDesignThemes.Wpf;

namespace TaxiGO
{
    public partial class RateDriverWindow : Window
    {
        private readonly TaxiGoContext _context;
        private readonly Order _order;
        private int _selectedRating = 0; // По умолчанию рейтинг 0 (все звёзды незакрашены)
        private int _hoveredRating = 0; // Для отслеживания наведённой звезды

        public RateDriverWindow(TaxiGoContext context, Order order)
        {
            InitializeComponent();
            _context = context ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            _order = order ?? throw new InvalidOperationException("Заказ не передан.");

            Loaded += RateDriverWindow_Loaded;
            Closing += RateDriverWindow_Closing;

            // Устанавливаем начальный рейтинг (0 звёзд)
            UpdateStars(_selectedRating);
        }

        private void RateDriverWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Ничего дополнительного не требуется при загрузке
        }

        private void RateDriverWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Включаем родительское окно при закрытии
            if (Owner != null)
            {
                Owner.IsEnabled = true;
                if (Owner.WindowState == WindowState.Minimized)
                {
                    Owner.WindowState = WindowState.Normal;
                }
                Owner.Activate();
            }
        }

        private void StarButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int rating))
            {
                _hoveredRating = rating;
                UpdateStarsPreview(_hoveredRating);
            }
        }

        private void StarButton_MouseLeave(object sender, MouseEventArgs e)
        {
            _hoveredRating = 0;
            UpdateStars(_selectedRating);
        }

        private void StarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int rating))
            {
                _selectedRating = rating;
                UpdateStars(_selectedRating);
            }
        }

        private void UpdateStars(int rating)
        {
            // Цвета для звёзд: закрашенная звезда - жёлтая (#FFFFC107), незакрашенная - серая (#FF888888)
            Star1.Kind = rating >= 1 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star1.Foreground = rating >= 1 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star2.Kind = rating >= 2 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star2.Foreground = rating >= 2 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star3.Kind = rating >= 3 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star3.Foreground = rating >= 3 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star4.Kind = rating >= 4 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star4.Foreground = rating >= 4 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star5.Kind = rating >= 5 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star5.Foreground = rating >= 5 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));
        }

        private void UpdateStarsPreview(int rating)
        {
            // Предпросмотр при наведении: подсвечиваем звёзды до указанного рейтинга
            Star1.Kind = rating >= 1 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star1.Foreground = rating >= 1 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star2.Kind = rating >= 2 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star2.Foreground = rating >= 2 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star3.Kind = rating >= 3 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star3.Foreground = rating >= 3 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star4.Kind = rating >= 4 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star4.Foreground = rating >= 4 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            Star5.Kind = rating >= 5 ? PackIconKind.Star : PackIconKind.StarOutline;
            Star5.Foreground = rating >= 5 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFC107")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRating < 1 || _selectedRating > 5)
            {
                MessageBox.Show("Пожалуйста, выберите рейтинг!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Переход ко второму этапу
            RatingPanel.Visibility = Visibility.Collapsed;
            CommentPanel.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Возврат к первому этапу
            CommentPanel.Visibility = Visibility.Collapsed;
            RatingPanel.Visibility = Visibility.Visible;
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string comment = CommentTextBox.Text.Trim();

            try
            {
                _order.ClientRating = _selectedRating;
                _order.ClientComment = string.IsNullOrEmpty(comment) ? null : comment;
                _context.SaveChanges();

                // Показываем сообщение в Snackbar родительского окна
                if (Owner is ClientWindow clientWindow)
                {
                    clientWindow.Snackbar.MessageQueue?.Enqueue("Оценка сохранена!");
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении оценки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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