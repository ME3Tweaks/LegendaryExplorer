using System.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public sealed class Class : ObjectType, IContainsFunctions
    {
        public string Package;
        public VariableType Parent;
        public VariableType _outerClass;
        public UnrealFlags.EClassFlags Flags;
        public string ConfigName;
        public List<VariableType> Interfaces { get; }
        public override List<VariableDeclaration> VariableDeclarations { get; }
        public override List<VariableType> TypeDeclarations { get; }
        public List<Function> Functions { get; }
        public List<State> States { get; }

        public CodeBody ReplicationBlock;
        public override DefaultPropertiesBlock DefaultProperties { get; set; }

        public List<string> VirtualFunctionNames;
        public List<Function> VirtualFunctionTable;

        public override ASTNodeType NodeType => ASTNodeType.Class;

        public bool IsInterface => Flags.Has(UnrealFlags.EClassFlags.Interface);

        public bool IsComponent => SameAsOrSubClassOf("Component") || SameAsOrSubClassOf("BioBaseComponent");

        //Despite being considered a component for most compiling purposes, descendants of BioBaseComponent do NOT have the component header before their properties
        public bool HasComponentPropertiesHeader => SameAsOrSubClassOf("Component");

        public bool IsNative => Flags.Has(UnrealFlags.EClassFlags.Native);

        //Sometimes, a class Export will have its Default__ object as an import, and no children.
        //So the actual definition is in another file, but instead of being included as an import, it's a strange partial definition.
        //In that case, we can;t do anything with it, so this is set to false.
        public bool IsFullyDefined = true;

        public Class(string name, VariableType parent, VariableType outer, UnrealFlags.EClassFlags flags,
                     List<VariableType> interfaces = null,
                     List<VariableType> types = null,
                     List<VariableDeclaration> vars = null,
                     List<Function> funcs = null,
                     List<State> states = null,
                     DefaultPropertiesBlock defaultProperties = null,
                     SourcePosition start = null, SourcePosition end = null)
            : base(name, start, end, EPropertyType.Object)
        {
            Parent = parent;
            OuterClass = outer;
            Flags = flags;
            Interfaces = interfaces ?? new List<VariableType>();
            VariableDeclarations = vars ?? new List<VariableDeclaration>();
            TypeDeclarations = types ?? new List<VariableType>();
            Functions = funcs ?? new List<Function>();
            States = states ?? new List<State>();
            DefaultProperties = defaultProperties ?? new DefaultPropertiesBlock();
            Type = ASTNodeType.Class;

            foreach (ASTNode node in ChildNodes)
            {
                node.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        #region Helpers

        public bool SameAsOrSubClassOf(Class c) => SameAsOrSubClassOf(c.Name);

        public bool SameAsOrSubClassOf(string name)
        {
            string inputName = name.ToLower();
            if (inputName == "object")
            {
                return true;
            }
            string nodeName = Name.ToLower();
            if (nodeName == inputName)
                return true;
            Class current = this;
            while (current.Parent != null && current.Parent.Name.ToLower() != "object")
            {
                if (current.Parent.Name.ToLower() == inputName)
                    return true;
                current = (Class)current.Parent;
            }
            return false;
        }

        public string GetInheritanceString()
        {
            string str = Name;
            Class current = this;
            while (current?.Parent != null)
            {
                current = current.Parent as Class;
                str = (current?.Name ?? "Object") + "." + str; 
            }
            return str;
        }

        public override string GetScope() => GetInheritanceString();

        public bool Implements(Class interfaceClass)
        {
            Class c = this;
            while (c != null)
            {
                if (c.Interfaces.Contains(interfaceClass))
                {
                    return true;
                }

                c = c.Parent as Class;
            }
            return false;
        }

        #endregion
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                foreach (VariableType interfaceType in Interfaces) yield return interfaceType;
                foreach (VariableType typeDeclaration in TypeDeclarations) yield return typeDeclaration;
                foreach (VariableDeclaration variableDeclaration in VariableDeclarations) yield return variableDeclaration;
                foreach (Function function in Functions) yield return function;
                foreach (State state in States) yield return state;
                if (ReplicationBlock is not null)
                {
                    yield return ReplicationBlock;
                }
                yield return DefaultProperties;
            }
        }

        public VariableType OuterClass
        {
            get => _outerClass ?? (Parent as Class)?.OuterClass;
            set => _outerClass = value;
        }
    }
}
