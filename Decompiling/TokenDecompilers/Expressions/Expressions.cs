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

            if (token >= 0x80) // native table
            {
                return DecompileNativeFunction(PopByte());
            }
            else if (token >= 0x71) // extended native table, 0x70 is unused
            {
                UInt16 index = (UInt16)(ReadUInt16() & 0x0FFF);
                return DecompileNativeFunction(index);
            }
            else switch (token)
            {
                // variable lookups
                case (byte)StandardByteCodes.LocalVariable:
                case (byte)StandardByteCodes.InstanceVariable:
                case (byte)StandardByteCodes.DefaultVariable:
                case (byte)StandardByteCodes.LocalOutVariable:

                case (byte)StandardByteCodes.Unkn_5B: // TODO: fix these, 
                case (byte)StandardByteCodes.Unkn_5C: // map them out with names / purposes, 
                case (byte)StandardByteCodes.Unkn_5D: // here for test purposes for now!
                case (byte)StandardByteCodes.Unkn_5E: 
                case (byte)StandardByteCodes.Unkn_5F:
                case (byte)StandardByteCodes.Unkn_60:
                case (byte)StandardByteCodes.Unkn_61:
                case (byte)StandardByteCodes.Unkn_62: 
                    
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

                // (class|object|struct).member
                case (byte)StandardByteCodes.ClassContext: // TODO: make decompile be "class'Name'.static.Inner
                case (byte)StandardByteCodes.Context:
                    return DecompileContext();

                case (byte)StandardByteCodes.StructMember:
                    return DecompileStructMember();

                // class<Name>(Obj)
                case(byte)StandardByteCodes.Metacast:
                    return DecompileCast(meta:true); // TODO: ugly hack to make this qork quickly

                // Self
                case (byte)StandardByteCodes.Self:
                    return null; //TODO

                // Skip(numBytes)
                case (byte)StandardByteCodes.Skip:
                    PopByte();
                    ReadInt16(); // MemSize
                    StartPositions.Pop();
                    return DecompileExpression(); //TODO: should this be in both expr and statement?

                // Function calls
                case (byte)StandardByteCodes.FinalFunction:
                case (byte)StandardByteCodes.GlobalFunction:
                    return DecompileFunctionCall();

                case (byte)StandardByteCodes.VirtualFunction:
                    return DecompileFunctionCall(byName: true);

                case (byte)StandardByteCodes.Unkn_65: // TODO, seems to be func call by name
                    return DecompileFunctionCall(byName: true, withUnknShort: true);

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
                    return new SymbolReference(null, null, null, "None"); // TODO: solve better

                // (bool expression)
                case (byte)StandardByteCodes.BoolVariable:
                    return DecompileBoolExprValue();

                // ClassName(Obj)
                case (byte)StandardByteCodes.DynamicCast:
                    return DecompileCast();

                // struct == struct 
                case (byte)StandardByteCodes.StructCmpEq:
                    return DecompileInOpNaive("==");

                // struct != struct 
                case (byte)StandardByteCodes.StructCmpNe:
                    return DecompileInOpNaive("!=");

                // primitiveType(expr)
                case (byte)StandardByteCodes.PrimitiveCast:
                    return null; //TODO

                // (bool expr) ? expr : expr
                case (byte)StandardByteCodes.Conditional:
                    return DecompileConditionalExpression();

                // end of script
                case (byte)StandardByteCodes.EndOfScript:
                    return null; // ERROR: unexpected end of script

                // (empty function param)
                case (byte)StandardByteCodes.EmptyParmValue:
                    PopByte();
                    StartPositions.Pop();
                    return new SymbolReference(null, null, null, ""); // TODO: solve better

                // arrayName.Length
                case (byte)StandardByteCodes.DynArrayLength:
                    return DecompileDynArrLength();
                
                // TODO: 51, 52 : InterfaceContext, InterfaceCast
                // TODO: 50, GoW_DefaultValue
                // TODO: 4F, Unkn
                // TODO: 4B, instandeDelegate
                // TODO: 49 : defaultParmValue
                // TODO: 48, outVariable
                // TODO: 42, 43 : DelegateFunction, DelegateProperty
                // TODO: 41, debugInfo
                // TODO: 3F, NoDelegate
                //TODO: 0x36, 0x39, 0x40, 0x46, 0x47, 0x54 -> 0x59 : Dynamic Array stuff
                //TODO: 0x2F  0x31 : Iterator, IteratorPop, IteratorNext
                //TODO: 0x29, nativeParm, should not be present?
                //TODO: 0xE, eatRetVal?
                // TODO: 0x3B - 0x3E native calls
                // TODO: 0x63 -> 0x65 ???

                default:

                    // ERROR!
                    break;
            }

            return null;
        }

        public Expression DecompileObjectLookup()
        {
            var index = ReadIndex();
            var obj = PCC.GetObjectEntry(index);
            if (obj == null)
                return null; // ERROR

            StartPositions.Pop();
            return new SymbolReference(null, null, null, obj.ObjectName);
        }

        public Expression DecompileContext()
        {
            PopByte();

            var left = DecompileExpression();
            if (left == null)
                return null; // ERROR

            ReadInt16(); // discard MemSize value. (size of expr-right in half-bytes)
            ReadIndex(); // discard RetValRef.
            ReadByte(); // discard unknown byte.

            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR

            StartPositions.Pop();
            return new CompositeSymbolRef(left, right, null, null);
        }

        public Expression DecompileStructMember()
        {
            PopByte();

            var MemberRef = ReadIndex();
            var StructRef = ReadIndex();

            ReadByte(); // discard unknown bytes
            ReadByte();

            var expr = DecompileExpression(); // get the expression for struct instance
            if (expr == null)
                return null; // ERROR

            StartPositions.Pop();
            var member = new SymbolReference(null, null, null, PCC.GetObjectEntry(MemberRef).ObjectName);
            return new CompositeSymbolRef(expr, member, null, null);
        }

        public Expression DecompileArrayRef()
        {
            PopByte();

            var index = DecompileExpression();
            if (index == null)
                return null; // ERROR

            var arrayExpr = DecompileExpression();
            if (arrayExpr == null)
                return null; // ERROR

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

        public Expression DecompileConditionalExpression()
        {
            PopByte();

            var cond = DecompileExpression();
            if (cond == null)
                return null; // ERROR

            ReadInt16(); // MemSizeA

            var trueExpr = DecompileExpression();
            if (trueExpr == null)
                return null; // ERROR

            ReadInt16(); // MemSizeB

            var falseExpr = DecompileExpression();
            if (falseExpr == null)
                return null; // ERROR

            StartPositions.Pop();
            return new ConditionalExpression(cond, trueExpr, falseExpr, null, null);
        }

        public Expression DecompileNativeFunction(UInt16 index)
        {
            var parameters = new List<Expression>();
            while (CurrentByte != (byte)StandardByteCodes.EndFunctionParms)
            {
                var param = DecompileExpression();
                if (param == null)
                    return null; // ERROR

                parameters.Add(param);
            }
            PopByte();

            // TODO: lookup native table etc..

            StartPositions.Pop();
            var func = new SymbolReference(null, null, null, index.ToString());
            return new FunctionCall(func, parameters, null, null);
        }

        public Expression DecompileCast(bool meta = false)
        {
            PopByte();
            var objRef = ReadIndex();
            var expr = DecompileExpression();
            if (expr == null)
                return null; // ERROR

            String type = PCC.GetObjectEntry(objRef).ObjectName;
            if (meta)
                type = "class<" + type + ">";

            StartPositions.Pop();
            return new CastExpression(new VariableType(type, null, null), expr, null, null);
        }

        public Expression DecompileFunctionCall(bool byName = false, bool withUnknShort = false)
        {
            PopByte();
            String funcName;
            if (byName)
                funcName = PCC.GetName(ReadNameRef());
            else
                funcName = PCC.GetObjectEntry(ReadIndex()).ObjectName;

            if (withUnknShort)
                ReadInt16(); // TODO: related to unkn65, split out?

            var parameters = new List<Expression>();
            while (CurrentByte != (byte)StandardByteCodes.EndFunctionParms)
            {
                var param = DecompileExpression();
                if (param == null)
                    return null; // ERROR

                parameters.Add(param);
            }
            PopByte();

            StartPositions.Pop();
            var func = new SymbolReference(null, null, null, funcName);
            return new FunctionCall(func, parameters, null, null);
        }
    }
}
