using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Utilities;
using System;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    /// <summary>
    /// Dispatches compilation to the appropriate UnrealScriptCompiler method based on export class.
    /// </summary>
    internal static class ScriptCompilationService
    {
        public readonly record struct CompilationResult(MessageLog Log, string EarlyReturnMessage);

        /// <summary>
        /// Compiles the given script text for the export. Returns a log on success,
        /// or an early-return message string if the export cannot be compiled.
        /// </summary>
        public static CompilationResult Compile(ExportEntry export, string scriptText, FileLib fileLib)
        {
            if (!export.IsDefaultObject && export.IsInDefaultsTree())
            {
                return new CompilationResult(null, "Cannot compile properties of a subobject. You can edit the properties in the subobject declaration in the Default__ object this is descended from.");
            }

            MessageLog log;
            UnrealScriptOptionsPackage usop = new();
            switch (export.ClassName)
            {
                case "Class":
                    (_, log) = UnrealScriptCompiler.CompileClass(export.FileRef, scriptText, fileLib, usop, export, export.Parent);
                    break;
                case "Function":
                    if (export.ObjectName.Name.StartsWith("__lambda__", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CompilationResult(null, "Lambdas cannot be edited on their own. You can edit them in the function they are defined in.");
                    }
                    (_, log) = UnrealScriptCompiler.CompileFunction(export, scriptText, fileLib, usop);
                    break;
                case "State":
                    (_, log) = UnrealScriptCompiler.CompileState(export, scriptText, fileLib, usop);
                    break;
                case "ScriptStruct":
                    (_, log) = UnrealScriptCompiler.CompileStruct(export, scriptText, fileLib, usop);
                    break;
                case "Enum":
                    (_, log) = UnrealScriptCompiler.CompileEnum(export, scriptText, fileLib, usop);
                    break;
                case "Const":
                    return new CompilationResult(null, "Cannot compile const declarations");
                case "IntProperty":
                case "BoolProperty":
                case "FloatProperty":
                case "NameProperty":
                case "StrProperty":
                case "StringRefProperty":
                case "ByteProperty":
                case "ObjectProperty":
                case "ComponentProperty":
                case "InterfaceProperty":
                case "ArrayProperty":
                case "StructProperty":
                case "BioMask4Property":
                case "MapProperty":
                case "ClassProperty":
                case "DelegateProperty":
                    return new CompilationResult(null, "Cannot individually compile property declarations. You can edit them as part of the class.");
                default:
                    (_, log) = UnrealScriptCompiler.CompileDefaultProperties(export, scriptText, fileLib, usop);
                    break;
            }
            return new CompilationResult(log, null);
        }
    }
}
