using System;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorer.Tools.PathfindingEditor;

namespace LegendaryExplorer.SharedUI.Converters
{
    public class EZFilteringActiveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PathfindingEditorWindow.EZFilterIncludeDirection filter && parameter is string condition)
            {
                switch (filter)
                {
                    case PathfindingEditorWindow.EZFilterIncludeDirection.Above when condition == "Above":
                        return true;
                    case PathfindingEditorWindow.EZFilterIncludeDirection.Below when condition == "Below":
                        return true;
                    case PathfindingEditorWindow.EZFilterIncludeDirection.BelowEquals when condition == "BelowEquals":
                        return true;
                    case PathfindingEditorWindow.EZFilterIncludeDirection.AboveEquals when condition == "AboveEquals":
                        return true;
                    case PathfindingEditorWindow.EZFilterIncludeDirection.None when condition == "None":
                        return true;
                }

                if (condition.StartsWith("Not"))
                {
                    condition = condition.Substring(3);
                    if (condition != filter.ToString())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
