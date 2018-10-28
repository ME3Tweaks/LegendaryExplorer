using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ME3Explorer.SharedUI.Converters
{
    [ValueConversion(typeof(PackageEditorWPF.CurrentViewMode), typeof(SolidColorBrush))]
    public class PackageEditorWPFActiveViewHighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is PackageEditorWPF.CurrentViewMode))
            {
                return Brushes.Transparent; ;
            }
            if (parameter == null || !(parameter is string))
            {
                return Brushes.Transparent;
            }
            if (value.ToString() == (string)parameter)
            {
                return Brushes.LightBlue;
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }

    [ValueConversion(typeof(IExportEntry), typeof(string))]
    public class ObjectStructPropertyTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is IExportEntry))
            {
                return "";
            }
            IExportEntry export = (IExportEntry)value;
            if (export.ClassName != "StructProperty" && export.ClassName != "ObjectProperty")
            {
                return "";
            }

            //attempt to find type
            string s = "";
            byte[] data = export.Data;
            int importindex = BitConverter.ToInt32(data, data.Length - 4);
            if (importindex < 0)
            {
                //import
                importindex *= -1;
                if (importindex > 0) importindex--;
                if (importindex <= export.FileRef.Imports.Count)
                {
                    s += " (" + export.FileRef.Imports[importindex].ObjectName + ")";
                }
            }
            else
            {
                //export
                if (importindex > 0) importindex--;
                if (importindex <= export.FileRef.Exports.Count)
                {
                    s += " [" + export.FileRef.Exports[importindex].ObjectName + "]";
                }
            }
            return s;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }

    [ValueConversion(typeof(IEntry), typeof(string))]
    public class EntryFileTypeIconPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IExportEntry exp && PackageEditorWPF.ExportFileTypes.Contains(exp.ClassName))
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        return "/PackageEditor/EntryIcons/icon_swf.png";
                    case "Texture2D":
                        return "/PackageEditor/EntryIcons/icon_texture2d.png";
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }

    [ValueConversion(typeof(IEntry), typeof(Visibility))]
    public class EntryFileTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IExportEntry exp && PackageEditorWPF.ExportFileTypes.Contains( exp.ClassName))
            {
                return Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
