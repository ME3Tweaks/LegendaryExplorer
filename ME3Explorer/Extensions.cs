using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ME3ExplorerCore.Helpers;
using SharpDX;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Matrix = System.Windows.Media.Matrix;
using Point = System.Windows.Point;

namespace ME3Explorer
{
    public static class UnrealExtensions
    {
        public static (Vector3 translation, Vector3 scale, Rotator rotation) UnrealDecompose(this SharpDX.Matrix m)
        {
            Vector3 translation = m.TranslationVector;
            Vector3 scale = new Vector3((float)Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12 + m.M13 * m.M13),
                (float)Math.Sqrt(m.M21 * m.M21 + m.M22 * m.M22 + m.M23 * m.M23),
                (float)Math.Sqrt(m.M31 * m.M31 + m.M32 * m.M32 + m.M33 * m.M33));

            if (MathUtil.IsZero(scale.X) ||
                MathUtil.IsZero(scale.Y) ||
                MathUtil.IsZero(scale.Z))
            {
                return (translation, scale, default);
            }

            m.M11 /= scale.X;
            m.M12 /= scale.X;
            m.M13 /= scale.X;

            m.M21 /= scale.Y;
            m.M22 /= scale.Y;
            m.M23 /= scale.Y;

            m.M31 /= scale.Z;
            m.M32 /= scale.Z;
            m.M33 /= scale.Z;
            return (translation, scale, ((ME3ExplorerCore.SharpDX.Matrix)m).GetRotator());
        }
    }
    public static class WPFExtensions
    {
        /// <summary>
        /// Binds a property
        /// </summary>
        /// <param name="bound">object to create binding on</param>
        /// <param name="boundProp">property to create binding on</param>
        /// <param name="source">object being bound to</param>
        /// <param name="sourceProp">property being bound to</param>
        /// <param name="converter">optional value converter</param>
        /// <param name="parameter">optional value converter parameter</param>
        public static void bind(this FrameworkElement bound, DependencyProperty boundProp, object source, string sourceProp,
                                IValueConverter converter = null, object parameter = null)
        {
            Binding b = new Binding { Source = source, Path = new PropertyPath(sourceProp) };
            if (converter != null)
            {
                b.Converter = converter;
                b.ConverterParameter = parameter;
            }
            bound.SetBinding(boundProp, b);
        }
        public static void bind(this FrameworkContentElement bound, DependencyProperty boundProp, object source, string sourceProp,
            IValueConverter converter = null, object parameter = null)
        {
            Binding b = new Binding { Source = source, Path = new PropertyPath(sourceProp) };
            if (converter != null)
            {
                b.Converter = converter;
                b.ConverterParameter = parameter;
            }
            bound.SetBinding(boundProp, b);
        }

        /// <summary>
        /// Starts a DoubleAnimation for a specified animated property on this element
        /// </summary>
        /// <param name="target">element to perform animation on</param>
        /// <param name="dp">The property to animate, which is specified as a dependency property identifier</param>
        /// <param name="toValue">The destination value of the animation</param>
        /// <param name="duration">The duration of the animation, in milliseconds</param>
        public static void BeginDoubleAnimation(this UIElement target, DependencyProperty dp, double toValue, int duration)
        {
            target.BeginAnimation(dp, new DoubleAnimation(toValue, TimeSpan.FromMilliseconds(duration)));
        }

        public static void AppendLine(this TextBoxBase box, string text)
        {
            box.AppendText(text + Environment.NewLine);
            box.ScrollToEnd();
        }

        public static BitmapImage ToBitmapImage(this System.Drawing.Image bitmap)
        {
            MemoryStream memory = new MemoryStream();
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
            memory.Position = 0;
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public static FrameworkElement GetChild(this ItemsControl itemsControl, string withName)
        {
            return itemsControl.Items.OfType<FrameworkElement>().FirstOrDefault(m => m.Name == withName);
        }

        public static System.Windows.Media.Color ToWPFColor(this System.Drawing.Color color)
        {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Drawing.Color ToWinformsColor(this System.Windows.Media.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }

    public static class ExternalExtensions
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);
        [DllImport("user32.dll")]
        static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, int lParam);

        public static void RestoreAndBringToFront(this Window window)
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            RestoreAndBringToFront(helper.Handle);
        }

        public static void RestoreAndBringToFront(this System.Windows.Forms.Form form) => RestoreAndBringToFront(form.Handle);

        public static void RestoreAndBringToFront(this IntPtr windowHandle)
        {
            //if window is minimized
            if (IsIconic(windowHandle))
            {
                const int SW_RESTORE = 9;
                ShowWindowAsync(windowHandle, SW_RESTORE);
            }

            SetForegroundWindow(windowHandle);
        }

        public static bool IsForegroundWindow(this System.Windows.Forms.Form form)
        {
            return GetForegroundWindow() == form.Handle;
        }

        public static bool IsForegroundWindow(this Window window)
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            return GetForegroundWindow() == helper.Handle;
        }

        //modified from https://social.msdn.microsoft.com/Forums/vstudio/en-US/df4db537-a201-4ab4-bb7e-db38a5c2b6e0/wpf-equivalent-of-winforms-controldrawtobitmap
        public static BitmapSource DrawToBitmapSource(this Visual target)
        {
            Rect bounds = VisualTreeHelper.GetDescendantBounds(target);

            RenderTargetBitmap renderTarget = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);

            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext context = visual.RenderOpen())
            {
                VisualBrush visualBrush = new VisualBrush(target);
                context.DrawRectangle(visualBrush, null, new Rect(new Point(), bounds.Size));
            }

            renderTarget.Render(visual);
            return renderTarget;
        }

        public static BitmapSource DrawToBitmapSource(this System.Windows.Forms.Control control)
        {
            const int WM_PRINT = 0x317, PRF_CLIENT = 4,
            PRF_CHILDREN = 0x10, PRF_NON_CLIENT = 2,
            COMBINED_PRINTFLAGS = PRF_CLIENT | PRF_CHILDREN | PRF_NON_CLIENT;

            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(control.Width, control.Height);
            using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);

            // paint control onto graphics
            IntPtr hWnd = control.Handle;
            IntPtr hDC = graphics.GetHdc();
            SendMessage(hWnd, WM_PRINT, hDC, COMBINED_PRINTFLAGS);
            graphics.ReleaseHdc(hDC);

            return bitmap.ToBitmapImage();
        }
    }

    //http://stackoverflow.com/a/11433814/1968930
    public static class HyperlinkExtensions
    {
        public static bool GetIsExternal(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsExternalProperty);
        }

        public static void SetIsExternal(DependencyObject obj, bool value)
        {
            obj.SetValue(IsExternalProperty, value);
        }
        public static readonly DependencyProperty IsExternalProperty =
            DependencyProperty.RegisterAttached("IsExternal", typeof(bool), typeof(HyperlinkExtensions), new PropertyMetadata(false, OnIsExternalChanged));

        private static void OnIsExternalChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var hyperlink = (Hyperlink)sender;

            if ((bool)args.NewValue)
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
            else
                hyperlink.RequestNavigate -= Hyperlink_RequestNavigate;
        }

        private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }
}