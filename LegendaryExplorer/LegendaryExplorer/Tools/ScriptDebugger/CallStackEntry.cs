using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.GameFilesystem;
using static LegendaryExplorer.Tools.ScriptDebugger.DebuggerInterface;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    public class CallStackEntry : NotifyPropertyChangedBase
    {
        public DebuggerFrame Frame;

        private bool _isNative;
        public bool IsNative
        {
            get => _isNative;
            set => SetProperty(ref _isNative, value);
        }

        public string FunctionFullPath { get; }

        public string DisplayText { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string FunctionFilePath;
        public string FunctionPathInFile;

        public CallStackEntry(DebuggerInterface debugger, DebuggerFrame frame)
        {
            Frame = frame;
            IsNative = frame.NativeFunction != IntPtr.Zero;
            FunctionFullPath = debugger.ReadASCIIString(frame.NodePath, frame.NodePathLength);
            if (IsNative)
            {
                DisplayText = "[Native] ";
            }
            DisplayText += FunctionFullPath;
            
            if (debugger.ReadObject(frame.Node).Linker is NLinker linker)
            {
                FunctionFilePath = Path.GetFullPath(linker.Filename, MEDirectories.GetExecutableFolderPath(debugger.Game));
            }
            else if (frame.FileName != IntPtr.Zero)
            {
                FunctionFilePath = Path.GetFullPath(debugger.ReadUnicodeString(frame.FileName, frame.FileNameLength), MEDirectories.GetExecutableFolderPath(debugger.Game));
            }
            if (FunctionFilePath is not null && Path.GetFileNameWithoutExtension(FunctionFilePath) is string fileName && FunctionFullPath.StartsWith(fileName))
            {
                FunctionPathInFile = FunctionFullPath[(fileName.Length + 1)..];
            }
            else
            {
                FunctionPathInFile = FunctionFullPath;
            }
        }
    }
}
