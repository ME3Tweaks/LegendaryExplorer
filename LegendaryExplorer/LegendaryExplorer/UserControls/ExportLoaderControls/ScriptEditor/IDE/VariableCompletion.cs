using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using FontAwesome5;
using FontAwesome5.Extensions;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class VariableCompletion : ICompletionData
    {
        private readonly VariableDeclaration varDecl;
        private string _description;
        private string _text;

        private VariableCompletion(VariableDeclaration decl)
        {
            varDecl = decl;
        }

        public static IEnumerable<VariableCompletion> GenerateCompletions(IEnumerable<VariableDeclaration> declarations)
        {
            foreach (VariableDeclaration decl in declarations)
            {
                string varTypeName = decl.VarType?.Name;
                //none of these are usable from unrealscript
                if (varTypeName is not null && (varTypeName is "Pointer" or "QWord" or "Double" || varTypeName.EndsWith("_Mirror")))
                {
                    continue;
                }
                yield return new VariableCompletion(decl);
            }
        }

        public string Text => _text ??= varDecl.Name;

        public object Description => _description ??= CodeBuilderVisitor.GetVariableDeclarationSignature(varDecl);

        public object Content => Text;

        public double Priority => 0;

        private static readonly ImageSource image = EFontAwesomeIcon.Solid_Table.CreateImageSource(Brushes.Black, 0.1);
        public ImageSource Image => image;


        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
