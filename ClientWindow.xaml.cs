using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using BCrypt.Net;
using TaxiGO.Models;
using Microsoft.EntityFrameworkCore;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using System.Windows.Shapes;
using System.Collections.Generic;
using TaxiGO.Services;
using System.Collections.ObjectModel;

namespace TaxiGO
{
    public partial class ClientWindow : Window
    {
        private readonly TaxiGoContext? _context;
        private readonly IServiceScope? _scope;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IGeocodingService _geocodingService;
        private readonly int _userId;
        private Grid? mainGrid;
        private Border? mainBorder;
        private string _userName;
        private bool _isEditingProfile = false;
        private readonly string _avatarsFolder;
        private string _originalName = string.Empty;
        private string _originalPhone = string.Empty;
        private string _originalEmail = string.Empty;
        private string _originalAddress = string.Empty;
        private Order? _currentOrder;
        private decimal _calculatedCost;
        private GMapMarker? _startMarker;
        private GMapMarker? _endMarker;
        private bool _isSelectingStartPoint = false;
        private bool _isSelectingEndPoint = false;
        private double _distanceKm;
        private System.Windows.Threading.DispatcherTimer _orderTimer;
        private Dictionary<int, TimeSpan> _orderTimeRemaining;
        private ObservableCollection<Order> _orders;

        public ClientWindow(string userName, int userId, IServiceScopeFactory scopeFactory, IGeocodingService geocodingService)
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

            InitializeMainContent();
            InitializeMap();

            Loaded += ClientWindow_Loaded;
            StateChanged += ClientWindow_StateChanged;
            SizeChanged += ClientWindow_SizeChanged;
            Closing += ClientWindow_Closing;

            // Инициализация таймера
            _orderTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _orderTimer.Tick += OrderTimer_Tick;
            _orderTimeRemaining = new Dictionary<int, TimeSpan>();
            _orders = new ObservableCollection<Order>(); // Инициализация коллекции
            _orderTimer.Start();
        }

        private void InitializeMap()
        {
            MapControl.MapProvider = OpenStreetMapProvider.Instance;
            MapControl.Position = new PointLatLng(59.9343, 30.3351); // Санкт-Петербург по умолчанию
            MapControl.MinZoom = 5;
            MapControl.MaxZoom = 18;
            MapControl.Zoom = 12;
            MapControl.ShowCenter = false;
            MapControl.MouseWheelZoomType = MouseWheelZoomType.MousePositionAndCenter;
            MapControl.CanDragMap = true;

            _startMarker = new GMapMarker(new PointLatLng(0, 0));
            _endMarker = new GMapMarker(new PointLatLng(0, 0));
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

            var icon = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = iconPath == "Start" ? MaterialDesignThemes.Wpf.PackIconKind.MapMarker : MaterialDesignThemes.Wpf.PackIconKind.FlagCheckered,
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

        private async void UpdateMapRoute()
        {
            string startAddress = StartPoint.Text.Trim();
            string endAddress = EndPoint.Text.Trim();

            if (string.IsNullOrEmpty(startAddress) || string.IsNullOrEmpty(endAddress))
            {
                return;
            }

            try
            {
                var startPoint = await _geocodingService.GeocodeAddressAsync(startAddress);
                var endPoint = await _geocodingService.GeocodeAddressAsync(endAddress);

                if (startPoint == null || endPoint == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Не удалось определить координаты одного из адресов.");
                    return;
                }

                MapControl.Markers.Clear();

                if (_startMarker != null)
                {
                    _startMarker.Position = startPoint.Value;
                    _startMarker.Shape = CreateCustomMarker("Start", "Точка отправления", 50, 50);
                    MapControl.Markers.Add(_startMarker);
                }

                if (_endMarker != null)
                {
                    _endMarker.Position = endPoint.Value;
                    _endMarker.Shape = CreateCustomMarker("End", "Точка назначения", 50, 50);
                    MapControl.Markers.Add(_endMarker);
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

                    MapControl.Markers.Add(gRoute);
                    _distanceKm = route.Distance; // Сохраняем расстояние в поле
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

                    MapControl.Markers.Add(gRoute);
                    _distanceKm = CalculateDirectDistance(startPoint.Value, endPoint.Value); // Сохраняем расстояние в поле
                    Snackbar.MessageQueue?.Enqueue($"Не удалось построить маршрут. Использовано прямое расстояние: {_distanceKm:F2} км.");
                }

                DistanceTextBlock.Text = $"Расстояние: {_distanceKm:F2} км"; // Обновляем текстовое поле
                MapControl.ZoomAndCenterMarkers(null);
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка построения маршрута: {ex.Message}");
                _distanceKm = 0; // Сбрасываем расстояние в случае ошибки
                DistanceTextBlock.Text = "Расстояние: 0 км";
            }
        }

        private void ClientWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _scope?.Dispose();
        }

        private void InitializeMainContent()
        {
            if (_context?.Tariffs != null)
            {
                TariffCombo.ItemsSource = _context.Tariffs.ToList();
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки тарифов.");
            }

            if (_context?.PaymentMethods != null)
            {
                var paymentMethods = _context.PaymentMethods
                    .Where(pm => pm.IsActive)
                    .ToList();
                PaymentMethodCombo.ItemsSource = paymentMethods;
                PaymentMethodCombo.DisplayMemberPath = "MethodName";
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки способов оплаты.");
            }

            LoadProfile();
        }

        private void ClientWindow_Loaded(object sender, RoutedEventArgs e)
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
            LoadOrderHistory();
        }

        private void ClientWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayoutForWindowState();
        }

        private void ClientWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateLayoutForWindowState();
            UpdateMaximizeRestoreIcon();
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
                OrderFormPanel.Width = Math.Min(windowWidth * 0.3, 400);

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
                OrderFormPanel.Width = 350;

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
                if (OrderTaxiPanel.Visibility == Visibility.Visible)
                {
                    ResetOrderTaxiTab(sender, e);
                }
                else if (OrderHistoryPanel.Visibility == Visibility.Visible)
                {
                    ResetOrderHistoryTab();
                }
                else if (ProfilePanel.Visibility == Visibility.Visible)
                {
                    ResetProfileTab(sender, e);
                }

                OrderTaxiNavButton.Tag = null;
                OrderHistoryNavButton.Tag = null;
                ProfileNavButton.Tag = null;
                clickedButton.Tag = "Active";

                var slideOutAnimation = Resources["SlideOutLeftAnimation"] as Storyboard;
                var slideInAnimation = Resources["SlideInRightAnimation"] as Storyboard;

                if (slideOutAnimation == null || slideInAnimation == null) return;

                ResetPanelState(OrderTaxiPanel);
                ResetPanelState(OrderHistoryPanel);
                ResetPanelState(ProfilePanel);

                if (clickedButton == OrderTaxiNavButton)
                {
                    SwitchPanel(OrderHistoryPanel, ProfilePanel, OrderTaxiPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "Заказать такси";
                    Title = "TaxiGO - Заказать такси";
                }
                else if (clickedButton == OrderHistoryNavButton)
                {
                    SwitchPanel(OrderTaxiPanel, ProfilePanel, OrderHistoryPanel, slideOutAnimation, slideInAnimation);
                    WelcomeText.Text = "История заказов";
                    Title = "TaxiGO - История заказов";
                    LoadOrderHistory();
                }
                else if (clickedButton == ProfileNavButton)
                {
                    SwitchPanel(OrderTaxiPanel, OrderHistoryPanel, ProfilePanel, slideOutAnimation, slideInAnimation);
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
            StartPoint.Text = string.Empty;
            EndPoint.Text = string.Empty;
            DistanceTextBlock.Text = "Расстояние: 0 км";
            _distanceKm = 0;
            CostTextBlock.Text = "Стоимость: 0 ₽";
            _calculatedCost = 0m;
            _isSelectingStartPoint = false;
            _isSelectingEndPoint = false;
            TariffCombo.SelectedItem = null;
            PaymentMethodCombo.SelectedItem = null;
            PromoCodeTextBox.Text = string.Empty;
            PromoCodeInfoTextBlock.Text = "";
            PromoCodeInfoTextBlock.Visibility = Visibility.Collapsed;
            _currentOrder = null;
        }

        private async void MapControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(MapControl);
            var latLng = MapControl.FromLocalToLatLng((int)position.X, (int)position.Y);

            if (_isSelectingStartPoint)
            {
                if (_startMarker == null)
                {
                    _startMarker = new GMapMarker(new PointLatLng(0, 0));
                }

                _startMarker.Position = latLng;
                MapControl.Markers.Remove(_startMarker);
                _startMarker.Shape = CreateCustomMarker("Start", "Точка отправления", 50, 50);
                MapControl.Markers.Add(_startMarker);
                var address = await _geocodingService.ReverseGeocodeAsync(latLng);
                StartPoint.Text = address ?? "Неизвестный адрес";
                _isSelectingStartPoint = false;

                if (_endMarker != null && _endMarker.Position.Lat != 0 && _endMarker.Position.Lng != 0)
                {
                    UpdateMapRouteAfterSelection();
                }
            }
            else if (_isSelectingEndPoint)
            {
                if (_endMarker == null)
                {
                    _endMarker = new GMapMarker(new PointLatLng(0, 0));
                }

                _endMarker.Position = latLng;
                MapControl.Markers.Remove(_endMarker);
                _endMarker.Shape = CreateCustomMarker("End", "Точка назначения", 50, 50);
                MapControl.Markers.Add(_endMarker);
                var address = await _geocodingService.ReverseGeocodeAsync(latLng);
                EndPoint.Text = address ?? "Неизвестный адрес";
                _isSelectingEndPoint = false;

                if (_startMarker != null && _startMarker.Position.Lat != 0 && _startMarker.Position.Lng != 0)
                {
                    UpdateMapRouteAfterSelection();
                }
            }
        }

        private void UpdateMapRouteAfterSelection()
        {
            if (_startMarker == null || _endMarker == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: Маркеры не инициализированы.");
                return;
            }

            try
            {
                MapControl.Markers.Clear();
                MapControl.Markers.Add(_startMarker);
                MapControl.Markers.Add(_endMarker);

                var route = OpenStreetMapProvider.Instance.GetRoute(_startMarker.Position, _endMarker.Position, false, false, 15);
                if (route != null && route.Points.Count > 1)
                {
                    var gRoute = new GMapRoute(route.Points)
                    {
                        ZIndex = -1,
                        Tag = "Route"
                    };

                    if (gRoute.Shape is System.Windows.Shapes.Path path)
                    {
                        path.Stroke = Brushes.Blue;
                        path.StrokeThickness = 4;
                        path.ToolTip = "Маршрут";
                    }

                    MapControl.Markers.Add(gRoute);

                    _distanceKm = route.Distance; // Сохраняем расстояние в поле
                }
                else
                {
                    // Запасной вариант: рисуем прямую линию
                    var points = new List<PointLatLng> { _startMarker.Position, _endMarker.Position };
                    var gRoute = new GMapRoute(points)
                    {
                        ZIndex = -1,
                        Tag = "DirectRoute"
                    };

                    if (gRoute.Shape is System.Windows.Shapes.Path path)
                    {
                        path.Stroke = Brushes.Red;
                        path.StrokeThickness = 4;
                        path.ToolTip = "Прямой маршрут (запасной)";
                    }

                    MapControl.Markers.Add(gRoute);

                    _distanceKm = CalculateDirectDistance(_startMarker.Position, _endMarker.Position); // Сохраняем расстояние в поле
                    Snackbar.MessageQueue?.Enqueue($"Не удалось построить маршрут между выбранными точками. Использовано прямое расстояние: {_distanceKm:F2} км.");
                }

                DistanceTextBlock.Text = $"Расстояние: {_distanceKm:F2} км"; // Обновляем текстовое поле
                MapControl.ZoomAndCenterMarkers(null);
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка построения маршрута: {ex.Message}");
                _distanceKm = 0; // Сбрасываем расстояние в случае ошибки
                DistanceTextBlock.Text = "Расстояние: 0 км";
            }
        }

        private double CalculateDirectDistance(PointLatLng point1, PointLatLng point2)
        {
            const double R = 6371; // Радиус Земли в километрах
            double lat1 = point1.Lat * Math.PI / 180;
            double lat2 = point2.Lat * Math.PI / 180;
            double deltaLat = (point2.Lat - point1.Lat) * Math.PI / 180;
            double deltaLon = (point2.Lng - point1.Lng) * Math.PI / 180;

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c; // Расстояние в километрах
        }

        private void SelectStartPointButton_Click(object sender, RoutedEventArgs e)
        {
            _isSelectingStartPoint = true;
            _isSelectingEndPoint = false;
            Snackbar.MessageQueue?.Enqueue("Выберите точку отправления на карте.");
        }

        private void SelectEndPointButton_Click(object sender, RoutedEventArgs e)
        {
            _isSelectingEndPoint = true;
            _isSelectingStartPoint = false;
            Snackbar.MessageQueue?.Enqueue("Выберите точку назначения на карте.");
        }

        private void StartPoint_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateMapRoute();
        }

        private void EndPoint_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateMapRoute();
        }

        private void TariffCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateCost_Click(sender, e);
        }

        private void CalculateCost_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(StartPoint.Text) || string.IsNullOrEmpty(EndPoint.Text) || TariffCombo.SelectedItem == null)
            {
                Snackbar.MessageQueue?.Enqueue("Заполните все обязательные поля!");
                ShakeElement(StartPoint);
                ShakeElement(EndPoint);
                ShakeElement(TariffCombo);
                return;
            }

            if (_distanceKm <= 0)
            {
                Snackbar.MessageQueue?.Enqueue("Необходимо построить маршрут для расчета расстояния!");
                return;
            }

            if (_distanceKm > 500)
            {
                Snackbar.MessageQueue?.Enqueue("Расстояние слишком большое! Максимально допустимое расстояние — 500 км.");
                return;
            }

            if (TariffCombo.SelectedItem is Tariff selectedTariff)
            {
                decimal baseCost = selectedTariff.BasePrice + (selectedTariff.PricePerKm * (decimal)_distanceKm);
                _calculatedCost = baseCost;

                if (_currentOrder?.PromoCode != null)
                {
                    decimal discount = _calculatedCost * ((decimal)_currentOrder.PromoCode.DiscountPercent / 100m);
                    _calculatedCost -= discount;
                    // Убедимся, что информация о промокоде отображается
                    PromoCodeInfoTextBlock.Text = $"Промокод '{_currentOrder.PromoCode.Code}' применён (-{_currentOrder.PromoCode.DiscountPercent}%)";
                    PromoCodeInfoTextBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    // Если промокод не применён, скрываем информацию
                    PromoCodeInfoTextBlock.Text = "";
                    PromoCodeInfoTextBlock.Visibility = Visibility.Collapsed;
                }

                CostTextBlock.Text = $"Стоимость: {Math.Round(_calculatedCost, 2)} ₽";
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: Выберите тариф!");
            }
        }

        private void ApplyPromoCode_Click(object sender, RoutedEventArgs e)
        {
            string promoCode = PromoCodeTextBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(promoCode))
            {
                Snackbar.MessageQueue?.Enqueue("Введите промокод!");
                ShakeElement(PromoCodeTextBox);
                return;
            }

            if (_context == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                return;
            }

            var promo = _context.PromoCodes?.FirstOrDefault(p => p.Code == promoCode && p.IsActive && p.ExpiryDate > DateTime.Now);
            if (promo == null)
            {
                Snackbar.MessageQueue?.Enqueue("Промокод недействителен или истёк!");
                ShakeElement(PromoCodeTextBox);
                return;
            }

            if (_currentOrder == null)
            {
                _currentOrder = new Order();
            }
            _currentOrder.PromoCodeId = promo.PromoCodeId;
            _currentOrder.PromoCode = promo;

            // Показываем информацию о промокоде рядом с ценой
            PromoCodeInfoTextBlock.Text = $"Промокод '{promoCode}' применён (-{promo.DiscountPercent}%)";
            PromoCodeInfoTextBlock.Visibility = Visibility.Visible;

            Snackbar.MessageQueue?.Enqueue($"Промокод {promoCode} применён! Скидка: {promo.DiscountPercent}%");
            CalculateCost_Click(sender, e);
        }

        private void OrderTaxi_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(StartPoint.Text) || string.IsNullOrEmpty(EndPoint.Text) || TariffCombo.SelectedItem == null)
            {
                Snackbar.MessageQueue?.Enqueue("Заполните все обязательные поля!");
                ShakeElement(StartPoint);
                ShakeElement(EndPoint);
                ShakeElement(TariffCombo);
                return;
            }

            if (PaymentMethodCombo.SelectedItem == null)
            {
                Snackbar.MessageQueue?.Enqueue("Выберите способ оплаты!");
                ShakeElement(PaymentMethodCombo);
                return;
            }

            if (_distanceKm <= 0)
            {
                Snackbar.MessageQueue?.Enqueue("Необходимо построить маршрут для расчета расстояния!");
                return;
            }

            if (_distanceKm > 500)
            {
                Snackbar.MessageQueue?.Enqueue("Расстояние слишком большое! Максимально допустимое расстояние — 500 км.");
                return;
            }

            if (TariffCombo.SelectedItem is Tariff selectedTariff)
            {
                int? promoCodeId = _currentOrder?.PromoCodeId;
                PromoCode? promoCode = _currentOrder?.PromoCode;

                _currentOrder = new Order
                {
                    ClientId = _userId,
                    StartPoint = StartPoint.Text,
                    EndPoint = EndPoint.Text,
                    TariffId = selectedTariff.TariffId,
                    Tariff = selectedTariff,
                    Status = "Pending",
                    OrderTime = DateTime.Now,
                    DistanceKm = (decimal)_distanceKm,
                    Cost = _calculatedCost
                };

                if (promoCodeId.HasValue)
                {
                    _currentOrder.PromoCodeId = promoCodeId;
                    _currentOrder.PromoCode = promoCode;
                }

                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                try
                {
                    // Сначала добавляем заказ в таблицу Orders и сохраняем, чтобы получить OrderId
                    _context.Orders?.Add(_currentOrder);
                    _context.SaveChanges();

                    // Теперь, когда у _currentOrder есть сгенерированный OrderId, создаём запись в OrderStatusHistories
                    var statusHistory = new OrderStatusHistory
                    {
                        OrderId = _currentOrder.OrderId, // Теперь OrderId корректный
                        Status = "Pending",
                        ChangeTime = DateTime.Now,
                        ChangedByUserId = _userId
                    };
                    _context.OrderStatusHistories?.Add(statusHistory);
                    _context.SaveChanges();

                    // Создаём запись в Payments
                    if (PaymentMethodCombo.SelectedItem is PaymentMethod selectedPaymentMethod)
                    {
                        var payment = new Payment
                        {
                            OrderId = _currentOrder.OrderId,
                            Amount = _calculatedCost,
                            PaymentTime = DateTime.Now,
                            PaymentMethodId = selectedPaymentMethod.PaymentMethodId
                        };
                        _context.Payments?.Add(payment);
                        _context.SaveChanges();

                        // Обновляем статус оплаты в заказе
                        _currentOrder.IsPaid = true;
                        _context.SaveChanges();

                        Snackbar.MessageQueue?.Enqueue("Заказ успешно создан!");
                        ClearMapButton_Click(sender, e);
                        LoadOrderHistory();
                    }
                    else
                    {
                        Snackbar.MessageQueue?.Enqueue("Ошибка: Выбранный способ оплаты недействителен.");
                    }
                }
                catch (Exception ex)
                {
                    Snackbar.MessageQueue?.Enqueue($"Ошибка при создании заказа: {ex.Message}");
                    // Если произошла ошибка, можно откатить изменения, удалив заказ
                    if (_currentOrder.OrderId != 0)
                    {
                        _context.Orders?.Remove(_currentOrder);
                        _context.SaveChanges();
                    }
                }
            }
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: Выбранный тариф недействителен.");
            }
        }

        private void CancelOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                // Запрашиваем подтверждение у пользователя
                var result = MessageBox.Show("Вы уверены, что хотите отменить заказ?", "Подтверждение отмены", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return; // Если пользователь отказался, прерываем выполнение
                }

                if (_context?.Orders != null)
                {
                    var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
                    if (order != null && order.Status == "Pending")
                    {
                        order.Status = "Canceled";
                        order.OrderCompletionTime = DateTime.Now;
                        _context.SaveChanges();
                        Snackbar.MessageQueue?.Enqueue("Заказ отменён.");
                        LoadOrderHistory();
                    }
                    else
                    {
                        Snackbar.MessageQueue?.Enqueue("Ошибка: Заказ не может быть отменён.");
                    }
                }
                else
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Не удалось отменить заказ.");
                }
            }
        }

        private void PayOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                if (_context?.Orders == null || _context?.Payments == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
                var payment = _context.Payments.FirstOrDefault(p => p.OrderId == orderId);

                if (order == null || payment == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Заказ или платёж не найден.");
                    return;
                }

                // Проверяем, можно ли оплатить заказ (например, статус "Pending" или "Accepted")
                if (order.Status != "Pending" && order.Status != "Accepted")
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Заказ нельзя оплатить в текущем статусе.");
                    return;
                }

                // Здесь могла бы быть интеграция с платёжной системой, но для примера просто подтверждаем оплату
                payment.PaymentTime = DateTime.Now;
                order.IsPaid = true; // Обновляем статус оплаты в заказе
                _context.SaveChanges();

                Snackbar.MessageQueue?.Enqueue($"Заказ #{orderId} успешно оплачен!");
                LoadOrderHistory();
            }
        }

        private void OrderTimer_Tick(object? sender, EventArgs e)
        {
            if (_context?.Orders == null) return;

            foreach (var order in _orders.Where(o => o.Status == "Pending"))
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

        private void RateDriver_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int orderId)
            {
                if (_context == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                    return;
                }

                var order = _context.Orders?.FirstOrDefault(o => o.OrderId == orderId);
                if (order == null || order.Status != "Completed")
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Нельзя оценить эту поездку.");
                    return;
                }

                var rateDriverWindow = new RateDriverWindow(_context, order)
                {
                    Owner = this
                };

                rateDriverWindow.ShowDialog();
            }
        }

        public void LoadOrderHistory()
        {
            if (_context == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: _context не инициализирован.");
                return;
            }

            if (_context.Orders == null)
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка: _context.Orders не инициализирован.");
                return;
            }

            try
            {
                var orders = _context.Orders
                    .Include(o => o.Tariff)
                    .Include(o => o.PromoCode)
                    .Include(o => o.Driver)
                    .ThenInclude(d => d != null ? d.Vehicle : null)
                    .Include(o => o.Payments)
                    .Where(o => o.ClientId == _userId)
                    .OrderByDescending(o => o.OrderTime)
                    .ToList();

                if (orders == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: Список заказов равен null.");
                    return;
                }

                foreach (var order in orders)
                {
                    // Проверяем Payments
                    if (order.Payments == null)
                    {
                        Snackbar.MessageQueue?.Enqueue($"Заказ #{order.OrderId}: Payments равен null.");
                        order.Payments = new List<Payment>(); // Инициализируем пустой список, чтобы избежать ошибки
                    }
                    order.IsPaid = order.Payments.Any(p => p != null && p.PaymentTime != null);

                    if (order.Status == "Pending" && order.OrderTime.HasValue)
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
                }

                // Проверяем, что _orders не null, и инициализируем, если это так
                if (_orders == null)
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: _orders не инициализирован. Инициализируем заново.");
                    _orders = new ObservableCollection<Order>();
                }

                _orders.Clear();
                foreach (var order in orders)
                {
                    _orders.Add(order);
                }

                OrderHistoryList.Tag = "InitialLoad";
                OrderHistoryList.ItemsSource = _orders;
            }
            catch (Exception ex)
            {
                Snackbar.MessageQueue?.Enqueue($"Ошибка загрузки истории заказов: {ex.Message}");
            }
        }

        private void LoadProfile()
        {
            if (_context?.Users != null)
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == _userId);
                if (user != null)
                {
                    ProfileHeader.Text = user.Name;
                    ProfileName.Text = user.Name;
                    ProfilePhone.Text = user.Phone;
                    ProfileEmail.Text = user.Email ?? "Не указан";
                    ProfileAddress.Text = user.Address ?? "Не указан";
                    ProfileRegistrationDate.Text = user.RegistrationDate.HasValue
                        ? user.RegistrationDate.Value.ToString("dd.MM.yyyy")
                        : "Не указана";

                    _originalName = ProfileName.Text;
                    _originalPhone = ProfilePhone.Text;
                    _originalEmail = ProfileEmail.Text;
                    _originalAddress = ProfileAddress.Text;

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
            else
            {
                Snackbar.MessageQueue?.Enqueue("Ошибка загрузки профиля.");
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
                _originalEmail = ProfileEmail.Text;
                _originalAddress = ProfileAddress.Text;

                ProfileName.IsReadOnly = false;
                ProfilePhone.IsReadOnly = false;
                ProfileEmail.IsReadOnly = false;
                ProfileAddress.IsReadOnly = false;

                Snackbar.MessageQueue?.Enqueue("Редактирование профиля включено.");
            }
        }

        private void SaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingProfile)
            {
                string name = ProfileName.Text.Trim();
                string phone = ProfilePhone.Text.Trim();
                string email = ProfileEmail.Text.Trim();
                string address = ProfileAddress.Text.Trim();

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

                if (!string.IsNullOrEmpty(email) && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    Snackbar.MessageQueue?.Enqueue("Введите корректный email!");
                    ShakeElement(ProfileEmail);
                    return;
                }

                if (_context?.Users != null)
                {
                    var user = _context.Users.FirstOrDefault(u => u.UserId == _userId);
                    if (user != null)
                    {
                        user.Name = name;
                        user.Phone = phone;
                        user.Email = string.IsNullOrEmpty(email) ? null : email;
                        user.Address = string.IsNullOrEmpty(address) ? null : address;
                        _context.SaveChanges();

                        ProfileHeader.Text = name;
                        _userName = name;

                        _isEditingProfile = false;
                        SaveEditButton.Visibility = Visibility.Collapsed;
                        CancelEditButton.Visibility = Visibility.Collapsed;
                        EditProfileButton.Visibility = Visibility.Visible;
                        ResetPasswordButton.Visibility = Visibility.Visible;

                        ProfileName.IsReadOnly = true;
                        ProfilePhone.IsReadOnly = true;
                        ProfileEmail.IsReadOnly = true;
                        ProfileAddress.IsReadOnly = true;

                        Snackbar.MessageQueue?.Enqueue("Профиль успешно обновлён!");
                    }
                    else
                    {
                        Snackbar.MessageQueue?.Enqueue("Ошибка сохранения профиля.");
                    }
                }
                else
                {
                    Snackbar.MessageQueue?.Enqueue("Ошибка: база данных недоступна.");
                }
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingProfile)
            {
                ProfileName.Text = _originalName;
                ProfilePhone.Text = _originalPhone;
                ProfileEmail.Text = _originalEmail;
                ProfileAddress.Text = _originalAddress;

                _isEditingProfile = false;
                SaveEditButton.Visibility = Visibility.Collapsed;
                CancelEditButton.Visibility = Visibility.Collapsed;
                EditProfileButton.Visibility = Visibility.Visible;
                ResetPasswordButton.Visibility = Visibility.Visible;

                ProfileName.IsReadOnly = true;
                ProfilePhone.IsReadOnly = true;
                ProfileEmail.IsReadOnly = true;
                ProfileAddress.IsReadOnly = true;

                Snackbar.MessageQueue?.Enqueue("Изменения отменены.");
            }
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
                            Snackbar.MessageQueue?.Enqueue("Ошибка обновления аватара.");
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
        private void ResetOrderTaxiTab(object sender, RoutedEventArgs e)
        {
            ClearMapButton_Click(sender, e);
            // Сбрасываем масштаб и позицию карты на Санкт-Петербург
            MapControl.Zoom = 12; // Как в InitializeMap
            MapControl.Position = new PointLatLng(59.9343, 30.3351); // Санкт-Петербург, как в InitializeMap
                                                                     // Сбрасываем прокрутку формы заказа
            OrderTaxiScrollViewer.ScrollToTop();
        }

        private void ResetOrderHistoryTab()
        {
            LoadOrderHistory();
            // Сбрасываем прокрутку списка заказов
            OrderHistoryScrollViewer.ScrollToTop();
        }

        private void ResetProfileTab(object sender, RoutedEventArgs e)
        {
            if (_isEditingProfile)
            {
                CancelEdit_Click(sender, e);
            }
            LoadProfile();
            // Сбрасываем прокрутку профиля
            ProfileScrollViewer.ScrollToTop();
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