using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaxiGO
{
    public class StatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string status = value.ToString() ?? string.Empty;
            string param = parameter.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(status) || string.IsNullOrEmpty(param))
                return Visibility.Collapsed;

            if (param == "NotCanceled")
            {
                return status != "Canceled" ? Visibility.Visible : Visibility.Collapsed;
            }

            return status == param ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}