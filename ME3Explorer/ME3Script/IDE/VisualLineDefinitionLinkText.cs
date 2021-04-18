using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.UnrealScript.Language.Tree;

namespace ME3Explorer.ME3Script.IDE
{
    public class VisualLineDefinitionLinkText : VisualLineText
    {
        private readonly ASTNode Node;

        public VisualLineDefinitionLinkText(VisualLine parentVisualLine, ASTNode node, int length) : base(parentVisualLine, length)
        {
            Node = node;
        }

        protected override void OnQueryCursor(QueryCursorEventArgs e)
        {
            if (Keyboard.Modifiers.Has(ModifierKeys.Control))
            {
                e.Handled = true;
                e.Cursor = Cursors.Hand;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !e.Handled && Keyboard.Modifiers.Has(ModifierKeys.Control))
            {
                string filePath = null;
                int uIndex = 0;
                string name = "UNKNOWN";
                if (Node is IHasFileReference hasFileReference)
                {
                    filePath = hasFileReference.FilePath;
                    uIndex = hasFileReference.UIndex;
                    name = hasFileReference.Name;
                }

                if (filePath is null)
                {
                    MessageBox.Show($"Unable to navigate to definition of \"{name}\". This can happen if it is afunction parameter or local variable");
                    e.Handled = true;
                    return;
                }
                var pwpf = new PackageEditorWPF();
                pwpf.Show();
                pwpf.LoadFile(filePath, uIndex);
                pwpf.RestoreAndBringToFront();
                e.Handled = true;
            }
        }

        protected override VisualLineText CreateInstance(int length)
        {
            return new VisualLineDefinitionLinkText(ParentVisualLine, Node, length);
        }
    }
}
