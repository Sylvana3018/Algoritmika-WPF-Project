using System;
using System.Globalization;
using System.Windows.Data;

namespace algoritm
{
    public class ProgressWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Assuming maximum width is 500, adjust based on your ProgressBar width
            double progressValue = (double)value;
            double maxWidth = 500.0;  // Adjust the maximum width of the progress bar
            return (progressValue / 100) * maxWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
