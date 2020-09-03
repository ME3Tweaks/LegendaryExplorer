using ME3Script.Analysis.Visitors;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Script.Utilities;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Decompiling
{
    public partial class ME3ByteCodeDecompiler
    {

        public Expression DecompileExpression()
        {
            StartPositions.Push((ushort)Position);
            var token = PeekByte;

            if (token >= 0x80) // native table
            {
                return DecompileNativeFunction(PopByte());
            }

            if (token >= 0x71) // extended native table, 0x70 is unused
            {
                var higher = ReadByte() & 0x0F;
                var lower = ReadByte();
                int index = (higher << 8) + lower;
                return DecompileNativeFunction((ushort)index);
            }

            switch (token)
            {
                // variable lookups
                case (byte)StandardByteCodes.DefaultVariable:

                    PopByte();
                    return DecompileDefaultReference();
                case (byte)StandardByteCodes.LocalOutVariable:
                case (byte)StandardByteCodes.LocalVariable:
                case (byte)StandardByteCodes.InstanceVariable:

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
                    return new SymbolReference(null, SELF, null, null); // TODO: solve better

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
                    return DecompileObjectConst();

                // 'name'
                case (byte)StandardByteCodes.NameConst:
                    return DecompileNameConst();

                // rot(1, 2, 3)
                case (byte)StandardByteCodes.RotationConst:
                    return DecompileRotationConst();  //TODO: properly

                // vect(1.0, 2.0, 3.0)
                case (byte)StandardByteCodes.VectorConst:
                    return DecompileVectorConst();

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
                    return new NoneLiteral();

                // (bool expression)
                case (byte)StandardByteCodes.BoolVariable:
                    return DecompileBoolExprValue();

                // ClassName(Obj)
                case (byte)StandardByteCodes.InterfaceCast:
                case (byte)StandardByteCodes.DynamicCast:
                    return DecompileCast();

                // struct == struct 
                case (byte)StandardByteCodes.StructCmpEq:
                    return DecompileStructComparison(true);

                // struct != struct 
                case (byte)StandardByteCodes.StructCmpNe:
                    return DecompileStructComparison(true);

                // delegate == delegate 
                case (byte)StandardByteCodes.EqualEqual_DelDel:
                    return DecompileDelegateComparison(true);

                // delegate != delegate 
                case (byte)StandardByteCodes.NotEqual_DelDel:
                    return DecompileDelegateComparison(false);

                // delegate == Function
                case (byte)StandardByteCodes.EqualEqual_DelFunc:
                    return DecompileDelegateComparison(true);

                // delegate != Function 
                case (byte)StandardByteCodes.NotEqual_DelFunc:
                    return DecompileDelegateComparison(false);

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
                    return new SymbolReference(null, ""); // TODO: solve better

                // arrayName.Length
                case (byte)StandardByteCodes.DynArrayLength:
                    return DecompileDynArrLength();

                // arrayName.Find(value)
                case (byte)StandardByteCodes.DynArrayFind:
                    return DecompileDynArrayFind();

                // arrayName.Find(StructProperty, value)
                case (byte)StandardByteCodes.DynArrayFindStruct:
                    return DecompileDynArrayFindStructMember();

                // arrayName.Insert(Index, Count)
                case (byte)StandardByteCodes.DynArrayInsert:
                    return DecompileDynArrayInsert();

                // arrayName.Remove(Index, Count)
                case (byte)StandardByteCodes.DynArrayRemove:
                    return DecompileDynArrayRemove();

                // arrayName.Add(value)
                case (byte)StandardByteCodes.DynArrayAdd:
                    return DecompileDynArrayAdd();

                // arrayName.AddItem(value)
                case (byte)StandardByteCodes.DynArrayAddItem:
                    return DecompileDynArrayAddItem();

                // arrayName.RemoveItem(value)
                case (byte)StandardByteCodes.DynArrayRemoveItem:
                    return DecompileDynArrayRemoveItem();

                // arrayName.InsertItem(StructProperty, value)
                case (byte)StandardByteCodes.DynArrayInsertItem:
                    return DecompileDynArrayInsertItem();

                // arrayName.Sort(value)
                case (byte)StandardByteCodes.DynArraySort:
                    return DecompileDynArraySort();

                // TODO: temporary delegate handling, probably wrong:
                case (byte)StandardByteCodes.DelegateFunction:
                    return DecompileDelegateFunction();

                case (byte)StandardByteCodes.DelegateProperty:
                    return DecompileDelegateProperty();

                case (byte)StandardByteCodes.NamedFunction:
                    return DecompileFunctionCall(byName: true, withFuncListIdx: true);



                /*****
                 * TODO: all of these needs changes, see functions below.
                 * */
                #region Unsupported

                case (byte)StandardByteCodes.NativeParm: // is this even present anywhere?
                    return DecompileNativeParm();

                case (byte)StandardByteCodes.GoW_DefaultValue:
                    return DecompileGoW_DefaultValue();

                case (byte)StandardByteCodes.StringRefConst:
                    return DecompileStringRefConst();

                case (byte)StandardByteCodes.InstanceDelegate:
                    return DecompileInstanceDelegate();

                #endregion


                // TODO: 41, debugInfo

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
            return new SymbolReference(null, obj.ObjectName.Instanced);
        }

        public Expression DecompileDefaultReference()
        {
            var obj = ReadObject();
            if (obj == null)
                return null; // ERROR

            StartPositions.Pop();
            return new DefaultReference(null, obj.ObjectName.Instanced);
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

            isInContextExpression = true;
            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR
            isInContextExpression = false;

            StartPositions.Pop();
            return new CompositeSymbolRef(left, right, isClass);
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
            var member = new SymbolReference(null, MemberRef.ObjectName.Instanced);
            return new CompositeSymbolRef(expr, member);
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

        public DelegateComparison DecompileDelegateComparison(bool isEqual)
        {
            //TODO: distinguish between deldel and delfunc 
            PopByte();

            var left = DecompileExpression();
            if (left == null)
                return null; // ERROR

            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR

            PopByte();//EndOfParms

            StartPositions.Pop();
            return new DelegateComparison(isEqual, left, right);
        }

        public StructComparison DecompileStructComparison(bool isEqual)
        {
            PopByte();

            ReadObject(); // struct type?

            var left = DecompileExpression();
            if (left == null)
                return null; // ERROR

            var right = DecompileExpression();
            if (right == null)
                return null; // ERROR

            StartPositions.Pop();
            return new StructComparison(isEqual, left, right);
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

        public Expression DecompileNativeFunction(ushort index)
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
                    var func = new SymbolReference(null, entry.Name, null, null);
                    call = new FunctionCall(func, parameters, null, null);
                    break;

                case NativeType.Operator:
                    var op = new InOpDeclaration(entry.Name, entry.Precedence, index, null, null, null);
                    call = new InOpReference(op, parameters[0], parameters[1], null, null);
                    break;

                case NativeType.PreOperator:
                    var preOp = new PreOpDeclaration(entry.Name, null, index, null);
                    call = new PreOpReference(preOp, parameters[0], null, null);
                    break;

                case NativeType.PostOperator:
                    var postOp = new PostOpDeclaration(entry.Name, null, index, null);
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

            string type = objRef.ObjectName.Instanced;
            if (meta)
                type = "class<" + type + ">";

            StartPositions.Pop();
            if (expr is NoneLiteral)
            {
                return expr;
            }
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
            string type = PrimitiveCastTable[typeToken];

            StartPositions.Pop();
            if (typeToken == (byte)ECast.ByteToInt && expr is IntegerLiteral)
            {
                return expr;
            }
            return new CastExpression(new VariableType(type, null, null), expr, null, null);
        }

        public Expression DecompileFunctionCall(bool byName = false, bool withFuncListIdx = false, bool global = false)
        {
            PopByte();
            string funcName;
            if (byName)
            {
                funcName = ReadNameReference();
            }
            else
            {
                var funcObj = ReadObject();
                funcName = funcObj.ObjectName.Instanced;

                if (AdditionalOperators.TryGetValue(funcName, out InOpDeclaration opDecl))
                {
                    Expression parm1, parm2;
                    if (CurrentIs(StandardByteCodes.Nothing))
                    {
                        PopByte();
                        parm1 = new SymbolReference(null, "None");
                    }
                    else
                    {
                        parm1 = DecompileExpression();
                    }
                    if (CurrentIs(StandardByteCodes.Nothing))
                    {
                        PopByte();
                        parm2 = new SymbolReference(null, "None");
                    }
                    else
                    {
                        parm2 = DecompileExpression();
                    }
                    PopByte();//EndFunctionParms

                    StartPositions.Pop();
                    return new InOpReference(opDecl, parm1, parm2);
                }
                
                if (IsSuper(funcObj)) // If we're calling ourself, it's a super call
                {
                    //TODO: put super calls into the ast, and properly decompile super calls that specifiy the class
                    var str = "super";
                    var classExp = DataContainer.Export.Parent;
                    while (classExp != null && classExp.ClassName != "Class")
                    {
                        classExp = classExp.Parent;
                    }
                    var currentClass = (classExp as ExportEntry).GetBinaryData<UClass>();
                    classExp = funcObj;
                    while (classExp != null && classExp.ClassName != "Class")
                    {
                        classExp = classExp.Parent;
                    }
                    var funcOuterClass = classExp.ObjectName.Instanced;
                    if (currentClass != null && currentClass.SuperClass != 0 && currentClass.SuperClass.GetEntry(PCC).ObjectName.Instanced == funcOuterClass)
                        funcName = str + "." + funcName;
                    else
                        funcName = str + "(" + funcOuterClass + ")." + funcName;
                }
            }

            if (global)
                funcName = "global." + funcName;

            if (withFuncListIdx)
                ReadInt16(); // TODO: store this 

            List<Expression> parameters = DecompileArgumentList();
            if (parameters is null)
            {
                return null;
            }

            StartPositions.Pop();
            var func = new SymbolReference(null, funcName);
            return new FunctionCall(func, parameters, null, null);
        }

        private List<Expression> DecompileArgumentList()
        {
            var parameters = new List<Expression>();
            while (!CurrentIs(StandardByteCodes.EndFunctionParms))
            {
                if (CurrentIs(StandardByteCodes.Nothing))
                {
                    PopByte(); // TODO: is this reasonable? what does it mean?
                    parameters.Add(new SymbolReference(null, "None"));
                    continue;
                }

                var param = DecompileExpression();
                if (param == null)
                    return null;

                parameters.Add(param);
            }

            PopByte();
            return parameters;
        }

        bool IsSuper(IEntry funcObj)
        {
            //can't be a super call if it's being called on a specific object
            if (isInContextExpression)
            {
                return false;
            }

            string funcName = funcObj.ObjectName.Instanced;
            ExportEntry parentContextExport = funcObj.Parent as ExportEntry;

            //if the function being called is in the current context, it's not a super call
            if (parentContextExport == DataContainer.Export.Parent)
            {
                return false;
            }

            //if it's in a parent context, it's a super call if there is a function with the same name in the current context
            return DataContainer.Export.Parent.GetChildren().Any(child => child.ObjectName.Instanced.CaseInsensitiveEquals(funcName));
        }

        public Expression DecompileNew()
        {
            PopByte();
            var parms = new List<Expression>();
            for (int n = 0; n < 5; n++)
            {
                if (CurrentIs(StandardByteCodes.Nothing))
                {
                    PopByte();
                    parms.Add(null);
                    continue;
                }

                var param = DecompileExpression();
                if (param == null)
                    return null; // ERROR

                parms.Add(param);
            }

            StartPositions.Pop();
            return new NewOperator(parms[0], parms[1], parms[2], parms[3], parms[4]);
        }

        public Expression DecompileDelegateFunction() // TODO: is this proper? Is it even used in ME3?
        {
            PopByte();
            var delegateProp = DecompileExpression();
            if (!(delegateProp is SymbolReference symRef)) return null;

            var delegateTypeName = ReadNameReference();

            var args = DecompileArgumentList();
            if (args == null) return null;

            StartPositions.Pop();
            return new DelegateCall(symRef, args);
        }

        public Expression DecompileDelegateProperty() // TODO: is this proper? Is it even used in ME3?
        {
            PopByte();
            var name = ReadNameReference();
            var obj = ReadObject(); 

            StartPositions.Pop();
            return new SymbolReference(null, name, null, null);
        }

#endregion

        /*
         * TODO: All of these need verification and changes
         * */
        #region UnsuportedDecompilers

        public Expression DecompileGoW_DefaultValue()
        {
            PopByte();
            var unkn = ReadByte();
            var expr = DecompileExpression();

            StartPositions.Pop();
            var op = new InOpDeclaration("", 0, 0, null, null, null);
            var objRef = new SymbolReference(null, "UNSUPPORTED: GoW_DefaultValue: Byte:" + unkn + " - ", null, null);
            return new InOpReference(op, objRef, expr, null, null);
        }

        public Expression DecompileNativeParm() // TODO: see code
        {
            PopByte();
            var obj = ReadObject();

            StartPositions.Pop();
            return new SymbolReference(null, "UNSUPPORTED: NativeParm: " + obj.ObjectName.Instanced + " : " + obj.ClassName, null, null);
        }

        public Expression DecompileInstanceDelegate() // TODO: check code, seems ok?
        {
            PopByte();
            var name = ReadNameReference();

            StartPositions.Pop();
            return new SymbolReference(null, name);
        }

        #endregion

    }
}
