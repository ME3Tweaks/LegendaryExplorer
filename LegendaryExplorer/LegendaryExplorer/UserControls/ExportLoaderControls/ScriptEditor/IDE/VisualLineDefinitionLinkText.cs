using System;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Rendering;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class VisualLineDefinitionLinkText : VisualLineText
    {
        private readonly ASTNode Node;
        private readonly Action<int, int> ScrollTo;

        public VisualLineDefinitionLinkText(VisualLine parentVisualLine, ASTNode node, int length, Action<int, int> scrollTo) : base(parentVisualLine, length)
        {
            Node = node;
            ScrollTo = scrollTo;
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
                e.Handled = true;
                GoToDefinition(Node, ScrollTo);
            }
        }

        public static void GoToDefinition(ASTNode node, Action<int, int> scrollTo)
        {
            string name = "UNKNOWN";
            node = node switch
            {
                StaticArrayType staticArrayType => staticArrayType.ElementType,
                ClassType classType => classType.ClassLimiter,
                DynamicArrayType dynArr => dynArr.ElementType,
                _ => node
            };
            if (node is IHasFileReference hasFileReference)
            {
                string filePath = hasFileReference.FilePath;
                int uIndex = hasFileReference.UIndex;
                name = hasFileReference.Name;

                if (filePath is not null)
                {
                    var pwpf = new Tools.PackageEditor.PackageEditorWindow();
                    pwpf.Show();
                    pwpf.LoadFile(filePath, uIndex);
                    pwpf.RestoreAndBringToFront();
                    return;
                }
            }
            if (node.StartPos >= 0 && node.TextLength > 0)
            {
                scrollTo(node.StartPos, node.TextLength);
                return;
            }
            if (node is PrimitiveType primitive)
            {
                MessageBox.Show($"Definitions of primitive types like '{primitive.Name}' are internal");
                return;
            }
            MessageBox.Show($"Unable to navigate to definition of \"{name}\". This can happen if it is defined in the script you are editing");
        }

        protected override VisualLineText CreateInstance(int length)
        {
            return new VisualLineDefinitionLinkText(ParentVisualLine, Node, length, ScrollTo);
        }
    }
}
