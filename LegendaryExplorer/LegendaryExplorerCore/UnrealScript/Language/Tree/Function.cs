using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class Function : ASTNode, IContainsByteCode, IHasFileReference
    {
        public string Name { get; }
        public CodeBody Body { get; set; }
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

        public bool IsVirtual => !Flags.Has(EFunctionFlags.Final) && !Flags.Has(EFunctionFlags.Static);

        public bool HasOptionalParms => Flags.Has(EFunctionFlags.HasOptionalParms) || Parameters.Any(parm => parm.IsOptional);

        public bool RetValNeedsDestruction;

        public byte OperatorPrecedence; //ME1/2
        public string FriendlyName; //ME1/2

        public Function SuperFunction;

        public Function(string name, EFunctionFlags flags,
                        VariableDeclaration returnValueDeclaration, CodeBody body,
                        List<FunctionParameter> parameters = null,
                        SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.Function, start, end)
        {
            Name = name;
            Body = body;
            ReturnValueDeclaration = returnValueDeclaration;
            Flags = flags;
            Parameters = parameters ?? new List<FunctionParameter>();
            Locals = new List<VariableDeclaration>();
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


        public string FilePath { get; init; }
        public int UIndex { get; init; }
    }
}
