using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KFreonLib.GUI
{
    /// <summary>
    /// Salts (I think) TreeNode implementation. Extends to allow some extra stuff to be stored in each node.
    /// </summary>
    public class myTreeNode : TreeNode
    {
        // KFreon: Extra stuff for easy texture storage.
        public List<int> TexInds;
        public int TexInd;
        public int TexCount { get; set; }

        public myTreeNode()
            : base()
        {
            TexInds = new List<int>();
            TexInd = -1;
        }

        public myTreeNode(string text, string name)
            : base()
        {
            TexInds = new List<int>();
            TexInd = -1;
            this.Text = text;
            this.Name = name;
        }

        public myTreeNode(string text)
            : base()
        {
            TexInds = new List<int>();
            TexInd = -1;
            this.Text = text;
        }


        /// <summary>
        /// Returns number of textures stored in node. Is recursive, thus including subnodes.
        /// </summary>
        /// <returns>Number of textures in node and all its subnodes.</returns>
        public int NodeTextureCount()
        {
            int retval = TexInds.Count;
            if (Nodes == null || Nodes.Count == 0)
                return retval;
            else
                foreach (myTreeNode temp in Nodes)
                    retval += temp.NodeTextureCount();
            return retval;
        }
    }


    /// <summary>
    /// Provides threadsafe methods for changing ToolStripProgressBars, incl Incrementing and setting Value and Maximum properties.
    /// </summary>
    public class ProgressBarChanger
    {
        // KFreon: Strip object for invoking on correct thread
        ToolStrip Strip = null;
        ToolStripProgressBar Progbar = null;


        /// <summary>
        /// Contructor.
        /// </summary>
        /// <param name="strip">Base strip object for correct invoking.</param>
        /// <param name="progbar">ProgressBar to be targeted.</param>
        public ProgressBarChanger(ToolStrip strip, ToolStripProgressBar progbar)
        {
            Strip = strip;
            Progbar = progbar;
        }


        /// <summary>
        /// Increments targeted ProgressBar.
        /// </summary>
        /// <param name="amount">Optional. Amount to increment bar by. Defaults to 1.</param>
        public void IncrementBar(int amount = 1)
        {
            if (Strip.InvokeRequired)
                Strip.BeginInvoke(new Action(() => Progbar.Increment(amount)));
            else
                Progbar.Increment(amount);
        }


        /// <summary>
        /// Sets Value and Maximum properties of targeted ProgressBar.
        /// </summary>
        /// <param name="start">Value to set Value property to. i.e. Current value.</param>
        /// <param name="end">Value to set Maximum property to. i.e. Number of increments in bar.</param>
        public void ChangeProgressBar(int start, int end = -1)
        {
            if (Strip.InvokeRequired)
                Strip.BeginInvoke(new Action(() => ChangeProgressBar(start, end)));
            else
            {
                Progbar.Maximum = (end == -1) ? Progbar.Maximum : end;
                Progbar.Value = start;
            }
        }
    }


    /// <summary>
    /// Provides threadsafe methods to update text of a ToolStripItem's Text property. 
    /// </summary>
    public class TextUpdater
    {
        Control control = null;
        ToolStrip strip = null;
        ToolStripItem item = null;

        /// <summary>
        /// Constructor using a Control like a TextBox. Unused for now.
        /// </summary>
        /// <param name="givenControl">Control to alter.</param>
        public TextUpdater(Control givenControl)
        {
            control = givenControl;
        }


        /// <summary>
        /// Constructor for given ToolStripItem.
        /// </summary>
        /// <param name="givenControl">Control to monitor.</param>
        /// <param name="givenStrip">Base strip to correctly invoke with.</param>
        public TextUpdater(ToolStripItem givenControl, ToolStrip givenStrip)
        {
            strip = givenStrip;
            item = givenControl;
        }


        /// <summary>
        /// Updates text of targeted text property.
        /// </summary>
        /// <param name="text">New text to display.</param>
        public void UpdateText(string text)
        {
            // KFreon: Check which control to update
            if (control == null)
            {
                if (strip.InvokeRequired)
                    strip.BeginInvoke(new Action(() => UpdateText(text)));
                else
                    item.Text = text;
            }
            else
            {
                if (control.InvokeRequired)
                    control.BeginInvoke(new Action(() => control.Text = text));
                else
                    control.Text = text;
            }
        }
    }
}
