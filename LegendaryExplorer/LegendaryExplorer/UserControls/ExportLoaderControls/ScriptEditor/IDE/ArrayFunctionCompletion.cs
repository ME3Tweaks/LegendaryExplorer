using FontAwesome5;
using FontAwesome5.Extensions;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Utilities;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE;

public sealed class ArrayFunctionCompletion : ICompletionData
{
    private object _description;
    public string Text { get; }

    private ArrayFunctionCompletion(Function func)
    {
        Text = func.Name;
        FauxFunction = func;
    }

    public static IEnumerable<ICompletionData> GenerateCompletions(MEGame game, DynamicArrayType arrType)
    {
        VariableType elementType = arrType.ElementType;
        PrimitiveType intType = new("int", EPropertyType.Int);
        PrimitiveType nameType = new("name", EPropertyType.Name);
        PrimitiveType objectType = new("object", EPropertyType.Object);
        string valueType = elementType.DisplayName();
        //ugly and no syntax highlighting, but this isn't valid unrealscript unfortunately, so... ¯\_(ツ)_/¯
        Const fauxSortDelegateType = new($"delegate<int SortDelegate({valueType}, {valueType})>)", "");
        List<ICompletionData> Completions =
        [
                new VariableCompletion(new(intType, default, "Length")),
                new ArrayFunctionCompletion(MakeFauxFunc("Add", true, [new (intType, default, "count")])),// "int Add(int count)"),
                new ArrayFunctionCompletion(MakeFauxFunc("AddItem", true, [new (elementType, default, "value")])),//$"int AddItem({valueType} value)"),
                new ArrayFunctionCompletion(MakeFauxFunc("Insert", false, [new (intType, default, "index"), new (intType, default, "count")])),//"Insert(int index, int count)"),
                new ArrayFunctionCompletion(MakeFauxFunc("Insert", true, [new (intType, default, "index"), new (elementType, default, "value")])),//$"int InsertItem(int index, {valueType} value)"),
                new ArrayFunctionCompletion(MakeFauxFunc("Remove", false, [new (intType, default, "index"), new (intType, default, "count")])),//"Remove(int index, int count)"),
                new ArrayFunctionCompletion(MakeFauxFunc("RemoveItem", true, [new (elementType, default, "value")])),//$"int RemoveItem({valueType} value)"),
                new ArrayFunctionCompletion(elementType is Struct ? MakeFauxFunc("Find", false, [new (nameType, default, "propertyName"), new (objectType, default, "value")]) 
                                                                  : MakeFauxFunc("Find", true, [new (elementType, default, "value")])),//"int Find(name propertyName, object value)" : $"int Find({valueType} value)"),
        ];

        if (game is not (MEGame.ME1 or MEGame.ME2))
        {
            Completions.Add(new ArrayFunctionCompletion(MakeFauxFunc("Sort", true, [new(fauxSortDelegateType, default, "sortDelegate")])));//$"Sort(delegate<int SortDelegate({valueType}, {valueType})> sortDelegate)")
        }
        return Completions;
    }

    private static Function MakeFauxFunc(string name, bool returns, List<FunctionParameter> parms)
    {
        var returnType = returns ? new VariableDeclaration(new PrimitiveType("int", EPropertyType.Int), UnrealFlags.EPropertyFlags.ReturnParm, "ReturnValue") : null;
        return new Function(name, default, returnType, null, parms);
    }

    private readonly Function FauxFunction;

    public object Description
    {
        get
        {
            if (_description is null)
            {
                _description = CompletionHelper.CreateDescriptionBlock(XamlCodeBuilder.GetFunctionSignature(FauxFunction));
            }
            return _description;
        }
    }

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