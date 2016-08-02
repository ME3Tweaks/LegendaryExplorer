using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ME3Explorer
{
    public class GenericWindow : IDisposable
    {
        Window wpf;
        Form winform;
        public Tool tool { get; private set; }
        public string fileName { get; private set; }

        EventHandler wpfClosed;
        FormClosedEventHandler winformClosed;

        public GenericWindow(Window w, string file)
        {
            wpf = w;
            tool = Tools.Items.FirstOrDefault(x => x.type == w.GetType());
            winform = null;
            fileName = file;
        }

        public GenericWindow(Form f, string file)
        {
            wpf = null;
            winform = f;
            tool = Tools.Items.FirstOrDefault(x => x.type == f.GetType());
            fileName = file;
        }

        public void RegisterClosed(Action handler)
        {
            if (wpf != null)
            {
                wpfClosed = (obj, args) =>
                {
                    handler();
                    Dispose();
                };
                wpf.Closed += wpfClosed;
            }
            else if (winform != null)
            {
                winformClosed = (obj, args) =>
                {
                    handler();
                    Dispose();
                };
                winform.FormClosed += winformClosed;
            }
        }

        public void Close()
        {
            if (wpf != null)
            {
                wpf.Close();
            }
            else if (winform != null)
            {
                winform.Close();
            }
        }

        public void BringToFront()
        {
            if (wpf != null)
            {
                wpf.BringToFront();
            }
            else if (winform != null)
            {
                winform.BringToFront();
            }
        }

        public void Dispose()
        {
            if (wpf != null)
            {
                wpf.Closed -= wpfClosed;
                wpf = null;
            }
            else if (winform != null)
            {
                winform.FormClosed -= winformClosed;
                winform = null;
            }
        }

        public static bool operator ==(GenericWindow gen, Window window)
        {
            return window == gen.wpf;
        }

        public static bool operator !=(GenericWindow gen, Window window)
        {
            return window != gen.wpf;
        }

        public static bool operator ==(GenericWindow gen, Form form)
        {
            return form == gen.winform;
        }

        public static bool operator !=(GenericWindow gen, Form form)
        {
            return form != gen.winform;
        }

        public BitmapSource GetImage()
        {
            if (wpf != null)
            {
                return CreateBitmapFromVisual(wpf);
            }
            else if (winform != null)
            {
                System.Drawing.Rectangle bounds = winform.ClientRectangle;
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
                winform.DrawToBitmap(bitmap, bounds);
                return bitmap.ToBitmapImage();
            }
            return null;
        }

        //modified from https://social.msdn.microsoft.com/Forums/vstudio/en-US/df4db537-a201-4ab4-bb7e-db38a5c2b6e0/wpf-equivalent-of-winforms-controldrawtobitmap
        private static BitmapSource CreateBitmapFromVisual(Visual target)
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
    }
}
