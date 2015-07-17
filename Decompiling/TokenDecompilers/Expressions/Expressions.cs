using ME3Data.DataTypes;
using ME3Data.DataTypes.ScriptTypes;
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
                var higher = ReadByte() & 0x0F;
                var lower = ReadByte();
                int index = (higher << 8) + lower;
                return DecompileNativeFunction((UInt16)index);
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

                // new (...) class (.)
                case (byte)StandardByteCodes.New: // TODO: support in AST
                    return DecompileNew();

                // (class|object|struct).member
                case (byte)StandardByteCodes.ClassContext: // TODO: support in AST
                    return DecompileContext(isClass:true);

                case (byte)StandardByteCodes.Context:
                    return DecompileContext();

                case (byte)StandardByteCodes.StructMember:
                    return DecompileStructMember();

                // unknown, interface
                case (byte)StandardByteCodes.InterfaceContext:
                    PopByte();
                    StartPositions.Pop();
                    return DecompileExpression(); // TODO: research this

                // class<Name>(Obj)
                case(byte)StandardByteCodes.Metacast:
                    return DecompileCast(meta:true); // TODO: ugly hack to make this qork quickly

                // Self
                case (byte)StandardByteCodes.Self:
                    PopByte();
                    StartPositions.Pop();
                    return new SymbolReference(null, null, null, "self"); // TODO: solve better

                // Skip(numBytes)
                case (byte)StandardByteCodes.Skip: // handles skips in operator arguments
                    PopByte();
                    ReadInt16(); // MemSize
                    StartPositions.Pop();
                    return DecompileExpression();

                // Function calls
                case (byte)StandardByteCodes.FinalFunction:
                    return DecompileFunctionCall();

                case (byte)StandardByteCodes.GlobalFunction:
                    return DecompileFunctionCall(byName: true, global: true); // TODO: is this correct?

                case (byte)StandardByteCodes.VirtualFunction:
                    return DecompileFunctionCall(byName: true);

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
                    return DecompileObjectConst(); //TODO: implement properly

                // 'name'
                case (byte)StandardByteCodes.NameConst:
                    return DecompileNameConst();

                // rot(1, 2, 3)
                case (byte)StandardByteCodes.RotationConst:
                    return DecompileRotationConst();  //TODO: properly

                // vect(1.0, 2.0, 3.0)
                case (byte)StandardByteCodes.VectorConst:
                    return DecompileVectorConst();  //TODO: properly

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
                case (byte)StandardByteCodes.EmptyDelegate: // TODO: is this correct?
                    PopByte();
                    StartPositions.Pop();
                    return new SymbolReference(null, null, null, "None"); // TODO: solve better

                // (bool expression)
                case (byte)StandardByteCodes.BoolVariable:
                    return DecompileBoolExprValue();

                // ClassName(Obj)
                case (byte)StandardByteCodes.InterfaceCast:
                case (byte)StandardByteCodes.DynamicCast:
                    return DecompileCast();

                // struct == struct 
                case (byte)StandardByteCodes.StructCmpEq:
                    return DecompileInOpNaive("==", isStruct: true);

                // struct != struct 
                case (byte)StandardByteCodes.StructCmpNe:
                    return DecompileInOpNaive("!=", isStruct: true);

                // delegate == delegate 
                case (byte)StandardByteCodes.EqualEqual_DelDel:
                    return DecompileInOpNaive("==", useEndOfParms: true);

                // delegate != delegate 
                case (byte)StandardByteCodes.NotEqual_DelDel:
                    return DecompileInOpNaive("!=", useEndOfParms: true);

                // delegate == Function
                case (byte)StandardByteCodes.EqualEqual_DelFunc:
                    return DecompileInOpNaive("==", useEndOfParms: true);

                // delegate != Function 
                case (byte)StandardByteCodes.NotEqual_DelFunc:
                    return DecompileInOpNaive("!=", useEndOfParms: true);

                // primitiveType(expr)
                case (byte)StandardByteCodes.PrimitiveCast:
                    return DecompilePrimitiveCast();

                // (bool expr) ? expr : expr
                case (byte)StandardByteCodes.Conditional:
                    return DecompileConditionalExpression();

                // end of script
                case (byte)StandardByteCodes.EndOfScript:
                    return null; // ERROR?

                // (empty function param)
                case (byte)StandardByteCodes.EmptyParmValue:
                    PopByte();
                    StartPositions.Pop();
                    return new SymbolReference(null, null, null, ""); // TODO: solve better

                // arrayName.Length
                case (byte)StandardByteCodes.DynArrayLength:
                    return DecompileDynArrLength();

                // arrayName.Find(value)
                case (byte)StandardByteCodes.DynArrayFind:
                    return DecompileDynArrFunction(name: "Find");

                // arrayName.Find(StructProperty, value)
                case (byte)StandardByteCodes.DynArrayFindStruct:
                    return DecompileDynArrFunction(name: "Find", secondArg: true);

                // arrayName.Insert(Index, Count)
                case (byte)StandardByteCodes.DynArrayInsert:
                    return DecompileDynArrFunction(name: "Insert", secondArg: true, withoutMemOffs: true);

                // arrayName.Remove(Index, Count)
                case (byte)StandardByteCodes.DynArrayRemove:
                    return DecompileDynArrFunction(name: "Remove", secondArg: true, withoutMemOffs: true);

                // arrayName.Add(value)
                case (byte)StandardByteCodes.DynArrayAdd:
                    return DecompileDynArrFunction(name: "Add", withoutMemOffs: true);

                // arrayName.AddItem(value)
                case (byte)StandardByteCodes.DynArrayAddItem:
                    return DecompileDynArrFunction(name: "AddItem");

                // arrayName.RemoveItem(value)
                case (byte)StandardByteCodes.DynArrayRemoveItem:
                    return DecompileDynArrFunction(name: "RemoveItem");

                // arrayName.InsertItem(StructProperty, value)
                case (byte)StandardByteCodes.DynArrayInsertItem:
                    return DecompileDynArrFunction(name: "InsertItem", secondArg: true);

                // arrayName.Sort(value)
                case (byte)StandardByteCodes.DynArraySort:
                    return DecompileDynArrFunction(name: "Sort");

                // TODO: temporary delegate handling, probably wrong:
                case (byte)StandardByteCodes.DelegateFunction:
                    return DecompileDelegateFunction();

                case (byte)StandardByteCodes.DelegateProperty:
                    return DecompileDelegateProperty();




                /*****
                 * TODO: all of these needs changes, see functions below.
                 * */
                #region Unsupported

                case (byte)StandardByteCodes.EatReturnValue:
                    return DecompileEatReturn();

                case (byte)StandardByteCodes.NativeParm: // is this even present anywhere?
                    return DecompileNativeParm();

                case (byte)StandardByteCodes.GoW_DefaultValue:
                    return DecompileGoW_DefaultValue();

                case (byte)StandardByteCodes.Unkn_4F:
                    return Decompile4F();

                case (byte)StandardByteCodes.InstanceDelegate:
                    return DecompileInstanceDelegate();

                case (byte)StandardByteCodes.Unkn_65: // TODO, seems to be func call by name
                    return DecompileFunctionCall(byName: true, withUnknShort: true);

                case (byte)StandardByteCodes.Assert:
                    return DecompileAssert();

                #endregion

                // TODO: 41, debugInfo
                // TODO: 0x5A, FilterEditorOnly?

                // TODO: 0x3B - 0x3E native calls

                default:

                    // ERROR!
                    break;
            }

            return null;
        }

        #region Decompilers

        public Expression DecompileObjectLookup()
        {
            var obj = ReadObject();
            if (obj == null)
                return null; // ERROR

            StartPositions.Pop();
            return new SymbolReference(null, null, null, obj.ObjectName);
        }

        public Expression DecompileContext(bool isClass = false)
        {
            PopByte();

            var left = DecompileExpression();
            if (left == null)
                return null; // ERROR

            ReadInt16(); // discard MemSize value. (size of expr-right in half-bytes)
            ReadObject(); // discard RetValRef.
            ReadByte(); // discard unknown byte.

            isInClassContext = isClass;
            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR
            isInClassContext = false;

            if (isClass)
            {
                var builder = new CodeBuilderVisitor(); // what a wonderful hack, TODO.
                left.AcceptVisitor(builder);
                var str = builder.GetCodeString() + ".static";
                left = new SymbolReference(null, null, null, str);
            }

            StartPositions.Pop();
            return new CompositeSymbolRef(left, right, null, null);
        }

        public Expression DecompileStructMember()
        {
            PopByte();

            var MemberRef = ReadObject();
            var StructRef = ReadObject();

            ReadByte(); // discard unknown bytes
            ReadByte();

            var expr = DecompileExpression(); // get the expression for struct instance
            if (expr == null)
                return null; // ERROR

            StartPositions.Pop();
            var member = new SymbolReference(null, null, null, MemberRef.ObjectName);
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

        public Expression DecompileInOpNaive(String opName, bool useEndOfParms = false, bool isStruct = false)
        {
            // TODO: ugly, wrong.
            PopByte();

            if (isStruct)
                ReadObject(); // struct type?

            var left = DecompileExpression();
            if (left == null)
                return null; // ERROR

            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR

            if (useEndOfParms)
                PopByte();

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
            while (!CurrentIs(StandardByteCodes.EndFunctionParms))
            {
                var param = DecompileExpression();
                if (param == null)
                    return null; // ERROR

                parameters.Add(param);
            }
            PopByte();

            var entry = NativeTable[index];
            Expression call = null;

            switch (entry.Type)
            {
                case NativeType.Function:
                    var func = new SymbolReference(null, null, null, entry.Name);
                    call = new FunctionCall(func, parameters, null, null);
                    break;

                case NativeType.Operator:   // TODO: table should hold precedence, currently all have 0 and it'll be a mess.
                    var op = new InOpDeclaration(entry.Name, entry.Precedence, false, null, null, null, null, null, null, null);
                    call = new InOpReference(op, parameters[0], parameters[1], null, null);
                    break;

                case NativeType.PreOperator:   // TODO: table should hold precedence, currently all have 0 and it'll be a mess.
                    var preOp = new PreOpDeclaration(entry.Name, false, null, null, null, null, null, null);
                    call = new PreOpReference(preOp, parameters[0], null, null);
                    break;

                case NativeType.PostOperator:   // TODO: table should hold precedence, currently all have 0 and it'll be a mess.
                    var postOp = new PostOpDeclaration(entry.Name, false, null, null, null, null, null, null);
                    call = new PostOpReference(postOp, parameters[0], null, null);
                    break;
            }

            StartPositions.Pop();
            return call;
        }

        public Expression DecompileCast(bool meta = false)
        {
            PopByte();
            var objRef = ReadObject();
            var expr = DecompileExpression();
            if (expr == null)
                return null; // ERROR

            String type = objRef.ObjectName;
            if (meta)
                type = "class<" + type + ">";

            StartPositions.Pop();
            return new CastExpression(new VariableType(type, null, null), expr, null, null);
        }

        public Expression DecompilePrimitiveCast()
        {
            PopByte();
            var typeToken = ReadByte();

            var expr = DecompileExpression();
            if (expr == null)
                return null; // ERROR

            // TODO: map this out, possibly most are implicit?
            String type = PrimitiveCastTable[typeToken];

            StartPositions.Pop();
            return new CastExpression(new VariableType(type, null, null), expr, null, null);
        }

        public Expression DecompileFunctionCall(bool byName = false, bool withUnknShort = false, bool global = false)
        {
            PopByte();
            String funcName;
            if (byName)
            {
                funcName = PCC.GetName(ReadNameRef());
            }
            else
            {
                var funcObj = ReadObject();
                funcName = funcObj.ObjectName;
                
                if (funcName == DataContainer.Name && !isInClassContext) // If we're calling ourself, it's a super call
                {
                    var str = "super";

                    var currentClass = DataContainer.ExportEntry.GetOuterOfType("Class").Object as ME3Class;
                    var funcOuterClass = funcObj.GetOuterOfType("Class").ObjectName;
                    if (currentClass != null && currentClass.SuperField != null && currentClass.SuperField.Name == funcOuterClass)
                        funcName = str + "." + funcName;
                    else
                        funcName = str + "(" + funcOuterClass + ")." + funcName;
                }
            }

            if (global)
                funcName = "global." + funcName;

            if (withUnknShort)
                ReadInt16(); // TODO: related to unkn65, split out? Possibly jump?

            var parameters = new List<Expression>();
            while (!CurrentIs(StandardByteCodes.EndFunctionParms))
            {
                if (CurrentIs(StandardByteCodes.Nothing))
                {
                    PopByte(); // TODO: is this reasonable? what does it mean?
                    parameters.Add(new SymbolReference(null, null, null, "None"));
                    continue;
                }

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

        public Expression DecompileNew() // TODO: replace this ugly-ass hack with proper AST support.
        {
            PopByte();
            var parameters = new List<Expression>();
            for (int n = 0; n < 5; n++)
            {
                if (CurrentIs(StandardByteCodes.Nothing))
                {
                    parameters.Add(null);
                    continue;
                }

                var param = DecompileExpression();
                if (param == null)
                    return null; // ERROR

                parameters.Add(param);
            }

            Expression first = null;
            if ((parameters[0] ?? parameters[1] ?? parameters[2]) != null)
            {
                var innerParms = new List<Expression>();
                if (parameters[0] != null)
                    innerParms.Add(parameters[0]);
                if (parameters[1] != null)
                    innerParms.Add(parameters[1]);
                if (parameters[2] != null)
                    innerParms.Add(parameters[2]);
                first = new FunctionCall(new SymbolReference(null, null, null, "new"), innerParms, null, null);
            }
            else {
                first = new SymbolReference(null, null, null, "new");
            }

            var second = parameters[3] ?? new SymbolReference(null, null, null, "NoClass??");

            var op = new InOpDeclaration("", 0, true, null, null, null, null, null, null, null);
            var firstHalf = new InOpReference(op, first, second, null, null);
            StartPositions.Pop();

            if (parameters[4] != null)
                return new InOpReference(op, firstHalf, parameters[4], null, null);
            else
                return firstHalf;
        }

        public Expression DecompileDelegateFunction() // TODO: is this proper? Is it even used in ME3?
        {
            PopByte();
            PopByte(); // unknown
            var obj = ReadObject(); // unknown objRef?

            Position--; //HACK!!!!! TODO
            return DecompileFunctionCall(byName: true);
        }

        public Expression DecompileDelegateProperty() // TODO: is this proper? Is it even used in ME3?
        {
            PopByte();
            var name = PCC.GetName(ReadNameRef());
            var obj = ReadObject(); // probably the delegate

            StartPositions.Pop();
            var objName = obj != null ? obj.ObjectName : "None";
            return new SymbolReference(null, null, null, name + "(" + objName + ")");
        }

#endregion

        /*
         * TODO: All of these need verification and changes
         * */
        #region UnsuportedDecompilers 

        public Expression DecompileEatReturn() 
        {
            PopByte();
            var obj = ReadObject();
            var expr = DecompileExpression();

            StartPositions.Pop();
            var op = new InOpDeclaration("", 0, true, null, null, null, null, null, null, null);
            var objRef = new SymbolReference(null, null, null, "UNSUPPORTED: EatReturnValue: " + obj.ObjectName + "|" + obj.ClassName + " -");
            return new InOpReference(op, objRef, expr, null, null);
        }

        public Expression DecompileGoW_DefaultValue()
        {
            PopByte();
            var unkn = ReadByte();
            var expr = DecompileExpression();

            StartPositions.Pop();
            var op = new InOpDeclaration("", 0, true, null, null, null, null, null, null, null);
            var objRef = new SymbolReference(null, null, null, "UNSUPPORTED: GoW_DefaultValue: Byte:" + unkn + " - ");
            return new InOpReference(op, objRef, expr, null, null);
        }

        public Expression DecompileNativeParm() 
        {
            PopByte();
            var obj = ReadObject();

            StartPositions.Pop();
            return new SymbolReference(null, null, null, "UNSUPPORTED: NativeParm: " + obj.ObjectName + " : " + obj.ClassName);
        }

        public Expression Decompile4F()
        {
            PopByte();
            var index = ReadIndex(); // Unknown
            /*var expr = DecompileExpression();

            StartPositions.Pop();
            var op = new InOpDeclaration("", 0, true, null, null, null, null, null, null, null);
            var objRef = new SymbolReference(null, null, null, "UNSUPPORTED: 4F (ME3Ex:add?): " + index.ToString("X8") + "| ");
            return new InOpReference(op, objRef, expr, null, null); */

            StartPositions.Pop();
            return new SymbolReference(null, null, null, "UNSUPPORTED: 4F (ME3Ex:add?): " + index.ToString("X8"));
        }

        public Expression DecompileInstanceDelegate()
        {
            PopByte();
            var name = PCC.GetName(ReadNameRef());

            StartPositions.Pop();
            return new SymbolReference(null, null, null, "UNSUPPORTED: InstanceDelegate: " + name);
        }

        public Expression DecompileAssert()
        {
            PopByte();
            var unkn1 = ReadUInt16(); // memoff?
            var unkn2 = ReadByte(); // true/false?
            var expr = DecompileExpression();

            var builder = new CodeBuilderVisitor(); // what a wonderful hack, TODO.
            expr.AcceptVisitor(builder);

            StartPositions.Pop();
            return new SymbolReference(null, null, null, "ASSERT[" + unkn1.ToString("X4") + "|" + unkn2.ToString("X2") + "](" + builder.ToString() + ")");
        }

        #endregion

    }
}
