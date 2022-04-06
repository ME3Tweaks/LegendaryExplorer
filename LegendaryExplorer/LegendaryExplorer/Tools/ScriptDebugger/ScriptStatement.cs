using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    public class ScriptStatement : NotifyPropertyChangedBase
    {
        public string Text { get; }
        
        public int Position { get; }

        private bool _hasBreakPoint;
        public bool HasBreakPoint
        {
            get => _hasBreakPoint;
            set => SetProperty(ref _hasBreakPoint, value);
        }

        private bool _isCurrentStatement;
        public bool IsCurrentStatement
        {
            get => _isCurrentStatement;
            set => SetProperty(ref _isCurrentStatement, value);
        }

        public ScriptStatement(string text, int position)
        {
            Text = text;
            Position = position;
        }
    }
}
