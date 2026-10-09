using System.Collections.Generic;
using System.Diagnostics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    [DebuggerDisplay("VariableDeclaration | {Name}, {VarType}")]
    public class VariableDeclaration : Statement, IHasFileReference
    {
        public UnrealFlags.EPropertyFlags Flags;

        public VariableType VarType;

        // Members supplied solely for editor/T3D lookup are not serialized into cooked classes.
        public bool IsSynthetic;
        public string Category;
        public string Name { get; }

        public int ArrayLength;

        public ushort ReplicationOffset = 0;

        public bool IsStaticArray => ArrayLength > 1;

        public bool IsTransient => Flags.Has(UnrealFlags.EPropertyFlags.Transient);

        public bool IsConst => Flags.Has(UnrealFlags.EPropertyFlags.Const);

        public VariableDeclaration(VariableType type, UnrealFlags.EPropertyFlags flags,
                                   string name, int arrayLength = 1, string category = "None", int start = -1, int end = -1)
            : base(ASTNodeType.VariableDeclaration, start, end)
        {
            Flags = flags;
            Name = name;
            ArrayLength = arrayLength;
            Category = category ?? "None";
            VarType = IsStaticArray && type is not StaticArrayType ? new StaticArrayType(type, ArrayLength) : type;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return VarType;
            }
        }

        public int GetSize(MEGame game) => VarType?.Size(game) ?? 0;

        public string FilePath { get; init; }
        public int UIndex { get; set; }

        public VariableDeclaration Clone()
        {
            return new VariableDeclaration(VarType, Flags, Name, ArrayLength, Category);
        }

        public bool IsOrHasInstancedObjectProperty(HashSet<Struct> visited = null)
        {
            var varType = VarType;
            while (true)
            {
                switch (varType)
                {
                    case StaticArrayType staticArrayType:
                        varType = staticArrayType.ElementType;
                        continue;
                    case DynamicArrayType dynamicArrayType:
                        varType = dynamicArrayType.ElementType;
                        continue;
                    case Struct strct:
                        visited ??= [];
                        if (!visited.Add(strct)) return false;
                        foreach (VariableDeclaration structVarDecl in strct.VariableDeclarations)
                        {
                            if (structVarDecl.IsOrHasInstancedObjectProperty(visited))
                            {
                                return true;
                            }
                        }
                        if (strct.Parent is Struct parent) { varType = parent; continue; }
                        return false;
                    case Class cls:
                        return !cls.IsComponent && Flags.Has(UnrealFlags.EPropertyFlags.EditInline | UnrealFlags.EPropertyFlags.ExportObject);
                    default:
                        return false;
                }
            }
        }
    }
}
