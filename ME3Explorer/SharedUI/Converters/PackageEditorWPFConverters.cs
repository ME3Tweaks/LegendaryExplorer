using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                return Brushes.Transparent;
            }
            if (!(parameter is string))
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

    [ValueConversion(typeof(ExportEntry), typeof(string))]
    public class ObjectStructPropertyTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ExportEntry))
            {
                return "";
            }
            ExportEntry export = (ExportEntry)value;
            if (export.ClassName != "StructProperty" && export.ClassName != "ObjectProperty")
            {
                return "";
            }

            //attempt to find type
            byte[] data = export.Data;
            int importindex = BitConverter.ToInt32(data, data.Length - 4);
            if (export.FileRef.GetEntry(importindex) is ImportEntry imp)
            {
                return $" ({imp.ObjectName})";
            }
            if (export.FileRef.GetEntry(importindex) is ExportEntry exp)
            {
                return $" [{exp.ObjectName}]";
            }
            return "";
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
            if (Properties.Settings.Default.PackageEditorWPF_ShowExportIcons && value is ExportEntry exp && !exp.ObjectName.StartsWith("Default__") && PackageEditorWPF.ExportIconTypes.Contains(exp.ClassName))
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        return "/PackageEditor/EntryIcons/icon_swf.png";
                    case "Texture2D":
                        return "/PackageEditor/EntryIcons/icon_texture2d.png";
                    case "WwiseStream":
                        return "/PackageEditor/EntryIcons/icon_sound.png";
                    case "BioTlkFile":
                        return "/PackageEditor/EntryIcons/icon_tlkfile.png";
                    case "World":
                        return "/PackageEditor/EntryIcons/icon_world.png";
                    case "Package":
                        string fname = Path.GetFileNameWithoutExtension(exp.FileRef.FilePath);
                        return fname.Equals(exp.ObjectName, StringComparison.InvariantCultureIgnoreCase) ? "/PackageEditor/EntryIcons/icon_package_fileroot.png" :  "/PackageEditor/EntryIcons/icon_package.png";
                    case "SkeletalMesh":
                    case "StaticMesh":
                    case "FracturedStaticMesh":
                        return "/PackageEditor/EntryIcons/icon_mesh.png";
                    case "Sequence":
                        return "/PackageEditor/EntryIcons/icon_sequence.png";
                    case "Material":
                        return "/PackageEditor/EntryIcons/icon_material.png";
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
            if (Properties.Settings.Default.PackageEditorWPF_ShowExportIcons && value is ExportEntry exp && !exp.ObjectName.StartsWith("Default__") && PackageEditorWPF.ExportIconTypes.Contains(exp.ClassName))
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

    [ValueConversion(typeof(IEntry), typeof(Visibility))]
    public class EntryClassVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Properties.Settings.Default.PackageEditorWPF_ShowExportIcons && value is ExportEntry exp && parameter is string type)
            {
                return exp.ClassName == type ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }

    [ValueConversion(typeof(IEntry), typeof(string))]
    public class EmbeddedFileToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ExportEntry exp)
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        return $"{(parameter as string)} shockwave flash file";
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }


}
