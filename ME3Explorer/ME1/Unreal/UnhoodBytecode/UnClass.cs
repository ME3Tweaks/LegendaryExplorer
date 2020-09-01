using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Packages;

namespace ME3Explorer.ME1.Unreal.UnhoodBytecode
{
    public abstract class UnBytecodeOwner// : Decompilable
    {
        protected readonly ExportEntry _self;
        protected readonly byte[] _bytecode;

        protected UnBytecodeOwner(ExportEntry self, byte[] bytecode)
        {
            _self = self;
            _bytecode = bytecode;
        }

        public ExportEntry Export { get { return _self; } }
        public IMEPackage Package { get { return _self.FileRef; } }

        public abstract void Decompile(TextBuilder result);

        protected StatementList ReadBytecode(out BytecodeReader bcReader)
        {
            var s = new MemoryStream(_bytecode);
            var reader = new BinaryReader(s);
            bcReader = new BytecodeReader(_self.FileRef, reader);
            var statements = new StatementList((Statement)null);
            bool keepParsing = true;
            while (s.Position < s.Length && keepParsing)
            {
                int startOffset = (int)s.Position;
                //Debug.WriteLine("Reading token at 0x" + startOffset.ToString("X4"));

                BytecodeToken bc;
                try
                {
                    bc = bcReader.ReadNext();
                }
                catch (Exception)
                {
                    //extra bytes at the end can trigger this. They are not used. may be something like byte aligning or something
                    break;
                }
                if (bc == null /*|| bc is EndOfScriptToken*/) break;
                statements.Add(new Statement(startOffset, (int)s.Position, bc, bcReader));
                if (bc is ErrorBytecodeToken)
                {
                    var errorToken = (ErrorBytecodeToken)bc;
                    int bytecode = errorToken.UnknownBytecode;
                    if (bytecode >= 0)
                    {
                        ProblemRegistry.RegisterUnknownBytecode((byte)bytecode, this, errorToken.SubsequentBytes);
                    }
                    else
                    {
                        ProblemRegistry.RegisterBytecodeError(this, errorToken.ToString());
                    }
                    break;
                }

                if (/*bc is LabelTableToken || /*bc is StopToken ||*/ bc is EndOfScriptToken)
                {
                    keepParsing = false;
                    break; //Nothing else to parse is bytecode
                }
            }
            return statements;
        }

        public void DecompileBytecode(StatementList statements, TextBuilder result, bool createControlStatements)
        {
            var labelTableStatement = statements.Find(s => s.Token is LabelTableToken);
            var labelTable = (LabelTableToken)labelTableStatement?.Token;
            result.HasErrors = statements.HasErrors();
            if (createControlStatements)
            {
                try
                {
                    statements.CreateControlStatements();
                }
                catch (Exception)
                {
                    ProblemRegistry.RegisterIncompleteControlFlow(this);
                }
                if (statements.IsIncompleteControlFlow())
                {
                    ProblemRegistry.RegisterIncompleteControlFlow(this);
                }
                statements.RemoveRedundantReturns();
            }
            statements.Print(result, labelTable, !createControlStatements);
        }
    }

    public abstract class UnContainer : UnBytecodeOwner
    {
        protected readonly IEntry _super;

        protected UnContainer(ExportEntry self, int superIndex, byte[] bytecode)
            : base(self, bytecode)
        {
            _super = superIndex == 0 ? null : _self.FileRef.GetEntry(superIndex);
        }

        //protected void DecompileChildren(TextBuilder result, bool reverse)
        //{
        //    var collection = reverse ? _self.Children.Reverse() : _self.Children;
        //    foreach (ExportEntry export in collection)
        //    {
        //        try
        //        {
        //            object instance = export.ReadInstance();
        //            if (instance is Decompilable)
        //            {
        //                ((Decompilable)instance).Decompile(result);
        //            }
        //            else
        //            {
        //                result.Append("// ").Append(export.ToString()).Append("\n");
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            result.Append("//!!! Error decompiling " + export.ObjectName + ": " + e.Message);
        //        }
        //    }
        //}

        internal ExportEntry FindMemberExport(string name)
        {
            var export = _self.FileRef.Exports.SingleOrDefault(e => e.ObjectName == name && e.idxLink == _self.UIndex);
            if (export != null) return export;

            var superExport = _super;
            if (superExport != null)
            {
                /*var superClass = superExport.ReadInstance() as UnContainer;
                if (superClass != null)
                {
                    return superClass.FindMemberExport(name);
                }*/
                Debug.WriteLine("Looking for super export -- TODO FIX!!");
            }
            return null;
        }
    }

    public class UnClass : UnContainer
    {
        private readonly IEntry _outerInstance;
        private readonly FlagValues _flags;
        private readonly IEntry _defaults;
        private readonly string _config;
        private readonly List<string> _hideCategories;
        private readonly List<IEntry> _interfaces;

        internal UnClass(ExportEntry self, int superIndex, IEntry outerInstance, byte[] bytecode, FlagValues flags,
            IEntry defaults, string config, List<string> hideCategories, List<IEntry> interfaces)
            : base(self, superIndex, bytecode)
        {
            _outerInstance = outerInstance;
            _flags = flags;
            _defaults = defaults;
            _config = config;
            _hideCategories = hideCategories;
            _interfaces = interfaces;
        }

        public override void Decompile(TextBuilder result)
        {
            result.Append("class ").Append(_self.ObjectName.Instanced);
            if (_super != null)
                result.Append(" extends ").Append(_super.ObjectName.Instanced);
            if (_outerInstance != null && _outerInstance.ObjectName != "Object")
            {
                result.NewLine().Append("    within ").Append(_outerInstance.ObjectName.Instanced);
            }
            if (_hideCategories.Count > 0)
            {
                result.NewLine().Append("    hidecategories(").Append(string.Join(",", _hideCategories.ToArray())).Append(")");
            }
            if (_interfaces.Count > 0)
            {
                var intfNames = _interfaces.ConvertAll(e => e.ObjectName.Instanced).ToArray();
                result.NewLine().Append("    implements(").Append(string.Join(",", intfNames)).Append(")");
            }
            if (_config != "None")
            {
                result.NewLine().Append("    config(").Append(_config).Append(")");
            }
            _flags.Except("Compiled", "Parsed", "Config", "Localized").Each(f => result.NewLine().Append("    ").Append(f.ToLower()));
            result.Append(";").NewLine().NewLine();
            //DecompileChildren(result, false);

            var statementList = ReadBytecode(out var _);
            if (statementList.Count > 0)
            {
                DecompileReplicationBlock(result, statementList);
            }
            //if (_defaults != null)
            //{
            //    DecompileDefaultProperties(result);
            //}
        }

        private void DecompileReplicationBlock(TextBuilder result, StatementList statementList)
        {
            result.Append("replication\n{\n").PushIndent();
            for (int i = 0; i < statementList.Count; i++)
            {
                List<string> names = FindReplicatedProperties(statementList[i].StartOffset);
                if (names.Count > 0)
                {
                    result.Indent().Append("if (").Append(statementList[i].Token.ToString()).Append(")").NewLine();
                    result.Indent().Append("    ");
                    foreach (string name in names)
                    {
                        result.Append(name);
                        if (name != names.Last()) result.Append(", ");
                    }
                    result.Append(";").NewLine().NewLine();
                }
            }
            result.Append("}").NewLine().NewLine().PopIndent();
        }

        //private void DecompileDefaultProperties(TextBuilder result)
        //{
        //    result.Append("defaultproperties\n{\n").PushIndent();
        //    var defaultsExport = _defaults.Resolve();
        //    UnPropertyList propertyList = Package.ReadPropertyList(defaultsExport, this);
        //    foreach (UnProperty prop in propertyList.Properties)
        //    {
        //        var name = prop.Name;
        //        if (name.StartsWith("__") && name.EndsWith("__Delegate"))
        //        {
        //            name = name.Substring(2, name.Length - 2 - 10);
        //        }
        //        if (prop.Value is UnPropertyArray)
        //        {
        //            var array = (UnPropertyArray)prop.Value;
        //            for (int i = 0; i < array.Count; i++)
        //            {
        //                result.Indent().Append(name).Append("(").Append(i).Append(")=")
        //                    .Append(ValueToString(array[i], array.ElementType)).NewLine();
        //            }
        //        }
        //        else
        //        {
        //            result.Indent().Append(name).Append("=").Append(ValueToString(prop.Value, prop.Type)).NewLine();
        //        }
        //    }
        //    foreach (ExportEntry export in defaultsExport.Children)
        //    {
        //        result.Indent().Append("// child object " + export.ObjectName + " of type " + export.ClassName).NewLine();
        //    }
        //    result.Append("}").NewLine().PopIndent();
        //}

        //private string ValueToString(object value, string type)
        //{
        //    if (value == null)
        //        return "?";
        //    if (type == "BoolProperty")
        //        return (bool)value ? "true" : "false";
        //    if (type == "StrProperty")
        //        return "\"" + value + "\"";
        //    if (type == "StructProperty")
        //        return StructToString((UnPropertyList)value);
        //    return value.ToString();
        //}

        //private string StructToString(UnPropertyList value)
        //{
        //    if (value == null) return "?";
        //    var result = value.Properties.Aggregate("", (s, prop) => s + "," + prop.Name + "=" + ValueToString(prop.Value, prop.Type));
        //    return "(" + (result.Length > 0 ? result.Substring(1) : result) + ")";
        //}

        private List<string> FindReplicatedProperties(int offset)
        {
            var result = new List<string>();
            Debug.WriteLine("Looking for replicated properties... TODO");
            //foreach (ExportEntry export in _self.Children)
            //{
            //    var instance = export.ReadInstance() as UnClassProperty;
            //    if (instance != null && instance.RepOffset == offset)
            //    {
            //        result.Add(export.ObjectName);
            //    }
            //}
            return result;
        }
    }
}
