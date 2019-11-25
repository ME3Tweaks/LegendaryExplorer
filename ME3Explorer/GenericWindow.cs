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
        public WPFBase WPF { get; private set; }
        public WinFormsBase WinForm { get; private set; }
        public Tool tool { get; }
        public string fileName { get; private set; }

        EventHandler wpfClosed;
        FormClosedEventHandler winformClosed;

        public event EventHandler Disposing;

        public GenericWindow(WPFBase wpf, string file)
        {
            WPF = wpf;
            tool = Tools.Items.FirstOrDefault(x => x.type == wpf.GetType());
            WinForm = null;
            fileName = file;
        }

        public GenericWindow(WinFormsBase winform, string file)
        {
            WPF = null;
            WinForm = winform;
            tool = Tools.Items.FirstOrDefault(x => x.type == winform.GetType());
            fileName = file;
        }

        public void handleUpdate(List<PackageUpdate> updates)
        {
            if (WPF != null)
            {
                WPF.handleUpdate(updates);
            }
            else
            {
                WinForm?.handleUpdate(updates);
            }
        }

        public void RegisterClosed(Action handler)
        {
            if (WPF != null)
            {
                wpfClosed = (obj, args) =>
                {
                    handler();
                    Dispose();
                };
                WPF.Closed += wpfClosed;
            }
            else if (WinForm != null)
            {
                winformClosed = (obj, args) =>
                {
                    handler();
                    Dispose();
                };
                WinForm.FormClosed += winformClosed;
            }
        }

        public void Close()
        {
            if (WPF != null)
            {
                WPF.Close();
            }
            else
            {
                WinForm?.Close();
            }
        }

        public void BringToFront()
        {
            if (WPF != null)
            {
                WPF.RestoreAndBringToFront();
            }
            else
            {
                WinForm?.RestoreAndBringToFront();
            }
        }

        public void Dispose()
        {
            if (WPF != null)
            {
                WPF.Closed -= wpfClosed;
                WPF = null;
            }
            else if (WinForm != null)
            {
                WinForm.FormClosed -= winformClosed;
                WinForm = null;
            }
            Disposing?.Invoke(this, EventArgs.Empty);
        }

        public static bool operator ==(GenericWindow gen, Window window)
        {
            return window == gen.WPF;
        }

        public static bool operator !=(GenericWindow gen, Window window)
        {
            return window != gen.WPF;
        }

        public static bool operator ==(GenericWindow gen, Form form)
        {
            return form == gen.WinForm;
        }

        public static bool operator !=(GenericWindow gen, Form form)
        {
            return form != gen.WinForm;
        }

        public BitmapSource GetImage()
        {
            if (WPF != null)
            {
                return WPF.DrawToBitmapSource();
            }
            else if (WinForm != null)
            {
                return WinForm.DrawToBitmapSource();
            }
            return null;
        }
    }
}
