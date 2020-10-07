using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;
using System.Linq;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3Script.Language.Tree
{
    public sealed class Struct : VariableType, IObjectType
    {
        public ScriptStructFlags Flags;
        public VariableType Parent;
        public List<VariableDeclaration> VariableDeclarations { get; }
        public List<VariableType> TypeDeclarations { get; }
        public DefaultPropertiesBlock DefaultProperties { get; }

        public Struct(string name, VariableType parent, ScriptStructFlags flags,
                      List<VariableDeclaration> variableDeclarations = null,
                      List<VariableType> typeDeclarations = null,
                      DefaultPropertiesBlock defaults = null,
                      SourcePosition start = null, SourcePosition end = null)
            : base(name, start, end, name switch
            {
                "Vector" => EPropertyType.Vector,
                "Rotator" => EPropertyType.Rotator,
                _ => EPropertyType.Struct
            })
        {
            Type = ASTNodeType.Struct;
            Flags = flags;
            VariableDeclarations = variableDeclarations ?? new List<VariableDeclaration>();
            TypeDeclarations = typeDeclarations ?? new List<VariableType>();
            Parent = parent;
            DefaultProperties = defaults ?? new DefaultPropertiesBlock();
            
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

        public string GetInheritanceString()
        {
            string str = this.Name;
            Struct current = this;
            while (current.Parent != null)
            {
                current = (Struct)current.Parent;
                str = current.Name + "." + str;
            }
            return str;
        }

        public override int Size => VariableDeclarations.Sum(decl => decl.GetSize()) + ((Parent as Struct)?.Size ?? 0);

        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                foreach (VariableDeclaration variableDeclaration in VariableDeclarations) yield return variableDeclaration;
                foreach (VariableType typeDeclaration in TypeDeclarations) yield return typeDeclaration;
                if (DefaultProperties != null) yield return DefaultProperties;
            }
        }
    }
}
