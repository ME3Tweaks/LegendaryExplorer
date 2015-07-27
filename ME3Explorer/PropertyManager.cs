using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.UnrealHelper;

namespace ME3Explorer
{
    public partial class PropertyManager : Form
    {
        public UPropertyReader UPR;

        public PropertyManager()
        {
            InitializeComponent();
        }

        private void PropertyManager_Load(object sender, EventArgs e)
        {
            UPR = new UPropertyReader();
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if(File.Exists(loc + "\\exec\\DefaultProp.xml"))
                UPR.ImportDefinitionsXML(loc + "\\exec\\DefaultProp.xml");
            RefreshView();
        }

        private void RefreshView()
        {
            listBox1.Items.Clear();
            for (int i = 0; i < UPR.Definitions.Count(); i++)
            {
                listBox1.Items.Add(UPR.Definitions[i].name);
            }
        }

        private void RefreshView(int n)
        {
            listBox2.Items.Clear();
            for (int i = 0; i < UPR.Definitions[n].props.Count; i++)
            {
                listBox2.Items.Add(UPR.Definitions[n].props[i].name);
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            listBox2.Items.Clear();
            for (int i = 0; i < UPR.Definitions[n].props.Count; i++)
                listBox2.Items.Add(UPR.Definitions[n].props[i].name);
        }

        private void addClassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string classname = Microsoft.VisualBasic.Interaction.InputBox("Please enter the new class name", "ME3 Explorer","", 0, 0);
            if (classname == "")
                return;
            UPR.AddClass(classname);
            RefreshView();
        }

        private void addPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            string propname = Microsoft.VisualBasic.Interaction.InputBox("Please enter the name of the new Property", "ME3 Explorer", "", 0, 0);
            if (propname == "")
                return;
            string counts = Microsoft.VisualBasic.Interaction.InputBox("Please enter the count of Elemets it will contain", "ME3 Explorer", "0", 0, 0);
            if (counts == "")
                return;
            int count = Convert.ToInt32(counts);
            List<UPropertyReader.PropertyMeta> tMeta = new List<UPropertyReader.PropertyMeta>();
            if (count < 0 || count > 100)
                return;
            for (int i = 0; i < count; i++)
            {
                UPropertyReader.PropertyMeta t = new UPropertyReader.PropertyMeta();
                string sizes = Microsoft.VisualBasic.Interaction.InputBox("What size has element #" + i.ToString() + "? (1,2 or 4 bytes)", "ME3 Explorer", "4", 0, 0);
                if (sizes == "")
                    return;
                int size = Convert.ToInt32(sizes);
                if (size != 1 && size != 2 && size != 4)
                    return;
                t.size = size;
                string types = Microsoft.VisualBasic.Interaction.InputBox("What type has element #" + i.ToString() + "? \n(0=Value, 1=Float, 2=Name, 3=Ignore)", "ME3 Explorer", "", 0, 0);
                if (types == "")
                    return;
                int type = Convert.ToInt32(types);
                if (type < 0 || type > 3)
                    return;
                t.type = type;
                if (type == 1 && size == 1)
                    return;
                tMeta.Add(t);
            }
            UPropertyReader.Property p = new UPropertyReader.Property();
            p.name = propname;
            p.Meta = tMeta;
            UPR.AddProp(UPR.Definitions[n].name, p);
            MessageBox.Show("Done.");
            RefreshView(n);
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            UPropertyReader.Property p = UPR.Definitions[n].props[m];
            string s = "";
            int pos=0;
            for (int i = 0; i < p.Meta.Count; i++)
            {
                s += pos.ToString("X") + "\t";
                switch (p.Meta[i].size)
                {
                    case 1:
                        s += " 8 bit 1 byte :";
                        break;
                    case 2:
                        s += "16 bit 2 byte :";
                        break;
                    case 4:
                        s += "32 bit 4 byte :";
                        break;
                }
                pos += p.Meta[i].size;
                switch (p.Meta[i].type)
                {
                    case 0:
                        s += "Integer\n";
                        break;
                    case 1:
                        s += "Float\n";
                        break;
                    case 2:
                        s += "Name\n";
                        break;
                    case 3:
                        s += "Ignore\n";
                        break;
                }
            }
            rtb1.Text = s;
        }

        private void saveDefinitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = "";
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "PropertyDef(*.xml)|*.xml";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = FileDialog1.FileName;
                UPR.ExportDefinitionsXML(path);
                MessageBox.Show("Done.");
            }
        }

        private void loadDefinitonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = "";
            OpenFileDialog FileDialog1 = new OpenFileDialog();
            FileDialog1.Filter = "PropertyDef(*.xml)|*.xml";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = FileDialog1.FileName;
                UPR.ImportDefinitionsXML(path);
                RefreshView();
            }
        }

        private void deleteClassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            UPR.Definitions.Remove(UPR.Definitions[n]);
            RefreshView();
        }

        private void deletePropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return; 
            UPR.Definitions[n].props.Remove(UPR.Definitions[n].props[m]);
            RefreshView(n);
        }

        private void loadDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\exec\\DefaultProp.xml"))
                UPR.ImportDefinitionsXML(loc + "\\exec\\DefaultProp.xml");
            RefreshView();
        }

        private void saveAsDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            UPR.ExportDefinitionsXML(loc + "\\exec\\DefaultProp.xml");
            MessageBox.Show("Done.");
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UPR.Definitions = new List<UPropertyReader.ClassDefinition>();
            RefreshView();
            listBox2.Items.Clear();
            rtb1.Clear();
        }

        private void importFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = "";
            OpenFileDialog FileDialog1 = new OpenFileDialog();
            FileDialog1.Filter = "PropertyDef(*.xml)|*.xml";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = FileDialog1.FileName;
                UPropertyReader t = new UPropertyReader();
                t.ImportDefinitionsXML(path);
                for (int i = 0; i < t.Definitions.Count; i++)
                {
                    int n = -1;
                    for (int j = 0; j < UPR.Definitions.Count; j++)
                        if (UPR.Definitions[j].name == t.Definitions[i].name)
                        {
                            n = j;
                            break;
                        }
                    if (n == -1)
                    {
                         System.Windows.Forms.DialogResult m = MessageBox.Show("Do you want to import class : \"" + t.Definitions[i].name + "\"?", "ME3 Explorer", MessageBoxButtons.YesNo);
                         if (m == System.Windows.Forms.DialogResult.Yes)
                             UPR.Definitions.Add(t.Definitions[i]);
                    }
                    else
                    {
                        UPropertyReader.ClassDefinition d = t.Definitions[i];
                        for (int j = 0; j < d.props.Count; j++)
                        {
                            int m = -1;
                            for (int k = 0; k < UPR.Definitions[n].props.Count; k++)
                                if (d.props[j].name == UPR.Definitions[n].props[k].name)
                                {
                                    m = j;
                                    break;
                                }
                            if (m == -1)
                            {
                                System.Windows.Forms.DialogResult m2 = MessageBox.Show("Do you want to import the property: \"" + t.Definitions[i].props[j].name + "\" into class : \"" + t.Definitions[i].name + "\"?", "ME3 Explorer", MessageBoxButtons.YesNo);
                                if (m2 == System.Windows.Forms.DialogResult.Yes)
                                    UPR.Definitions[n].props.Add(t.Definitions[i].props[j]);
                            }
                        }
                    }
                }
                RefreshView();
            }
        }

        private void copyClassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            string classname = Microsoft.VisualBasic.Interaction.InputBox("Please enter a new class name", "ME3 Explorer", "", 0, 0);
            if (classname == "")
                return;
            UPropertyReader.ClassDefinition c = new UPropertyReader.ClassDefinition();
            c = UPR.Definitions[n];
            c.name = classname;
            UPR.Definitions.Add(c);
            RefreshView();
        }

        private void PropertyManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }
    }
}
