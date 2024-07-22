using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.SharedUI.Converters
{
    [ValueConversion(typeof(PackageEditorWindow.CurrentViewMode), typeof(SolidColorBrush))]
    public class PackageEditorWindowActiveViewHigherlighterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is PackageEditorWindow.CurrentViewMode))
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
            ReadOnlySpan<byte> data = export.DataReadOnly;
            int importindex = EndianReader.ToInt32(data.Slice(data.Length - 4), export.FileRef.Endian);
            if (export.FileRef.GetEntry(importindex) is ImportEntry imp)
            {
                return $" ({imp.ObjectName.Instanced})";
            }
            if (export.FileRef.GetEntry(importindex) is ExportEntry exp)
            {
                return $" [{exp.ObjectName.Instanced}]";
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
            if (Settings.PackageEditor_ShowExportTypeIcons && value is ExportEntry { IsDefaultObject: false } exp && PackageEditorWindow.ExportIconTypes.Contains(exp.ClassName))
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        return "/Tools/PackageEditor/ExportIcons/icon_swf.png";
                    case "Texture2D":
                        return "/Tools/PackageEditor/ExportIcons/icon_texture2d.png";
                    case "WwiseStream":
                        return "/Tools/PackageEditor/ExportIcons/icon_sound.png";
                    case "BioTlkFile":
                        return "/Tools/PackageEditor/ExportIcons/icon_tlkfile.png";
                    case "World":
                        return "/Tools/PackageEditor/ExportIcons/icon_world.png";
                    case "Function":
                        return "/Tools/PackageEditor/ExportIcons/icon_function.png";
                    case "Class":
                        return "/Tools/PackageEditor/ExportIcons/icon_class.png";
                    case "TextureCube":
                        return "/Tools/PackageEditor/ExportIcons/icon_texturecube.png";
                    case "Package":
                        string fname = Path.GetFileNameWithoutExtension(exp.FileRef.FilePath);
                        return fname != null && fname.Equals(exp.ObjectName.Instanced, StringComparison.InvariantCultureIgnoreCase)
                            ? "/Tools/PackageEditor/ExportIcons/icon_package_fileroot.png"
                            : "/Tools/PackageEditor/ExportIcons/icon_package.png";
                    case "SkeletalMesh":
                    case "StaticMesh":
                    case "FracturedStaticMesh":
                        return "/Tools/PackageEditor/ExportIcons/icon_mesh.png";
                    case "Sequence":
                        return "/Tools/PackageEditor/ExportIcons/icon_sequence.png";
                    case "Material":
                        return "/Tools/PackageEditor/ExportIcons/icon_material.png";
                    case "State":
                        return "/Tools/PackageEditor/ExportIcons/icon_state.png";
                    case "Bio2DA":
                    case "Bio2DANumberedRows":
                        return "/Tools/PackageEditor/ExportIcons/icon_2da.png";
                }
            }
            else if (Settings.PackageEditor_ShowExportTypeIcons && value is ExportEntry { IsDefaultObject: true })
            {
                return "/Tools/PackageEditor/ExportIcons/icon_default.png";
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
            if (Settings.PackageEditor_ShowExportTypeIcons && value is ExportEntry exp && (exp.IsDefaultObject || PackageEditorWindow.ExportIconTypes.Contains(exp.ClassName)))
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
            if (Settings.PackageEditor_ShowExportTypeIcons && value is ExportEntry exp && parameter is string type)
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
                    case "BioSoundNodeWaveStreamingData":
                        return "ISACT Content Bank files";
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
