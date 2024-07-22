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
using LegendaryExplorerCore.UnrealScript.Language.Util;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class FunctionCompletion : ICompletionData
    {
        private readonly Function function;
        private string _description;
        private string _text;

        private FunctionCompletion(Function func)
        {
            function = func;
        }

        public static IEnumerable<FunctionCompletion> GenerateCompletions(IEnumerable<Function> functions, Class currentClass, bool staticsOnly = false, bool latents = false, bool iterators = false)
        {
            foreach (Function func in functions)
            {
                if (staticsOnly != func.IsStatic)
                {
                    continue;
                }
                if (!latents && func.Flags.Has(UnrealFlags.EFunctionFlags.Latent))
                {
                    continue;
                }
                if (!iterators && func.Flags.Has(UnrealFlags.EFunctionFlags.Iterator))
                {
                    continue;
                }
                if (func.IsOperator)
                {
                    continue;
                }
                if (func.Flags.Has(UnrealFlags.EFunctionFlags.Private) && currentClass != NodeUtils.GetContainingClass(func))
                {
                    continue;
                }
                if (func.Flags.Has(UnrealFlags.EFunctionFlags.Protected) && currentClass is not null && !currentClass.SameAsOrSubClassOf(NodeUtils.GetContainingClass(func)))
                {
                    continue;
                }
                yield return new FunctionCompletion(func);
            }
        }

        public string Text => _text ??= function.Name;

        public object Description => _description ??= CodeBuilderVisitor.GetFunctionSignature(function);

        public object Content => Text;

        public double Priority => 0;

        private static readonly ImageSource image = EFontAwesomeIcon.Solid_Cube.CreateImageSource(Brushes.Black, 0.1);
        public ImageSource Image => image;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text + "()");
            if (function.Parameters.Count > 0)
            {
                textArea.Caret.Offset -= 1;
            }
        }
    }
}
