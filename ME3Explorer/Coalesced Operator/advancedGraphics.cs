using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using ME3Explorer.Coalesced_Editor;

namespace ME3Explorer.Coalesced_Operator
{
    public partial class advancedGraphics : Form
    {
        public static string tempPath = System.IO.Path.GetTempPath() + "CoalTemp\\";
        public string coalescedPath;
        

        FileWrapper JSONfile = opClass.LoadJSON(tempPath + "\\04_bioengine.json");
        public advancedGraphics()
        {
            InitializeComponent();

            char[] forSplitting = {'(', ')', ',', '='};
            

            #region Textures and stuff.
            //
            //Textures and meshes
            //

            // Characters setting
            string temp = opClass.ReadEntry(JSONfile, "systemsettings", "texturegroup_character", 0);
            string[] temp_split = temp.Split(forSplitting);

            if (temp_split[6] == "1") comboBox1.SelectedItem = "Medium";
            else if (temp_split[6] == "2") comboBox1.SelectedItem = "Low";
            else comboBox1.SelectedItem = "High";


            //Objects
             temp = opClass.ReadEntry(JSONfile, "systemsettings", "texturegroup_character_1024", 0);
             temp_split = temp.Split(forSplitting);

             if (temp_split[6] == "1") comboBox2.SelectedItem = "Medium";
             else if (temp_split[6] == "2") comboBox2.SelectedItem = "Low";
             else if (temp_split[6] == "0") comboBox2.SelectedItem = "High";

            //World objects
             temp = opClass.ReadEntry(JSONfile, "systemsettings", "texturegroup_character_diff", 0);
             temp_split = temp.Split(forSplitting);

             if (temp_split[6] == "1") comboBox3.SelectedItem = "Medium";
             else if (temp_split[6] == "2") comboBox3.SelectedItem = "Low";
             else if (temp_split[6] == "0") comboBox3.SelectedItem = "High";

            //Environment
             temp = opClass.ReadEntry(JSONfile, "systemsettings", "texturegroup_environment_1024", 0);
             temp_split = temp.Split(forSplitting);

             if (temp_split[6] == "1") comboBox4.SelectedItem = "Medium";
             else if (temp_split[6] == "2") comboBox4.SelectedItem = "Low";
             else if (temp_split[6] == "0") comboBox4.SelectedItem = "High";

            //VFX
             temp = opClass.ReadEntry(JSONfile, "systemsettings", "texturegroup_vfx_1024", 0);
             temp_split = temp.Split(forSplitting);

             if (temp_split[6] == "1") comboBox5.SelectedItem = "Medium";
             else if (temp_split[6] == "2") comboBox5.SelectedItem = "Low";
             else if (temp_split[6] == "0") comboBox5.SelectedItem = "High";

            //skeletal
            string skeletalLOD = opClass.ReadEntry(JSONfile, "systemsettings", "skeletalmeshlodbias", 0);

            if (skeletalLOD == "1") comboBox6.SelectedItem = "Low";
            else if (skeletalLOD == "0") comboBox6.SelectedItem = "Normal";
            
           
            toolTip1.SetToolTip(label1, "Setting to lower settings makes characters in-cinematics and conversations use lower quality textures. Might help with performance on machines with less video memory.");
            toolTip2.SetToolTip(label2, "Setting to lower settings makes some characters and some misc objects in-game use lower quality textures. Might help with performance on machines with less video memory.");
            toolTip3.SetToolTip(label3, "Setting to lower settings makes objects in game such as weaponry and misc items use lowe quality textures. Might help with performance on machines with less video memory.");
            toolTip4.SetToolTip(label4, "Setting to lower settings makes in-game static objects (parts of enviroment like tables and chairs and other decorations) use lower quality textures.");
            toolTip5.SetToolTip(label5, "Setting to lower settings makes textures used on in-game effects like explosions lower quality. Might help with performance on intensive battle scenes on lower end macines.");
            toolTip6.SetToolTip(label6, "Setting to lower settings reduces the complexity of skeletal mesh models in game. Might help with performance on older machines.");
            #endregion


            #region Post process and stuff. 
            //
            // Post process and misc
            //

            //radial blur
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "allowradialblur", 0);
            if (temp == "true" || temp == "True") comboBox7.SelectedItem = "ON";
            else comboBox7.SelectedItem = "OFF";

            //motion blur
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "motionblur", 0);
            if (temp == "true" || temp == "True") comboBox8.SelectedItem = "ON";
            else comboBox8.SelectedItem = "OFF";

            //Light shafts
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "ballowlightshafts", 0);
            if (temp == "true" || temp == "True") comboBox9.SelectedItem = "ON";
            else comboBox9.SelectedItem = "OFF";

            //FXAA
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "ballowpostprocessaa", 0);
            if (temp == "true" || temp == "True") comboBox10.SelectedItem = "ON";
            else comboBox10.SelectedItem = "OFF";

            //MLAA
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "ballowpostprocessmlaa", 0);
            if (temp == "true" || temp == "True") comboBox11.SelectedItem = "ON";
            else comboBox11.SelectedItem = "OFF";

            //Bloom
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "bloom", 0);
            if (temp == "true" || temp == "True") comboBox12.SelectedItem = "ON";
            else comboBox12.SelectedItem = "OFF";

            //HQ Bloom
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "usehighqualitybloom", 0);
            if (temp == "true" || temp == "True") comboBox13.SelectedItem = "ON";
            else comboBox13.SelectedItem = "OFF";

            //DOF
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "depthoffield", 0);
            if (temp == "true" || temp == "True") comboBox14.SelectedItem = "ON";
            else comboBox14.SelectedItem = "OFF";

            //Distorzion
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "distortion", 0);
            if (temp == "true" || temp == "True") comboBox15.SelectedItem = "ON";
            else comboBox15.SelectedItem = "OFF";

            //Decals
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "dynamicdecals", 0);
            if (temp == "true" || temp == "True") comboBox16.SelectedItem = "ON";
            else comboBox16.SelectedItem = "OFF";

            //lights
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "dynamiclights", 0);
            if (temp == "true" || temp == "True") comboBox17.SelectedItem = "ON";
            else comboBox17.SelectedItem = "OFF";

            //shadows
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "dynamicshadows", 0);
            if (temp == "true" || temp == "True") comboBox19.SelectedItem = "ON";
            else comboBox19.SelectedItem = "OFF";

            //shadow resolution
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "maxshadowresolution", 0);
            comboBox21.SelectedItem = temp;

            //better shadows
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "ballowbettermodulatedshadows", 0);
            if (temp == "true" || temp == "True") comboBox20.SelectedItem = "ON";
            else comboBox20.SelectedItem = "OFF";

            //V-sync
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "usevsync", 0);
            if (temp == "true" || temp == "True") comboBox22.SelectedItem = "ON";
            else comboBox22.SelectedItem = "OFF";

            //Anisotrophic filter
            temp = opClass.ReadEntry(JSONfile, "systemsettings", "maxanisotropy", 0);
            if (temp == "1") comboBox18.SelectedItem = "1x - none";
            else if (temp == "2") comboBox18.SelectedItem = "2x";
            else if (temp == "3") comboBox18.SelectedItem = "4x";



            toolTip7.SetToolTip(label7, "Motion blur with a center. Used in explosion and other effects");
            toolTip8.SetToolTip(label8, "Motion blur effect used when sprinting and turning around in-combat");
            toolTip9.SetToolTip(label9, "Light shafts produced from bright sources (usually suns) and occluded by objects to produce \"volume shadow\" effect");
            toolTip10.SetToolTip(label10, "nVidia's Fast Aproximate Anti-Aliasing (FXAA). Produces good image quality without much loss in performance on newer machines. Default is on");
            toolTip11.SetToolTip(label11, "ATI's Morphological Anti-Aliasing (MLAA), produces superior image quality, but expect a performance loss of aproximately 30-50%. Run only on beefy computer! Default is off.");
            toolTip12.SetToolTip(label12, "Well known post process effect. Disable if you find it annoying. ");
            toolTip13.SetToolTip(label13, "A higher quality algorithm for rendering Bloom effect.");
            toolTip14.SetToolTip(label14, "Effect which produces \"blurring\" of objects which would be out of focus of virtual camera. Is nice. ");
            toolTip15.SetToolTip(label15, "This effect distorts the image, for example \"heat haze\" effect produced briefly by muzzle flashes on weapons.");
            toolTip16.SetToolTip(label16, "Toggle the effects such as bullet holes and scorch marks which show on surfaces");
            toolTip17.SetToolTip(label17, "Toggle if dynamics lights are enabled and cast light on nearby surfaces, for example: firearm muzzle flashes and power effects");
            toolTip18.SetToolTip(label18, "Self-explanatory: if true, dynamic shadows are cast by characters and objects.");
            toolTip19.SetToolTip(label19, "Resolution of the shadows cast by characters and objects on the world. Higher resolution - less jagged the shadows appear");
            toolTip20.SetToolTip(label20, "Better modulated shadows are of higher quality and more photo-realism, but only slightly.");
            toolTip21.SetToolTip(label21, "Will force the GPU to wait for a new monitor refresh cycle before sending rendered image. If your machine does not have a constant 60+FPS framerate, it's advisable to turn this off as it might help to improve performance.");
            toolTip22.SetToolTip(label22, "Sets the quality of anisotrophic filtering of textures. Higher settings produce sharper and clearer textures when viewed at an angle, but require significant graphical processing power");

            #endregion
        }

        private void tabPage2_Click(object sender, EventArgs e)
        {

        }

        private void advancedGraphics_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Text = "Moment...";
            Application.DoEvents();

            //
            //To modify Textures and meshes
            //

            //Characters
            if (comboBox1.SelectedItem == "High") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character", 0, "(MinLODSize=1,MaxLODSize=4096,LODBias=0,MinMagFilter=aniso,MipFilter=point)");
            else if (comboBox1.SelectedItem == "Medium") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character", 0, "(MinLODSize=1,MaxLODSize=4096,LODBias=1,MinMagFilter=aniso,MipFilter=point)");
            else opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character", 0, "(MinLODSize=1,MaxLODSize=4096,LODBias=2,MinMagFilter=aniso,MipFilter=point)");

            //Objects
            if (comboBox2.SelectedItem == "High") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character_1024", 0, "(MinLODSize=32,MaxLODSize=1024,LODBias=0)");
            else if (comboBox2.SelectedItem == "Medium") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character_1024", 0, "(MinLODSize=32,MaxLODSize=1024,LODBias=1)");
            else opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character_1024", 0, "(MinLODSize=32,MaxLODSize=1024,LODBias=2)");


            //Weapons
            if (comboBox3.SelectedItem == "High") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character_diff", 0, "(MinLODSize=32,MaxLODSize=512,LODBias=0)");
            else if (comboBox3.SelectedItem == "Medium") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character_diff", 0, "(MinLODSize=32,MaxLODSize=512,LODBias=1)");
            else opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_character_diff", 0, "(MinLODSize=32,MaxLODSize=512,LODBias=2)");

            //Environment
            if (comboBox4.SelectedItem == "High") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_environment_1024", 0, "(MinLODSize=32,MaxLODSize=1024,LODBias=0)");
            else if (comboBox4.SelectedItem == "Medium") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_environment_1024", 0, "(MinLODSize=32,MaxLODSize=1024,LODBias=1)");
            else opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_environment_1024", 0, "(MinLODSize=32,MaxLODSize=1024,LODBias=2)");

            //VFX
            if (comboBox5.SelectedItem == "High") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_vfx_1024", 0, "(MinLODSize=8,MaxLODSize=1024,LODBias=0)");
            else if (comboBox5.SelectedItem == "Medium") opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_vfx_1024", 0, "(MinLODSize=8,MaxLODSize=1024,LODBias=1)");
            else opClass.WriteEntry(JSONfile, "systemsettings", "texturegroup_vfx_1024", 0, "(MinLODSize=8,MaxLODSize=1024,LODBias=2)");

            //Skeletals
            if (comboBox6.SelectedItem == "Normal") opClass.WriteEntry(JSONfile, "systemsettings", "skeletalmeshlodbias", 0, "0");
            else opClass.WriteEntry(JSONfile, "systemsettings", "skeletalmeshlodbias", 0, "1");


            //finally;
            opClass.SaveJSON(JSONfile, tempPath + "\\04_bioengine.json");
            opClass.SaveBIN(coalescedPath, tempPath);
            button1.Text = "Modify!";
            MessageBox.Show("Coalesced.bin file successfully compiled! Enjoy your game!");

        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Text = "Moment...";
            Application.DoEvents();
            //To modify Post process stuff
            //Oh boy, here we go...


            //radial blur
            if (comboBox7.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "allowradialblur", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "allowradialblur", 0, "False");

            //motion blur
            if (comboBox8.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "motionblur", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "motionblur", 0, "False");

            //Lightshafts
            if (comboBox9.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "ballowlightshafts", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "ballowlightshafts", 0, "False");

            //FXAA
            if (comboBox10.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "ballowpostprocessaa", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "ballowpostprocessaa", 0, "False");

            //MLAA
            if (comboBox11.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "ballowpostprocessmlaa", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "ballowpostprocessmlaa", 0, "False");

            //Bloom
            if (comboBox12.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "bloom", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "bloom", 0, "False");

            //HQ Bloom
            if (comboBox13.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "usehighqualitybloom", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "usehighqualitybloom", 0, "False");

            //DOF
            if (comboBox14.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "depthoffield", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "depthoffield", 0, "False");

            //Distorsion
            if (comboBox15.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "distortion", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "distortion", 0, "False");

            //Dynamic decals
            if (comboBox16.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "dynamicdecals", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "dynamicdecals", 0, "False");

            //Dynamic lights
            if (comboBox17.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "dynamiclights", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "dynamiclights", 0, "False");

            //Dynamic shadows
            if (comboBox19.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "dynamicshadows", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "dynamicshadows", 0, "False");

            //Shadow resolution
            opClass.WriteEntry(JSONfile, "systemsettings", "maxshadowresolution", 0, comboBox21.SelectedItem.ToString() );

            //Better shadows
            if (comboBox20.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "ballowbettermodulatedshadows", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "ballowbettermodulatedshadows", 0, "False");

            //V-sync
            if (comboBox22.SelectedItem == "ON") opClass.WriteEntry(JSONfile, "systemsettings", "usevsync", 0, "True");
            else opClass.WriteEntry(JSONfile, "systemsettings", "usevsync", 0, "False");

            //Anisotrophic filtering
            if (comboBox18.SelectedItem == "1x - none") opClass.WriteEntry(JSONfile, "systemsettings", "maxanisotropy", 0, "1");
            else if (comboBox18.SelectedItem == "2x") opClass.WriteEntry(JSONfile, "systemsettings", "maxanisotropy", 0, "2");
            else opClass.WriteEntry(JSONfile, "systemsettings", "maxanisotropy", 0, "3");


            opClass.SaveJSON(JSONfile, tempPath + "\\04_bioengine.json");
            opClass.SaveBIN(coalescedPath, tempPath);
            button2.Text = "Modify!";
            MessageBox.Show("Coalesced.bin file successfully compiled! Enjoy your game!");
            


        }

       
    }
}
