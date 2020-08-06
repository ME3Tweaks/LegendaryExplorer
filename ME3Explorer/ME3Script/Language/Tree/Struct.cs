using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Script.Language.Tree
{
    public sealed class Struct : VariableType
    {
        public ScriptStructFlags Flags;
        public VariableType Parent;
        public List<VariableDeclaration> Members;
        public DefaultPropertiesBlock DefaultProperties;

        public Struct(string name, ScriptStructFlags flags,
            List<VariableDeclaration> members,
            SourcePosition start, SourcePosition end, VariableType parent = null)
            : base(name, start, end)
        {
            Type = ASTNodeType.Struct;
            Flags = flags;
            Members = members;
            Parent = parent;
            
            foreach (ASTNode node in ChildNodes)
            {
                node.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public bool SameOrSubStruct(string name)
        {
            string nodeName = this.Name.ToLower();
            string inputName = name.ToLower();
            if (nodeName == inputName)
                return true;
            Struct current = this;
            while (current.Parent != null)
            {
                if (current.Parent.Name.ToLower() == inputName)
                    return true;
                current = (Struct)current.Parent;
            }
            return false;
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if (Parent != null) yield return Parent;
                foreach (VariableDeclaration variableDeclaration in Members) yield return variableDeclaration;
                if (DefaultProperties != null) yield return DefaultProperties;
            }
        }
    }
}
