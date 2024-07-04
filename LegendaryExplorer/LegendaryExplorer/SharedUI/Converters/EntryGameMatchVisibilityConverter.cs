using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class EntryGameMatchVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string matchParaml && value is IEntry entry)
            {
                string[] parms = matchParaml.Split('_').ToArray();
                string gameParm = parms[0]; //we should always have this
                MEGame gameToMatch = MEGame.Unknown;
                switch (gameParm)
                {
                    case "ME1":
                        gameToMatch = MEGame.ME1;
                        break;
                    case "ME2":
                        gameToMatch = MEGame.ME2;
                        break;
                    case "ME3":
                        gameToMatch = MEGame.ME3;
                        break;
                    case "LE1":
                        gameToMatch = MEGame.LE1;
                        break;
                    case "LE2":
                        gameToMatch = MEGame.LE2;
                        break;
                    case "LE3":
                        gameToMatch = MEGame.LE3;
                        break;
                    case "UDK":
                        gameToMatch = MEGame.UDK;
                        break;
                }

                bool isInverted = false;
                if (parms.Length > 1)
                {
                    isInverted = parms[1] == "Not";
                }

                if (isInverted)
                {
                    return gameToMatch != entry.Game ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    return gameToMatch == entry.Game ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
