using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Parsing;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    [DebuggerDisplay("Function | {Name}")]
    public class Function : ASTNode, IContainsByteCode, IHasFileReference
    {
        public string Name { get; }
        public CodeBody Body { get; set; }
        public TokenStream Tokens { get; init; }
        public List<VariableDeclaration> Locals { get; set; }
        public VariableDeclaration ReturnValueDeclaration;
        public VariableType ReturnType => ReturnValueDeclaration?.VarType;
        public bool CoerceReturn => ReturnValueDeclaration?.Flags.Has(EPropertyFlags.CoerceParm) ?? false;
        public EFunctionFlags Flags;
        public List<FunctionParameter> Parameters;

        public int NativeIndex;

        public DelegateType VarType;

        public bool IsNative => Flags.Has(EFunctionFlags.Native);

        public bool IsDefined => Flags.Has(EFunctionFlags.Defined);

        public bool IsOperator => Flags.Has(EFunctionFlags.Operator);

        public bool IsVirtual => !Flags.Has(EFunctionFlags.Final); //&& !Flags.Has(EFunctionFlags.Static);

        public bool IsStatic => Flags.Has(EFunctionFlags.Static);

        public bool HasOptionalParms => Flags.Has(EFunctionFlags.HasOptionalParms) || Parameters.Any(parm => parm.IsOptional);

        //final event functions, despite not being called virtually, go in the VTable.
        //I assume this is because event functions get an auto-generated c++ func that calls the unrealscript func.
        //The c++ func could have a more performant implementation if the unrealscript func was in the vtable
        public bool ShouldBeInVTable => IsVirtual || Flags.Has(EFunctionFlags.Event);

        public bool RetValNeedsDestruction;

        public byte OperatorPrecedence; //ME1/2
        public string FriendlyName; //ME1/2

        public Function SuperFunction;

        public Function(string name, EFunctionFlags flags,
                        VariableDeclaration returnValueDeclaration, CodeBody body,
                        List<FunctionParameter> parameters = null,
                        int start = -1, int end = -1)
            : base(ASTNodeType.Function, start, end)
        {
            Name = name;
            Body = body;
            ReturnValueDeclaration = returnValueDeclaration;
            Flags = flags;
            Parameters = parameters ?? [];
            Locals = [];
            VarType = new DelegateType(this)
            {
                IsFunction = true,
                Declaration = this
            };
            if (Body != null) Body.Outer = this;
        }

        public bool SignatureEquals(Function other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (ReturnType != other.ReturnType || Parameters.Count != other.Parameters.Count)
            {
                return false;
            }

            for (int i = 0; i < Parameters.Count; i++)
            {
                var thisParam = Parameters[i];
                var otherParam = other.Parameters[i];
                if (!NodeUtils.TypeEqual(thisParam.VarType, otherParam.VarType) || thisParam.ArrayLength != otherParam.ArrayLength)
                {
                    return false;
                }
            }
            return true;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if(ReturnType != null) yield return ReturnType;
                foreach (FunctionParameter functionParameter in Parameters) yield return functionParameter;
                foreach (VariableDeclaration variableDeclaration in Locals) yield return variableDeclaration;
                if (Body != null) yield return Body;
            }
        }

        public string GetScope() => $"{GetOuterScope()}.{Name}";

        public string GetOuterScope() =>
            Outer switch
            {
                Class cls => cls.GetScope(),
                State state => state.GetScope(),
                _ => throw new ArgumentOutOfRangeException(nameof(Outer))
            };

        public string FilePath { get; init; }
        public int UIndex { get; init; }
    }
}
