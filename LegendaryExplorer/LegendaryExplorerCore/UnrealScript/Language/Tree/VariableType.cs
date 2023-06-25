using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class VariableType : ASTNode, IHasFileReference
    {
        public readonly string Name;
        public ASTNode Declaration;
        public virtual ASTNodeType NodeType => Declaration?.Type ?? ASTNodeType.INVALID;

        public EPropertyType PropertyType;

        public virtual int Size(MEGame game) => PropertyType switch
        {
            EPropertyType.None => 0,
            EPropertyType.Byte => 1,
            EPropertyType.Int => 4,
            EPropertyType.Bool => 4,
            EPropertyType.Float => 4,
            EPropertyType.Object => game.IsLEGame() ? 8 : 4,
            EPropertyType.Name => 8,
            EPropertyType.Delegate => game.IsLEGame() ? 16 : 12,
            EPropertyType.Interface => game.IsLEGame() ? 16 : 8,
            EPropertyType.Struct => 0,
            EPropertyType.Vector => 12,
            EPropertyType.Rotator => 12,
            EPropertyType.String => 0,
            EPropertyType.Map => 0,
            EPropertyType.StringRef => 4,
            _ => throw new ArgumentOutOfRangeException()
        };

        public VariableType(string name, int start = -1, int end = -1, EPropertyType propType = EPropertyType.None)
            : base(ASTNodeType.VariableType, start, end) 
        {
            Name = name;
            PropertyType = propType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if(Declaration != null) yield return Declaration;
            }
        }

        public virtual string GetScope()
        {
            if (Outer is VariableType varType)
            {
                return $"{varType.GetScope()}.{Name}";
            }

            return Name;
        }

        public string FilePath { get; init; }
        public int UIndex { get; init; }

        string IHasFileReference.Name => Name;
    }

    public static class VariableTypeExtensions
    {
        public static string DisplayName(this VariableType type)
        {
            if (type is null)
            {
                return "None";
            }
            var builder = new CodeBuilderVisitor();
            builder.AppendTypeName(type);
            string fullTypeName = builder.GetOutput();
            if (type is StaticArrayType staticArrayType)
            {
                fullTypeName = $"{fullTypeName}[{staticArrayType.Length}]";
            }
            return fullTypeName;
        }
    }
}
