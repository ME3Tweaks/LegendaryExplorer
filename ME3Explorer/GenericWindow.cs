using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public class GenericWindow : IDisposable
    {
        WPFBase wpf;
        WinFormsBase winform;
        public Tool tool { get; private set; }
        public string fileName { get; private set; }

        EventHandler wpfClosed;
        FormClosedEventHandler winformClosed;

        public event EventHandler Disposing;

        public GenericWindow(WPFBase w, string file)
        {
            wpf = w;
            tool = Tools.Items.FirstOrDefault(x => x.type == w.GetType());
            winform = null;
            fileName = file;
        }

        public GenericWindow(WinFormsBase f, string file)
        {
            wpf = null;
            winform = f;
            tool = Tools.Items.FirstOrDefault(x => x.type == f.GetType());
            fileName = file;
        }

        public void handleUpdate(List<PackageUpdate> updates)
        {
            if (wpf != null)
            {
                wpf.handleUpdate(updates);
            }
            else if (winform != null)
            {
                winform.handleUpdate(updates);
            }
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
                wpf.RestoreAndBringToFront();
            }
            else if (winform != null)
            {
                winform.RestoreAndBringToFront();
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
            Disposing?.Invoke(this, EventArgs.Empty);
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
                return wpf.DrawToBitmapSource();
            }
            else if (winform != null)
            {
                return winform.DrawToBitmapSource();
            }
            return null;
        }
    }
}
