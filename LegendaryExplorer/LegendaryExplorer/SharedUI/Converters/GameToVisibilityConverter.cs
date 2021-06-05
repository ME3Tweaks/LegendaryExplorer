using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Converters
{
    [Localizable(false)]
    public class GameToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string gameStr)
            {
                bool inverted = false;
                // Handle 'Not_'
                if (gameStr.IndexOf('_') > 0)
                {
                    var splitparms = gameStr.Split('_');
                    inverted = splitparms.Any(x => x == "Not");
                    gameStr = splitparms.Last();
                }
                // MEGame Parsing
                if (Enum.TryParse(gameStr, out MEGame parameterGame))
                {
                    if (inverted ^ parameterGame == (MEGame)value) return Visibility.Visible;
                }
                // Game parsing - EG 'Not_Game3'
                else if(gameStr.StartsWith("Game"))
                {
                    var gameNumStr = gameStr.Substring(4, 1);
                    var gameNum = 3;
                    if (int.TryParse(gameNumStr, out gameNum) && gameNum > 0 && gameNum < 4)
                    {
                        bool shouldReturnVisible = false;
                        switch (gameNum)
                        {
                            case 1:
                                shouldReturnVisible = inverted ^ ((MEGame)value).IsGame1();
                                break;
                            case 2:
                                shouldReturnVisible = inverted ^ ((MEGame)value).IsGame2();
                                break;
                            case 3:
                                shouldReturnVisible = inverted ^ ((MEGame)value).IsGame3();
                                break;
                        }

                        if (shouldReturnVisible) return Visibility.Visible;
                    }
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}