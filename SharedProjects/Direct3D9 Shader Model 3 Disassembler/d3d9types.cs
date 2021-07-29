using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct3D9_Shader_Model_3_Disassembler
{
    //D3DSHADER_INSTRUCTION_OPCODE_TYPE
    //https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/d3d9types/ne-d3d9types-_d3dshader_instruction_opcode_type
    internal enum OpcodeType : uint
    {
        D3DSIO_NOP = 0,
        D3DSIO_MOV = 1,
        D3DSIO_ADD = 2,
        D3DSIO_SUB = 3,
        D3DSIO_MAD = 4,
        D3DSIO_MUL = 5,
        D3DSIO_RCP = 6,
        D3DSIO_RSQ = 7,
        D3DSIO_DP3 = 8,
        D3DSIO_DP4 = 9,
        D3DSIO_MIN = 10,
        D3DSIO_MAX = 11,
        D3DSIO_SLT = 12,
        D3DSIO_SGE = 13,
        D3DSIO_EXP = 14,
        D3DSIO_LOG = 15,
        D3DSIO_LIT = 16,
        D3DSIO_DST = 17,
        D3DSIO_LRP = 18,
        D3DSIO_FRC = 19,
        D3DSIO_M4x4 = 20,
        D3DSIO_M4x3 = 21,
        D3DSIO_M3x4 = 22,
        D3DSIO_M3x3 = 23,
        D3DSIO_M3x2 = 24,
        D3DSIO_CALL = 25,
        D3DSIO_CALLNZ = 26,
        D3DSIO_LOOP = 27,
        D3DSIO_RET = 28,
        D3DSIO_ENDLOOP = 29,
        D3DSIO_LABEL = 30,
        D3DSIO_DCL = 31,
        D3DSIO_POW = 32,
        D3DSIO_CRS = 33,
        D3DSIO_SGN = 34,
        D3DSIO_ABS = 35,
        D3DSIO_NRM = 36,
        D3DSIO_SINCOS = 37,
        D3DSIO_REP = 38,
        D3DSIO_ENDREP = 39,
        D3DSIO_IF = 40,
        D3DSIO_IFC = 41,
        D3DSIO_ELSE = 42,
        D3DSIO_ENDIF = 43,
        D3DSIO_BREAK = 44,
        D3DSIO_BREAKC = 45,
        D3DSIO_MOVA = 46,
        D3DSIO_DEFB = 47,
        D3DSIO_DEFI = 48,
        D3DSIO_TEXCOORD = 64,
        D3DSIO_TEXKILL = 65,
        D3DSIO_TEX = 66,
        D3DSIO_TEXBEM = 67,
        D3DSIO_TEXBEML = 68,
        D3DSIO_TEXREG2AR = 69,
        D3DSIO_TEXREG2GB = 70,
        D3DSIO_TEXM3x2PAD = 71,
        D3DSIO_TEXM3x2TEX = 72,
        D3DSIO_TEXM3x3PAD = 73,
        D3DSIO_TEXM3x3TEX = 74,
        D3DSIO_RESERVED0 = 75,
        D3DSIO_TEXM3x3SPEC = 76,
        D3DSIO_TEXM3x3VSPEC = 77,
        D3DSIO_EXPP = 78,
        D3DSIO_LOGP = 79,
        D3DSIO_CND = 80,
        D3DSIO_DEF = 81,
        D3DSIO_TEXREG2RGB = 82,
        D3DSIO_TEXDP3TEX = 83,
        D3DSIO_TEXM3x2DEPTH = 84,
        D3DSIO_TEXDP3 = 85,
        D3DSIO_TEXM3x3 = 86,
        D3DSIO_TEXDEPTH = 87,
        D3DSIO_CMP = 88,
        D3DSIO_BEM = 89,
        D3DSIO_DP2ADD = 90,
        D3DSIO_DSX = 91,
        D3DSIO_DSY = 92,
        D3DSIO_TEXLDD = 93,
        D3DSIO_SETP = 94,
        D3DSIO_TEXLDL = 95,
        D3DSIO_BREAKP = 96,
        D3DSIO_PHASE = 0xFFFD,
        D3DSIO_COMMENT = 0xFFFE,
        D3DSIO_END = 0xFFFF
    }

    //_D3DSHADER_PARAM_REGISTER_TYPE
    //https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/d3d9types/ne-d3d9types-_d3dshader_param_register_type
    public enum D3DSHADER_PARAM_REGISTER_TYPE : uint
    {
        TEMP = 0,
        INPUT = 1,
        CONST = 2,
        ADDR = 3, //vertex shader
        TEXTURE = 3, //pixel shader
        RASTOUT = 4, 
        ATTROUT = 5,
        TEXCRDOUT = 6,
        OUTPUT = 6,
        CONSTINT = 7,
        COLOROUT = 8,
        DEPTHOUT = 9,
        SAMPLER = 10,
        CONST2 = 11,
        CONST3 = 12,
        CONST4 = 13,
        CONSTBOOL = 14,
        LOOP = 15,
        TEMPFLOAT16 = 16,
        MISCTYPE = 17,
        LABEL = 18,
        PREDICATE = 19
    }
    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dxregister-set
    public enum D3DXREGISTER_SET
    {
        BOOL,
        INT4,
        FLOAT4,
        SAMPLER
    }

    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dxparameter-class
    public enum D3DXPARAMETER_CLASS
    {
        SCALAR,
        VECTOR,
        MATRIX_ROWS,
        MATRIX_COLUMNS,
        OBJECT,
        STRUCT,
    }
    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dxparameter-type
    public enum D3DXPARAMETER_TYPE
    {
        VOID,
        BOOL,
        INT,
        FLOAT,
        STRING,
        TEXTURE,
        TEXTURE1D,
        TEXTURE2D,
        TEXTURE3D,
        TEXTURECUBE,
        SAMPLER,
        SAMPLER1D,
        SAMPLER2D,
        SAMPLER3D,
        SAMPLERCUBE,
        PIXELSHADER,
        VERTEXSHADER,
        PIXELFRAGMENT,
        VERTEXFRAGMENT,
        UNSUPPORTED,
    }
    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3ddeclusage
    public enum D3DDECLUSAGE
    {
        POSITION = 0,
        BLENDWEIGHT = 1,
        BLENDINDICES = 2,
        NORMAL = 3,
        PSIZE = 4,
        TEXCOORD = 5,
        TANGENT = 6,
        BINORMAL = 7,
        TESSFACTOR = 8,
        POSITIONT = 9,
        COLOR = 10,
        FOG = 11,
        DEPTH = 12,
        SAMPLE = 13,
    }

    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dsampler-texture-type
    public enum D3DSAMPLER_TEXTURE_TYPE
    {
        D3DSTT_UNKNOWN = 0, // uninitialized value
        D3DSTT_2D = 2, // dcl_2d s# (for declaring a 2-D texture)
        D3DSTT_CUBE = 3, // dcl_cube s# (for declaring a cube texture)
        D3DSTT_VOLUME = 4, // dcl_volume s# (for declaring a volume texture)
    }

    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dxshader-constanttable
    internal struct D3DConstantTable
    {
        public uint Size;
        public uint Creator;
        public uint Version;
        public uint Constants;
        public uint ConstantInfo;
        public uint Flags;
        public uint Target;

        public D3DConstantTable(Stream stream)
        {
            Size = stream.ReadUInt32();
            Creator = stream.ReadUInt32();
            Version = stream.ReadUInt32();
            Constants = stream.ReadUInt32();
            ConstantInfo = stream.ReadUInt32();
            Flags = stream.ReadUInt32();
            Target = stream.ReadUInt32();
        }
    }

    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dxshader-constantinfo
    internal struct ConstRegisterInfo
    {
        public uint Name;
        public ushort RegisterSet;
        public ushort RegisterIndex;
        public ushort RegisterCount;
        public ushort Reserved;
        public uint TypeInfo;
        public uint DefaultValue;

        public ConstRegisterInfo(Stream stream)
        {
            Name = stream.ReadUInt32();
            RegisterSet = stream.ReadUInt16();
            RegisterIndex = stream.ReadUInt16();
            RegisterCount = stream.ReadUInt16();
            Reserved = stream.ReadUInt16();
            TypeInfo = stream.ReadUInt32();
            DefaultValue = stream.ReadUInt32();
        }
    }

    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dxshader-typeinfo
    internal struct TypeInfo
    {
        public ushort Class;
        public ushort Type;
        public ushort Rows;
        public ushort Columns;
        public ushort Elements;
        public ushort StructMembers;
        public uint StructMemberInfo;

        public TypeInfo(Stream stream)
        {
            Class = stream.ReadUInt16();
            Type = stream.ReadUInt16();
            Rows = stream.ReadUInt16();
            Columns = stream.ReadUInt16();
            Elements = stream.ReadUInt16();
            StructMembers = stream.ReadUInt16();
            StructMemberInfo = stream.ReadUInt32();
        }
    }

    //https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dxshader-structmemberinfo
    internal struct StructMemberInfo
    {
        public uint Name;
        public uint TypeInfo;

        public StructMemberInfo(uint name, uint typeInfo)
        {
            Name = name;
            TypeInfo = typeInfo;
        }
    }

    //https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx9-graphics-reference-asm-ps-instructions-modifiers-ps-2-0
    [Flags]
    public enum ResultModifiers
    {
        Saturate = 1,
        Partial_Precision = 2,
        Centroid = 4
    }

    internal static class d3d9types
    {
        public static Dictionary<OpcodeType, string> opcodeNames = new Dictionary<OpcodeType, string>
        {
            {OpcodeType.D3DSIO_NOP, "nop"},
            {OpcodeType.D3DSIO_MOV, "mov"},
            {OpcodeType.D3DSIO_ADD, "add"},
            {OpcodeType.D3DSIO_SUB, "sub"},
            {OpcodeType.D3DSIO_MAD, "mad"},
            {OpcodeType.D3DSIO_MUL, "mul"},
            {OpcodeType.D3DSIO_RCP, "rcp"},
            {OpcodeType.D3DSIO_RSQ, "rsq"},
            {OpcodeType.D3DSIO_DP3, "dp3"},
            {OpcodeType.D3DSIO_DP4, "dp4"},
            {OpcodeType.D3DSIO_MIN, "min"},
            {OpcodeType.D3DSIO_MAX, "max"},
            {OpcodeType.D3DSIO_SLT, "slt"},
            {OpcodeType.D3DSIO_SGE, "sge"},
            {OpcodeType.D3DSIO_EXP, "exp"},
            {OpcodeType.D3DSIO_LOG, "log"},
            {OpcodeType.D3DSIO_LIT, "lit"},
            {OpcodeType.D3DSIO_DST, "dst"},
            {OpcodeType.D3DSIO_LRP, "lrp"},
            {OpcodeType.D3DSIO_FRC, "frc"},
            {OpcodeType.D3DSIO_M4x4, "m4x4"},
            {OpcodeType.D3DSIO_M4x3, "m4x3"},
            {OpcodeType.D3DSIO_M3x4, "m3x4"},
            {OpcodeType.D3DSIO_M3x3, "m3x3"},
            {OpcodeType.D3DSIO_M3x2, "m3x2"},
            {OpcodeType.D3DSIO_CALL, "call"},
            {OpcodeType.D3DSIO_CALLNZ, "callnz"},
            {OpcodeType.D3DSIO_LOOP, "loop"},
            {OpcodeType.D3DSIO_RET, "ret"},
            {OpcodeType.D3DSIO_ENDLOOP, "endloop"},
            {OpcodeType.D3DSIO_LABEL, "label"},
            {OpcodeType.D3DSIO_DCL, "dcl"},
            {OpcodeType.D3DSIO_POW, "pow"},
            {OpcodeType.D3DSIO_CRS, "crs"},
            {OpcodeType.D3DSIO_SGN, "sgn"},
            {OpcodeType.D3DSIO_ABS, "abs"},
            {OpcodeType.D3DSIO_NRM, "nrm"},
            {OpcodeType.D3DSIO_SINCOS, "sincos"},
            {OpcodeType.D3DSIO_REP, "rep"},
            {OpcodeType.D3DSIO_ENDREP, "endrep"},
            {OpcodeType.D3DSIO_IF, "if"},
            {OpcodeType.D3DSIO_IFC, "ifc"},
            {OpcodeType.D3DSIO_ELSE, "else"},
            {OpcodeType.D3DSIO_ENDIF, "endif"},
            {OpcodeType.D3DSIO_BREAK, "break"},
            {OpcodeType.D3DSIO_BREAKC, "breakc"},
            {OpcodeType.D3DSIO_MOVA, "mova"},
            {OpcodeType.D3DSIO_DEFB, "defb"},
            {OpcodeType.D3DSIO_DEFI, "defi"},
            {OpcodeType.D3DSIO_TEXCOORD, "texcoord"},
            {OpcodeType.D3DSIO_TEXKILL, "texkill"},
            {OpcodeType.D3DSIO_TEX, "texld"},
            {OpcodeType.D3DSIO_TEXBEM, "texbem"},
            {OpcodeType.D3DSIO_TEXBEML, "texbeml"},
            {OpcodeType.D3DSIO_TEXREG2AR, "texreg2ar"},
            {OpcodeType.D3DSIO_TEXREG2GB, "texreg2gb"},
            {OpcodeType.D3DSIO_TEXM3x2PAD, "texm3x2pad"},
            {OpcodeType.D3DSIO_TEXM3x2TEX, "texm3x2tex"},
            {OpcodeType.D3DSIO_TEXM3x3PAD, "texm3x3pad"},
            {OpcodeType.D3DSIO_TEXM3x3TEX, "texm3x3tex"},
            {OpcodeType.D3DSIO_RESERVED0, ""},
            {OpcodeType.D3DSIO_TEXM3x3SPEC, "texm3x3spec"},
            {OpcodeType.D3DSIO_TEXM3x3VSPEC, "texm3x3vspec"},
            {OpcodeType.D3DSIO_EXPP, "expp"},
            {OpcodeType.D3DSIO_LOGP, "logp"},
            {OpcodeType.D3DSIO_CND, "cnd"},
            {OpcodeType.D3DSIO_DEF, "def"},
            {OpcodeType.D3DSIO_TEXREG2RGB, "texreg2rgb"},
            {OpcodeType.D3DSIO_TEXDP3TEX, "texdp3tex"},
            {OpcodeType.D3DSIO_TEXM3x2DEPTH, "texm3x2depth"},
            {OpcodeType.D3DSIO_TEXDP3, "texdp3"},
            {OpcodeType.D3DSIO_TEXM3x3, "texm3x3"},
            {OpcodeType.D3DSIO_TEXDEPTH, "texdepth"},
            {OpcodeType.D3DSIO_CMP, "cmp"},
            {OpcodeType.D3DSIO_BEM, "bem"},
            {OpcodeType.D3DSIO_DP2ADD, "dp2add"},
            {OpcodeType.D3DSIO_DSX, "dsx"},
            {OpcodeType.D3DSIO_DSY, "dsy"},
            {OpcodeType.D3DSIO_TEXLDD, "texldd"},
            {OpcodeType.D3DSIO_SETP, "setp"},
            {OpcodeType.D3DSIO_TEXLDL, "texldl"},
            {OpcodeType.D3DSIO_BREAKP, "break pred"},
            {OpcodeType.D3DSIO_PHASE, "phase"},
            {OpcodeType.D3DSIO_COMMENT, ""},
            {OpcodeType.D3DSIO_END, ""},
        };

        //https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx9-graphics-reference-asm-vs-registers-vs-3-0
        //https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx9-graphics-reference-asm-ps-registers-ps-3-0
        public static Dictionary<D3DSHADER_PARAM_REGISTER_TYPE, string> registerNames = new Dictionary<D3DSHADER_PARAM_REGISTER_TYPE, string>
        {
            [D3DSHADER_PARAM_REGISTER_TYPE.TEMP] = "r",
            [D3DSHADER_PARAM_REGISTER_TYPE.INPUT] = "v",
            [D3DSHADER_PARAM_REGISTER_TYPE.CONST] = "c",
            [D3DSHADER_PARAM_REGISTER_TYPE.ADDR] = "a", //vertex shader
            [D3DSHADER_PARAM_REGISTER_TYPE.TEXTURE] = "t", //pixel shader
            [D3DSHADER_PARAM_REGISTER_TYPE.RASTOUT] = "o",
            [D3DSHADER_PARAM_REGISTER_TYPE.ATTROUT] = "ATTROUT",
            [D3DSHADER_PARAM_REGISTER_TYPE.TEXCRDOUT] = "o", //vertex shader
            [D3DSHADER_PARAM_REGISTER_TYPE.OUTPUT] = "o", //pixel shader
            [D3DSHADER_PARAM_REGISTER_TYPE.CONSTINT] = "i",
            [D3DSHADER_PARAM_REGISTER_TYPE.COLOROUT] = "oC",
            [D3DSHADER_PARAM_REGISTER_TYPE.DEPTHOUT] = "oDepth",
            [D3DSHADER_PARAM_REGISTER_TYPE.SAMPLER] = "s",
            [D3DSHADER_PARAM_REGISTER_TYPE.CONST2] = "CONST2",
            [D3DSHADER_PARAM_REGISTER_TYPE.CONST3] = "CONST3",
            [D3DSHADER_PARAM_REGISTER_TYPE.CONST4] = "CONST4",
            [D3DSHADER_PARAM_REGISTER_TYPE.CONSTBOOL] = "b",
            [D3DSHADER_PARAM_REGISTER_TYPE.LOOP] = "aL",
            [D3DSHADER_PARAM_REGISTER_TYPE.TEMPFLOAT16] = "TEMPFLOAT16",
            [D3DSHADER_PARAM_REGISTER_TYPE.MISCTYPE] = "MISCTYPE",
            [D3DSHADER_PARAM_REGISTER_TYPE.LABEL] = "l",
            [D3DSHADER_PARAM_REGISTER_TYPE.PREDICATE] = "p",
        };

        public static Dictionary<D3DDECLUSAGE, string> declarationTypes = new Dictionary<D3DDECLUSAGE, string>
        {
            [D3DDECLUSAGE.POSITION] = "_position",
            [D3DDECLUSAGE.BLENDWEIGHT] = "_blendweight",
            [D3DDECLUSAGE.BLENDINDICES] = "_blendindices",
            [D3DDECLUSAGE.NORMAL] = "_normal",
            [D3DDECLUSAGE.PSIZE] = "_psize",
            [D3DDECLUSAGE.TEXCOORD] = "_texcoord",
            [D3DDECLUSAGE.TANGENT] = "_tangent",
            [D3DDECLUSAGE.BINORMAL] = "_binormal",
            [D3DDECLUSAGE.TESSFACTOR] = "_tessfactor",
            [D3DDECLUSAGE.POSITIONT] = "_positiont",
            [D3DDECLUSAGE.COLOR] = "_color",
            [D3DDECLUSAGE.FOG] = "_fog",
            [D3DDECLUSAGE.DEPTH] = "_depth",
            [D3DDECLUSAGE.SAMPLE] = "_sample",
        };

        public static Dictionary<D3DSAMPLER_TEXTURE_TYPE, string> samplerTexTypes = new Dictionary<D3DSAMPLER_TEXTURE_TYPE, string>
        {
            [D3DSAMPLER_TEXTURE_TYPE.D3DSTT_2D] = "_2d",
            [D3DSAMPLER_TEXTURE_TYPE.D3DSTT_CUBE] = "_cube",
            [D3DSAMPLER_TEXTURE_TYPE.D3DSTT_VOLUME] = "_volume",
        };

        public static Dictionary<D3DXPARAMETER_TYPE, string> paramTypes = new Dictionary<D3DXPARAMETER_TYPE, string>
        {
            [D3DXPARAMETER_TYPE.VOID] = "void",
            [D3DXPARAMETER_TYPE.BOOL] = "bool",
            [D3DXPARAMETER_TYPE.INT] = "int",
            [D3DXPARAMETER_TYPE.FLOAT] = "float",
            [D3DXPARAMETER_TYPE.STRING] = "string",
            [D3DXPARAMETER_TYPE.TEXTURE] = "texture",
            [D3DXPARAMETER_TYPE.TEXTURE1D] = "texture1D",
            [D3DXPARAMETER_TYPE.TEXTURE2D] = "texture2D",
            [D3DXPARAMETER_TYPE.TEXTURE3D] = "texture3D",
            [D3DXPARAMETER_TYPE.TEXTURECUBE] = "textureCUBE",
            [D3DXPARAMETER_TYPE.SAMPLER] = "sampler",
            [D3DXPARAMETER_TYPE.SAMPLER1D] = "sampler1D",
            [D3DXPARAMETER_TYPE.SAMPLER2D] = "sampler2D",
            [D3DXPARAMETER_TYPE.SAMPLER3D] = "sampler3D",
            [D3DXPARAMETER_TYPE.SAMPLERCUBE] = "samplerCUBE",
            [D3DXPARAMETER_TYPE.PIXELSHADER] = "pixelshader",
            [D3DXPARAMETER_TYPE.VERTEXSHADER] = "vertexshader",
            [D3DXPARAMETER_TYPE.PIXELFRAGMENT] = "pixelfragment",
            [D3DXPARAMETER_TYPE.VERTEXFRAGMENT] = "vertexfragment",
            [D3DXPARAMETER_TYPE.UNSUPPORTED] = "UNSUPPORTED",
        };

        public static Dictionary<D3DXREGISTER_SET, string> registerSets = new Dictionary<D3DXREGISTER_SET, string>
        {
            [D3DXREGISTER_SET.BOOL] = "b",
            [D3DXREGISTER_SET.INT4] = "i",
            [D3DXREGISTER_SET.FLOAT4] = "c",
            [D3DXREGISTER_SET.SAMPLER] = "s"
        };
    }
}
