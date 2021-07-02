using System;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.SharedUI.Converters
{
    public class SFAREntryToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DLCPackage.FileEntryStruct fes)
            {
                var compressed = fes.BlockSizeTableIndex != 0xFFFFFFFF;
                var uncompSize = FileSize.FormatSize(fes.RealUncompressedSize);
                return $"{(compressed ? $"Compressed" : "Uncompressed")}, uncompressed size: {uncompSize}";
            }
            return $"Invalid value {value}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
