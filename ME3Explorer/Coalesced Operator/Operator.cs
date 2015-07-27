using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using ME3Explorer.Coalesced_Editor;
using Gibbed.MassEffect3.FileFormats.Coalesced;
using KFreonLib.Debugging;

namespace ME3Explorer.Coalesced_Operator
{
    public partial class Operator : Form
    {
        CoalEditor editor = new CoalEditor();


        public string tempPath = "";
        public List<string> files;
        public string jsonname;
        public byte[] jsonfile;
        public FileWrapper file;

        string selected = "";
        string[] name = { };
        string parentNode;
        string[] parent = { };
        string weaponClass = "";
        string weaponLocation = "";

        string weaponPrefix = "";
        string enginePrefix = "";

        public string coalescedPath;
        public bool coalescedLoaded = false;

        public Operator()
        {
            InitializeComponent();



        }


        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (coalescedLoaded)
            {
                tv1.Nodes.Clear();

                label1.Text = "Weapons";

                checkBox1.Visible = true;
                checkBox2.Visible = true;
                checkBox1.Text = "Unlimited ammo";
                checkBox2.Text = "No reloads";

                checkBox3.Visible = false;
                checkBox4.Visible = false;
                checkBox5.Visible = false;
                checkBox6.Visible = false;


                label5.Text = "Damage";
                label6.Text = "Mag size";
                label7.Text = "Rate of fire";
                label8.Text = "Spare ammo";
                label9.Text = "Weight";
                label10.Text = "Hip fire accuracy";
                label11.Text = "Aiming accuracy";
                label12.Text = "Hip fire recoil";
                label13.Text = "Aiming recoil";



                //here opening Json file which contains good stuff


                file = opClass.LoadJSON(tempPath + "\\" + weaponPrefix + "_bioweapon.json");

                Dictionary<string, Dictionary<string, List<Entry>>> d = file.Sections;

                //here defining treenodes that will be added later
                TreeNode t = new TreeNode("Weapons");
                TreeNode categoryAssault = new TreeNode("Assault rifles");
                TreeNode categorySMG = new TreeNode("Submachine guns");
                TreeNode categoryShotguns = new TreeNode("Shotguns");
                TreeNode categorySniper = new TreeNode("Sniper Rifles");
                TreeNode categoryPistols = new TreeNode("Pistols");

                #region weapon sorting by categories
                //here scanning through JSON file and sorting out weapons in treeview catergories

                bool found = false;

                DebugOutput.PrintLn("Scanning for weapons and sorting in categories.");

                foreach (KeyValuePair<string, Dictionary<string, List<Entry>>> d2 in d)
                {


                    string[] names = d2.Key.Split('.');

                    if (names.Length > 1)
                    {

                        string[] gun = names[1].Split('_');


                        if (gun.Length >= 2)
                        {
                            if (gun[0] == "sfxweapon")
                            {

                                if (gun[1] == "assaultrifle")
                                {
                                    if (gun[2] != "base")
                                    {
                                        TreeNode t2 = new TreeNode(gun[2]);
                                        if (gun.Length == 4) t2.Text += "_" + gun[3];
                                        categoryAssault.Nodes.Add(t2);
                                        DebugOutput.PrintLn("Found assault rifle: \t" + gun[2]);

                                        if (!found)
                                        {
                                            weaponLocation = names[0] + "." + gun[0];
                                            found = true;

                                        }

                                    }
                                }
                                if (gun[1] == "smg")
                                {

                                    if (gun[2] != "base")
                                    {
                                        TreeNode t2 = new TreeNode(gun[2]);
                                        if (gun.Length == 4) t2.Text += "_" + gun[3];
                                        categorySMG.Nodes.Add(t2);
                                        DebugOutput.PrintLn("Found submachine gun: \t" + gun[2]);

                                        if (!found)
                                        {
                                            weaponLocation = names[0] + "." + gun[0];
                                            found = true;
                                        }
                                    }
                                }
                                if (gun[1] == "shotgun")
                                {
                                    if (gun[2] != "base")
                                    {
                                        TreeNode t2 = new TreeNode(gun[2]);
                                        if (gun.Length == 4) t2.Text += "_" + gun[3];
                                        categoryShotguns.Nodes.Add(t2);
                                        DebugOutput.PrintLn("Found shotgun: \t\t" + gun[2]);

                                        if (!found)
                                        {
                                            weaponLocation = names[0] + "." + gun[0];
                                            found = true;
                                        }
                                    }
                                }
                                if (gun[1] == "sniperrifle")
                                {
                                    if (gun[2] != "base")
                                    {
                                        TreeNode t2 = new TreeNode(gun[2]);
                                        if (gun.Length == 4) t2.Text += "_" + gun[3];
                                        categorySniper.Nodes.Add(t2);
                                        DebugOutput.PrintLn("Found sniper rifle: \t" + gun[2]);

                                        if (!found)
                                        {
                                            weaponLocation = names[0] + "." + gun[0];
                                            found = true;
                                        }
                                    }
                                }
                                if (gun[1] == "pistol")
                                {
                                    if (gun[2] != "base")
                                    {
                                        TreeNode t2 = new TreeNode(gun[2]);
                                        if (gun.Length == 4) t2.Text += "_" + gun[3];
                                        categoryPistols.Nodes.Add(t2);
                                        DebugOutput.PrintLn("Found pistol: \t\t" + gun[2]);

                                        if (!found)
                                        {
                                            weaponLocation = names[0] + "." + gun[0];
                                            found = true;
                                        }
                                    }
                                }
                            }
                        }

                    }



                }


                #endregion

                DebugOutput.PrintLn("Found weapon prefix: " + weaponLocation);

                t.Nodes.Add(categoryAssault);
                t.Nodes.Add(categorySMG);
                t.Nodes.Add(categoryShotguns);
                t.Nodes.Add(categorySniper);
                t.Nodes.Add(categoryPistols);
                tv1.Nodes.Clear();
                tv1.Nodes.Add(t);
            }
            else MessageBox.Show("Coalesced file not yet loaded! Please use the load button up there.");


        }
        #region toolstrip
        private void toolStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            tv1.Nodes.Clear();
            label1.Text = "Keybinder";
            Keybinder keybinder = new Keybinder();
            keybinder.tempPath = tempPath;
            keybinder.coalescedPATH = coalescedPath;
            keybinder.Show();


        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            if (coalescedLoaded)
            {
                advancedGraphics graphx = new advancedGraphics();
                graphx.coalescedPath = coalescedPath;
                graphx.Show();
            }
            else MessageBox.Show("You didn't load your Coalesced file yet! Please do that before moving forward! I mean, this is Coalesced Operator, after all.");

        }
        #endregion

        private void Operator_Load(object sender, EventArgs e)
        {
            coalescedLoaded = false;

            button1.Visible = false;
            button2.Visible = false;
            label1.Text = "";
            checkBox1.Visible = true;
            checkBox2.Visible = false;
            checkBox3.Visible = false;
            checkBox4.Visible = false;
            checkBox5.Visible = false;
            checkBox6.Visible = false;



        }



        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            if (coalescedLoaded)
            {
                file = opClass.LoadJSON(tempPath + "\\" + weaponPrefix + "_bioweapon.json");
                label1.Text = "Inventory";

                tv1.Nodes.Clear();

                checkBox1.Visible = true;
                checkBox1.Text = "Skip intro movie?";

                checkBox2.Visible = false;
                checkBox3.Visible = false;
                checkBox4.Visible = false;
                checkBox5.Visible = false;
                checkBox6.Visible = false;

                textBox2.Text = "";
                textBox3.Text = "";
                textBox4.Text = "";
                textBox5.Text = "";
                textBox6.Text = "";
                textBox7.Text = "";
                textBox8.Text = "";
                textBox9.Text = "";
                textBox10.Text = "";
                textBox11.Text = "";
                textBox13.Text = "";

                tv1.Nodes.Add("General inventory controls");

                label5.Text = "Fuel efficiency"; //textbox2
                label6.Text = "Fuel capacity";
                label7.Text = "Grenades";
                label8.Text = "Medigel";
                label9.Text = "";
                label10.Text = "";
                label11.Text = "";
                label12.Text = "";
                label13.Text = "";


                textBox2.Text = opClass.ReadEntry(file, "sfxgame.sfxinventorymanager", "fuelefficiency", 0);
                textBox3.Text = opClass.ReadEntry(file, "sfxgame.sfxinventorymanager", "maxfuel", 0);
                textBox4.Text = opClass.ReadEntry(file, "sfxgame.sfxinventorymanager", "maxgrenades", 0);
                textBox5.Text = opClass.ReadEntry(file, "sfxgame.sfxinventorymanager", "maxmedigel", 0);
            }
            else MessageBox.Show("Coalesced file not loaded yet!");

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            #region test checkboxes
            if (textBox1.Text == "Powers")
            {
                checkBox1.Visible = true;
                checkBox1.Text = "Armor protects";
                checkBox2.Visible = true;
                checkBox2.Text = "Cooldown x0.5";
                checkBox3.Visible = true;
                checkBox3.Text = "Cooldown x1.5";
                checkBox4.Visible = false;
                checkBox5.Visible = false;
                checkBox6.Visible = false;
            }

            if (textBox1.Text == "AI")
            {
                checkBox1.Text = "AI damage x0.5";
                checkBox2.Visible = true;
                checkBox2.Text = "AI damage x1.5";
                checkBox3.Visible = true;
                checkBox3.Text = "AI reaction time 40%";
                checkBox4.Visible = true;
                checkBox4.Text = "AI reaction time 200%";
                checkBox5.Visible = false;
                checkBox6.Visible = false;
            }

            if (textBox1.Text == "Squadmates")
            {
                checkBox1.Text = "AI damage x0.5";
                checkBox2.Visible = true;
                checkBox2.Text = "AI damage x1.5";
                checkBox3.Visible = true;
                checkBox3.Text = "AI damage x1.0";
                checkBox4.Visible = false;
                checkBox5.Visible = false;
                checkBox6.Visible = false;
            }
            if (textBox1.Text == "Global cheats")
            {
                checkBox1.Text = "God mode";
                checkBox2.Visible = false;
                checkBox3.Visible = false;
                checkBox4.Visible = false;
                checkBox5.Visible = false;
                checkBox6.Visible = false;
            }
            #endregion
        }


        #region bunch of stuff that does nothing

        private void testSAVEToolStripMenuItem_Click(object sender, EventArgs e)
        {


        }


        private void label5_Click(object sender, EventArgs e)
        {

        }
        private void panel5_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
        #endregion

        private void testLOADToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DebugOutput.PrintLn("Attempting to open file...");

            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Coalesced files|*.bin";

            if (d.ShowDialog() != System.Windows.Forms.DialogResult.Cancel)
            {
                tempPath = System.IO.Path.GetTempPath() + "CoalTemp\\";
                DebugOutput.PrintLn("Created temp folder : " + tempPath);

                opClass.LoadBIN(d.FileName, tempPath);
                DebugOutput.PrintLn("Loaded Coalesced bin file: " + d.FileName.ToString());

                StreamReader reader = new StreamReader(tempPath + "\\@coalesced.json");

                string infoFile;
                string[] tempString;

                do
                {
                    infoFile = reader.ReadLine();
                    tempString = infoFile.Split('_', '.');
                    if (tempString.Length != 1)
                    {
                        if (tempString[1] == "bioweapon")
                        {
                            weaponPrefix = tempString[0].Split('"')[1];
                            DebugOutput.PrintLn("Found bioweapon.ini, prefix: " + weaponPrefix);
                            break;
                        }
                    }

                } while (!reader.EndOfStream);

                reader = new StreamReader(tempPath + "\\@coalesced.json");

                do
                {
                    infoFile = reader.ReadLine();
                    tempString = infoFile.Split('_', '.');
                    if (tempString.Length != 1)
                    {
                        if (tempString[1] == "bioengine")
                        {
                            enginePrefix = tempString[0].Split('"')[1];
                            DebugOutput.PrintLn("Found bioengine.ini, prefix: " + enginePrefix);
                            break;
                        }
                    }

                } while (!reader.EndOfStream);

                reader.Close();

                if (weaponPrefix == "")
                {
                    MessageBox.Show("DLC .bin file loaded doesn't contain any relevant data to load. \n\nPlease click Open Coalesced button again and load another file");
                    DebugOutput.PrintLn("Did not find bioweapon.ini file in JSON collection. Aborting.");
                }

                else
                {

                    coalescedPath = d.FileName;

                    button1.Visible = true;
                    button2.Visible = true;
                    coalescedLoaded = true;
                    tv1.Nodes.Clear();
                }

            }
            else DebugOutput.PrintLn("User aborted. Coalesced not loaded.");

        }


        private void tv1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (label1.Text == "Weapons")
            {
                weaponClass = "";
                selected = tv1.SelectedNode.ToString();
                name = selected.Split(' ');
                textBox1.Text = name[1];

                if (name[1] != "Weapons")
                {
                    parentNode = tv1.SelectedNode.Parent.ToString();
                    parent = parentNode.Split(' ');

                    //weapon categorization, to correctly read and write treeview and JSON

                    if (parent[1] == "Assault") weaponClass = "assaultrifle";
                    if (parent[1] == "Submachine") weaponClass = "smg";
                    if (parent[1] == "Sniper") weaponClass = "sniperrifle";
                    if (parent[1] == "Shotguns") weaponClass = "shotgun";
                    if (parent[1] == "Pistols") weaponClass = "pistol";

                }


                textBox2.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "damage", 0);
                textBox3.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "magsize", 0);
                textBox4.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "rateoffire", 0);
                textBox5.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "maxspareammo", 0);
                textBox6.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "encumbranceweight", 0);
                textBox7.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "minaimerror", 0);
                textBox8.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "maxaimerror", 0);
                textBox9.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "minzoomaimerror", 0);
                textBox10.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "maxzoomaimerror", 0);
                textBox11.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "recoil", 0);
                textBox13.Text = opClass.ReadEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "zoomrecoil", 0);

                DebugOutput.PrintLn("Loaded stats for " + weaponClass + " " + name[1]);
            }

        }



        private void button1_Click(object sender, EventArgs e)
        {

            button1.Text = "Moment...";
            Application.DoEvents(); /*making the bastard render out 
                                            the "Moment" button before 
                                            doing demanding sh*t */

            opClass.SaveBIN(coalescedPath, tempPath);

            DebugOutput.PrintLn("Saved :");
            DebugOutput.PrintLn(coalescedPath);
            DebugOutput.PrintLn(tempPath);

            button1.Text = "2. Modify!";
            MessageBox.Show("All saved modifications applied!", "Done!");

        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (label1.Text == "Weapons")
            {
                if (checkBox1.Checked) opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "binfiniteammo", 0, "true");
                else opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "binfiniteammo", 0, "false");

                DebugOutput.PrintLn("Infinite ammo sorted: ");
                if (checkBox1.Checked) DebugOutput.PrintLn("True");
                else DebugOutput.PrintLn("False");

                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "damage", 0, textBox2.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "magsize", 0, textBox3.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "rateoffire", 0, textBox4.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "maxspareammo", 0, textBox5.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "encumbranceweight", 0, textBox6.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "minaimerror", 0, textBox7.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "maxaimerror", 0, textBox8.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "minzoomaimerror", 0, textBox9.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "maxzoomaimerror", 0, textBox10.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "recoil", 0, textBox11.Text);
                opClass.WriteEntry(file, weaponLocation + "_" + weaponClass + "_" + name[1], "zoomrecoil", 0, textBox13.Text);

                opClass.SaveJSON(file, tempPath + "\\" + weaponPrefix + "_bioweapon.json");

                DebugOutput.PrintLn("Saved " + name[1] + "'s properties in bioweapon.ini");

                MessageBox.Show("Changes to " + name[1] + " saved! \n\nWhen you're done with modifying all weapons, use \"Modify!\" button to apply all modifications", "Done");

            }

            if (label1.Text == "Inventory")
            {
                FileWrapper file2 = new FileWrapper();
                file2 = opClass.LoadJSON(tempPath + "\\" + enginePrefix + "_bioengine.json");
                List<Entry> entries = opClass.ReadAllEntries(file2, "fullscreenmovie", "startupmovies").ToList();
                int index = 0;

                if (checkBox1.Checked)
                {
                    foreach (Entry E in entries) if (E.ToString() == "ME_sig_logo") index = entries.IndexOf(E);
                    opClass.WriteEntry(file2, "fullscreenmovie", "startupmovies", index, ";ME3_sig_logo");
                    DebugOutput.PrintLn("Disabled intro movies. Saved in bioengine.ini");
                }

                else
                {
                    foreach (Entry E in entries) if (E.ToString() == ";ME_sig_logo") index = entries.IndexOf(E);
                    opClass.WriteEntry(file2, "fullscreenmovie", "startupmovies", index, "ME3_sig_logo");
                    DebugOutput.PrintLn("Enabled intro movies. Saved in bioengine.ini");
                }

                opClass.SaveJSON(file2, tempPath + "\\" + enginePrefix + "_bioengine.json");

                opClass.WriteEntry(file, "sfxgame.sfxinventorymanager", "fuelefficiency", 0, textBox2.Text);
                opClass.WriteEntry(file, "sfxgame.sfxinventorymanager", "maxfuel", 0, textBox3.Text);
                opClass.WriteEntry(file, "sfxgame.sfxinventorymanager", "maxgrenades", 0, textBox4.Text);
                opClass.WriteEntry(file, "sfxgame.sfxinventorymanager", "maxmedigel", 0, textBox5.Text);

                opClass.SaveJSON(file, tempPath + "\\" + weaponPrefix + "_bioweapon.json");

                DebugOutput.PrintLn("Saved inventory changes in bioweapon.ini");

                MessageBox.Show("Stored changes made to inventory properties! \n\nMake sure to use the \"Modify!\" button to apply them! ", "Done");

            }

        }

        private void Operator_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
            DebugOutput.PrintLn("Closed Operator.");
        }

    }
}


