using ME3Script.Analysis.Visitors;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.IO;
using ME3Explorer.ME3Script.Utilities;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using StreamHelpers;

namespace ME3Script.Decompiling
{
    //TODO: most likely cleaner to convert to stack-based solution like the tokenstream, investigate.
    public partial class ME3ByteCodeDecompiler : ObjectReader 
    {
        private readonly UStruct DataContainer;
        private IMEPackage PCC => DataContainer.Export.FileRef;
        private byte PopByte() { return ReadByte(); }

        private byte PeekByte => Position < Size ? _data[Position] : (byte)0;

        private byte PrevByte => (byte)(Position > 0 ? _data[Position - 1] : 0);

        private Dictionary<ushort, Statement> StatementLocations;
        private Stack<ushort> StartPositions;
        private List<List<Statement>> Scopes;
        private Stack<int> CurrentScope;

        private Stack<FunctionParameter> OptionalParams;
        private List<FunctionParameter> Parameters;

        private List<LabelTableEntry> LabelTable;

        private Stack<ushort> ForEachScopes; // For tracking ForEach etc endpoints

        private bool isInClassContext = false; // For super lookups

        private bool CurrentIs(StandardByteCodes val)
        {
            return PeekByte == (byte)val;
        }

        private int _totalPadding;
        private IEntry ReadObject()
        {
            var index = ReadInt32();
            var remaining = DataContainer.ScriptStorageSize - (Position - _totalPadding);
            Buffer.BlockCopy(_data, Position, _data, Position + 4, remaining); // copy the data forward
            Buffer.BlockCopy(new byte[]{0,0,0,0}, 0, _data, Position, 4); // write 0 padding

            _totalPadding += 4;
            Position += 4;

            return PCC.GetEntry(index);
        }
        public NameReference ReadNameReference()
        {
            return new NameReference(PCC.GetNameEntry(ReadInt32()), ReadInt32());
        }

        public ME3ByteCodeDecompiler(UStruct dataContainer, List<FunctionParameter> parameters = null)
            :base(new byte[dataContainer.ScriptBytecodeSize])
        {
            Buffer.BlockCopy(dataContainer.ScriptBytes, 0, _data, 0, dataContainer.ScriptStorageSize);
            DataContainer = dataContainer;
            Parameters = parameters;
        }

        public CodeBody Decompile()
        {
            // Skip native funcs
            if (DataContainer is UFunction Func && Func.FunctionFlags.HasFlag(FunctionFlags.Native))
            {
                var comment = new ExpressionOnlyStatement(null, null, new SymbolReference(null, null, null, "// Native function"));
                return new CodeBody(new List<Statement> { comment }, null, null);
            }

            Position = 0;
            _totalPadding = 0;
            CurrentScope = new Stack<int>();
            var statements = new List<Statement>();
            StatementLocations = new Dictionary<ushort, Statement>();
            StartPositions = new Stack<ushort>();
            Scopes = new List<List<Statement>>();
            LabelTable = new List<LabelTableEntry>();
            ForEachScopes = new Stack<ushort>();

            DecompileDefaultParameterValues(statements);

            Scopes.Add(statements);
            CurrentScope.Push(Scopes.Count - 1);
            while (Position < Size && !CurrentIs(StandardByteCodes.EndOfScript))
            {
                var current = DecompileStatement();
                if (current == null && PeekByte == (byte)StandardByteCodes.EndOfScript)
                    break; // Natural end after label table, no error
                if (current == null)
                    break; // TODO: ERROR!

                statements.Add(current);
            }
            CurrentScope.Pop(); ;
            AddStateLabels();

            return new CodeBody(statements, null, null);
        }

        private void AddStateLabels()
        {
            foreach (var label in LabelTable)
            {
                var node = new StateLabel(label.NameRef, (int)label.Offset, null, null);
                var statement = StatementLocations[(ushort)label.Offset];
                for (int n = 0; n < Scopes.Count; n++)
                {
                    var index = Scopes[n].IndexOf(statement);
                    if (index != -1)
                        Scopes[n].Insert(index, node);
                }
            }
        }

        private void DecompileDefaultParameterValues(List<Statement> statements)
        {
            OptionalParams = new Stack<FunctionParameter>();
            if (DataContainer is UFunction func) // Gets all optional params for default value parsing
            {
                foreach (FunctionParameter param in Parameters.Where(param => param.IsOptional))
                {
                    OptionalParams.Push(param);
                }
            }

            while (PeekByte == (byte)StandardByteCodes.DefaultParmValue 
                || PeekByte == (byte)StandardByteCodes.Nothing)
            {
                StartPositions.Push((ushort)Position);
                var token = PopByte();
                if (token == (byte)StandardByteCodes.DefaultParmValue) // default value assigned
                {
                    
                    ReadInt16(); //MemLength of value
                    var value = DecompileExpression();
                    PopByte(); // end of value

                    var builder = new CodeBuilderVisitor(); // what a wonderful hack, TODO.
                    value.AcceptVisitor(builder);

                    if (OptionalParams.Count != 0)
                    {
                        var parm = OptionalParams.Pop();
                        parm.Variables.First().Name += " = " + builder.GetCodeString();
                        StartPositions.Pop();
                    }
                    else
                    {       // TODO: weird, research how to deal with this
                        var comment = new SymbolReference(null, null, null, "// Orphaned Default Parm: " + builder.GetCodeString());
                        var statement = new ExpressionOnlyStatement(null, null, comment);
                        StatementLocations.Add(StartPositions.Pop(), statement);
                        statements.Add(statement);
                    }
                }
            }
        }
    }
}
