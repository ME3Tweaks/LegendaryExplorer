using ME3Data.DataTypes.ScriptTypes;
using ME3Data.Utility;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public class ME3ByteCodeDecompiler : ObjectReader
    {
        private ME3Struct DataContainer;

        private Byte CurrentByte { get { return _data[Position]; } } // TODO: meaningful error handling here..
        private Byte PopByte { get { return ReadByte(); } }
        private Byte PeekByte { get { return Position < Size ? _data[Position + 1] : (byte)0; } }
        private Byte PrevByte { get { return Position > 0 ? _data[Position - 1] : (byte)0; } }

        private bool CurrentIs(StandardByteCodes val)
        {
            return CurrentByte == (byte)val;
        }

        public ME3ByteCodeDecompiler(ME3Struct dataContainer)
            :base(dataContainer.DataScript)
        {
            DataContainer = dataContainer;
        }

        public CodeBody Decompile()
        {
            Reset();
            var statememnts = new List<Statement>();

            while (Position < Size && !CurrentIs(StandardByteCodes.EndOfScript))
            {
                var current = DecompileStatement();
                if (current == null)
                    return null; // ERROR!

                statememnts.Add(current);
            }

            return null;
        }

        public Statement DecompileStatement()
        {
            var token = PopByte;
            
            if (token >= 0x80) // native table
            {
                // TODO: native lookup
            }
            else if (token >= 0x71) // extended native table, 0x70 is unused
            {
                // TODO: build extended value, then native lookup
            }
            else 
                switch (token)
                {
                    // return [expression];
                    case (byte)StandardByteCodes.Return:

                        break;
                        
                    // switch (expression)
                    case (byte)StandardByteCodes.Switch:

                        break;

                    // if (expression)
                    case (byte)StandardByteCodes.JumpIfNot:

                        break;

                    // stop;
                    case (byte)StandardByteCodes.Stop:

                        break;

                    // [nothing]
                    case (byte)StandardByteCodes.Nothing:

                        break;

                    // Goto label
                    case (byte)StandardByteCodes.GotoLabel:

                        break;

                    // TODO: eatreturnvalue?

                    // variable = expression;
                    case (byte)StandardByteCodes.Let:

                        break;

                    // new class();
                    case (byte)StandardByteCodes.New:

                        break;

                    // bool = expression
                    case (byte)StandardByteCodes.LetBool:

                        break;

                    // [skip x bytes]
                    case (byte)StandardByteCodes.Skip:

                        break;

                    // object.function(args);
                    case (byte)StandardByteCodes.VirtualFunction:

                        break;

                    // object.function(args);
                    case (byte)StandardByteCodes.FinalFunction:

                        break;

                    // foreach IteratorFunction(...)
                    case (byte)StandardByteCodes.Iterator:

                        break;

                    // global.function(args);
                    case (byte)StandardByteCodes.GlobalFunction:

                        break;

                    // arrayName.Insert(Index, Count);
                    case (byte)StandardByteCodes.DynArrayInsert:

                        break;

                    // TODO: 0x3B - 0x3E native calls here?

                    // arrayName.Remove(Index, Count);
                    case (byte)StandardByteCodes.DynArrayRemove:

                        break;

                    // delegateName(args);
                    case (byte)StandardByteCodes.DelegateFunction:

                        break;

                    // Sort(Delegate) ???
                    case (byte)StandardByteCodes.DelegateProperty:

                        break;

                    // Delegate = expression
                    case (byte)StandardByteCodes.LetDelegate:

                        break;

                    // boolean expression ? expression : expression;
                    case (byte)StandardByteCodes.Conditional:

                        break;

                    //TODO: unkn4F and GoW_DefaultValue ???

                    // arrayName.Add(Count);
                    case (byte)StandardByteCodes.DynArrayAdd:

                        break;

                    // arrayName.AddItem(expression);
                    case (byte)StandardByteCodes.DynArrayAddItem:

                        break;

                    // arrayName.RemoveItem(expression);
                    case (byte)StandardByteCodes.DynArrayRemoveItem:

                        break;

                    // arrayName.InsertItem(Index, expression);
                    case (byte)StandardByteCodes.DynArrayInsertItem:

                        break;

                    // foreach arrayName(valuevariable[, indexvariable])
                    case (byte)StandardByteCodes.DynArrayIterator:

                        break;

                    // arrayName.Sort(SortDelegate);
                    case (byte)StandardByteCodes.DynArraySort:

                        break;

                    // TODO: 0x5A -> 0x65 ???

                    default:
                        // ERROR!

                        break;
                }

            return null;
        }
    }
}
