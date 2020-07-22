using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Script.Analysis.Visitors;
using ME3Script.Decompiling;
using ME3Script.Language.Tree;

namespace ME3Explorer.ME3Script
{
    /// <summary>
    /// Interaction logic for UnrealScriptIDE.xaml
    /// </summary>
    public partial class UnrealScriptIDE : ExportLoaderControl
    {
        private string _scriptText;
        public string ScriptText
        {
            get => _scriptText;
            set => SetProperty(ref _scriptText, value);
        }

        public UnrealScriptIDE()
        {
            InitializeComponent();
            DataContext = this;
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            exportEntry.ClassName switch
            {
                "Class" => true,
                "State" => true,
                "Function" => true,
                _ => false
            };

        public override void LoadExport(ExportEntry export)
        {
            CurrentLoadedExport = export;
            try
            {
                ASTNode ast;
                switch (export.ClassName)
                {
                    case "Class":
                        ast = ME3ObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>());
                        break;
                    case "Function":
                        ast = ME3ObjectToASTConverter.ConvertFunction(export.GetBinaryData<UFunction>());
                        break;
                    case "State":
                        ast = ME3ObjectToASTConverter.ConvertState(export.GetBinaryData<UState>());
                        break;
                    case "Enum":
                        ast = ME3ObjectToASTConverter.ConvertEnum(export.GetBinaryData<UEnum>());
                        break;
                    case "ScriptStruct":
                        ast = ME3ObjectToASTConverter.ConvertStruct(export.GetBinaryData<UScriptStruct>());
                        break;
                    default:
                        if (export.ClassName.EndsWith("Property") && ObjectBinary.From(export) is UProperty uProp)
                        {
                            ast = ME3ObjectToASTConverter.ConvertVariable(uProp);
                        }
                        else
                        {
                            ast = ME3ObjectToASTConverter.ConvertDefaultProperties(export.GetProperties(), Pcc);
                        }
                        break;
                }

                if (ast != null)
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    ast.AcceptVisitor(codeBuilder);
                    ScriptText = codeBuilder.GetCodeString();
                }
            }
            catch (Exception e)
            {
                ScriptText = $"Error occured while decompiling {export.InstancedFullPath}:\n\n{e.FlattenException()}";
            }
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            ScriptText = string.Empty;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new BytecodeEditor(), CurrentLoadedExport)
                {
                    Title = $"Script Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            //
        }
    }
}
