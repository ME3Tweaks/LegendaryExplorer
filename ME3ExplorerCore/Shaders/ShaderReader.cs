﻿using ME3ExplorerCore.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ME3ExplorerCore.Shaders
{
    public class ShaderReader
    {
        public static ShaderInfo DisassembleShader(byte[] shaderByteCode, out string disassembly)
        {
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                var info = DisassembleShader(shaderByteCode, writer);
                disassembly = sb.ToString();
                return info;
            }
        }
        public static ShaderInfo DisassembleShader(byte[] shaderByteCode, TextWriter writer = null) => DisassembleShader(new MemoryStream(shaderByteCode), writer);

        public static ShaderInfo DisassembleShader(Stream inStream, TextWriter writer = null)
        {
            uint vers = inStream.ReadUInt32();
            var minor = vers.bits(7, 0);
            var major = vers.bits(15, 8);
            var shaderType = (Frequency)vers.bits(31, 16);
            if (shaderType != Frequency.Pixel && shaderType != Frequency.Vertex)
            {
                Console.WriteLine("Not a valid Shader!");
                return null;
            }

            int instructionCount = 0;
            int texInstructionCount = 0;
            int indent = 2;
            var shaderInfo = new ShaderInfo
            {
                Frequency = shaderType
            };
            while (inStream.Position + 4 <= inStream.Length)
            {
                uint token = inStream.ReadUInt32();
                var opcode = (OpcodeType)token.bits(15, 0);
                if (opcode == OpcodeType.D3DSIO_END)
                {
                    writer?.WriteLine();
                    writer?.WriteLine(shaderType == Frequency.Pixel
                                          ? $"// approximately {instructionCount + texInstructionCount} instruction slots used ({texInstructionCount} texture, {instructionCount} arithmetic)"
                                          : $"// approximately {instructionCount} instruction slots used");
                    return shaderInfo;
                }
                else if (opcode == OpcodeType.D3DSIO_COMMENT)
                {
                    //writer?.WriteLine("Begin COMMENT");
                    uint length = token.bits(30, 16);
                    long commentEnd = length * 4 + inStream.Position;
                    if (inStream.ReadUInt32() == CTAB)
                    {
                        long ctabPos = inStream.Position;
                        var header = new D3DConstantTable(inStream);
                        inStream.Seek(ctabPos + header.ConstantInfo, SeekOrigin.Begin);
                        var constantInfos = new ConstRegisterInfo[header.Constants];
                        for (int i = 0; i < header.Constants; i++)
                        {
                            constantInfos[i] = new ConstRegisterInfo(inStream);
                        }

                        inStream.Seek(ctabPos + header.Creator, SeekOrigin.Begin);
                        string creator = inStream.ReadStringASCIINull();
                        writer?.WriteLine("//");
                        writer?.WriteLine($"// Generated by {creator}");
                        writer?.WriteLine("//");
                        shaderInfo.Constants = new ConstantInfo[header.Constants];
                        //writer?.WriteLine("BEGIN Constant Table");
                        for (int i = 0; i < constantInfos.Length; i++)
                        {
                            ConstRegisterInfo info = constantInfos[i];
                            inStream.Seek(ctabPos + info.TypeInfo, SeekOrigin.Begin);
                            var type = new TypeInfo(inStream);
                            inStream.Seek(ctabPos + info.Name, SeekOrigin.Begin);
                            string name = inStream.ReadStringASCIINull();
                            shaderInfo.Constants[i] = new ConstantInfo(name,
                                                                       (D3DXREGISTER_SET)info.RegisterSet,
                                                                       info.RegisterIndex,
                                                                       info.RegisterCount,
                                                                       info.DefaultValue,
                                                                       (D3DXPARAMETER_CLASS)type.Class,
                                                                       (D3DXPARAMETER_TYPE)type.Type,
                                                                       type.Rows,
                                                                       type.Columns,
                                                                       type.Elements);
#if DEBUGGING
                                writer?.WriteLine();
                                writer?.WriteLine(name);
                                writer?.WriteLine($"RegisterSet: {(D3DXREGISTER_SET)info.RegisterSet}, RegisterIndex: {info.RegisterIndex}, RegisterCount: {info.RegisterCount}," +
                                                 $" DefaultValue: {info.DefaultValue}");
                                writer?.WriteLine($"Class: {(D3DXPARAMETER_CLASS)type.Class}, Type: {(D3DXPARAMETER_TYPE)type.Type}, Rows: {type.Rows}, Columns: {type.Columns}," +
                                                 $" Elements: {type.Elements}, StructMembers {type.StructMembers}, StructMemberInfo: {type.StructMemberInfo}");
#endif
                        }

                        writer?.WriteLine("// Parameters:");
                        writer?.WriteLine("//");
                        foreach (ConstantInfo info in shaderInfo.Constants)
                        {
                            string line = $"//   {d3d9types.paramTypes[info.ParameterType]}";
                            switch (info.ParameterClass)
                            {
                                case D3DXPARAMETER_CLASS.SCALAR:
                                case D3DXPARAMETER_CLASS.OBJECT:
                                    break;
                                case D3DXPARAMETER_CLASS.VECTOR:
                                    line += info.Columns;
                                    break;
                                case D3DXPARAMETER_CLASS.MATRIX_COLUMNS:
                                    line += $"{info.Rows}x{info.Columns}";
                                    break;
                                case D3DXPARAMETER_CLASS.MATRIX_ROWS:
                                    line += "ROWS?????????";
                                    break;
                                case D3DXPARAMETER_CLASS.STRUCT:
                                    line += "STRUCT?????????";
                                    break;
                            }

                            line += " ";
                            line += info.Name;
                            if (info.Elements > 1)
                            {
                                line += $"[{info.Elements}]";
                            }

                            line += ";";
                            writer?.WriteLine(line);
                        }

                        writer?.WriteLine("//");
                        writer?.WriteLine("//");

                        int maxNameLength = Math.Max(12, shaderInfo.Constants.Max(c => c.Name.Length));
                        int regTextLength = Math.Max(5, shaderInfo.Constants.Max(c => c.RegisterIndex.NumDigits()) + 1);
                        int sizeTextLength = Math.Max(4, shaderInfo.Constants.Max(c => c.RegisterCount.NumDigits()));
                        writer?.WriteLine("// Registers:");
                        writer?.WriteLine("//");
                        writer?.WriteLine($"//   {"Name".PadRight(maxNameLength)} {"Reg".PadRight(regTextLength)} {"Size".PadRight(sizeTextLength)}");
                        writer?.WriteLine($"//   {new string('-', maxNameLength)} {new string('-', regTextLength)} {new string('-', sizeTextLength)}");
                        foreach (ConstantInfo info in shaderInfo.Constants.OrderBy(c => c.RegisterSet).ThenBy(c => c.RegisterIndex))
                        {
                            writer?.WriteLine($"//   {info.Name.PadRight(maxNameLength)} {d3d9types.registerSets[info.RegisterSet]}{info.RegisterIndex.ToString().PadRight(regTextLength - 1)}" +
                                              $" {info.RegisterCount.ToString().PadLeft(sizeTextLength)}");
                        }

                        writer?.WriteLine("//");

                        //writer?.WriteLine("END Constant Table");
                    }

                    inStream.Seek(commentEnd, SeekOrigin.Begin);
                    //writer?.WriteLine("End COMMENT");
                    writer?.WriteLine();
                    writer?.WriteLine($"    {(shaderType == Frequency.Pixel ? "ps" : "vs")}_{major}_{minor}");
                }
                else if (opcode == OpcodeType.D3DSIO_DCL)
                {
                    uint declToken = inStream.ReadUInt32();
                    var declarationType = (D3DDECLUSAGE)declToken.bits(4, 0);
                    var samplerTexType = (D3DSAMPLER_TEXTURE_TYPE)declToken.bits(30, 27);
                    string suffix = "";
                    if (samplerTexType != D3DSAMPLER_TEXTURE_TYPE.D3DSTT_UNKNOWN)
                    {
                        suffix = d3d9types.samplerTexTypes[samplerTexType];
                    }
                    else
                    {
                        suffix = d3d9types.declarationTypes[declarationType];
                        uint usageIndex = declToken.bits(19, 16);
                        if (usageIndex != 0)
                        {
                            suffix += usageIndex;
                        }

                        uint destToken = inStream.ReadUInt32();
                        inStream.Seek(-4, SeekOrigin.Current);
                        var declaration = new ParameterDeclaration(declarationType,
                                                          (int)usageIndex,
                                                          (D3DSHADER_PARAM_REGISTER_TYPE)(destToken.bits(30, 28) | destToken.bits(12, 11) << 3),
                                                          (int)destToken.bits(10, 0),
                                                          new WriteMask(destToken.bit(16), destToken.bit(17), destToken.bit(18), destToken.bit(19)),
                                                          ((ResultModifiers)token.bits(23, 20)).HasFlag(ResultModifiers.Partial_Precision));
                        if (declaration.RegisterType == D3DSHADER_PARAM_REGISTER_TYPE.INPUT)
                        {
                            shaderInfo.InputDeclarations.Add(declaration);
                        }
                        else
                        {
                            shaderInfo.OutputDeclarations.Add(declaration);
                        }
                    }


                    string destinationParameterTokenString = ReadDestinationParameterToken(inStream, shaderType);
                    writer?.WriteLine($"    dcl{suffix}{destinationParameterTokenString}");
                }
                else
                {
                    instructionCount++;
                    uint instructionSize = token.bits(27, 24);
                    bool isPredicated = token.bit(28); //todo: implement
                    //writer?.WriteLine($"{d3d9types.opcodeNames[opcode]} (instruction size: {instructionSize})");
                    var parameters = new List<string>();
                    string line;
                    void setLineStart() => line = $"{new string(' ', indent * 2)}{d3d9types.opcodeNames[opcode]}";

                    string appendComparison()
                    {
                        //todo: not sure about this, test
                        switch (token.bits(23, 16))
                        {
                            case 0:
                                line += "_gt";
                                break;
                            case 1:
                                line += "_lt";
                                break;
                            case 2:
                                line += "_ge";
                                break;
                            case 3:
                                line += "_le";
                                break;
                            case 4:
                                line += "_eq";
                                break;
                            case 5:
                                line += "_ne";
                                break;
                        }

                        return line;
                    }

                    setLineStart();

                    switch (opcode)
                    {
                        case OpcodeType.D3DSIO_NOP:
                        case OpcodeType.D3DSIO_RET:
                        case OpcodeType.D3DSIO_BREAK:
                            break;
                        case OpcodeType.D3DSIO_MOV:
                        case OpcodeType.D3DSIO_RCP:
                        case OpcodeType.D3DSIO_RSQ:
                        case OpcodeType.D3DSIO_EXP:
                        case OpcodeType.D3DSIO_LOG:
                        case OpcodeType.D3DSIO_LIT:
                        case OpcodeType.D3DSIO_FRC:
                        case OpcodeType.D3DSIO_ABS:
                        case OpcodeType.D3DSIO_NRM:
                        case OpcodeType.D3DSIO_MOVA:
                        case OpcodeType.D3DSIO_EXPP:
                        case OpcodeType.D3DSIO_DSX:
                        case OpcodeType.D3DSIO_DSY:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_ADD:
                        case OpcodeType.D3DSIO_SUB:
                        case OpcodeType.D3DSIO_MUL:
                        case OpcodeType.D3DSIO_DP3:
                        case OpcodeType.D3DSIO_DP4:
                        case OpcodeType.D3DSIO_MIN:
                        case OpcodeType.D3DSIO_MAX:
                        case OpcodeType.D3DSIO_SLT:
                        case OpcodeType.D3DSIO_SGE:
                        case OpcodeType.D3DSIO_DST:
                        case OpcodeType.D3DSIO_M4x4:
                        case OpcodeType.D3DSIO_M4x3:
                        case OpcodeType.D3DSIO_M3x4:
                        case OpcodeType.D3DSIO_M3x3:
                        case OpcodeType.D3DSIO_M3x2:
                        case OpcodeType.D3DSIO_POW:
                        case OpcodeType.D3DSIO_CRS:
                        case OpcodeType.D3DSIO_LOGP:
                        case OpcodeType.D3DSIO_BEM:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_MAD:
                        case OpcodeType.D3DSIO_LRP:
                        case OpcodeType.D3DSIO_SGN:
                        case OpcodeType.D3DSIO_CND:
                        case OpcodeType.D3DSIO_CMP:
                        case OpcodeType.D3DSIO_DP2ADD:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_CALL:
                            parameters.Add(ReadLabelToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_CALLNZ:
                            parameters.Add(ReadLabelToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_LOOP:
                            parameters.Add("aL");
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            indent++;
                            break;
                        case OpcodeType.D3DSIO_REP:
                        case OpcodeType.D3DSIO_IF:
                            line += " ";
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            indent++;
                            break;
                        case OpcodeType.D3DSIO_IFC:
                            line = appendComparison();
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            indent++;
                            break;
                        case OpcodeType.D3DSIO_ELSE:
                            indent--;
                            setLineStart();
                            indent++;
                            break;
                        case OpcodeType.D3DSIO_ENDREP:
                        case OpcodeType.D3DSIO_ENDLOOP:
                        case OpcodeType.D3DSIO_ENDIF:
                            indent--;
                            //these shouldn't be indented, so rewrite the opcode
                            setLineStart();
                            break;
                        case OpcodeType.D3DSIO_LABEL:
                        case OpcodeType.D3DSIO_BREAKP:
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_SINCOS:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            //unclear to me whether this has 2 or 4 parameters
                            if (instructionSize == 4)
                            {
                                parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                                parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            }

                            break;
                        case OpcodeType.D3DSIO_BREAKC:
                            line = appendComparison();
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_SETP:
                            line = appendComparison();
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_DEF:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add($"{inStream.ReadFloat()}");
                            parameters.Add($"{inStream.ReadFloat()}");
                            parameters.Add($"{inStream.ReadFloat()}");
                            parameters.Add($"{inStream.ReadFloat()}");
                            instructionCount--;
                            break;
                        case OpcodeType.D3DSIO_DEFB:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add($"{inStream.ReadUInt32() > 0}");
                            instructionCount--;
                            break;
                        case OpcodeType.D3DSIO_DEFI:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add($"{inStream.ReadInt32()}");
                            parameters.Add($"{inStream.ReadInt32()}");
                            parameters.Add($"{inStream.ReadInt32()}");
                            parameters.Add($"{inStream.ReadInt32()}");
                            instructionCount--;
                            break;
                        case OpcodeType.D3DSIO_TEXKILL:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            break;
                        case OpcodeType.D3DSIO_TEXLDD:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            texInstructionCount++;
                            break;
                        case OpcodeType.D3DSIO_TEX:
                        case OpcodeType.D3DSIO_TEXM3x3SPEC:
                        case OpcodeType.D3DSIO_TEXLDL:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            texInstructionCount++;
                            break;
                        case OpcodeType.D3DSIO_TEXBEM:
                        case OpcodeType.D3DSIO_TEXBEML:
                        case OpcodeType.D3DSIO_TEXREG2AR:
                        case OpcodeType.D3DSIO_TEXREG2GB:
                        case OpcodeType.D3DSIO_TEXM3x2PAD:
                        case OpcodeType.D3DSIO_TEXM3x2TEX:
                        case OpcodeType.D3DSIO_TEXM3x3PAD:
                        case OpcodeType.D3DSIO_TEXM3x3TEX:
                        case OpcodeType.D3DSIO_TEXM3x3VSPEC:
                        case OpcodeType.D3DSIO_TEXREG2RGB:
                        case OpcodeType.D3DSIO_TEXDP3TEX:
                        case OpcodeType.D3DSIO_TEXM3x2DEPTH:
                        case OpcodeType.D3DSIO_TEXDP3:
                        case OpcodeType.D3DSIO_TEXM3x3:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            parameters.Add(ReadSourceParameterToken(inStream, shaderType));
                            texInstructionCount++;
                            break;
                        case OpcodeType.D3DSIO_TEXDEPTH:
                            parameters.Add(ReadDestinationParameterToken(inStream, shaderType));
                            texInstructionCount++;
                            break;
                        default:
                            for (; instructionSize > 0; instructionSize--)
                            {
                                inStream.ReadUInt32();
                            }

                            break;
                    }

                    writer?.WriteLine($"{line}{string.Join(", ", parameters)}");
                }
            }

            Console.WriteLine("No End Token found!");
            return null;
        }

        public static string GetShaderDisassembly(byte[] shaderByteCode) => GetShaderDisassembly(new MemoryStream(shaderByteCode));

        public static string GetShaderDisassembly(Stream shaderBytecodeStream)
        {
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                DisassembleShader(shaderBytecodeStream, writer);
            }

            return sb.ToString();
        }

        public static ShaderInfo GetShaderInfo(Stream shaderBytecodeStream) => DisassembleShader(shaderBytecodeStream);

        public static ShaderInfo GetShaderInfo(byte[] shaderByteCode) => GetShaderInfo(new MemoryStream(shaderByteCode));


        private const uint CTAB = 0x42415443;
        private static readonly string[] swizzleLookup = { "x", "y", "z", "w" };
        //https://docs.microsoft.com/en-us/windows-hardware/drivers/display/source-parameter-token
        private static string ReadSourceParameterToken(Stream fs, Frequency shaderType)
        {
            uint token = fs.ReadUInt32();
            var register = getRegister(token, shaderType);
            bool usesRelativeAddressing = token.bit(13);
            if (usesRelativeAddressing)
            {
                uint token2 = fs.ReadUInt32();
                var register2 = getRegister(token2, shaderType);
                return $"{register}[{register2}{GetSwizzle(token2)}]";
            }

            string swizzle = GetSwizzle(token);

            return $"{register}{swizzle}";

            string GetSwizzle(uint u)
            {
                var xSwizzle = swizzleLookup[u.bits(17, 16)];
                var ySwizzle = swizzleLookup[u.bits(19, 18)];
                var zSwizzle = swizzleLookup[u.bits(21, 20)];
                var wSwizzle = swizzleLookup[u.bits(23, 22)];
                if (xSwizzle == "x" && ySwizzle == "y" && zSwizzle == "z" && wSwizzle == "w")
                {
                    xSwizzle = ySwizzle = zSwizzle = wSwizzle = "";
                }
                else if (wSwizzle == zSwizzle)
                {
                    wSwizzle = "";
                    if (zSwizzle == ySwizzle)
                    {
                        zSwizzle = "";
                        if (ySwizzle == xSwizzle)
                        {
                            ySwizzle = "";
                        }
                    }
                }
                string swiz = $"{xSwizzle}{ySwizzle}{zSwizzle}{wSwizzle}";
                return swiz.Length > 0 ? $".{swiz}" : swiz;
            }
        }

        //https://docs.microsoft.com/en-us/windows-hardware/drivers/display/destination-parameter-token
        private static string ReadDestinationParameterToken(Stream fs, Frequency shaderType)
        {
            uint token = fs.ReadUInt32();
            var register = getRegister(token, shaderType);
            bool usesRelativeAddressing = token.bit(13);
            string x = token.bit(16) ? "x" : "";
            string y = token.bit(17) ? "y" : "";
            string z = token.bit(18) ? "z" : "";
            string w = token.bit(19) ? "w" : "";
            string writeMask = x == "x" && y == "y" && z == "z" && w == "w" ? "" : $".{x}{y}{z}{w}";

            var resultModifier = (ResultModifiers)token.bits(23, 20);
            string resultModifiers = "";
            if (resultModifier.HasFlag(ResultModifiers.Centroid))
            {
                resultModifiers = $"_centroid{resultModifiers}";
            }
            if (resultModifier.HasFlag(ResultModifiers.Partial_Precision))
            {
                resultModifiers = $"_pp{resultModifiers}";
            }
            if (resultModifier.HasFlag(ResultModifiers.Saturate))
            {
                resultModifiers = $"_sat{resultModifiers}";
            }

            if (usesRelativeAddressing)
            {
                uint token2 = fs.ReadUInt32();
                var register2 = getRegister(token2, shaderType);
                return $"{resultModifiers}{register}[{register2}{writeMask}]";
            }

            return $"{resultModifiers} {register}{writeMask}";
        }

        //https://docs.microsoft.com/en-us/windows-hardware/drivers/display/label-token
        private static string ReadLabelToken(Stream fs, Frequency shaderType)
        {
            return " " + getRegister(fs.ReadUInt32(), shaderType);
        }

        private static string getRegister(uint token, Frequency shaderType)
        {
            uint registerNumber = token.bits(10, 0);
            var registerType = (D3DSHADER_PARAM_REGISTER_TYPE)(token.bits(30, 28) | token.bits(12, 11) << 3);
            string registerMnemonic = d3d9types.registerNames[registerType];
            switch ((uint)registerType)
            {
                case 3u when shaderType == Frequency.Vertex:
                    registerMnemonic = "a";
                    break;
                case 3u when shaderType == Frequency.Pixel:
                    registerMnemonic = "t";
                    break;
            }

            string register = $"{registerMnemonic}{registerNumber}";
            uint sourceModifier = token.bits(27, 24);
            //https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx9-graphics-reference-asm-ps-registers-modifiers
            switch (sourceModifier)
            {
                case 1: //negate
                    return $"-{register}";
                case 2: //bias
                    return $"{register}_bias";
                case 3: //bias and negate
                    return $"-{register}_bias";
                case 4: //Signed Scaling
                    return $"{register}_bx2";
                case 5: //Signed Scaling and negate
                    return $"-{register}_bx2";
                case 6: //Complement?
                    return $"{register}_COMPLEMENT";
                case 0xb: //abs
                    return $"{register}_abs";
                case 0xc: //abs and negate
                    return $"-{register}_abs";
                case 0xd: //not
                    return $"!{register}";
                default:
                    return register;
            }
        }
    }
}