using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct3D9_Shader_Model_3_Disassembler
{
    public class ShaderInfo
    {
        public Frequency Frequency;
        public ConstantInfo[] Constants;
        public List<ParameterDeclaration> InputDeclarations = new List<ParameterDeclaration>();
        public List<ParameterDeclaration> OutputDeclarations = new List<ParameterDeclaration>();
    }
    public enum Frequency : uint
    {
        Vertex = 0xFFFE,
        Pixel = 0xFFFF,
    }

    public class ParameterDeclaration
    {
        public readonly D3DDECLUSAGE Usage;
        public readonly int UsageIndex;
        public readonly D3DSHADER_PARAM_REGISTER_TYPE RegisterType;
        public readonly int RegisterIndex;
        public readonly WriteMask WriteMask;
        public readonly bool PartialPrecision;
        public int Size => (WriteMask.X ? 1 : 0) + (WriteMask.Y ? 1 : 0) + (WriteMask.Z ? 1 : 0) + (WriteMask.W ? 1 : 0);

        public ParameterDeclaration(D3DDECLUSAGE usage, int usageIndex, D3DSHADER_PARAM_REGISTER_TYPE registerType, int registerIndex, WriteMask writeMask, bool partialPrecision)
        {
            Usage = usage;
            UsageIndex = usageIndex;
            RegisterType = registerType;
            RegisterIndex = registerIndex;
            WriteMask = writeMask;
            PartialPrecision = partialPrecision;
        }
    }

    public readonly struct WriteMask
    {
        public readonly bool X;
        public readonly bool Y;
        public readonly bool Z;
        public readonly bool W;

        public WriteMask(bool x, bool y, bool z, bool w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    public class ConstantInfo
    {
        public readonly string Name;
        public readonly D3DXREGISTER_SET RegisterSet;
        public readonly int RegisterIndex;
        public readonly int RegisterCount;
        public readonly uint DefaultValue;
        public readonly D3DXPARAMETER_CLASS ParameterClass;
        public readonly D3DXPARAMETER_TYPE ParameterType;
        public readonly int Rows;
        public readonly int Columns;
        public readonly int Elements;
        public readonly StructMember[] StructMembers;

        public ConstantInfo(string name, D3DXREGISTER_SET registerSet, int registerIndex, int registerCount, uint defaultValue, D3DXPARAMETER_CLASS parameterClass,
                            D3DXPARAMETER_TYPE parameterType, int rows, int columns, int elements, StructMember[] structMembers)
        {
            Name = name;
            RegisterSet = registerSet;
            RegisterIndex = registerIndex;
            RegisterCount = registerCount;
            DefaultValue = defaultValue;
            ParameterClass = parameterClass;
            ParameterType = parameterType;
            Rows = rows;
            Columns = columns;
            Elements = elements;
            StructMembers = structMembers;
        }
    }

    public class StructMember
    {
        public string Name;
        public ConstantInfo TypeInfo;
    }
}
