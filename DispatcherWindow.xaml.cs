using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TaxiGO.Models;
using System.Collections.ObjectModel;
using System.Windows.Media.Effects;
using System.IO;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Defaults;

namespace TaxiGO
{
    public partial class DispatcherWindow : Window
    {
        private readonly TaxiGoContext _context;
        private readonly IServiceScope _scope;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _userName;
        private Grid? _mainGrid;
        private Border? _mainBorder;
        private ObservableCollection<Order> _pendingOrders;
        private ObservableCollection<Order> _activeOrders;
        private string _currentStatisticsPeriod = "Сегодня";
        public ObservableCollection<ISeries> ChartSeries { get; set; } = new ObservableCollection<ISeries>();

        public DispatcherWindow(string userName, IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();
            _userName = userName;
            _scopeFactory = scopeFactory;
            _scope = scopeFactory.CreateScope();
            _context = _scope.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");

            DataContext = this;

            Snackbar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

            _pendingOrders = new ObservableCollection<Order>();
            _activeOrders = new ObservableCollection<Order>();

            Loaded += DispatcherWindow_Loaded;
            StateChanged += DispatcherWindow_StateChanged;
            SizeChanged += DispatcherWindow_SizeChanged;
            Closing += DispatcherWindow_Closing;

            LoadPendingOrders();
            LoadActiveOrders();
            InitializeStatusFilters();
        }

        private void DispatcherWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _mainGrid = FindName("MainGrid") as Grid;
            _mainBorder = FindName("MainBorder") as Border;
            if (_mainGrid == null || _mainBorder == null)
            {
                MessageBox.Show("Не удалось найти MainGrid или MainBorder. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            UpdateLayoutForWindowState();
            UpdateMaximizeRestoreIcon();
            PendingOrdersList.ItemContainerGenerator.StatusChanged += PendingOrdersList_ContainerStatusChanged;
        }

        private void DispatcherWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayoutForWindowState();
        }

        private void DispatcherWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateLayoutForWindowState();
            UpdateMaximizeRestoreIcon();
        }

        private void DispatcherWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
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
            if (_mainBorder == null || _mainGrid == null) return;

            double windowWidth = ActualWidth;
            double windowHeight = ActualHeight;

            var windowClip = _mainGrid.Clip as RectangleGeometry;
            if (windowClip == null) return;

            windowClip.Rect = new Rect(0, 0, windowWidth, windowHeight);

            var sidebarGrid = _mainBorder.Child as Grid;
            if (sidebarGrid == null) return;

            var sidebarBorder = sidebarGrid.Children?.OfType<Border>().FirstOrDefault();
            if (sidebarBorder == null) return;

            var sidebarContentGrid = sidebarBorder.Child as Grid;
            if (sidebarContentGrid == null) return;

            if (WindowState == WindowState.Maximized)
            {
                _mainBorder.CornerRadius = new CornerRadius(0);
                _mainBorder.Effect = null;
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
                _mainBorder.CornerRadius = new CornerRadius(20);
                _mainBorder.Effect = new DropShadowEffect
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

        private void InitializeStatusFilters()
        {
            StatusFilterComboBox.ItemsSource = new[] { "Все", "Pending", "Accepted", "Completed", "Canceled" };
            StatusFilterComboBox.SelectedIndex = 0;
            ActiveStatusFilterComboBox.ItemsSource = new[] { "Все", "Accepted", "Completed", "Canceled" };
            ActiveStatusFilterComboBox.SelectedIndex = 0;
        }

        private void LoadPendingOrders(string searchText = "", string statusFilter = "Все")
        {
            if (_context.Orders == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки ожидающих заказов.");
                return;
            }

            try
            {
                var query = _context.Orders
                    .Include(o => o.Client)
                    .Include(o => o.Tariff)
                    .Include(o => o.Driver)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    searchText = searchText.ToLower();
                    query = query.Where(o =>
                        o.OrderId.ToString().Contains(searchText) ||
                        (o.Client != null && o.Client.Name != null && o.Client.Name.ToLower().Contains(searchText)) ||
                        (o.StartPoint != null && o.StartPoint.ToLower().Contains(searchText)) ||
                        (o.EndPoint != null && o.EndPoint.ToLower().Contains(searchText)));
                }

                if (statusFilter != "Все")
                {
                    query = query.Where(o => o.Status == statusFilter);
                }
                else
                {
                    query = query.Where(o => o.Status == "Pending" || o.Status == "Accepted");
                }

                var orders = query.OrderBy(o => o.OrderTime).ToList();

                _pendingOrders.Clear();
                foreach (var order in orders)
                {
                    _pendingOrders.Add(order);
                }

                PendingOrdersList.ItemsSource = _pendingOrders;
                LoadAvailableDriversForOrders();
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки ожидающих заказов: {ex.Message}");
            }
        }

        private void LoadActiveOrders(string searchText = "", string statusFilter = "Все")
        {
            if (_context.Orders == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки заказов.");
                return;
            }

            try
            {
                var query = _context.Orders
                    .Include(o => o.Client)
                    .Include(o => o.Driver)
                    .Include(o => o.Tariff)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    searchText = searchText.ToLower();
                    query = query.Where(o =>
                        o.OrderId.ToString().Contains(searchText) ||
                        (o.Client != null && o.Client.Name != null && o.Client.Name.ToLower().Contains(searchText)) ||
                        (o.StartPoint != null && o.StartPoint.ToLower().Contains(searchText)) ||
                        (o.EndPoint != null && o.EndPoint.ToLower().Contains(searchText)));
                }

                if (statusFilter != "Все")
                {
                    query = query.Where(o => o.Status == statusFilter);
                }
                else
                {
                    query = query.Where(o => o.Status == "Accepted" || o.Status == "Completed" || o.Status == "Canceled");
                }

                var orders = query.OrderByDescending(o => o.OrderTime).ToList();

                _activeOrders.Clear();
                foreach (var order in orders)
                {
                    _activeOrders.Add(order);
                }

                ActiveOrdersList.ItemsSource = _activeOrders;
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки заказов: {ex.Message}");
            }
        }

        private void LoadStatistics(string period = "Сегодня")
        {
            try
            {
                _currentStatisticsPeriod = period;
                var ordersQuery = _context.Orders?.AsQueryable() ?? throw new InvalidOperationException("Orders не инициализированы.");
                DateTime startDate;

                switch (period)
                {
                    case "Сегодня":
                        startDate = DateTime.Today;
                        break;
                    case "Неделя":
                        startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek); // Начало недели
                        break;
                    case "Месяц":
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); // Начало месяца
                        break;
                    default:
                        startDate = DateTime.Today;
                        break;
                }

                var totalOrders = ordersQuery.Count(o => o.OrderTime >= startDate);
                var totalRevenue = ordersQuery
                    .Where(o => o.OrderTime >= startDate && o.Cost.HasValue)
                    .Sum(o => o.Cost.GetValueOrDefault());
                var activeDrivers = _context.DriverAvailabilities != null
                    ? _context.DriverAvailabilities.Count(da => da.IsAvailable && da.LastUpdate >= startDate)
                    : 0;

                var statsPanel = StatisticsPanel.Children.OfType<Grid>().FirstOrDefault();
                if (statsPanel != null)
                {
                    var leftBorder = statsPanel.Children.OfType<Border>().FirstOrDefault();
                    if (leftBorder != null)
                    {
                        var stackPanel = leftBorder.Child as StackPanel;
                        if (stackPanel != null)
                        {
                            var texts = stackPanel.Children.OfType<TextBlock>().ToList();
                            if (texts.Count >= 4)
                            {
                                texts[1].Text = $"Заказов: {totalOrders}";
                                texts[2].Text = $"Доход: {totalRevenue:F2} ₽";
                                texts[3].Text = $"Активных водителей: {activeDrivers}";
                            }
                        }
                    }
                }

                UpdateOrdersChart(period, startDate);
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки статистики: {ex.Message}");
            }
        }

        private void UpdateOrdersChart(string period, DateTime startDate)
        {
            try
            {
                var ordersQuery = _context.Orders?.AsQueryable() ?? throw new InvalidOperationException("Orders не инициализированы.");
                var ordersByDate = ordersQuery
                    .Where(o => o.OrderTime.HasValue && o.OrderTime.Value >= startDate)
                    .GroupBy(o => o.OrderTime.Value.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToList();

                ChartSeries.Clear();
                ChartSeries.Add(new ColumnSeries<DateTimePoint>
                {
                    Name = "Заказы",
                    Values = ordersByDate.Select(x => new DateTimePoint(x.Date, x.Count)).ToArray(),
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                    Stroke = null
                });

                if (OrdersChart != null)
                {
                    OrdersChart.XAxes = new[]
                    {
                new Axis
                {
                    LabelsRotation = 45,
                    Labeler = value => new DateTime((long)value).ToString("dd.MM"),
                    UnitWidth = TimeSpan.FromDays(1).Ticks
                }
            };
                    OrdersChart.YAxes = new[]
                    {
                new Axis
                {
                    Labeler = value => value.ToString("N0"),
                    MinStep = 1
                }
            };
                }
                else
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Элемент графика не найден.");
                }
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки графика: {ex.Message}");
            }
        }

        private void LoadAvailableDriversForOrders()
        {
            try
            {
                if (_context.Users == null || _context.DriverAvailabilities == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: таблицы пользователей или доступности водителей недоступны.");
                    return;
                }

                var availableDrivers = _context.Users
                    .Where(u => u.Role == "Driver" && u.IsActive)
                    .Join(_context.DriverAvailabilities,
                        user => user.UserId,
                        availability => availability.DriverId,
                        (user, availability) => new { User = user, Availability = availability })
                    .Where(x => x.Availability.IsAvailable)
                    .Select(x => new { x.User.UserId, DisplayName = $"{x.User.Name} ({x.User.Phone})" })
                    .ToList();

                if (!availableDrivers.Any())
                {
                    Snackbar.MessageQueue?.Enqueue("Нет доступных водителей.");
                }

                foreach (var item in PendingOrdersList.Items)
                {
                    var order = item as Order;
                    if (order == null) continue;

                    var container = PendingOrdersList.ItemContainerGenerator.ContainerFromItem(order) as FrameworkElement;
                    if (container == null) continue;

                    var comboBox = FindVisualChild<ComboBox>(container, "DriverComboBox");
                    if (comboBox != null)
                    {
                        comboBox.ItemsSource = availableDrivers;
                        comboBox.DisplayMemberPath = "DisplayName";
                        comboBox.SelectedValuePath = "UserId";
                        comboBox.Tag = order.OrderId;
                        comboBox.SelectionChanged += DriverComboBox_SelectionChanged;
                    }
                }
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки водителей: {ex.Message}");
            }
        }

        private void DriverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.Tag is int orderId)
            {
                var order = _pendingOrders.FirstOrDefault(o => o.OrderId == orderId);
                if (order != null && comboBox.SelectedValue is int driverId)
                {
                    order.DriverId = driverId;
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject? parent, string? name = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    if (string.IsNullOrEmpty(name) || (child is FrameworkElement fe && fe.Name == name))
                        return result;
                }

                var descendant = FindVisualChild<T>(child, name);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

        private void AssignOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                var order = _pendingOrders.FirstOrDefault(o => o.OrderId == orderId);
                if (order == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Заказ не найден.");
                    return;
                }

                var container = PendingOrdersList.ItemContainerGenerator.ContainerFromItem(order) as FrameworkElement;
                if (container == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Не удалось найти элемент заказа.");
                    return;
                }

                var comboBox = FindVisualChild<ComboBox>(container, "DriverComboBox");
                if (comboBox?.SelectedValue is int driverId)
                {
                    if (order.Status != "Pending")
                    {
                        Snackbar.MessageQueue?.Enqueue("Ошибка: Заказ уже назначен или завершён.");
                        return;
                    }

                    try
                    {
                        order.DriverId = driverId;
                        order.Status = "Accepted";

                        var statusHistory = new OrderStatusHistory
                        {
                            OrderId = order.OrderId,
                            Status = "Accepted",
                            ChangeTime = DateTime.Now,
                            ChangedByUserId = driverId
                        };
                        _context.OrderStatusHistories?.Add(statusHistory);

                        _context.SaveChanges();
                        Snackbar.MessageQueue?.Enqueue($"Заказ #{orderId} успешно назначен водителю!");

                        LoadPendingOrders();
                        LoadActiveOrders();
                    }
                    catch (Exception ex)
                    {
                        Snackbar.MessageQueue?.Enqueue($"Ошибка при назначении заказа: {ex.Message}");
                    }
                }
                else
                {
                    Snackbar.MessageQueue?.Enqueue("Выберите водителя!");
                    if (comboBox != null)
                    {
                        ShakeElement(comboBox);
                    }
                }
            }
        }

        private void CancelOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                var order = _pendingOrders.FirstOrDefault(o => o.OrderId == orderId);
                if (order == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Заказ не найден.");
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите отменить заказ #{orderId}?", "Отмена заказа", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        order.Status = "Canceled";
                        var statusHistory = new OrderStatusHistory
                        {
                            OrderId = order.OrderId,
                            Status = "Canceled",
                            ChangeTime = DateTime.Now,
                            ChangedByUserId = 0 // ID диспетчера недоступен
                        };
                        _context.OrderStatusHistories?.Add(statusHistory);

                        _context.SaveChanges();
                        Snackbar.MessageQueue?.Enqueue($"Заказ #{orderId} успешно отменён!");

                        LoadPendingOrders();
                        LoadActiveOrders();
                    }
                    catch (Exception ex)
                    {
                        Snackbar.MessageQueue?.Enqueue($"Ошибка при отмене заказа: {ex.Message}");
                    }
                }
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                AssignOrdersNavButton.Tag = null;
                OrderHistoryNavButton.Tag = null;
                StatisticsNavButton.Tag = null;
                clickedButton.Tag = "Active";

                var slideOutAnimation = Resources["SlideOutLeftAnimation"] as Storyboard;
                var slideInAnimation = Resources["SlideInRightAnimation"] as Storyboard;

                if (slideOutAnimation == null || slideInAnimation == null) return;

                ResetPanelState(AssignOrdersPanel);
                ResetPanelState(OrderHistoryPanel);
                ResetPanelState(StatisticsPanel);

                if (clickedButton == AssignOrdersNavButton)
                {
                    SwitchPanel(new[] { OrderHistoryPanel, StatisticsPanel }, AssignOrdersPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Назначить заказы";
                    Title = "TaxiGO - Назначить заказы";
                    LoadPendingOrders();
                }
                else if (clickedButton == OrderHistoryNavButton)
                {
                    SwitchPanel(new[] { AssignOrdersPanel, StatisticsPanel }, OrderHistoryPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "История заказов";
                    Title = "TaxiGO - История заказов";
                    LoadActiveOrders();
                }
                else if (clickedButton == StatisticsNavButton)
                {
                    SwitchPanel(new[] { AssignOrdersPanel, OrderHistoryPanel }, StatisticsPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Статистика";
                    Title = "TaxiGO - Статистика";
                    LoadStatistics(_currentStatisticsPeriod);
                }
            }
        }

        private void ResetPanelState(Grid panel)
        {
            panel.Visibility = Visibility.Hidden;
            if (panel.RenderTransform == null || panel.RenderTransform.GetType() != typeof(TranslateTransform))
                panel.RenderTransform = new TranslateTransform();
            ((TranslateTransform)panel.RenderTransform).X = 0;
            panel.Opacity = 0;
        }

        private void SwitchPanel(Grid[] fromPanels, Grid toPanel, Storyboard slideOutAnimation, Storyboard slideInAnimation)
        {
            var visiblePanel = fromPanels.FirstOrDefault(p => p.Visibility == Visibility.Visible);
            if (visiblePanel != null)
            {
                Storyboard.SetTarget(slideOutAnimation, visiblePanel);
                slideOutAnimation.Completed += (s, ev) =>
                {
                    foreach (var panel in fromPanels)
                    {
                        panel.Visibility = Visibility.Hidden;
                    }
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

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
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
            else if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShakeElement(UIElement element)
        {
            var shakeAnimation = Resources["ShakeAnimation"] as Storyboard;
            if (shakeAnimation != null)
            {
                Storyboard.SetTarget(shakeAnimation, element);
                shakeAnimation.Begin();
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender == SearchTextBox)
            {
                LoadPendingOrders(SearchTextBox.Text, StatusFilterComboBox.SelectedItem?.ToString() ?? "Все");
            }
            else if (sender == ActiveSearchTextBox)
            {
                LoadActiveOrders(ActiveSearchTextBox.Text, ActiveStatusFilterComboBox.SelectedItem?.ToString() ?? "Все");
            }
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == StatusFilterComboBox)
            {
                LoadPendingOrders(SearchTextBox.Text, StatusFilterComboBox.SelectedItem?.ToString() ?? "Все");
            }
            else if (sender == ActiveStatusFilterComboBox)
            {
                LoadActiveOrders(ActiveSearchTextBox.Text, ActiveStatusFilterComboBox.SelectedItem?.ToString() ?? "Все");
            }
        }

        private void StatisticsPeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatisticsPanel.Visibility == Visibility.Visible && sender is ComboBox comboBox && comboBox.SelectedItem is string period)
            {
                _currentStatisticsPeriod = period;
                LoadStatistics(period);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender == RefreshButton)
            {
                SearchTextBox.Text = "";
                StatusFilterComboBox.SelectedIndex = 0;
                LoadPendingOrders();
            }
            else if (sender == ActiveRefreshButton)
            {
                ActiveSearchTextBox.Text = "";
                ActiveStatusFilterComboBox.SelectedIndex = 0;
                LoadActiveOrders();
            }
            else if (sender == StatisticsRefreshButton)
            {
                LoadStatistics(_currentStatisticsPeriod);
            }
        }

        private void PendingOrdersList_ContainerStatusChanged(object? sender, EventArgs e)
        {
            if (PendingOrdersList.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                LoadAvailableDriversForOrders();
            }
        }
    }
}