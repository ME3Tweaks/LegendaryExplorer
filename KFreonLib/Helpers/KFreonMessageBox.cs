using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KFreonLib.Helpers
{
    public partial class KFreonMessageBox : Form
    {
        /// <summary>
        /// Provides a more customisable MessageBox with 3 buttons available. 
        /// DialogResults: Button1 = OK, button2 = Abort, button3 = Cancel.
        /// </summary>
        /// <param name="Title"></param>
        /// <param name="Message"></param>
        /// <param name="Button1Text"></param>
        /// <param name="Button2Text"></param>
        /// <param name="Button3Text"></param>
        /// <param name="icon"></param>
        public KFreonMessageBox(string Title, string Message, string Button1Text, MessageBoxIcon icon, string Button2Text = null, string Button3Text = null)
        {
            InitializeComponent();
            this.Text = Title;
            label1.Text = Message;
            button1.Text = Button1Text;

            if (Button2Text != null)
                button2.Text = Button2Text;
            button2.Visible = Button2Text != null;

            if (Button3Text != null)
                button3.Text = Button3Text;
            button3.Visible = Button3Text != null;

            // KFreon: Reset positions?
            //166, 500 = 330


            // KFreon: Deal with icon/picture
            if (pictureBox1.Image != null)
                pictureBox1.Image.Dispose();

            if (icon != 0)
                pictureBox1.Image = GetSystemImageForMessageBox(icon);
            else
                pictureBox1.Image = new Bitmap(1, 1);
        }

        private Bitmap GetSystemImageForMessageBox(MessageBoxIcon icon)
        {
            string test = Enum.GetName(typeof (MessageBoxIcon), (object)icon);
            Bitmap bmp = null;
            try
            {
                switch (test)
                {
                    case "Asterisk":
                        bmp = SystemIcons.Asterisk.ToBitmap();
                        break;
                    case "Error":
                        bmp = SystemIcons.Error.ToBitmap();
                        break;
                    case "Exclamation":
                        bmp = SystemIcons.Exclamation.ToBitmap();
                        break;
                    case "Hand":
                        bmp = SystemIcons.Hand.ToBitmap();
                        break;
                    case "Information":
                        bmp = SystemIcons.Information.ToBitmap();
                        break;
                    case "None":
                        break;
                    case "Question":
                        bmp = SystemIcons.Question.ToBitmap();
                        break;
                    case "Stop":
                        bmp = SystemIcons.Shield.ToBitmap();
                        break;
                    case "Warning":
                        bmp = SystemIcons.Warning.ToBitmap();
                        break;
                }
            }
            catch
            {
                Debugging.DebugOutput.PrintLn("Error getting image from MessageBoxIcon.");
            }
            
            return bmp;
        }
    }
}
