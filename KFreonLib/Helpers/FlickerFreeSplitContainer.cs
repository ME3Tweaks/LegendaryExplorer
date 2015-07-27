using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

namespace KFreonLib.Helpers
{
    public partial class FlickerFreeSplitContainer : SplitContainer
    {
        public FlickerFreeSplitContainer()
        {

            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            MethodInfo objMethodInfo = typeof(Control).GetMethod("SetStyle", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] objArgs = new object[] { ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true };

            this.DoubleBuffered = true;
            objMethodInfo.Invoke(this.Panel1, objArgs);
            objMethodInfo.Invoke(this.Panel2, objArgs);

        }
    }
}
