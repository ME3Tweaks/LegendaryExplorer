using ME3Data.DataTypes;
using ME3Data.DataTypes.ScriptTypes;
using ME3Data.FileFormats.PCC;
using ME3Data.Utility;
using ME3Script.Analysis.Visitors;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    //TODO: most likely cleaner to convert to stack-based solution like the tokenstream, investigate.
    public partial class ME3ByteCodeDecompiler : ObjectReader 
    {
        private ME3Struct DataContainer;
        private PCCFile PCC { get { return DataContainer.ExportEntry.CurrentPCC; } }

        private Byte CurrentByte { get { return _data[Position]; } } // TODO: meaningful error handling here..
        private Byte PopByte() { return ReadByte(); }
        private Byte PeekByte { get { return Position < Size ? _data[Position + 1] : (byte)0; } }
        private Byte PrevByte { get { return Position > 0 ? _data[Position - 1] : (byte)0; } }

        private Dictionary<UInt16, Statement> StatementLocations;
        private Stack<UInt16> StartPositions;
        private Stack<List<Statement>> Scopes;

        private Stack<FunctionParameter> OptionalParams;
        private List<FunctionParameter> Parameters;

        private bool CurrentIs(StandardByteCodes val)
        {
            return CurrentByte == (byte)val;
        }

        private int _totalPadding;
        private ObjectTableEntry ReadObject()
        {
            var index = ReadIndex();
            var remaining = DataContainer.DataScriptSize - (Position - _totalPadding);
            Buffer.BlockCopy(_data, Position, _data, Position + 4, remaining); // copy the data forward
            Buffer.BlockCopy(new byte[]{0,0,0,0}, 0, _data, Position, 4); // write 0 padding

            _totalPadding += 4;
            Position += 4;

            return PCC.GetObjectEntry(index);
        }

        public ME3ByteCodeDecompiler(ME3Struct dataContainer, List<FunctionParameter> parameters = null)
            :base(dataContainer.ByteScript)
        {
            DataContainer = dataContainer;
            Parameters = parameters;
        }

        public CodeBody Decompile()
        {
            Position = 0;
            _totalPadding = 0;
            var statements = new List<Statement>();
            StatementLocations = new Dictionary<UInt16, Statement>();
            StartPositions = new Stack<UInt16>();
            Scopes = new Stack<List<Statement>>();

            DecompileDefaultParameterValues();

            Scopes.Push(statements);
            while (Position < Size && !CurrentIs(StandardByteCodes.EndOfScript))
            {
                var current = DecompileStatement();
                if (current == null)
                    break; // TODO: ERROR!

                statements.Add(current);
            }
            Scopes.Pop();

            return new CodeBody(statements, null, null);
        }

        private void DecompileDefaultParameterValues()
        {
            OptionalParams = new Stack<FunctionParameter>();
            var func = DataContainer as ME3Function;
            if (func != null) // Gets all optional params for default value parsing
            {
                for (int n = 0; n < Parameters.Count; n++)
                {
                    if (func.Parameters[n].PropertyFlags.HasFlag(PropertyFlags.OptionalParm))
                        OptionalParams.Push(Parameters[n]);
                }
            }

            while (CurrentByte == (byte)StandardByteCodes.DefaultParmValue 
                || CurrentByte == (byte)StandardByteCodes.Nothing)
            {
                var token = PopByte();
                var parm = OptionalParams.Pop();
                if (token == (byte)StandardByteCodes.DefaultParmValue) // default value assigned
                {
                    ReadInt16(); //MemSize of value
                    var value = DecompileExpression();
                    PopByte(); // end of value

                    var builder = new CodeBuilderVisitor(); // what a wonderful hack, TODO.
                    value.AcceptVisitor(builder);
                    parm.Variables.First().Name += " = " + builder.GetCodeString();
                }
            }
        }
    }
}
