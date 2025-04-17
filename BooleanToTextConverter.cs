using System;
using System.Globalization;
using System.Windows.Data;

namespace TaxiGO
{
    public class BooleanToTextConverter : IValueConverter
    {
        public string TrueText { get; set; } = "Yes";
        public string FalseText { get; set; } = "No";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueText : FalseText;
            }
            return FalseText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}