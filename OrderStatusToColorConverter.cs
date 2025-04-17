using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaxiGO
{
    public class OrderStatusToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] == null)
                return Brushes.Transparent;

            string? status = values[0]?.ToString();
            if (status == null)
                return Brushes.Transparent;

            switch (status)
            {
                case "Pending":
                    return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow
                case "Accepted":
                    return new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
                case "Completed":
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                case "Canceled":
                    return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                default:
                    return Brushes.Transparent;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}