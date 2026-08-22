using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Misc.Themes;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.SharedUI.Converters
{
    /// <summary>
    /// Some export-type tree icons (raster PNGs, not vector) are mostly dark pixels and disappear against a
    /// dark background - repainting them by hand isn't practical for every icon, so this inverts the ones
    /// that need it at load time and caches the result, rather than shipping unique dark-mode art or paying
    /// an inversion cost on every render. Add a filename to <see cref="IconsToInvert"/> to opt another icon
    /// in once you've checked it against the dark background - everything else is unaffected.
    /// </summary>
    internal static class DarkModeIconInverter
    {
        private static readonly HashSet<string> IconsToInvert = new(StringComparer.OrdinalIgnoreCase)
        {
            "icon_sound.png", // WwiseStream
            "icon_function.png", // Function
        };

        private static readonly Dictionary<string, BitmapSource> Cache = new();

        /// <param name="path">A pack-relative icon path, e.g. "/Tools/PackageEditor/ExportIcons/icon_sound.png"</param>
        public static object GetIconSource(string path)
        {
            if (path == null || !ThemeManager.IsDarkMode || !IconsToInvert.Contains(Path.GetFileName(path)))
            {
                return path;
            }

            if (!Cache.TryGetValue(path, out BitmapSource inverted))
            {
                inverted = Invert(path);
                Cache[path] = inverted;
            }

            return inverted;
        }

        private static BitmapSource Invert(string path)
        {
            var original = new BitmapImage();
            original.BeginInit();
            original.UriSource = new Uri("pack://application:,,," + path, UriKind.Absolute);
            original.CacheOption = BitmapCacheOption.OnLoad;
            original.EndInit();

            var bgra = new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
            int stride = bgra.PixelWidth * 4;
            byte[] pixels = new byte[bgra.PixelHeight * stride];
            bgra.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                // Bgra32 byte order is B,G,R,A - invert color channels, leave alpha (transparency) untouched
                pixels[i] = (byte)(255 - pixels[i]);
                pixels[i + 1] = (byte)(255 - pixels[i + 1]);
                pixels[i + 2] = (byte)(255 - pixels[i + 2]);
            }

            var result = BitmapSource.Create(bgra.PixelWidth, bgra.PixelHeight, original.DpiX, original.DpiY, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }
    }

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
                return ThemeManager.IsDarkMode ? new SolidColorBrush(Color.FromRgb(0x09, 0x47, 0x71)) : Brushes.LightBlue;
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
            string path = null;
            if (Settings.PackageEditor_ShowExportTypeIcons && value is ExportEntry { IsDefaultObject: false } exp && PackageEditorWindow.ExportIconTypes.Contains(exp.ClassName))
            {
                switch (exp.ClassName)
                {
                    case "BioSWF":
                    case "GFxMovieInfo":
                        path = "/Tools/PackageEditor/ExportIcons/icon_swf.png";
                        break;
                    case "Texture2D":
                        path = "/Tools/PackageEditor/ExportIcons/icon_texture2d.png";
                        break;
                    case "WwiseStream":
                        path = "/Tools/PackageEditor/ExportIcons/icon_sound.png";
                        break;
                    case "BioTlkFile":
                        path = "/Tools/PackageEditor/ExportIcons/icon_tlkfile.png";
                        break;
                    case "World":
                        path = "/Tools/PackageEditor/ExportIcons/icon_world.png";
                        break;
                    case "Function":
                        path = "/Tools/PackageEditor/ExportIcons/icon_function.png";
                        break;
                    case "Class":
                        path = "/Tools/PackageEditor/ExportIcons/icon_class.png";
                        break;
                    case "TextureCube":
                        path = "/Tools/PackageEditor/ExportIcons/icon_texturecube.png";
                        break;
                    case "Package":
                        string fname = Path.GetFileNameWithoutExtension(exp.FileRef.FilePath);
                        path = fname != null && fname.Equals(exp.ObjectName.Instanced, StringComparison.InvariantCultureIgnoreCase)
                            ? "/Tools/PackageEditor/ExportIcons/icon_package_fileroot.png"
                            : "/Tools/PackageEditor/ExportIcons/icon_package.png";
                        break;
                    case "SkeletalMesh":
                    case "StaticMesh":
                    case "FracturedStaticMesh":
                        path = "/Tools/PackageEditor/ExportIcons/icon_mesh.png";
                        break;
                    case "Sequence":
                        path = "/Tools/PackageEditor/ExportIcons/icon_sequence.png";
                        break;
                    case "Material":
                        path = "/Tools/PackageEditor/ExportIcons/icon_material.png";
                        break;
                    case "MaterialInstanceConstant":
                        path = "/Tools/PackageEditor/ExportIcons/icon_materialinstanceconstant.png";
                        break;
                    case "DecalMaterial":
                        path = "/Tools/PackageEditor/ExportIcons/icon_decalmaterial.png";
                        break;
                    case "State":
                        path = "/Tools/PackageEditor/ExportIcons/icon_state.png";
                        break;
                    case "Bio2DA":
                    case "Bio2DANumberedRows":
                        path = "/Tools/PackageEditor/ExportIcons/icon_2da.png";
                        break;
                }
            }
            else if (Settings.PackageEditor_ShowExportTypeIcons && value is ExportEntry { IsDefaultObject: true })
            {
                path = "/Tools/PackageEditor/ExportIcons/icon_default.png";
            }

            return path == null ? null : DarkModeIconInverter.GetIconSource(path);
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
