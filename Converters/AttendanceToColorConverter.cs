using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace algoritm.Converters
{
    public class AttendanceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double attendancePercentage)
            {
                if (attendancePercentage < 30)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c2858c"));
                else if (attendancePercentage >= 30 && attendancePercentage < 70)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3989dc"));
                else
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5ca582"));
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}