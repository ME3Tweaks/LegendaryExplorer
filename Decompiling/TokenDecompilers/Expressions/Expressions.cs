using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public partial class ME3ByteCodeDecompiler
    {

        public Expression DecompileExpression()
        {
            StartPositions.Push((UInt16)Position);
            var token = CurrentByte;

            switch (token)
            {
                // variable lookups
                case (byte)StandardByteCodes.LocalVariable:
                case (byte)StandardByteCodes.InstanceVariable:
                case (byte)StandardByteCodes.DefaultVariable:
                case (byte)StandardByteCodes.LocalOutVariable:
                    //TODO: 0x5B -> 0x62 all seem to be the same in the exe
                    PopByte();
                    return DecompileObjectLookup();

                case (byte)StandardByteCodes.Nothing:
                    PopByte();
                    StartPositions.Pop();
                    return DecompileExpression(); // TODO, solve this better?

                // array[index]
                case (byte)StandardByteCodes.DynArrayElement: //TODO: possibly separate this
                case (byte)StandardByteCodes.ArrayElement:
                    //TODO
                    return null;

                // new class(...)
                case (byte)StandardByteCodes.New:
                    //TODO
                    return null;

                // class.context
                case (byte)StandardByteCodes.ClassContext:
                case (byte)StandardByteCodes.Context:
                    return DecompileContext();

                //TODO: 0xE, eatRetVal?
                // TODO: 0x3B - 0x3E native calls
                //TODO: unkn4F and GoW_DefaultValue ???
                // TODO: 0x5A -> 0x65 ???

                default:

                    // ERROR!
                    break;
            }

            return null;
        }

        public SymbolReference DecompileObjectLookup()
        {
            var index = ReadIndex();
            var obj = PCC.GetObjectEntry(index);
            if (obj == null)
                return null; // ERROR

            StartPositions.Pop();
            return new SymbolReference(null, null, null, obj.ObjectName);
        }

        public CompositeSymbolRef DecompileContext()
        {
            var left = DecompileExpression();
            if (left == null)
                return null; // ERROR

            ReadInt16(); // discard MemSize value.
            ReadIndex(); // discard RetValRef.
            ReadByte(); // discard unknown byte.

            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR

            if (!typeof(SymbolReference).IsAssignableFrom(left.GetType())
                || !typeof(SymbolReference).IsAssignableFrom(right.GetType()))
            {
                return null; // ERROR
            }

            StartPositions.Pop();
            return new CompositeSymbolRef(left as SymbolReference, right as SymbolReference, null, null);
        }
    }
}
