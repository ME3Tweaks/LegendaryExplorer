using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Script.Analysis.Symbols;
using ME3Script.Analysis.Visitors;
using ME3Script.Compiling.Errors;
using ME3Script.Decompiling;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Parsing;

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

        private ASTNode _rootNode;

        public ASTNode RootNode
        {
            get => _rootNode;
            set => SetProperty(ref _rootNode, value);
        }

        public UnrealScriptIDE()
        {
            InitializeComponent();
            DataContext = this;
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            exportEntry.Game == MEGame.ME3 && (exportEntry.ClassName switch
            {
                "Class" => true,
                "State" => true,
                "Function" => true,
                _ => false
            } || exportEntry.IsDefaultObject);

        public override void LoadExport(ExportEntry export)
        {
            CurrentLoadedExport = export;
            (RootNode, ScriptText) = DecompileExport(CurrentLoadedExport);
        }

        public static (ASTNode node, string text) DecompileExport(ExportEntry export)
        {
            try
            {
                ASTNode astNode = null;
                switch (export.ClassName)
                {
                    case "Class":
                        astNode = ME3ObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>());
                        break;
                    case "Function":
                        astNode = ME3ObjectToASTConverter.ConvertFunction(export.GetBinaryData<UFunction>());
                        break;
                    case "State":
                        astNode = ME3ObjectToASTConverter.ConvertState(export.GetBinaryData<UState>());
                        break;
                    case "Enum":
                        astNode = ME3ObjectToASTConverter.ConvertEnum(export.GetBinaryData<UEnum>());
                        break;
                    case "ScriptStruct":
                        astNode = ME3ObjectToASTConverter.ConvertStruct(export.GetBinaryData<UScriptStruct>());
                        break;
                    default:
                        if (export.ClassName.EndsWith("Property") && ObjectBinary.From(export) is UProperty uProp)
                        {
                            astNode = ME3ObjectToASTConverter.ConvertVariable(uProp);
                        }
                        else
                        {
                            astNode = ME3ObjectToASTConverter.ConvertDefaultProperties(export);
                        }

                        break;
                }

                if (astNode != null)
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    astNode.AcceptVisitor(codeBuilder);
                    return (astNode, codeBuilder.GetCodeString());
                }
            }
            catch (Exception e)
            {
                return (null, $"Error occured while decompiling {export?.InstancedFullPath}:\n\n{e.FlattenException()}");
            }

            return (null, "Could not decompile!");
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            ScriptText = string.Empty;
            RootNode = null;
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

        private void Decompile_OnClick(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport != null)
            {
                (RootNode, ScriptText) = DecompileExport(CurrentLoadedExport);
            }
        }

        private void CompileAST_OnClick(object sender, RoutedEventArgs e)
        {
            if (ScriptText != null)
            {
                MessageLog log;
                (RootNode, log) = CompileAST(ScriptText);

                if (RootNode != null && log.AllErrors.IsEmpty())
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    RootNode.AcceptVisitor(codeBuilder);
                    ScriptText = codeBuilder.GetCodeString();
                }

                outputListBox.ItemsSource = log.Content;
            }
        }

        public static (ASTNode ast, MessageLog log) CompileAST(string script)
        {
            var log = new MessageLog();
            var parser = new ClassOutlineParser(new TokenStream<string>(new StringLexer(script, log)), log);
            try
            {
                Class ast = parser.ParseDocument();
                if (ast != null)
                {
                    log.LogMessage("Parsed!");
                    //var symbols = SymbolTable.CreateBaseTable(ast);
                    //var validator = new ClassValidationVisitor(log, symbols);
                    //ast.AcceptVisitor(validator);
                }
                else
                {
                    log.LogMessage("Parse failed!");
                }

                return (ast, log);
            }
            catch (Exception e)
            {
                log.LogMessage($"Parse failed! Exception: {e}");
                return (null, log);
            }

        }

        private void outputListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count == 1 && e.AddedItems[0] is PositionedMessage msg)
            {
                scriptTextBox.Focus();
                scriptTextBox.Select(msg.Start.CharIndex, msg.End.CharIndex - msg.Start.CharIndex);
            }
        }
    }
}
