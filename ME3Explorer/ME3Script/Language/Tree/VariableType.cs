using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class VariableType : ASTNode
    {
        public string Name;
        public ASTNode Declaration;
        public virtual ASTNodeType NodeType => Declaration?.Type ?? ASTNodeType.INVALID;

        public EPropertyType PropertyType;

        public virtual int Size => PropertyType switch
        {
            EPropertyType.None => 0,
            EPropertyType.Byte => 1,
            EPropertyType.Int => 4,
            EPropertyType.Bool => 4,
            EPropertyType.Float => 4,
            EPropertyType.Object => 4,
            EPropertyType.Name => 8,
            EPropertyType.Delegate => 12,
            EPropertyType.Interface => 8,
            EPropertyType.Struct => 0,
            EPropertyType.Vector => 12,
            EPropertyType.Rotator => 12,
            EPropertyType.String => 0,
            EPropertyType.Map => 0,
            EPropertyType.StringRef => 4,
            _ => throw new ArgumentOutOfRangeException()
        };

        public VariableType(string name, SourcePosition start = null, SourcePosition end = null, EPropertyType propType = EPropertyType.None)
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
    }
}
