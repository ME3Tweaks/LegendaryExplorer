using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace KFreonLib.Helpers
{
    /// <summary>
    /// Provides methods interface to change paths. Displays any given paths and returns new paths if provided.
    /// Basically a glorified input box.
    /// </summary>
    public partial class PathChanger : Form
    {
        // KFreon: Return properties
        public string PathME1 { get; set; }
        public string PathME2 { get; set; }
        public string PathME3 { get; set; }


        /// <summary>
        /// Provides an interface to change all 3 game paths. Displays path to BIOGame.
        /// </summary>
        /// <param name="ME1path">ME1 BIOGame path. Can be null or empty.</param>
        /// <param name="ME2path">ME2 BIOGame path. Can be null or empty.</param>
        /// <param name="ME3path">ME3 BIOGame path. Can be null or empty.</param>
        public PathChanger(string ME1path, string ME2path, string ME3path)
        {
            InitializeComponent();

            // KFreon: Set things up
            if (!String.IsNullOrEmpty(ME1path))
                ME1Path.Text = ME1path;
            else
                ME1Path.Enabled = false;

            if (!String.IsNullOrEmpty(ME2path))
                ME2Path.Text = ME2path;
            else
                ME2Path.Enabled = false;

            if (!String.IsNullOrEmpty(ME3path))
                ME3Path.Text = ME3path;
            else
                ME3Path.Enabled = false;
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            // KFreon: Set paths
            if (!String.IsNullOrEmpty(ME1Path.Text))
                PathME1 = ME1Path.Text;

            if (!String.IsNullOrEmpty(ME2Path.Text))
                PathME2 = ME2Path.Text;

            if (!String.IsNullOrEmpty(ME3Path.Text))
                PathME3 = ME3Path.Text;
            this.Close();
        }

        private void CancelPushButton_Click(object sender, EventArgs e)
        {
            // KFreon: Nullify paths to represent cancellation.
            PathME1 = null;
            PathME2 = null;
            PathME3 = null;
            this.Close();
        }

        private void ME3Box_KeyDown(object sender, KeyEventArgs e)
        {
            DoEnter(e);
        }

        private void ME2Box_KeyDown(object sender, KeyEventArgs e)
        {
            DoEnter(e);
        }

        private void ME1Box_KeyDown(object sender, KeyEventArgs e)
        {
            DoEnter(e);
        }

        private void DoEnter(KeyEventArgs enter)
        {
            if (enter.KeyCode == Keys.Enter)
                OKButton.Focus();
        }

        private void BrowseME1Button_Click(object sender, EventArgs e)
        {
            ME1Path.Text = Browser(1) ?? ME1Path.Text;
        }

        private void BrowseME2Button_Click(object sender, EventArgs e)
        {
            ME2Path.Text = Browser(2) ?? ME2Path.Text;
        }

        private void BrowseME3Button_Click(object sender, EventArgs e)
        {
            ME3Path.Text = Browser(3) ?? ME3Path.Text;
        }

        private string Browser(int which)
        {
            string retval = null;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Mass Effect " + which + " executable.";
                string filter = "MassEffect" + (which == 1 ? "" : which.ToString()) + ".exe|MassEffect" + (which == 1 ? "" : which.ToString()) + ".exe";
                ofd.Filter = filter;

                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    retval = GetBIOGame(ofd.FileName, which);
            }
                
            return retval;
        }


        // KFreon: Gets biogame from exe path
        string GetBIOGame(string path, int whichGame)
        {
            string BIOGame = Path.GetDirectoryName(Path.GetDirectoryName(path));

            if (whichGame == 3)
                BIOGame = Path.GetDirectoryName(BIOGame);
            return Path.Combine(BIOGame, "BIOGame");
        }
    }
}
