using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows;

namespace ME1Explorer
{

    public static class taskbar
    {
        public class task_list
        {
            public Form tool;
            public ToolStripButton icon;
            public int ID;
            public bool poppedOut;
            public TaskbarToolButton button;
            public Window wpfWindow;
        }
        private static ToolStrip strip;
        private static Form mdiParent;
        public static ToolStrip Strip { set { strip = value; mdiParent = (Form)value.TopLevelControl; } get { return strip; } }
        public static Image symbol;
        public static List<task_list> tools = new List<task_list>();
        public static int id_counter = 0;

        public class TaskbarToolButton : ToolStripButton
        {
            ContextMenu _contextMenu;

            public ContextMenu ContextMenu
            {
                get { return _contextMenu; }
                set { _contextMenu = value; }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && _contextMenu != null)
                    _contextMenu.Show(taskbar.strip, e.Location);
            }
        }

        //call this in Form1 if you wish for your tool to appear in taskbar: taskbar.AddTool(Form X, imageList1.Images[Y]);
        //X is the form you're currently calling, example "px" for property manager
        //Y is the image to use for taskbar icon. 
        public static void AddTool(Form original, Image symbol, bool startPoppedOut = false, Window wpfWindow = null)
        {
            original.MdiParent = mdiParent;
            original.WindowState = FormWindowState.Maximized;
            original.Show();

            //creating the button
            TaskbarToolButton tool_button = new TaskbarToolButton();
            tool_button.Image = symbol;
            tool_button.Tag = id_counter.ToString();
            tool_button.Click += new EventHandler(tipek_onclick);
            if (wpfWindow != null)
            {
                tool_button.Text = wpfWindow.Title;
                tool_button.ContextMenu = new ContextMenu(new MenuItem[] { new MenuItem("This tool cannot be docked.") });
                tool_button.ContextMenu.MenuItems[0].Enabled = false;
            }
            else
            {
                tool_button.Text = original.Text;
                tool_button.ContextMenu = new ContextMenu(new MenuItem[] { new MenuItem("Undock tool"), new MenuItem("Dock tool") });
                tool_button.ContextMenu.MenuItems[0].Click += HandleDockEvent;
                tool_button.ContextMenu.MenuItems[1].Click += HandleDockEvent;
                tool_button.ContextMenu.MenuItems[1].Enabled = startPoppedOut;
                tool_button.ContextMenu.MenuItems[0].Enabled = !startPoppedOut;
            }
            tool_button.ContextMenu.Tag = tool_button.Tag;

            strip.ImageScalingSize = new System.Drawing.Size(64, 64);
            //adding to toolstrip
            strip.Items.Add(tool_button);
            //creating new structure entry
            task_list add = new task_list();
            add.icon = tool_button;
            add.tool = original;
            add.ID = id_counter;
            add.poppedOut = startPoppedOut;
            add.button = tool_button;
            add.wpfWindow = wpfWindow;
            tools.Add(add);
            //counter, you be here here.
            id_counter++;
        }
        //to remove the tool from taskbar when closing, make sure you have a FormClosing event and add: taskbar.RemoveTool(this);
        public static void RemoveTool(object sender)
        {
            Form origin = sender as Form; //see which form called the removal
            if (origin != null)
            {
                for (int i = 0; i < tools.Count(); i++)
                {                                       //search through structure list, see if we can find a match
                    if (tools[i].tool == origin)
                    {                                   //if match to origin form found, remove the icon from the toolbar associated with it
                        strip.Items.Remove(tools[i].icon);
                        tools.RemoveAt(i);
                    }
                }
            }
            else
            {
                var wpf = sender as Window;
                for (int i = 0; i < tools.Count(); i++)
                {                                       
                    if (tools[i].wpfWindow == wpf)
                    {                                   
                        strip.Items.Remove(tools[i].icon);
                        tools.RemoveAt(i);
                    }
                }
            }
        }
        public static void tipek_onclick(object sender, EventArgs e)
        {
            ToolStripButton origin = sender as ToolStripButton;
            int tagID = int.Parse(origin.Tag.ToString());
            
            for (int i = 0; i < tools.Count(); i++)
            {
                if (tools[i].ID == tagID)
                {
                    if (tools[i].wpfWindow != null)
                        tools[i].wpfWindow.Focus();
                    else
                        tools[i].tool.BringToFront();
                }
            }
        }

        public static void HandleDockEvent(object sender, EventArgs e)
        {
            // Heff: Un-dock tool if deemed needed.
            var origin = (sender as MenuItem).Parent;
            int tagID = int.Parse(origin.Tag.ToString());

            for (int i = 0; i < tools.Count(); i++)
            {
                if (tools[i].ID == tagID)
                {
                    var taskEntry = tools[i];
                    if (taskEntry.poppedOut)
                    {
                        taskEntry.tool.Hide();
                        taskEntry.tool.MdiParent = mdiParent;
                        if (taskEntry.tool.MainMenuStrip != null)
                        {
                            taskEntry.tool.MainMenuStrip.Hide();
                        }
                        taskEntry.tool.Show();
                        taskEntry.tool.BringToFront();
                        taskEntry.tool.WindowState = FormWindowState.Maximized;
                        taskEntry.button.ContextMenu.MenuItems[0].Enabled = true;
                        taskEntry.button.ContextMenu.MenuItems[1].Enabled = false;
                        tools[i].poppedOut = false;
                    }
                    else
                    {
                        taskEntry.tool.Hide();
                        taskEntry.tool.MdiParent = null;
                        if (taskEntry.tool.MainMenuStrip != null)
                        {
                            taskEntry.tool.MainMenuStrip.Dock = DockStyle.Top;
                            taskEntry.tool.Controls.Add(taskEntry.tool.MainMenuStrip);
                            taskEntry.tool.MainMenuStrip.Show();
                        }
                        taskEntry.tool.Show();
                        taskEntry.tool.BringToFront();
                        taskEntry.tool.WindowState = FormWindowState.Normal;
                        taskEntry.button.ContextMenu.MenuItems[0].Enabled = false;
                        taskEntry.button.ContextMenu.MenuItems[1].Enabled = true;
                        tools[i].poppedOut = true;
                    }
                }
            }
        }
    }
}
