using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Inherits from Export Loader Control. A control using this class has the ability to load its own files when popped out into its own window, for things like TLK file loading which are not package files.
    /// </summary>
    public abstract class FileExportLoaderControl : ExportLoaderControl
    {
        public abstract void LoadFile(string filepath);
        public abstract string LoadedFile { get; set; }
        public abstract bool CanLoadFile();
        internal abstract void OpenFile();
    }
}
