using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer
{
    partial class AboutME3Explorer : Form
    {
        public AboutME3Explorer()
        {
            InitializeComponent();
            this.Text = String.Format("About {0}", AssemblyTitle);
            this.labelProductName.Text = "Mass Effect 3 Explorer";
            this.labelVersion.Text = String.Format("Revision: {0}", Assembly.GetExecutingAssembly().GetName().Version.Build.ToString());
            this.labelCompanyName.Text = "Credits:";
            this.textBoxDescription.Text = AssemblyDescription;
        }

        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyDescription
        {
            get
            {
                List<String> developers = new List<String>();
                developers.Add("TOOLSET CREATORS:");
                developers.Add("- WarrantyVoider");
                developers.Add("- KFreon");
                developers.Add("- AmaroK86");
                developers.Add("- Eudaimonium");
                developers.Add("- Saltisgood");

                developers.Add("");
                developers.Add("");

                developers.Add("PROGRAMMING:");
                developers.Add("- Ashley66444");
                developers.Add("- Aquadran");
                developers.Add("- BCSWowbagger");
                developers.Add("- Erik JS");
                developers.Add("- FemShep");
                developers.Add("- Fog.Gene");
                developers.Add("- Heff");
                developers.Add("- JohnP");
                developers.Add("- MrFob");
                developers.Add("- SirCxyrtyx");

                developers.Add("");
                developers.Add("");

                developers.Add("THIRD PARTY CODE:");
                developers.Add("- Eliot");
                developers.Add("- TheFeckless");
                developers.Add("- gibbed");
                developers.Add("- gildor");

                developers.Add("");
                developers.Add("");
                developers.Add("ICONS and BACKGROUNDS:");
                developers.Add("- Giftfish");
                developers.Add("- paragade icon by Joshua Bell, sailok.deviantart.com");
                developers.Add("- hex code texture by Maksim Kabakou, Shutterstock");

                StringBuilder sb = new StringBuilder();
                foreach (String developer in developers)
                {
                    sb.Append(developer);
                    sb.Append("\r\n");
                }
                return sb.ToString();
            }
        }
        #endregion

        private void okButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
