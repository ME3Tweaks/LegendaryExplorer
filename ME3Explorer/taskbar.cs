using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer
{

    public static class taskbar
    {
        public struct task_list
        {
            public Form tool;
            public ToolStripButton icon;
            public int ID;
        }
        public static ToolStrip strip;
        public static Image symbol;
        public static List<task_list> tools = new List<task_list>();
        public static int id_counter = 0;


        //call this in Form1 if you wish for your tool to appear in taskbar: taskbar.AddTool(Form X, imageList1.Images[Y]);
        //X is the form you're currently calling, example "px" for property manager
        //Y is the image to use for taskbar icon. 
        public static void AddTool(Form original, Image symbol)
        {

            //creating the button
            ToolStripButton tool_button = new ToolStripButton();
            tool_button.Text = original.Text;
            tool_button.Image = symbol;
            tool_button.Tag = tools.Count().ToString();
            tool_button.Click += new EventHandler(tipek_onclick); //I can haz event handler? No? ORLY?!
            //adding to toolstrip
            strip.Items.Add(tool_button);
            //creating new structure entry
            task_list add = new task_list();
            add.icon = tool_button;
            add.tool = original;
            add.ID = id_counter;
            tools.Add(add);
            //counter, you be here here.
            id_counter++;
        }
        //to remove the tool from taskbar when closing, make sure you have a FormClosing event and add: taskbar.RemoveTool(this);
        public static void RemoveTool(object sender)
        {
            Form origin = sender as Form; //see which form called the removal
            for (int i = 0; i < tools.Count(); i++)
            {                                       //search through structure list, see if we can find a match
                if (tools[i].tool == origin)
                {                                   //if match to origin form found, remove the icon from the toolbar associated with it
                    strip.Items.Remove(tools[i].icon);

                }
            }

        }
        public static void tipek_onclick(object sender, EventArgs e)
        {
            ToolStripButton origin = sender as ToolStripButton;
            //see which button called the event, bring up matching form to focus (or to front, whatever)
            int tagID = int.Parse(origin.Tag.ToString());
            for (int i = 0; i < tools.Count(); i++)
            {
                if (tools[i].ID == tagID) tools[i].tool.BringToFront();
            }

            //Danke, Voider!
        }

    }
}
