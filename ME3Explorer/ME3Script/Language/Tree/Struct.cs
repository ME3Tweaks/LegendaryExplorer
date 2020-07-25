using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public sealed class Struct : VariableType
    {
        public List<Specifier> Specifiers;
        public VariableType Parent;
        public List<VariableDeclaration> Members;
        public DefaultPropertiesBlock DefaultProperties;

        public Struct(string name, List<Specifier> specs,
            List<VariableDeclaration> members,
            SourcePosition start, SourcePosition end, VariableType parent = null)
            : base(name, start, end)
        {
            Type = ASTNodeType.Struct;
            Specifiers = specs;
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
                foreach (Specifier specifier in Specifiers) yield return specifier;
                if (Parent != null) yield return Parent;
                foreach (VariableDeclaration variableDeclaration in Members) yield return variableDeclaration;
                if (DefaultProperties != null) yield return DefaultProperties;
            }
        }
    }
}
