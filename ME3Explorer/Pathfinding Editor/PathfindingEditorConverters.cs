using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ME3Explorer.Pathfinding_Editor
{

    public class EZFilteringActiveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PathfindingEditorWPF.EZFilterIncludeDirection filter && parameter is string condition)
            {
                switch (filter)
                {
                    case PathfindingEditorWPF.EZFilterIncludeDirection.Above when condition == "Above":
                        return true;
                    case PathfindingEditorWPF.EZFilterIncludeDirection.Below when condition == "Below":
                        return true;
                    case PathfindingEditorWPF.EZFilterIncludeDirection.BelowEquals when condition == "BelowEquals":
                        return true;
                    case PathfindingEditorWPF.EZFilterIncludeDirection.AboveEquals when condition == "AboveEquals":
                        return true;
                    case PathfindingEditorWPF.EZFilterIncludeDirection.None when condition == "None":
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
