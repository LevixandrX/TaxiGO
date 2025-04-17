using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using LiveChartsCore;
using MaterialDesignThemes.Wpf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TaxiGO.Models;
using TaxiGO.Services;

namespace TaxiGO
{
    public partial class DriverWindow : Window
    {
        private readonly TaxiGoContext? _context;
        private readonly IServiceScope? _scope;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IGeocodingService _geocodingService;
        private readonly int _userId;
        private readonly string _userName;
        private readonly string _avatarsFolder;
        private Grid? mainGrid;
        private Border? mainBorder;
        private bool _isEditingProfile = false;
        private string _originalName = string.Empty;
        private string _originalPhone = string.Empty;
        private string _originalEmail = string.Empty;
        private string _originalVehicleModel = string.Empty;
        private ObservableCollection<Order> _availableOrders;
        private ObservableCollection<Order> _myOrders;
        private GMapMarker? _startMarker;
        private GMapMarker? _endMarker;
        private System.Windows.Threading.DispatcherTimer _orderTimer;
        private Dictionary<int, TimeSpan> _orderTimeRemaining;
        public ObservableCollection<ISeries> ChartSeries { get; set; } = new ObservableCollection<ISeries>();

        public DriverWindow(string userName, int userId, IServiceScopeFactory scopeFactory, IGeocodingService geocodingService)
        {
            InitializeComponent();
            _scopeFactory = scopeFactory;
            _scope = scopeFactory.CreateScope();
            _context = _scope.ServiceProvider.GetService<TaxiGoContext>() ?? throw new InvalidOperationException("TaxiGoContext не инициализирован.");
            _geocodingService = geocodingService ?? throw new ArgumentNullException(nameof(geocodingService));
            _userId = userId;
            _userName = userName;

            // Инициализация MessageQueue для Snackbar
            Snackbar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

            _avatarsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
            if (!Directory.Exists(_avatarsFolder))
            {
                Directory.CreateDirectory(_avatarsFolder);
            }

            _availableOrders = new ObservableCollection<Order>();
            _myOrders = new ObservableCollection<Order>();
            _orderTimeRemaining = new Dictionary<int, TimeSpan>();

            InitializeMap();
            InitializeMainContent();

            Loaded += DriverWindow_Loaded;
            StateChanged += DriverWindow_StateChanged;
            SizeChanged += DriverWindow_SizeChanged;
            Closing += DriverWindow_Closing;

            // Инициализация таймера
            _orderTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _orderTimer.Tick += OrderTimer_Tick;
            _orderTimer.Start();
        }

        private void UploadAvatar_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Выберите аватар"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string fileExtension = System.IO.Path.GetExtension(openFileDialog.FileName).ToLower();
                    string newFileName = $"{_userId}{fileExtension}";
                    string newFilePath = System.IO.Path.Combine(_avatarsFolder, newFileName);

                    File.Copy(openFileDialog.FileName, newFilePath, true);

                    if (_context?.Users != null)
                    {
                        var user = _context.Users.FirstOrDefault(u => u.UserId == _userId);
                        if (user != null)
                        {
                            if (!string.IsNullOrEmpty(user.AvatarPath) && File.Exists(user.AvatarPath))
                            {
                                try
                                {
                                    File.Delete(user.AvatarPath);
                                }
                                catch { /* Игнорируем ошибки удаления */ }
                            }

                            user.AvatarPath = newFilePath;
                            var log = new Log
                            {
                                UserId = _userId,
                                Action = "Водитель обновил аватар",
                                Timestamp = DateTime.Now
                            };
                            _context.Logs?.Add(log);

                            _context.SaveChanges();

                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(newFilePath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            AvatarImage.Source = bitmap;
                            AvatarPlaceholder.Visibility = Visibility.Collapsed;

                            Snackbar.MessageQueue?.Enqueue("Аватар успешно обновлён!");
                        }
                        else
                        {
                            Snackbar.MessageQueue?.Enqueue("Ошибка: Пользователь не найден.");
                        }
                    }
                    else
                    {
                        Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    }
                }
                catch (Exception ex)
                {
                    Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки аватара: {ex.Message}");
                }
            }
        }

        private void InitializeMap()
        {
            MapControl.MapProvider = OpenStreetMapProvider.Instance;
            MapControl.Position = new PointLatLng(59.9343, 30.3351); // Санкт-Петербург
            MapControl.MinZoom = 5;
            MapControl.MaxZoom = 18;
            MapControl.Zoom = 12;
            MapControl.ShowCenter = false;
            MapControl.MouseWheelZoomType = MouseWheelZoomType.MousePositionAndCenter;
            MapControl.CanDragMap = true;

            _startMarker = new GMapMarker(new PointLatLng(0, 0));
            _endMarker = new GMapMarker(new PointLatLng(0, 0));
        }

        private void InitializeMainContent()
        {
            LoadAvailableOrders();
            LoadMyOrders();
            LoadProfile();
        }

        private void DriverWindow_Loaded(object sender, RoutedEventArgs e)
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

        private void DriverWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayoutForWindowState();
        }

        private void DriverWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateLayoutForWindowState();
            UpdateMaximizeRestoreIcon();
        }

        private void DriverWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _orderTimer?.Stop();
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
                AvailableOrdersScrollViewer.Width = Math.Min(windowWidth * 0.3, 400);

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
                AvailableOrdersScrollViewer.Width = 350;

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

        private FrameworkElement CreateCustomMarker(string iconPath, string toolTip, double width = 50, double height = 50)
        {
            var container = new Grid
            {
                Width = width,
                Height = height,
                ToolTip = toolTip
            };

            var pinShape = new Ellipse
            {
                Width = width - 10,
                Height = height - 10,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(66, 165, 245), 0),
                        new GradientStop(Color.FromRgb(33, 150, 243), 1)
                    }
                },
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            var icon = new PackIcon
            {
                Kind = iconPath == "Start" ? PackIconKind.MapMarker : PackIconKind.FlagCheckered,
                Width = width / 2,
                Height = height / 2,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            container.Children.Add(pinShape);
            container.Children.Add(icon);

            container.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 320,
                ShadowDepth = 3,
                Opacity = 0.5,
                BlurRadius = 10
            };

            var scaleTransform = new ScaleTransform(1, 1);
            container.RenderTransform = scaleTransform;
            container.RenderTransformOrigin = new Point(0.5, 0.5);

            var pulseAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.1,
                Duration = TimeSpan.FromSeconds(1),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);

            var opacityAnimation = new DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(1),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            pinShape.BeginAnimation(Ellipse.OpacityProperty, opacityAnimation);

            container.RenderTransform = new TranslateTransform(-width / 2, -height);
            return container;
        }

        private async void UpdateMapRoute(Order order, GMapControl mapControl)
        {
            if (string.IsNullOrEmpty(order.StartPoint) || string.IsNullOrEmpty(order.EndPoint))
                return;

            try
            {
                var startPoint = await _geocodingService.GeocodeAddressAsync(order.StartPoint);
                var endPoint = await _geocodingService.GeocodeAddressAsync(order.EndPoint);

                if (startPoint == null || endPoint == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Не удалось определить координаты одного из адресов.");
                    return;
                }

                mapControl.Markers.Clear();

                if (_startMarker != null)
                {
                    _startMarker.Position = startPoint.Value;
                    _startMarker.Shape = CreateCustomMarker("Start", "Точка отправления", 50, 50);
                    mapControl.Markers.Add(_startMarker);
                }

                if (_endMarker != null)
                {
                    _endMarker.Position = endPoint.Value;
                    _endMarker.Shape = CreateCustomMarker("End", "Точка назначения", 50, 50);
                    mapControl.Markers.Add(_endMarker);
                }

                var route = OpenStreetMapProvider.Instance.GetRoute(startPoint.Value, endPoint.Value, false, false, 15);
                if (route != null && route.Points.Count > 1)
                {
                    var gRoute = new GMapRoute(route.Points)
                    {
                        ZIndex = -1,
                        Tag = "Route"
                    };

                    if (gRoute.Shape is System.Windows.Shapes.Path path)
                    {
                        path.Stroke = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1),
                            GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(255, 64, 129), 0),
                        new GradientStop(Color.FromRgb(124, 77, 255), 0.5),
                        new GradientStop(Color.FromRgb(0, 229, 255), 1)
                    }
                        };
                        path.StrokeThickness = 7;
                        path.StrokeLineJoin = PenLineJoin.Round;
                        path.Opacity = 0.9;
                        path.Effect = new DropShadowEffect
                        {
                            Color = Colors.Black,
                            BlurRadius = 15,
                            ShadowDepth = 0,
                            Opacity = 0.4
                        };
                        path.ToolTip = $"Маршрут ({route.Points.Count} точек)";

                        var dashAnimation = new DoubleAnimation
                        {
                            From = 0,
                            To = 20,
                            Duration = TimeSpan.FromSeconds(1.5),
                            RepeatBehavior = RepeatBehavior.Forever,
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                        };
                        path.StrokeDashArray = new DoubleCollection { 15, 10 };
                        path.StrokeDashOffset = 0;
                        path.BeginAnimation(System.Windows.Shapes.Path.StrokeDashOffsetProperty, dashAnimation);

                        var glowAnimation = new DoubleAnimation
                        {
                            From = 0.7,
                            To = 1.0,
                            Duration = TimeSpan.FromSeconds(1),
                            AutoReverse = true,
                            RepeatBehavior = RepeatBehavior.Forever,
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                        };
                        path.BeginAnimation(System.Windows.Shapes.Path.OpacityProperty, glowAnimation);
                    }

                    mapControl.Markers.Add(gRoute);
                }
                else
                {
                    var points = new List<PointLatLng> { startPoint.Value, endPoint.Value };
                    var gRoute = new GMapRoute(points)
                    {
                        ZIndex = -1,
                        Tag = "DirectRoute"
                    };

                    if (gRoute.Shape is System.Windows.Shapes.Path path)
                    {
                        path.Stroke = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1),
                            GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(255, 99, 71), 0),
                        new GradientStop(Color.FromRgb(255, 193, 7), 1)
                    }
                        };
                        path.StrokeThickness = 6;
                        path.StrokeLineJoin = PenLineJoin.Round;
                        path.Opacity = 0.85;
                        path.Effect = new DropShadowEffect
                        {
                            Color = Colors.Black,
                            BlurRadius = 12,
                            ShadowDepth = 0,
                            Opacity = 0.3
                        };
                        path.ToolTip = "Прямой маршрут (запасной)";

                        var dashAnimation = new DoubleAnimation
                        {
                            From = 0,
                            To = 15,
                            Duration = TimeSpan.FromSeconds(2),
                            RepeatBehavior = RepeatBehavior.Forever,
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                        };
                        path.StrokeDashArray = new DoubleCollection { 10, 8 };
                        path.StrokeDashOffset = 0;
                        path.BeginAnimation(System.Windows.Shapes.Path.StrokeDashOffsetProperty, dashAnimation);
                    }

                    mapControl.Markers.Add(gRoute);
                }

                mapControl.ZoomAndCenterMarkers(null);
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка построения маршрута: {ex.Message}");
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                if (AvailableOrdersPanel.Visibility == Visibility.Visible)
                {
                    ResetAvailableOrdersTab();
                }
                else if (MyOrdersPanel.Visibility == Visibility.Visible)
                {
                    ResetMyOrdersTab();
                }
                else if (ProfilePanel.Visibility == Visibility.Visible)
                {
                    ResetProfileTab(sender, e);
                }

                AvailableOrdersNavButton.Tag = null;
                MyOrdersNavButton.Tag = null;
                ProfileNavButton.Tag = null;
                clickedButton.Tag = "Active";

                var slideOutAnimation = Resources["SlideOutLeftAnimation"] as Storyboard;
                var slideInAnimation = Resources["SlideInRightAnimation"] as Storyboard;

                if (slideOutAnimation == null || slideInAnimation == null) return;

                ResetPanelState(AvailableOrdersPanel);
                ResetPanelState(MyOrdersPanel);
                ResetPanelState(ProfilePanel);

                if (clickedButton == AvailableOrdersNavButton)
                {
                    SwitchPanel(MyOrdersPanel, ProfilePanel, AvailableOrdersPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Доступные заказы";
                    Title = "TaxiGO - Доступные заказы";
                    LoadAvailableOrders();
                }
                else if (clickedButton == MyOrdersNavButton)
                {
                    SwitchPanel(AvailableOrdersPanel, ProfilePanel, MyOrdersPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Мои заказы";
                    Title = "TaxiGO - Мои заказы";
                    LoadMyOrders();
                }
                else if (clickedButton == ProfileNavButton)
                {
                    SwitchPanel(AvailableOrdersPanel, MyOrdersPanel, ProfilePanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Профиль";
                    Title = "TaxiGO - Профиль";
                    LoadProfile();
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

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            if (MapControl.Zoom < MapControl.MaxZoom)
            {
                MapControl.Zoom++;
            }
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (MapControl.Zoom > MapControl.MinZoom)
            {
                MapControl.Zoom--;
            }
        }

        private void ClearMapButton_Click(object sender, RoutedEventArgs e)
        {
            MapControl.Markers.Clear();
            _startMarker = new GMapMarker(new PointLatLng(0, 0));
            _endMarker = new GMapMarker(new PointLatLng(0, 0));
            MapControl.Position = new PointLatLng(59.9343, 30.3351);
            MapControl.Zoom = 12;
        }

        private async void AcceptOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                var order = _context.Orders?.Include(o => o.Client).FirstOrDefault(o => o.OrderId == orderId);
                if (order == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Заказ не найден.");
                    return;
                }

                if (order.Status != "Pending")
                {
                    Snackbar.MessageQueue?.Enqueue("Этот заказ уже обработан.");
                    return;
                }

                var driverAvailability = _context.DriverAvailabilities?.FirstOrDefault(da => da.DriverId == _userId);
                if (driverAvailability == null || !driverAvailability.IsAvailable)
                {
                    Snackbar.MessageQueue?.Enqueue("Вы не доступны для принятия заказов.");
                    return;
                }

                order.DriverId = _userId;
                order.Status = "Accepted";
                var statusHistory = new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    Status = "Accepted",
                    ChangeTime = DateTime.Now,
                    ChangedByUserId = _userId
                };
                _context.OrderStatusHistories?.Add(statusHistory);

                var log = new Log
                {
                    UserId = _userId,
                    Action = $"Водитель принял заказ #{order.OrderId}",
                    Timestamp = DateTime.Now
                };
                _context.Logs?.Add(log);

                await _context.SaveChangesAsync();
                Snackbar.MessageQueue?.Enqueue("Заказ успешно принят!");
                LoadAvailableOrders();
                LoadMyOrders();
            }
        }

        private async void CompleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                var order = _context.Orders?.Include(o => o.Client).FirstOrDefault(o => o.OrderId == orderId);
                if (order == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Заказ не найден.");
                    return;
                }

                if (order.Status == "Completed")
                {
                    Snackbar.MessageQueue?.Enqueue("Этот заказ уже завершён!");
                    return;
                }

                if (order.Status != "Accepted")
                {
                    Snackbar.MessageQueue?.Enqueue("Заказ не принят для завершения.");
                    return;
                }

                order.Status = "Completed";
                order.OrderCompletionTime = DateTime.Now;
                var statusHistory = new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    Status = "Completed",
                    ChangeTime = DateTime.Now,
                    ChangedByUserId = _userId
                };
                _context.OrderStatusHistories?.Add(statusHistory);

                var log = new Log
                {
                    UserId = _userId,
                    Action = $"Водитель завершил заказ #{order.OrderId}",
                    Timestamp = DateTime.Now
                };
                _context.Logs?.Add(log);

                await _context.SaveChangesAsync();
                Snackbar.MessageQueue?.Enqueue("Заказ успешно завершён!");
                LoadMyOrders();
            }
        }

        private async void CancelOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                var result = MessageBox.Show("Вы уверены, что хотите отменить заказ?", "Подтверждение отмены", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                var order = _context.Orders?.Include(o => o.Client).FirstOrDefault(o => o.OrderId == orderId);
                if (order == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Заказ не найден.");
                    return;
                }

                if (order.Status != "Accepted")
                {
                    Snackbar.MessageQueue?.Enqueue("Этот заказ нельзя отменить.");
                    return;
                }

                order.Status = "Pending";
                order.DriverId = null;
                var statusHistory = new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    Status = "Pending",
                    ChangeTime = DateTime.Now,
                    ChangedByUserId = _userId
                };
                _context.OrderStatusHistories?.Add(statusHistory);

                var log = new Log
                {
                    UserId = _userId,
                    Action = $"Водитель отменил заказ #{order.OrderId}",
                    Timestamp = DateTime.Now
                };
                _context.Logs?.Add(log);

                await _context.SaveChangesAsync();
                Snackbar.MessageQueue?.Enqueue("Заказ успешно отменён!");
                LoadMyOrders();
                LoadAvailableOrders();
            }
        }

        private void RateClient_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                var order = _context.Orders?.Include(o => o.Client).FirstOrDefault(o => o.OrderId == orderId);
                if (order == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Заказ не найден.");
                    return;
                }

                if (order.Status != "Completed")
                {
                    Snackbar.MessageQueue?.Enqueue("Оценить клиента можно только после завершения заказа.");
                    return;
                }

                if (order.ClientRating.HasValue)
                {
                    Snackbar.MessageQueue?.Enqueue("Клиент уже оценён.");
                    return;
                }

                // Открываем окно RateClientWindow
                var ratingWindow = new RateClientWindow(_context, order)
                {
                    Owner = this
                };

                ratingWindow.ShowDialog();
                LoadMyOrders();
            }
        }

        private void OrderTimer_Tick(object? sender, EventArgs e)
        {
            if (_context?.Orders == null) return;

            foreach (var order in _availableOrders.Where(o => o.Status == "Pending"))
            {
                if (order.OrderTime == null) continue;

                if (!_orderTimeRemaining.ContainsKey(order.OrderId))
                {
                    var timeElapsed = DateTime.Now - order.OrderTime.Value;
                    var timeRemaining = TimeSpan.FromMinutes(20) - timeElapsed;
                    _orderTimeRemaining[order.OrderId] = timeRemaining > TimeSpan.Zero ? timeRemaining : TimeSpan.Zero;
                }

                if (_orderTimeRemaining[order.OrderId] > TimeSpan.Zero)
                {
                    _orderTimeRemaining[order.OrderId] = _orderTimeRemaining[order.OrderId].Subtract(TimeSpan.FromSeconds(1));
                    order.TimeRemaining = _orderTimeRemaining[order.OrderId].ToString(@"mm\:ss");
                    order.TimerExpired = false;
                }
                else
                {
                    order.TimeRemaining = "00:00";
                    order.TimerExpired = true;
                    if (order.Status == "Pending")
                    {
                        order.Status = "Canceled";
                        order.OrderCompletionTime = DateTime.Now;

                        var statusHistory = new OrderStatusHistory
                        {
                            OrderId = order.OrderId,
                            Status = "Canceled",
                            ChangeTime = DateTime.Now,
                            ChangedByUserId = _userId
                        };
                        _context.OrderStatusHistories?.Add(statusHistory);

                        _context.SaveChanges();
                        Snackbar.MessageQueue?.Enqueue($"Заказ #{order.OrderId} отменён: время ожидания истекло.");
                    }
                }
            }
        }

        private void LoadAvailableOrders()
        {
            if (_context?.Orders == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки доступных заказов.");
                return;
            }

            try
            {
                var orders = _context.Orders
                    .Include(o => o.Client)
                    .Include(o => o.Tariff)
                    .Where(o => o.Status == "Pending" && o.DriverId == null)
                    .OrderBy(o => o.OrderTime)
                    .ToList();

                _availableOrders.Clear();
                foreach (var order in orders)
                {
                    if (order.OrderTime.HasValue)
                    {
                        TimeSpan timeSinceOrder = DateTime.Now - order.OrderTime.Value;
                        TimeSpan maxWaitTime = TimeSpan.FromMinutes(20);

                        if (timeSinceOrder < maxWaitTime)
                        {
                            TimeSpan remaining = maxWaitTime - timeSinceOrder;
                            order.TimeRemaining = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                            order.TimerExpired = false;

                            if (!_orderTimeRemaining.ContainsKey(order.OrderId))
                            {
                                _orderTimeRemaining[order.OrderId] = remaining;
                            }
                        }
                        else
                        {
                            order.TimeRemaining = "00:00";
                            order.TimerExpired = true;
                        }
                    }
                    else
                    {
                        order.TimeRemaining = "—";
                        order.TimerExpired = false;
                    }

                    _availableOrders.Add(order);
                    UpdateMapRoute(order, MapControl);
                }

                AvailableOrdersList.ItemsSource = _availableOrders;
                AvailableOrdersScrollViewer.ScrollToTop();
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки доступных заказов: {ex.Message}");
            }
        }

        private void LoadMyOrders()
        {
            if (_context?.Orders == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки ваших заказов.");
                return;
            }

            try
            {
                var orders = _context.Orders
                    .Include(o => o.Client)
                    .Include(o => o.Tariff)
                    .Include(o => o.OrderStatusHistories)
                    .Where(o => o.DriverId == _userId ||
                               o.OrderStatusHistories.Any(h => h.Status == "Canceled" && h.ChangedByUserId == _userId))
                    .OrderByDescending(o => o.OrderTime)
                    .ToList();

                _myOrders.Clear();
                foreach (var order in orders)
                {
                    // Устанавливаем флаг, можно ли оценивать клиента
                    order.CanRateClient = order.Status == "Completed" && !order.ClientRating.HasValue;
                    _myOrders.Add(order);
                }

                MyOrdersList.ItemsSource = _myOrders;
                MyOrdersScrollViewer.ScrollToTop();
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки ваших заказов: {ex.Message}");
            }
        }

        private void LoadProfile()
        {
            if (_context?.Users == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки профиля.");
                return;
            }

            try
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == _userId);
                var vehicle = _context.Vehicles?.FirstOrDefault(v => v.DriverId == _userId);

                if (user != null)
                {
                    ProfileHeader.Text = user.Name;
                    ProfileName.Text = user.Name;
                    ProfilePhone.Text = user.Phone;
                    ProfileVehicle.Text = vehicle?.Model ?? "Не назначена";
                    ProfileRegistrationDate.Text = user.RegistrationDate.HasValue
                        ? user.RegistrationDate.Value.ToString("dd.MM.yyyy")
                        : "Не указана";

                    _originalName = ProfileName.Text;
                    _originalPhone = ProfilePhone.Text;
                    _originalVehicleModel = ProfileVehicle.Text;

                    if (!string.IsNullOrEmpty(user.AvatarPath) && File.Exists(user.AvatarPath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(user.AvatarPath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            AvatarImage.Source = bitmap;
                            AvatarPlaceholder.Visibility = Visibility.Collapsed;
                        }
                        catch
                        {
                            AvatarImage.Source = null;
                            AvatarPlaceholder.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        AvatarImage.Source = null;
                        AvatarPlaceholder.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Пользователь не найден.");
                }
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки профиля: {ex.Message}");
            }
        }

        private async void AvailabilityToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_context == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            try
            {
                var availability = _context.DriverAvailabilities?.FirstOrDefault(da => da.DriverId == _userId);
                if (availability == null)
                {
                    availability = new DriverAvailability
                    {
                        DriverId = _userId,
                        IsAvailable = true,
                        LastUpdate = DateTime.Now
                    };
                    _context.DriverAvailabilities?.Add(availability);
                }
                else
                {
                    availability.IsAvailable = true;
                    availability.LastUpdate = DateTime.Now;
                }

                var log = new Log
                {
                    UserId = _userId,
                    Action = "Водитель стал доступен",
                    Timestamp = DateTime.Now
                };
                _context.Logs?.Add(log);

                await _context.SaveChangesAsync();
                Snackbar.MessageQueue?.Enqueue("Статус изменён: вы доступны для заказов.");
                LoadAvailableOrders();
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка изменения статуса: {ex.Message}");
                AvailabilityToggle.IsChecked = false;
            }
        }

        private async void AvailabilityToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_context == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            try
            {
                var availability = _context.DriverAvailabilities?.FirstOrDefault(da => da.DriverId == _userId);
                if (availability != null)
                {
                    availability.IsAvailable = false;
                    availability.LastUpdate = DateTime.Now;

                    var log = new Log
                    {
                        UserId = _userId,
                        Action = "Водитель стал недоступен",
                        Timestamp = DateTime.Now
                    };
                    _context.Logs?.Add(log);

                    await _context.SaveChangesAsync();
                    Snackbar.MessageQueue?.Enqueue("Статус изменён: вы недоступны для заказов.");
                    _availableOrders.Clear();
                    AvailableOrdersList.ItemsSource = _availableOrders;
                }
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка изменения статуса: {ex.Message}");
                AvailabilityToggle.IsChecked = true;
            }
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditingProfile)
            {
                _isEditingProfile = true;
                SaveEditButton.Visibility = Visibility.Visible;
                CancelEditButton.Visibility = Visibility.Visible;
                EditProfileButton.Visibility = Visibility.Collapsed;
                ResetPasswordButton.Visibility = Visibility.Collapsed;

                _originalName = ProfileName.Text;
                _originalPhone = ProfilePhone.Text;
                _originalVehicleModel = ProfileVehicle.Text;

                ProfileName.IsReadOnly = false;
                ProfilePhone.IsReadOnly = false;
                ProfileVehicle.IsReadOnly = false;

                Snackbar.MessageQueue?.Enqueue("Редактирование профиля включено.");
            }
        }

        private async void SaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditingProfile) return;

            string name = ProfileName.Text.Trim();
            string phone = ProfilePhone.Text.Trim();
            string vehicleModel = ProfileVehicle.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                Snackbar.MessageQueue?.Enqueue("Имя не может быть пустым!");
                ShakeElement(ProfileName);
                return;
            }

            if (!Regex.IsMatch(phone, @"^\+?\d{10,15}$"))
            {
                Snackbar.MessageQueue?.Enqueue("Введите корректный номер телефона!");
                ShakeElement(ProfilePhone);
                return;
            }

            if (string.IsNullOrEmpty(vehicleModel))
            {
                Snackbar.MessageQueue?.Enqueue("Модель автомобиля не может быть пустой!");
                ShakeElement(ProfileVehicle);
                return;
            }

            if (_context?.Users == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            try
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == _userId);
                var vehicle = _context.Vehicles?.FirstOrDefault(v => v.DriverId == _userId);

                if (user != null)
                {
                    user.Name = name;
                    user.Phone = phone;

                    if (vehicle != null)
                    {
                        vehicle.Model = vehicleModel;
                    }
                    else
                    {
                        vehicle = new Vehicle
                        {
                            DriverId = _userId,
                            Model = vehicleModel,
                            LicensePlate = "Не указан",
                            Type = "Не указан",
                            Status = "Available"
                        };
                        _context.Vehicles?.Add(vehicle);
                    }

                    var log = new Log
                    {
                        UserId = _userId,
                        Action = "Водитель обновил профиль",
                        Timestamp = DateTime.Now
                    };
                    _context.Logs?.Add(log);

                    await _context.SaveChangesAsync();

                    ProfileHeader.Text = name;
                    _isEditingProfile = false;
                    SaveEditButton.Visibility = Visibility.Collapsed;
                    CancelEditButton.Visibility = Visibility.Collapsed;
                    EditProfileButton.Visibility = Visibility.Visible;
                    ResetPasswordButton.Visibility = Visibility.Visible;

                    ProfileName.IsReadOnly = true;
                    ProfilePhone.IsReadOnly = true;
                    ProfileVehicle.IsReadOnly = true;

                    Snackbar.MessageQueue?.Enqueue("Профиль успешно обновлён!");
                }
                else
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Пользователь не найден.");
                }
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка сохранения профиля: {ex.Message}");
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingProfile)
            {
                ProfileName.Text = _originalName;
                ProfilePhone.Text = _originalPhone;
                ProfileVehicle.Text = _originalVehicleModel;

                _isEditingProfile = false;
                SaveEditButton.Visibility = Visibility.Collapsed;
                CancelEditButton.Visibility = Visibility.Collapsed;
                EditProfileButton.Visibility = Visibility.Visible;
                ResetPasswordButton.Visibility = Visibility.Visible;

                ProfileName.IsReadOnly = true;
                ProfilePhone.IsReadOnly = true;
                ProfileVehicle.IsReadOnly = true;

                Snackbar.MessageQueue?.Enqueue("Изменения отменены.");
            }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (_context?.Users != null)
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == _userId);
                if (user == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Пользователь не найден.");
                    return;
                }

                var resetPasswordWindow = new ResetPasswordWindow(_context, user)
                {
                    Owner = this
                };
                if (resetPasswordWindow.ShowDialog() == true)
                {
                    Snackbar.MessageQueue?.Enqueue("Пароль успешно сброшен!");
                }
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
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
            if (e.ChangedButton == MouseButton.Left && !_isEditingProfile)
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

        private void ResetAvailableOrdersTab()
        {
            ClearMapButton_Click(this, new RoutedEventArgs());
            LoadAvailableOrders();
            AvailableOrdersScrollViewer.ScrollToTop();
        }

        private void ResetMyOrdersTab()
        {
            LoadMyOrders();
            MyOrdersScrollViewer.ScrollToTop();
        }

        private void ResetProfileTab(object sender, RoutedEventArgs e)
        {
            if (_isEditingProfile)
            {
                CancelEdit_Click(sender, e);
            }
            LoadProfile();
            ProfileScrollViewer.ScrollToTop();
        }

        private void ShakeElement(Control element)
        {
            var shakeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 5,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            var transform = new TranslateTransform();
            element.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty, shakeAnimation);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}