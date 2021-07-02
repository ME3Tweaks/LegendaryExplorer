using System;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(IMEPackage), typeof(MEGame))]
    public class GameTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IMEPackage pcc)
            {
                return pcc.Game;
            }
            return MEGame.ME3;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
