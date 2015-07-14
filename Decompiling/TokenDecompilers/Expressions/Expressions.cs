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
                    return DecompileExpression(); // TODO, solve this better? What about variable assignments etc?

                // array[index]
                case (byte)StandardByteCodes.DynArrayElement: //TODO: possibly separate this
                case (byte)StandardByteCodes.ArrayElement:
                    return DecompileArrayRef();

                // new class(...)
                case (byte)StandardByteCodes.New:
                    //TODO
                    return null;

                // class.context
                case (byte)StandardByteCodes.ClassContext:
                case (byte)StandardByteCodes.Context:
                    return DecompileContext();

                // class<Name>(Obj)
                case(byte)StandardByteCodes.Metacast:
                    return null; //TODO

                // Self
                case (byte)StandardByteCodes.Self:
                    return null; //TODO

                // Skip(numBytes)
                case (byte)StandardByteCodes.Skip:
                    PopByte();
                    ReadInt16(); // MemSize
                    StartPositions.Pop();
                    return DecompileExpression(); //TODO: how does this work for real?

                // Function calls
                case (byte)StandardByteCodes.VirtualFunction:
                case (byte)StandardByteCodes.FinalFunction:
                case (byte)StandardByteCodes.GlobalFunction:
                    return null; //TODO

                // int, eg. 5
                case (byte)StandardByteCodes.IntConst:
                    return DecompileIntConst();

                // float, eg. 5.5
                case (byte)StandardByteCodes.FloatConst:
                    return DecompileFloatConst();

                // "string"
                case (byte)StandardByteCodes.StringConst:
                    return DecompileStringConst();

                // Object
                case (byte)StandardByteCodes.ObjectConst:
                    return null; // DecompileObjectConst(); TODO

                // 'name'
                case (byte)StandardByteCodes.NameConst:
                    return DecompileNameConst();

                // rot(1, 2, 3)
                case (byte)StandardByteCodes.RotationConst:
                    return null; // DecompileRotationConst(); TODO

                // vect(1.0, 2.0, 3.0)
                case (byte)StandardByteCodes.VectorConst:
                    return null; // DecompileVectorConst(); TODO

                // byte, eg. 0B
                case (byte)StandardByteCodes.ByteConst:
                case (byte)StandardByteCodes.IntConstByte:
                    return DecompileByteConst();

                // 0
                case (byte)StandardByteCodes.IntZero:
                    return DecompileIntConstVal(0);

                // 1
                case (byte)StandardByteCodes.IntOne:
                    return DecompileIntConstVal(1);

                // true
                case (byte)StandardByteCodes.True:
                    return DecompileBoolConstVal(true);

                // false
                case (byte)StandardByteCodes.False:
                    return DecompileBoolConstVal(false);

                // None    (object literal)
                case (byte)StandardByteCodes.NoObject:
                    PopByte();
                    StartPositions.Pop();
                    return new StringLiteral("None", null, null); // TODO: solve better

                // (bool expression)
                case (byte)StandardByteCodes.BoolVariable:
                    return DecompileBoolExprValue();

                // ClassName(Obj)
                case (byte)StandardByteCodes.DynamicCast:
                    return null; //TODO

                // struct == struct 
                case (byte)StandardByteCodes.StructCmpEq:
                    return DecompileInOpNaive("==");

                // struct != struct 
                case (byte)StandardByteCodes.StructCmpNe:
                    return DecompileInOpNaive("!=");

                //TODO: 0x2F  0x31 : Iterator, IteratorPop, IteratorNext
                //TODO: 0x29, nativeParm, should not be present?
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
            return new CompositeSymbolRef(left, right, null, null);
        }

        public ArraySymbolRef DecompileArrayRef()
        {
            PopByte();

            var index = DecompileExpression();
            if (index == null)
                return null; // ERROR

            var arrayExpr = DecompileExpression();
            if (arrayExpr == null)
                return null; // ERROR

            if (!typeof(SymbolReference).IsAssignableFrom(index.GetType())
                || !typeof(SymbolReference).IsAssignableFrom(arrayExpr.GetType()))
            {
                return null; // ERROR
            }

            StartPositions.Pop();
            return new ArraySymbolRef(arrayExpr, index, null, null);
        }

        public Expression DecompileBoolExprValue()
        {
            PopByte(); 

            var value = DecompileExpression();
            if (value == null)
                return null; // ERROR

            StartPositions.Pop();
            return value; // TODO: is this correct? should we contain it?
        }

        public Expression DecompileInOpNaive(String opName)
        {
            // TODO: ugly, wrong.
            PopByte();

            var left = DecompileExpression();
            if (left == null)
                return null; // ERROR

            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR

            StartPositions.Pop();
            var op = new InOpDeclaration(opName, 0, true, null, null, null, null, null, null, null);
            return new InOpReference(op, left, right, null, null); 
        }
    }
}
