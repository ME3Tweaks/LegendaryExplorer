using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.Unreal;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode
{

    public class BytecodeToken
    {
        public int NativeIndex;
        public string _text;
        private readonly int _offset;

        public BytecodeReader.ME1OpCodes OpCode { get; internal set; }

        public BytecodeToken(string text, int offset)
        {
            _text = text;
            _offset = offset;
        }

        public int GetOffset()
        {
            return _offset;
        }

        public override string ToString()
        {
            return _text;
        }

        public BytecodeSingularToken ToBytecodeSingularToken(int scriptStartOffset)
        {
            BytecodeSingularToken bcst = new BytecodeSingularToken();
            string opcodetext = OpCode.ToString();

            if (NativeIndex > 0)
            {
                var function = CachedNativeFunctionInfo.GetNativeFunction(NativeIndex); //this could be done dynamically. But if you're able to add native functions to the exe you probably know more about the engine than me
                if (function != null)
                {
                    opcodetext = $"NATIVE_{function.Name}";
                }
                //else
                //{
                //    throw new Exception($"Native opcode not found for index {NativeIndex}!");
                //}
            }
            if (opcodetext == "91") Debugger.Break();
            opcodetext = $"[{((byte)OpCode):X2}] {opcodetext}";
            bcst.CurrentStack = _text;
            bcst.OpCodeString = opcodetext;
            bcst.StartPos = _offset + scriptStartOffset;

            return bcst;
        }

    }

    class ReturnToken : BytecodeToken
    {
        private readonly BytecodeToken _returnValue;

        public ReturnToken(BytecodeToken returnValue, int offset)
            : base(returnValue.ToString().Length > 0 ? "return " + returnValue : "return", offset)
        {
            _returnValue = returnValue;
        }

        public BytecodeToken ReturnValue
        {
            get { return _returnValue; }
        }
    }

    class SwitchToken : BytecodeToken
    {
        public SwitchToken(string text, BytecodeToken expr, int offset) : base("switch (" + text + ")", offset)
        {
            Expr = expr;
        }

        public BytecodeToken Expr { get; private set; }
    }

    class CaseToken : BytecodeToken
    {
        public CaseToken(string text, int offset) : base("case " + text, offset)
        {
        }
    }

    class DefaultToken : BytecodeToken
    {
        public DefaultToken(int offset) : base("default", offset)
        {
        }
    }

    abstract class JumpToken : BytecodeToken
    {
        private readonly int _targetOffset;

        protected JumpToken(string text, int targetOffset, int offset) : base(text, offset)
        {
            _targetOffset = targetOffset;
        }

        public int TargetOffset
        {
            get { return _targetOffset; }
        }
    }

    class UncondJumpToken : JumpToken
    {
        public UncondJumpToken(int targetOffset, int offset) : base($"jump to 0x{targetOffset:X}", targetOffset, offset)
        {
        }
    }

    class JumpIfNotToken : JumpToken
    {
        private readonly BytecodeToken _condition;

        public JumpIfNotToken(int targetOffset, BytecodeToken condition, int offset)
            : base($"if (!{condition}) jump to 0x{targetOffset:X}", targetOffset, offset)
        {
            _condition = condition;
        }

        public BytecodeToken Condition
        {
            get { return _condition; }
        }
    }

    class ErrorBytecodeToken : BytecodeToken
    {
        public ErrorBytecodeToken(string text, int unknownBytecode, byte[] subsequentBytes, int offset) : base(text, offset)
        {
            UnknownBytecode = unknownBytecode;
            SubsequentBytes = subsequentBytes;
        }

        public int UnknownBytecode { get; private set; }
        public byte[] SubsequentBytes { get; private set; }
    }

    class EndParmsToken : BytecodeToken
    {
        public EndParmsToken(string text, int offset) : base(text, offset)
        {
        }
    }

    class DefaultValueToken : BytecodeToken
    {
        public DefaultValueToken(string text, int offset) : base(text, offset) { }
    }

    class DefaultParamValueToken : BytecodeToken
    {
        public DefaultParamValueToken(string text, int offset) : base(text, offset)
        {
        }
    }

    class ForeachToken : JumpToken
    {
        internal ForeachToken(int targetOffset, BytecodeToken expr, int offset) : base($"foreach ({expr}) end {targetOffset:X}", targetOffset, offset)
        {
            Expr = expr;
        }

        internal ForeachToken(int targetOffset, BytecodeToken expr, BytecodeToken iteratorExpr, int offset)
            : base($"foreach ({iteratorExpr} in {expr}) end {targetOffset:X}", targetOffset, offset)
        {
            Expr = expr;
            IteratorExpr = iteratorExpr;
        }

        public BytecodeToken Expr { get; private set; }
        public BytecodeToken IteratorExpr { get; private set; }
    }

    class IteratorNextToken : BytecodeToken
    {
        public IteratorNextToken(int offset) : base("IteratorNext", offset) { }
    }

    class IteratorPopToken : BytecodeToken
    {
        public IteratorPopToken(int offset) : base("IteratorPop", offset) { }
    }

    class NothingToken : BytecodeToken
    {
        public NothingToken(int offset, string displayString = "") : base(displayString, offset) { }
    }


    class StopToken : BytecodeToken
    {
        public StopToken(int offset) : base("EX_Stop (stop state)", offset) { }
    }


    class EndOfScriptToken : BytecodeToken
    {
        public EndOfScriptToken(int offset) : base("[End of script]", offset) { }
    }

    public class LabelTableToken : BytecodeToken
    {
        private readonly Dictionary<int, string> _labels = new Dictionary<int, string>();

        public LabelTableToken(int offset) : base("", offset) { }

        public void AddLabel(string name, int offset)
        {
            _labels[offset] = name;
        }

        public string GetLabel(int offset)
        {
            string result;
            if (_labels.TryGetValue(offset, out result))
                return result;
            return null;
        }

        public void updateUIText()
        {
            _text = "Label table:";
            foreach (var l in _labels)
            {
                _text += $"\n{l.Value} @ {l.Key:X8}";
            }
        }
    }

    public class BytecodeReader
    {
        /// <summary>
        /// Map of offsets (from the first byte of this token!) to a name reference
        /// </summary>
        public Dictionary<long, NameReference> NameReferences = new Dictionary<long, NameReference>();
        /// <summary>
        /// Map of offsets (from the first byte of this token!) to an entry reference
        /// </summary>
        public Dictionary<long, IEntry> EntryReferences = new Dictionary<long, IEntry>();

        public List<BytecodeToken> ReadTokens = new List<BytecodeToken>();
        public enum ME1OpCodes
        {
            EX_LocalVariable = 0x00,
            EX_InstanceVariable = 0x01,
            EX_DefaultVariable = 0x02,
            EX_Return = 0x04,
            EX_Switch = 0x05,
            EX_Jump = 0x06,
            EX_JumpIfNot = 0x07,
            EX_Stop = 0x08,
            EX_Assert = 0x09,
            EX_Case = 0x0A,
            EX_Nothing = 0x0B,
            EX_LabelTable = 0x0C,
            EX_GotoLabel = 0x0D,
            EX_EatReturnValue = 0x0E,
            EX_Let = 0x0F,
            EX_DynArrayElement = 0x10,
            EX_New = 0x11,
            EX_ClassContext = 0x12,
            EX_Metacast = 0x13,
            EX_LetBool = 0x14,
            // EX_EndParmValue = 0x15?
            EX_EndFunctionParms = 0x16,
            EX_Self = 0x17,
            EX_Skip = 0x18,
            EX_Context = 0x19,
            EX_ArrayElement = 0x1A,
            EX_VirtualFunction = 0x1B,
            EX_FinalFunction = 0x1C,
            EX_IntConst = 0x1D,
            EX_FloatConst = 0x1E,
            EX_StringConst = 0x1F,
            EX_ObjectConst = 0x20,
            EX_NameConst = 0x21,
            EX_RotationConst = 0x22,
            EX_VectorConst = 0x23,
            EX_ByteConst = 0x24,
            EX_IntZero = 0x25,
            EX_IntOne = 0x26,
            EX_True = 0x27,
            EX_False = 0x28,
            EX_NativeParm = 0x29,
            EX_NoObject = 0x2A,
            EX_IntConstByte = 0x2C,
            EX_BoolVariable = 0x2D,
            EX_DynamicCast = 0x2E,
            EX_Iterator = 0x2F,
            EX_IteratorPop = 0x30,
            EX_IteratorNext = 0x31,
            EX_StructCmpEq = 0x32,
            EX_StructCmpNe = 0x33,
            EX_UnicodeStringConst = 0x34,
            EX_StructMember = 0x35,
            EX_DynArrayLength = 0x36,
            EX_GlobalFunction = 0x37,
            EX_PrimitiveCast = 0x38,
            EX_DynArrayInsert = 0x39,
            EX_ReturnNothing = 0x3A,
            EX_EqualEqual_DelDel = 0x3B,
            EX_NotEqual_DelDel = 0x3C,
            EX_EqualEqual_DelFunc = 0x3D,
            EX_NotEqual_DelFunc = 0x3E,
            EX_EmptyDelegate = 0x3F,
            EX_DynArrayRemove = 0x40,
            EX_DebugInfo = 0x41,
            EX_DelegateFunction = 0x42,
            EX_DelegateProperty = 0x43,
            EX_LetDelegate = 0x44,
            EX_Conditional = 0x45,
            EX_DynArrayFind = 0x46,
            EX_DynArrayFindStruct = 0x47,
            EX_LocalOutVariable = 0x48,
            EX_DefaultParmValue = 0x49,
            EX_EmptyParmValue = 0x4A, //PC ONLY!
            EX_ME1XBox_StrRefConst = 0x4A, //XBOX ME1
            EX_InstanceDelegate = 0x4B, //PC ONLY!
            EX_ME1XBox_DynArrayAdd = 0x4B, //XBOX ME1
            EX_StringRefConst = 0x4F,
            EX_GoW_DefaultValue = 0x50,
            EX_InterfaceContext = 0x51,
            EX_InterfaceCast = 0x52,
            EX_EndOfScript = 0x53,
            EX_DynArrayAdd = 0x54,
            EX_DynArrayAddItem = 0x55,
            EX_DynArrayRemoveItem = 0x56,
            EX_DynArrayInsertItem = 0x57,
            EX_DynArrayIterator = 0x58,

            EX_ExtendedNative = 0x60,
            EX_FirstNative = 0x70
        }

        enum ECastToken : byte
        {
            InterfaceToObject = 0x36,
            InterfaceToString = 0x37,
            InterfaceToBool = 0x38,
            RotatorToVector = 0x39,
            ByteToInt = 0x3A,
            ByteToBool = 0x3B,
            ByteToFloat = 0x3C,
            IntToByte = 0x3D,
            IntToBool = 0x3E,
            IntToFloat = 0x3F,
            BoolToByte = 0x40,
            BoolToInt = 0x41,
            BoolToFloat = 0x42,
            FloatToByte = 0x43,
            FloatToInt = 0x44,
            FloatToBool = 0x45,
            ObjectToInterface = 0x46,
            ObjectToBool = 0x47,
            NameToBool = 0x48,
            StringToByte = 0x49,
            StringToInt = 0x4A,
            StringToBool = 0x4B,
            StringToFloat = 0x4C,
            StringToVector = 0x4D,
            StringToRotator = 0x4E,
            VectorToBool = 0x4F,
            VectorToRotator = 0x50,
            RotatorToBool = 0x51,
            ByteToString = 0x52,
            IntToString = 0x53,
            BoolToString = 0x54,
            FloatToString = 0x55,
            ObjectToString = 0x56,
            NameToString = 0x57,
            VectorToString = 0x58,
            RotatorToString = 0x59,
            DelegateToString = 0x5A,
            StringRefToInt = 0x5B,
            StringRefToString = 0x5C,
            IntToStringRef = 0x5D,
            StringToName = 0x60
        };

        private readonly IMEPackage _package;
        public readonly BinaryReader _reader;

        public BytecodeReader(IMEPackage package, BinaryReader reader)
        {
            _package = package;
            _reader = reader;
        }

        private readonly ME1OpCodes[] OpCodesThatReturnNextToken = { ME1OpCodes.EX_Skip, ME1OpCodes.EX_EatReturnValue, ME1OpCodes.EX_ReturnNothing, ME1OpCodes.EX_BoolVariable, ME1OpCodes.EX_InterfaceContext };
        /// <summary>
        /// ME3Explorer intercepting function to build the token list. As tokens are read the tokens list will be updated.
        /// This method is used to prevent significant modifications to ReadNextInternal() (originally ReadNext)
        /// </summary>
        /// <returns></returns>
        public BytecodeToken ReadNext(bool dontAddToken = false)
        {
            ME1OpCodes b = (ME1OpCodes)_reader.ReadByte();
            _reader.BaseStream.Position--;

            if (OpCodesThatReturnNextToken.Contains(b))
            {
                BytecodeToken t = new BytecodeToken("", (int)_reader.BaseStream.Position);

                t.OpCode = b;
                ReadTokens.Add(t);
            }

            //if (b == ME1OpCodes.EX_Skip)
            //{
            //    Debugger.Break();
            //}

            //Debug.WriteLine("Read bytecode " + ((byte)b).ToString("X2") + " " + b + " at " + _reader.BaseStream.Position.ToString("X8"));
            BytecodeToken bct = ReadNextInternal();
            //Debug.WriteLine("Bytecode finished: " + ((byte)b).ToString("X2") + " " + b + " from " + _reader.BaseStream.Position.ToString("X8"));

            if (!OpCodesThatReturnNextToken.Contains(b))
            {
                bct.OpCode = b;
                ReadTokens.Add(bct);
            }
            return bct;
        }

        private BytecodeToken ReadNextInternal()
        {
            int readerpos = (int)_reader.BaseStream.Position;
            ME1OpCodes b = (ME1OpCodes)_reader.ReadByte();
            switch (b)
            {
                case ME1OpCodes.EX_LocalVariable:
                case ME1OpCodes.EX_InstanceVariable:
                case ME1OpCodes.EX_NativeParm:
                    return ReadRef(r => r != null ? r.ObjectName.Instanced : "Unresolved");

                case ME1OpCodes.EX_DefaultVariable:
                    return ReadRef(r => $"Default.{r.ObjectName.Instanced}");

                case ME1OpCodes.EX_Return:
                    {
                        BytecodeToken returnValue = ReadNext();
                        return new ReturnToken(returnValue, readerpos);
                    }
                case ME1OpCodes.EX_Assert:
                    {
                        _reader.ReadInt16();
                        _reader.ReadByte();
                        return WrapNextBytecode(c => new BytecodeToken($"assert({c})", readerpos));
                    }

                case ME1OpCodes.EX_Switch:
                    {
                        //if (_package.Game == MEGame.ME1 && _package.Platform != MEPackage.GamePlatform.PS3)
                        //                        if (_package.Platform == MEPackage.GamePlatform.Xenon || _package.Game != MEGame.ME1)
                        //{
                        //ME1 Xbox this is occurs before the EX_Switch token...
                        _reader.ReadByte(); //Property type or something
                        //This is a workaround for EX_Switch, maybe
                        //}
                        BytecodeToken switchExpr = ReadNext();
                        return new SwitchToken(switchExpr.ToString(), switchExpr, readerpos);
                    }
                case ME1OpCodes.EX_Case:
                    {
                        short offset = _reader.ReadInt16();
                        if (offset == -1) return new DefaultToken(readerpos);
                        BytecodeToken caseExpr = ReadNext();
                        return new CaseToken(caseExpr.ToString(), readerpos);
                    }

                case ME1OpCodes.EX_Jump:
                    {
                        int offset = _reader.ReadInt16();
                        return new UncondJumpToken(offset, readerpos);
                    }

                case ME1OpCodes.EX_JumpIfNot:
                    {
                        short offset = _reader.ReadInt16();
                        BytecodeToken condition = ReadNext();
                        if (IsInvalid(condition)) return WrapErrToken("if (!" + condition, condition);
                        return new JumpIfNotToken(offset, condition, readerpos);
                    }

                case ME1OpCodes.EX_LabelTable:
                    {
                        var token = new LabelTableToken(readerpos);

                        var labelName = ReadName();
                        var offset = _reader.ReadInt32();
                        while (offset != 0xFFFF)
                        {
                            token.AddLabel(labelName, offset);
                            labelName = ReadName();
                            offset = _reader.ReadInt32();
                        }

                        token.updateUIText();
                        return token;
                    }

                case ME1OpCodes.EX_GotoLabel:
                    return WrapNextBytecode(op => Token("goto " + op, readerpos));

                case ME1OpCodes.EX_Self:
                    return Token("self", readerpos);

                case ME1OpCodes.EX_Skip:
                    _reader.ReadInt16();
                    //Returning readnext causes a new token to be read
                    return ReadNext();

                case ME1OpCodes.EX_EatReturnValue:
                    ReadEntryRef(out _);
                    return ReadNext();

                case ME1OpCodes.EX_Nothing:
                    return new NothingToken(readerpos, "EX_Nothing");

                case ME1OpCodes.EX_Stop:
                    //_reader.ReadInt16(); Stop seems to be only 1 byte. Not 3
                    return new StopToken(readerpos);

                case ME1OpCodes.EX_IntZero:
                    return Token("0", readerpos);

                case ME1OpCodes.EX_IntOne:
                    return Token("1", readerpos);

                case ME1OpCodes.EX_True:
                    return Token("true", readerpos);

                case ME1OpCodes.EX_False:
                    return Token("false", readerpos);

                case ME1OpCodes.EX_NoObject:
                case ME1OpCodes.EX_EmptyDelegate:
                    return Token("None", readerpos);

                case ME1OpCodes.EX_Let:
                case ME1OpCodes.EX_LetBool:
                case ME1OpCodes.EX_LetDelegate:
                    BytecodeToken lhs = ReadNext();
                    if (IsInvalid(lhs)) return lhs;
                    BytecodeToken rhs = ReadNext();
                    if (IsInvalid(rhs)) return WrapErrToken(lhs + " = " + rhs, rhs);
                    return Token(lhs + " = " + rhs, readerpos);

                case ME1OpCodes.EX_IntConst:
                    return Token(_reader.ReadInt32().ToString(), readerpos);

                case ME1OpCodes.EX_FloatConst:
                    return Token(_reader.ReadSingle().ToString(), readerpos);

                case ME1OpCodes.EX_StringConst:
                    {
                        var s = ReadAsciiz().Replace("\n", "\\n").Replace("\t", "\\t");
                        return Token($"\"{s}\"", readerpos);
                    }

                case ME1OpCodes.EX_ByteConst:
                case ME1OpCodes.EX_IntConstByte:
                    return Token(_reader.ReadByte().ToString(), readerpos);

                case ME1OpCodes.EX_ObjectConst:
                    {
                        var item = ReadEntryRef(out var objectIndex);
                        if (item == null) return ErrToken("Unresolved class item " + objectIndex);
                        return Token($"{item.ClassName}'{item.ObjectName.Instanced}'", readerpos);
                    }

                case ME1OpCodes.EX_NameConst:
                    return Token($"'{ReadName()}'", readerpos);
                case ME1OpCodes.EX_EndFunctionParms:
                    return new EndParmsToken(")", readerpos);

                case ME1OpCodes.EX_ClassContext:
                case ME1OpCodes.EX_Context:
                    {
                        var contextId = _reader.BaseStream.Position - 1;
                        //Debug.WriteLine($"Reading EX_Context START at 0x{(contextId):X8} (ID {contextId})");
                        var context = ReadNext();
                        if (IsInvalid(context)) return context;

                        //if (_package.Platform != MEPackage.GamePlatform.PC && _reader.BaseStream.Position % 2 != 0)
                        //{
                        //    _reader.ReadByte(); //Byte align
                        //}

                        int exprSize = _reader.ReadInt16();
                        //Debug.WriteLine($" >> {contextId}: ExprSize {exprSize} at 0x{(_reader.BaseStream.Position - 2):X8}");


                        int bSize = _reader.ReadByte();
                        //Debug.WriteLine($" >> {contextId}: bSize {bSize} at 0x{(_reader.BaseStream.Position - 1):X8}");

                        //Debug.WriteLine($" >> {contextId}: Value at 0x{(_reader.BaseStream.Position):X8}");
                        var value = ReadNext();

                        //Debug.WriteLine($" >> {contextId}: END OF EX_Context 0x{(_reader.BaseStream.Position):X8}");

                        if (IsInvalid(value)) return WrapErrToken($"{context}.{value}", value);
                        return Token($"{context}.{value}", readerpos);
                    }

                case ME1OpCodes.EX_InterfaceContext:
                    return ReadNext();

                case ME1OpCodes.EX_FinalFunction:
                    {
                        var item = ReadEntryRef(out var functionIndex);
                        if (item == null) return ErrToken("Unresolved function item " + functionIndex);
                        string functionName = item.ObjectName.Instanced;
                        return ReadCall(readerpos, functionName);
                    }

                case ME1OpCodes.EX_PrimitiveCast:
                    {
                        ECastToken conversionType = (ECastToken)_reader.ReadByte();
                        var v = ReadNext();
                        string castStr;
                        if (Enum.IsDefined(typeof(ECastToken), conversionType))
                        {
                            castStr = conversionType.ToString();
                        }
                        else
                        {
                            castStr = "UNKNOWN_CAST";
                        }
                        return Token($"{castStr}({v})", readerpos);
                    }

                case ME1OpCodes.EX_VirtualFunction:
                    return ReadCall(readerpos, ReadName());

                case ME1OpCodes.EX_GlobalFunction:
                    return ReadCall(readerpos, "Global." + ReadName());

                case ME1OpCodes.EX_BoolVariable:
                    return ReadNext();
                case ME1OpCodes.EX_ReturnNothing:
                    ReadEntryRef(out var objectRefIdx);
                    if (_package.IsEntry(objectRefIdx))
                    {
                        return Token($"ReturnNothing({_package.getObjectName(objectRefIdx)})", readerpos);
                    }
                    else
                    {
                        return Token($"ReturnNothing(Unknown reference {objectRefIdx})", readerpos);
                    }
                case ME1OpCodes.EX_DynamicCast:
                    {
                        var item = ReadEntryRef(out var typeIndex);
                        return WrapNextBytecode(op => Token($"{item.ObjectName.Instanced}({op})", readerpos));
                    }

                case ME1OpCodes.EX_Metacast:
                    {
                        var item = ReadEntryRef(out var typeIndex);
                        if (item == null) return ErrToken("Unresolved class item " + typeIndex);
                        return WrapNextBytecode(op => Token($"Class<{item.ObjectName.Instanced}>({op})", readerpos));
                    }

                case ME1OpCodes.EX_StructMember:
                    {
                        var field = ReadEntryRef(out var _1);
                        var structType = ReadEntryRef(out var _2);
                        int wSkip = field.FileRef.Platform == MEPackage.GamePlatform.Xenon && field.FileRef.Game == MEGame.ME1 ? _reader.ReadByte() : _reader.ReadInt16(); //ME1 Xenon seems to only use 1 byte?
                        var token = ReadNext();
                        if (IsInvalid(token)) return token;
                        return Token($"{token}.{field.ObjectName.Instanced}", readerpos);
                    }

                case ME1OpCodes.EX_ArrayElement:
                case ME1OpCodes.EX_DynArrayElement:
                    {
                        var index = ReadNext();
                        if (IsInvalid(index)) return index;
                        var array = ReadNext();
                        if (IsInvalid(array)) return array;
                        return Token($"{array}[{index}]", readerpos);
                    }

                case ME1OpCodes.EX_DynArrayLength:
                    return WrapNextBytecode(op => Token($"{op}.Length", readerpos));

                case ME1OpCodes.EX_StructCmpEq:
                    return CompareStructs("==");

                case ME1OpCodes.EX_StructCmpNe:
                    return CompareStructs("!=");

                case ME1OpCodes.EX_EndOfScript:
                    return new EndOfScriptToken(readerpos);

                case ME1OpCodes.EX_EmptyParmValue when _package.Platform != MEPackage.GamePlatform.Xenon || _package.Game != MEGame.ME1:
                case ME1OpCodes.EX_GoW_DefaultValue:
                    return new DefaultValueToken("", readerpos);

                case ME1OpCodes.EX_DefaultParmValue:
                    {
                        var size = _reader.ReadInt16();
                        var offset = _reader.BaseStream.Position;
                        var defaultValueExpr = ReadNext();
                        _reader.BaseStream.Position = offset + size;
                        return new DefaultParamValueToken(defaultValueExpr.ToString(), readerpos);
                    }

                case ME1OpCodes.EX_LocalOutVariable:
                    var packageItem = ReadEntryRef(out var valueIndex);
                    if (packageItem == null) return ErrToken("Unresolved package item " + valueIndex);
                    return Token(packageItem.ObjectName.Instanced, readerpos);

                case ME1OpCodes.EX_Iterator:
                    var expr = ReadNext();
                    int loopEnd = _reader.ReadInt16();
                    if (IsInvalid(expr)) return WrapErrToken("foreach " + expr, expr);
                    return new ForeachToken(loopEnd, expr, readerpos);

                case ME1OpCodes.EX_IteratorPop:
                    return new IteratorPopToken(readerpos);

                case ME1OpCodes.EX_IteratorNext:
                    return new IteratorNextToken(readerpos);

                case ME1OpCodes.EX_New:
                    var outer = ReadNext();
                    if (IsInvalid(outer)) return outer;
                    var name = ReadNext();
                    if (IsInvalid(name)) return name;
                    var flags = ReadNext();
                    if (IsInvalid(flags)) return flags;
                    var cls = ReadNext();
                    if (IsInvalid(cls)) return cls;
                    return Token($"new({JoinTokens(outer, name, flags, cls)})", readerpos);

                case ME1OpCodes.EX_VectorConst:
                    var f1 = _reader.ReadSingle();
                    var f2 = _reader.ReadSingle();
                    var f3 = _reader.ReadSingle();
                    return Token($"vect({f1},{f2},{f3})", readerpos);

                case ME1OpCodes.EX_RotationConst:
                    var i1 = _reader.ReadInt32();
                    var i2 = _reader.ReadInt32();
                    var i3 = _reader.ReadInt32();
                    return Token($"rot({i1},{i2},{i3})", readerpos);

                case ME1OpCodes.EX_InterfaceCast:
                    {
                        var interfaceName = ReadEntryRef(out var _);
                        return WrapNextBytecode(op => Token($"{interfaceName.ObjectName.Instanced}({op})", readerpos));
                    }

                case ME1OpCodes.EX_Conditional:
                    {
                        var condition = ReadNext();
                        if (IsInvalid(condition)) return condition;
                        var trueSize = _reader.ReadInt16();
                        var pos = _reader.BaseStream.Position;
                        var truePart = ReadNext();
                        if (IsInvalid(truePart)) return WrapErrToken($"{condition} ? {truePart}", truePart);
                        if (_reader.BaseStream.Position != pos + trueSize)
                            return ErrToken("conditional true part size mismatch");
                        var falseSize = _reader.ReadInt16();
                        pos = _reader.BaseStream.Position;
                        var falsePart = ReadNext();
                        if (IsInvalid(truePart)) return WrapErrToken($"{condition} ? {truePart} : {falsePart}", falsePart);
                        Debug.Assert(_reader.BaseStream.Position == pos + falseSize);
                        return Token($"{condition} ? {truePart} : {falsePart}", readerpos);
                    }

                case ME1OpCodes.EX_DynArrayFind:
                    return ReadDynArray1ArgMethodSpecial("Find");

                case ME1OpCodes.EX_DynArrayFindStruct:
                    return ReadDynArray2ArgMethod("Find", true);

                case ME1OpCodes.EX_DynArrayRemove:
                    return ReadDynArray2ArgMethod("Remove", false);

                case ME1OpCodes.EX_DynArrayInsert:
                    return ReadDynArray2ArgMethod("Insert", false);

                case ME1OpCodes.EX_ME1XBox_DynArrayAdd when _package.Platform == MEPackage.GamePlatform.Xenon && _package.Game == MEGame.ME1:
                // Dybuk discovered that DynArrayAdd is 0x4B in ME1 Xbox. Not sure about ME2 PC uses different opcode
                case ME1OpCodes.EX_DynArrayAdd:
                    return ReadDynArray1ArgMethod("Add");

                case ME1OpCodes.EX_DynArrayAddItem:
                    return ReadDynArray1ArgMethod("AddItem");

                case ME1OpCodes.EX_DynArrayRemoveItem:
                    return ReadDynArray1ArgMethod("RemoveItem");

                case ME1OpCodes.EX_DynArrayInsertItem:
                    return ReadDynArray2ArgMethod("InsertItem", true);

                case ME1OpCodes.EX_DynArrayIterator:
                    {
                        var array = ReadNext();
                        if (IsInvalid(array)) return array;
                        var iteratorVar = ReadNext();
                        if (IsInvalid(iteratorVar)) return iteratorVar;
                        //_reader.ReadInt16(); //Num bytes to skip if null
                        byte indexParmPrecense = _reader.ReadByte();
                        var indexParm = ReadNext();
                        var endOffset = _reader.ReadInt16();
                        return new ForeachToken(endOffset, array, iteratorVar, readerpos);
                    }

                case ME1OpCodes.EX_DelegateProperty:
                case ME1OpCodes.EX_InstanceDelegate when _package.Platform != MEPackage.GamePlatform.Xenon: // might need scoped to ME1 only
                    return Token(ReadName(), readerpos);

                case ME1OpCodes.EX_DelegateFunction:
                    {
                        var receiver = ReadNext();
                        if (IsInvalid(receiver)) return receiver;
                        var methodName = ReadName();
                        if (receiver.ToString().StartsWith("__") && receiver.ToString().EndsWith("__Delegate"))
                        {
                            return ReadCall(readerpos, methodName);
                        }
                        return ReadCall(readerpos, receiver + "." + methodName);
                    }

                case ME1OpCodes.EX_EqualEqual_DelDel:
                case ME1OpCodes.EX_EqualEqual_DelFunc:
                    return CompareDelegates("==");

                case ME1OpCodes.EX_NotEqual_DelDel:
                case ME1OpCodes.EX_NotEqual_DelFunc:
                    return CompareDelegates("!=");

                case ME1OpCodes.EX_StringRefConst:
                case ME1OpCodes.EX_ME1XBox_StrRefConst when _package.Platform == MEPackage.GamePlatform.Xenon && _package.Game == MEGame.ME1:
                    return ReadStringRefConst(readerpos);

                default:
                    if ((int)b >= 0x60)
                    {
                        return ReadNativeCall((byte)b);
                    }
                    return ErrToken("// unknown bytecode " + ((byte)b).ToString("X2"), (int)b);
            }
        }

        private BytecodeToken ReadStringRefConst(int start)
        {
            int index = _reader.ReadInt32();

            string text = _package.Game switch
            {
                MEGame.ME1 => ME1TalkFiles.FindDataById(index, _package),
                MEGame.ME2 => ME2TalkFiles.FindDataById(index),
                _ => "ME3Explorer message: N/A"
            };

            var token = new BytecodeToken($"${index}({text})", start)
            {
                NativeIndex = 0,
                OpCode = ME1OpCodes.EX_StringRefConst
            };

            return token;
        }

        private BytecodeToken CompareDelegates(string op)
        {
            int readerpos = (int)_reader.BaseStream.Position - 1;

            var operand1 = ReadNext();
            if (IsInvalid(operand1)) return operand1;
            var operand2 = ReadNext();
            if (IsInvalid(operand2)) return operand2;
            ReadNext();  // close paren
            return Token(operand1 + " " + op + " " + operand2, readerpos);
        }

        private BytecodeToken ReadDynArray1ArgMethodSpecial(string methodName)
        {
            int readerpos = (int)_reader.BaseStream.Position - 1;

            var arrayExpression = ReadNext();
            if (IsInvalid(arrayExpression)) return arrayExpression;
            if (_package.Platform == MEPackage.GamePlatform.Xenon && _package.Game == MEGame.ME1)
            {
                _reader.ReadByte(); //This is a workaround for EX_Switch, maybe?
            }

            var numBytesToSkipIfExpressionNull = _reader.ReadUInt16();

            ////array.
            //if (_package.Platform == MEPackage.GamePlatform.PC && _package.Game == MEGame.ME2)
            //{
            //    //if (array.OpCode == ME1OpCodes.EX_LocalVariable ||
            //    //    array.OpCode == ME1OpCodes.EX_Context)
            //    //{
            //    var arrayObj = EntryReferences[EntryReferences.Max(x => x.Key)]; //Get the last read reference (what a hack)
            //    if (arrayObj.ClassName == "ArrayProperty")
            //    {
            //        //We have to look this up to see if we need to skip 2 bytes
            //        //    Because they couldn't just use the right opcode
            //        ExportEntry ee = arrayObj as ExportEntry;
            //        if (ee == null && arrayObj is ImportEntry ie)
            //        {
            //            ee = EntryImporter.ResolveImport(ie);
            //        }

            //        if (ee != null)
            //        {
            //            var holdsItemsOfTypeIdx = EndianReader.ToInt32(ee.Data, ee.DataSize - 4, _package.Endian);
            //            var itemOfType = _package.GetEntry(holdsItemsOfTypeIdx);
            //            if (/*itemOfType.ClassName == "StructProperty" || */itemOfType.ClassName == "NameProperty")
            //            {
            //                var exprSize = _reader.ReadInt16();
            //            }
            //        }
            //        else
            //        {
            //            Debug.WriteLine("ERROR: COULD NOT FIND CLASS TO CHECK AGAINST! Not skipping 2 bytes");
            //        }

            //        Debug.WriteLine($"Entry type: {arrayObj.ClassName}");
            //    }
            //}
            var indexer = ReadNext();
            if (IsInvalid(indexer)) return WrapErrToken(arrayExpression + "." + methodName + "(" + indexer, indexer);
            return Token(arrayExpression + "." + methodName + "(" + indexer + ")", readerpos);
        }

        private BytecodeToken ReadDynArray1ArgMethod(string methodName)
        {
            int readerpos = (int)_reader.BaseStream.Position - 1;

            var arrayExpression = ReadNext();
            if (IsInvalid(arrayExpression)) return arrayExpression;
            //if (_package.Platform == MEPackage.GamePlatform.Xenon && _package.Game == MEGame.ME1)
            //{
            //    _reader.ReadByte(); //This is a workaround for EX_Switch, maybe?
            //}

            if (methodName != "Add" && _package.Game == MEGame.ME2 && _package.Platform != MEPackage.GamePlatform.PS3)
            {
                var numBytesToSkipIfExpressionNull = _reader.ReadUInt16();
            }
            //array.
            //if (_package.Platform == MEPackage.GamePlatform.PC && _package.Game == MEGame.ME2)
            //{
            //    //if (array.OpCode == ME1OpCodes.EX_LocalVariable ||
            //    //    array.OpCode == ME1OpCodes.EX_Context)
            //    //{
            //    var arrayObj = EntryReferences[EntryReferences.Max(x => x.Key)]; //Get the last read reference (what a hack)
            //    if (arrayObj.ClassName == "ArrayProperty")
            //    {
            //        //We have to look this up to see if we need to skip 2 bytes
            //        //    Because they couldn't just use the right opcode
            //        ExportEntry ee = arrayObj as ExportEntry;
            //        if (ee == null && arrayObj is ImportEntry ie)
            //        {
            //            ee = EntryImporter.ResolveImport(ie);
            //        }

            //        if (ee != null)
            //        {
            //            var holdsItemsOfTypeIdx = EndianReader.ToInt32(ee.Data, ee.DataSize - 4, _package.Endian);
            //            var itemOfType = _package.GetEntry(holdsItemsOfTypeIdx);
            //            if (/*itemOfType.ClassName == "StructProperty" || */itemOfType.ClassName == "NameProperty")
            //            {
            //                var exprSize = _reader.ReadInt16();
            //            }
            //        }
            //        else
            //        {
            //            Debug.WriteLine("ERROR: COULD NOT FIND CLASS TO CHECK AGAINST! Not skipping 2 bytes");
            //        }

            //        Debug.WriteLine($"Entry type: {arrayObj.ClassName}");
            //    }
            //}
            // }


            var indexer = ReadNext();
            if (IsInvalid(indexer)) return WrapErrToken(arrayExpression + "." + methodName + "(" + indexer, indexer);
            return Token(arrayExpression + "." + methodName + "(" + indexer + ")", readerpos);
        }

        private BytecodeToken ReadDynArray2ArgMethod(string methodName, bool skip2Bytes)
        {
            int readerpos = (int)_reader.BaseStream.Position;

            var array = ReadNext();
            if (IsInvalid(array)) return array;
            if (skip2Bytes) _reader.ReadInt16();
            var index = ReadNext();
            if (IsInvalid(index)) return index;
            var count = ReadNext();
            if (IsInvalid(count)) return count;
            return Token(array + "." + methodName + "(" + index + ", " + count + ")", readerpos);
        }

        private static string JoinTokens(params BytecodeToken[] tokens)
        {
            var result = new StringBuilder();
            foreach (var token in tokens)
            {
                string s = token.ToString();
                if (s.Length > 0)
                {
                    if (result.Length > 0) result.Append(",");
                    result.Append(s);
                }
            }
            return result.ToString();
        }

        private BytecodeToken CompareStructs(string op)
        {
            int pos = (int)_reader.BaseStream.Position - 1;

            ReadEntryRef(out int structClassUIndex);
            var operand1 = ReadNext();
            if (IsInvalid(operand1)) return operand1;
            var operand2 = ReadNext();
            if (IsInvalid(operand2)) return operand2;
            return Token(operand1 + " " + op + " " + operand2, pos);
        }

        internal static BytecodeToken Token(string text, int offset, int nativeindex = 0)
        {
            return new BytecodeToken(text, offset)
            {
                NativeIndex = nativeindex
            };
        }

        internal BytecodeToken ErrToken(string text, int invalidBytecode)
        {
            int pos = (int)_reader.BaseStream.Position;
            int length = (int)(_reader.BaseStream.Length - _reader.BaseStream.Position);
            byte[] subsequentBytes = _reader.ReadBytes(length);
            return new ErrorBytecodeToken(text + " " + DumpBytes(subsequentBytes, 0, -1), invalidBytecode, subsequentBytes, pos);
        }

        internal BytecodeToken ErrToken(string text)
        {
            return ErrToken(text, -1);
        }

        internal BytecodeToken WrapErrToken(string text, BytecodeToken token)
        {
            int pos = (int)_reader.BaseStream.Position;
            var errorBytecodeToken = token as ErrorBytecodeToken;
            if (errorBytecodeToken != null)
            {
                return new ErrorBytecodeToken(text, errorBytecodeToken.UnknownBytecode,
                                              errorBytecodeToken.SubsequentBytes, pos);
            }
            return ErrToken(text);
        }

        internal string ReadName()
        {
            var pos = _reader.BaseStream.Position;
            var nref = _reader.ReadInt32();
            var nrefinstance = _reader.ReadInt32();
            var name = _package.GetNameEntry(nref);
            var nameref = new NameReference(name, nrefinstance);
            NameReferences[pos] = nameref;
            return nameref.Instanced;
        }

        internal IEntry ReadEntryRef(out int idx)
        {
            var pos = _reader.BaseStream.Position;
            idx = _reader.ReadInt32();
            var entry = _package.GetEntry(idx);

            //Add always so we have relinkability... I guess
            if (entry != null)
            {
                EntryReferences[pos] = entry;
            }
            //Following two conditions are for relinking
            else if (idx < 0)
            {
                EntryReferences[pos] = new ImportEntry(_package) { Index = Math.Abs(idx) - 1 }; //Force UIndex
                return _package.Imports.First(); //this is so rest of parser won't crash. It's a real hack...
            }
            else if (idx > 0)
            {
                EntryReferences[pos] = new ExportEntry(_package, 0, _package.Names[0]) { Index = Math.Abs(idx) - 1 }; //Force UIndex
                return _package.Exports.First(); //this is so rest of parser won't crash. It's a real hack...
            }


            return entry;
        }

        internal BytecodeToken ReadRef(Func<IEntry, string> func)
        {
            int pos = (int)_reader.BaseStream.Position - 1; //We already read the bytecode token.
            var item = ReadEntryRef(out var index);
            return Token(func(item), pos);
        }

        private BytecodeToken WrapNextBytecode(Func<BytecodeToken, BytecodeToken> func)
        {
            BytecodeToken operand = ReadNext();
            if (IsInvalid(operand)) return operand;
            return func(operand);
        }

        private string ReadAsciiz()
        {
            var result = new StringBuilder();
            char c;
            while ((c = _reader.ReadChar()) != '\0')
            {
                result.Append(c);
            }
            return result.ToString();
        }

        private static bool IsInvalid(BytecodeToken token)
        {
            return token == null || token is ErrorBytecodeToken;
        }

        public static string DumpBytes(byte[] bytes, int startIndex, int maxCount)
        {
            var result = new StringBuilder();
            for (int i = startIndex; i < bytes.Length && i < startIndex + maxCount; i++)
            {
                result.Append(bytes[i].ToString("X2")).Append(" ");
            }
            return result.ToString();
        }

        private BytecodeToken ReadCall(int pos, string functionName)
        {
            BytecodeToken p;
            var builder = new StringBuilder(functionName + "(");
            int count = 0;
            do
            {
                try
                {
                    p = ReadNext();
                }
                catch (EndOfStreamException)
                {
                    return ErrToken("EOF exception reading call parameters");
                }
                if (IsInvalid(p)) return WrapErrToken(builder + p.ToString(), p);
                if (!(p is DefaultValueToken))
                {
                    if (count > 0 && !(p is EndParmsToken)) builder.Append(", ");
                    builder.Append(p);
                    count++;
                }
            } while (!(p is EndParmsToken));
            return Token(builder.ToString(), pos);
        }

        private BytecodeToken ReadNativeCall(byte b)
        {
            int pos = (int)_reader.BaseStream.Position - 1;
            int nativeIndex;
            if ((b & 0xF0) == 0x60) //Extended Native
            {
                byte b2 = _reader.ReadByte();
                nativeIndex = ((b - 0x60) << 8) + b2;
            }
            else
            {
                nativeIndex = b; //Native
            }

            var function = CachedNativeFunctionInfo.GetNativeFunction(nativeIndex); //have to figure out how to do this, it's looking up name of native function
            if (function == null)
                return ErrToken("// invalid native function " + nativeIndex);
            if (function.PreOperator || function.PostOperator)
            {
                var p = ReadNext();
                if (IsInvalid(p)) return p;
                ReadNext();   // end of parms
                if (function.PreOperator)
                    return Token((function.HumanReadableControlToken ?? function.Name) + p, pos, nativeIndex);
                return Token(p + (function.HumanReadableControlToken ?? function.Name), pos, nativeIndex);
            }
            if (function.Operator)
            {
                var p1 = ReadNext();
                if (IsInvalid(p1)) return WrapErrToken(function.Name + "(" + p1, p1);
                var p2 = ReadNext();
                if (IsInvalid(p2)) return WrapErrToken(p1 + " " + function.Name + " " + p2, p2);
                ReadNext();  // end of parms
                return Token(p1 + " " + (function.HumanReadableControlToken ?? function.Name) + " " + p2, pos, nativeIndex);
            }
            var totalToken = ReadCall(pos, function.Name);
            totalToken.NativeIndex = nativeIndex;
            return totalToken;
        }
    }

    public class CachedNativeFunctionInfo
    {
        public static Dictionary<int, CachedNativeFunctionInfo> NativeFunctionInfo;

        public int nativeIndex;
        public bool PreOperator;
        public bool PostOperator;
        public bool Operator;
        public string Name;
        public string Filename;
        public string HumanReadableControlToken; //like ==, ++, etc

        public static CachedNativeFunctionInfo GetNativeFunction(int index)
        {
            CachedNativeFunctionInfo result;
            if (NativeFunctionInfo == null)
            {
                LoadME1NativeFunctionsInfo();
            }
            if (NativeFunctionInfo != null) //file check
            {
                if (!NativeFunctionInfo.TryGetValue(index, out result)) return null;
                return result;
            }
            return null;
        }

        private static bool ME1NativeInfoLoaded;
        internal static void LoadME1NativeFunctionsInfo()
        {
            if (ME1NativeInfoLoaded) return; //already loaded
            try
            {
                if (LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("Infos.zip", $"ME1NativeFunctionInfo.json") is string raw)
                {
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { NativeFunctionInfo });
                    NativeFunctionInfo = blob.NativeFunctionInfo;
                    ME1NativeInfoLoaded = true;
                }
            }
            catch
            {
                return;
            }
        }
    }
}
