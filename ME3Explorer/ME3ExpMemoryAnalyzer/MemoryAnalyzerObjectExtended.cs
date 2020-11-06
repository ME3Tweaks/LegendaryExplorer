using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using ME3ExplorerCore.Misc;

namespace ME3Explorer.ME3ExpMemoryAnalyzer
{
    public class MemoryAnalyzerObjectExtended : MemoryAnalyzerObject
    {
        public MemoryAnalyzerObjectExtended(string referenceName, WeakReference reference) : base(referenceName, reference) { }

        public override string ReferenceStatus
        {
            get
            {
                if (reference.IsAlive)
                {
                    if (reference.Target is FrameworkElement w)
                    {
                        return w.IsLoaded ? "In Memory, Open" : "In Memory, Closed";
                    }
                    if (reference.Target is System.Windows.Forms.Control f)
                    {
                        return f.IsDisposed ? "In Memory, Disposed" : "In Memory, Active";
                    }
                    return "In Memory";
                }
                return "Garbage Collected";
            }
        }
    }
}