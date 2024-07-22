using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
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
using System.Numerics;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Libraries;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Image = LegendaryExplorerCore.Textures.Image;
using Point = System.Windows.Point;

namespace LegendaryExplorer.Misc
{
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

        /// <summary>
        /// Starts a DoubleAnimation for a specified animated property on this element
        /// </summary>
        /// <param name="target">element to perform animation on</param>
        /// <param name="dp">The property to animate, which is specified as a dependency property identifier</param>
        /// <param name="toValue">The destination value of the animation</param>
        /// <param name="duration">The duration of the animation, in milliseconds</param>
        /// <param name="onCompleted">Function to run once animation is completed</param>
        public static void BeginDoubleAnimation(this UIElement target, DependencyProperty dp, double toValue, int duration, EventHandler onCompleted)
        {
            var animation = new DoubleAnimation(toValue, TimeSpan.FromMilliseconds(duration));
            animation.Completed += onCompleted;
            target.BeginAnimation(dp, animation);
        }

        public static void Add(this Storyboard sb, AnimationTimeline anim, DependencyObject target, string targetPropertyPath)
        {
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, new PropertyPath(targetPropertyPath));
            sb.Children.Add(anim);
        }

        public static void AddDoubleAnimation(this Storyboard sb, double toValue, int duration, DependencyObject target, string targetPropertyPath)
        {
            sb.Add(new DoubleAnimation(toValue, TimeSpan.FromMilliseconds(duration)), target, targetPropertyPath);
        }

        public static void AppendLine(this TextBoxBase box, string text)
        {
            box.AppendText(text + Environment.NewLine);
            box.ScrollToEnd();
        }

        public static BitmapImage ToBitmapImage(this System.Drawing.Image bitmap, System.Drawing.Imaging.ImageFormat destFormat = null)
        {
            MemoryStream memory = new MemoryStream();
            bitmap.Save(memory, destFormat ?? System.Drawing.Imaging.ImageFormat.Bmp);
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
        public static void RestoreAndBringToFront(this Window window)
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            RestoreAndBringToFront(helper.Handle);
        }

        public static void RestoreAndBringToFront(this System.Windows.Forms.Form form) => RestoreAndBringToFront(form.Handle);

        public static void RestoreAndBringToFront(this IntPtr windowHandle)
        {
            //if window is minimized
            if (WindowsAPI.IsIconic(windowHandle))
            {
                const int SW_RESTORE = 9;
                WindowsAPI.ShowWindowAsync(windowHandle, SW_RESTORE);
            }

            WindowsAPI.SetForegroundWindow(windowHandle);
        }

        public static void SetForegroundWindow(this Window window)
        {
            WindowsAPI.SetForegroundWindow(new WindowInteropHelper(window).Handle);
        }

        public static bool IsForegroundWindow(this System.Windows.Forms.Form form)
        {
            return WindowsAPI.GetForegroundWindow() == form.Handle;
        }

        public static bool IsForegroundWindow(this Window window)
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            return WindowsAPI.GetForegroundWindow() == helper.Handle;
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
            WindowsAPI.SendMessage(hWnd, WM_PRINT, hDC, COMBINED_PRINTFLAGS);
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
            OpenURL(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        public static void OpenURL(string url)
        {
            // This can throw exception if windows has no browser set
            // Which seems like a weird issue but it definitely happens
            // for some users.
            try
            {
                using var link = new Process
                {
                    StartInfo =
                    {
                        FileName = url,
                        UseShellExecute = true
                    }
                };
                link.Start();
            }
            catch (Exception e)
            {
                // kind of a hack but it works. Clipboard also suffers from this same issue so just let user handle it.
                PromptDialog.Prompt(null,
                    $"The URL could not be opened in your default web browser due to an error: {e.Message}. You can manually copy and paste the link below into your browser.",
                    "Error opening link", url, true);
            }
        }
    }

    public static class TreeViewExtension
    {
        public static IEnumerable<System.Windows.Forms.TreeNode> FlattenTreeView(this System.Windows.Forms.TreeView tv)
        {
            return tv.Nodes.Cast<System.Windows.Forms.TreeNode>().SelectMany(FlattenTree);

            List<System.Windows.Forms.TreeNode> FlattenTree(System.Windows.Forms.TreeNode rootNode)
            {
                var nodes = new List<System.Windows.Forms.TreeNode> { rootNode };
                foreach (System.Windows.Forms.TreeNode node in rootNode.Nodes)
                {
                    nodes.AddRange(FlattenTree(node));
                }
                return nodes;
            }
        }

        /// <summary>
        /// Select specified item in a TreeView
        /// </summary>
        public static void SelectItem(this TreeView treeView, object item)
        {
            if (treeView.ItemContainerGenerator.ContainerFromItemRecursive(item) is TreeViewItem tvItem)
            {
                tvItem.IsSelected = true;
            }
        }
    }

    public static class TreeViewExtensions
    {
        public static TreeViewItem ContainerFromItemRecursive(this ItemContainerGenerator root, object item)
        {
            if (root.ContainerFromItem(item) is TreeViewItem treeViewItem)
                return treeViewItem;
            foreach (var subItem in root.Items)
            {
                treeViewItem = root.ContainerFromItem(subItem) as TreeViewItem;
                var search = treeViewItem?.ItemContainerGenerator.ContainerFromItemRecursive(item);
                if (search != null)
                    return search;
            }
            return null;
        }
    }
}