using System;
using System.Collections.Generic;
using System.Windows.Media;
using FontAwesome5;
using FontAwesome5.Extensions;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE;

public class ArrayFunctionCompletion : ICompletionData
{
    private readonly string _description;
    public string Text { get; }

    private ArrayFunctionCompletion(string name, string sig)
    {
        Text = name;
        _description = sig;
    }

    public static IEnumerable<ICompletionData> GenerateCompletions(MEGame game, DynamicArrayType arrType)
    {
        VariableType elementType = arrType.ElementType;
        string valueType = elementType.DisplayName();
        var completions = new List<ICompletionData>
        {
            new CompletionData("Length", "int Length"),
            new ArrayFunctionCompletion("Add", "int Add(int count)"),
            new ArrayFunctionCompletion("AddItem", $"int AddItem({valueType} value)"),
            new ArrayFunctionCompletion("Insert", "Insert(int index, int count)"),
            new ArrayFunctionCompletion("InsertItem", $"int InsertItem(int index, {valueType} value)"),
            new ArrayFunctionCompletion("Remove", "Remove(int index, int count)"),
            new ArrayFunctionCompletion("RemoveItem", $"int RemoveItem({valueType} value)"),
            new ArrayFunctionCompletion("Find", elementType is Struct ? "int Find(name propertyName, object value)" : $"int Find({valueType} value)")
        };

        if (game is not MEGame.ME1 or MEGame.ME2)
        {
            completions.Add(new ArrayFunctionCompletion("Sort", $"Sort(delegate<int SortDelegate({valueType}, {valueType})> sortDelegate)"));
        }
        return completions;
    }

    public object Description => _description;

    public object Content => Text;

    public double Priority => 0;

    private static readonly ImageSource image = EFontAwesomeIcon.Solid_Cube.CreateImageSource(Brushes.Black, 0.1);
    public ImageSource Image => image;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text + "()");
        textArea.Caret.Offset -= 1;
    }
}