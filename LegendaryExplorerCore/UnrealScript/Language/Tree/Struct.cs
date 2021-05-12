using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
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

        public override int Size
        {
            get
            {
                (int structSize, _) = GetSizeAndAlign();
                return structSize;
            }
        }

        private (int structSize, int structAlign) GetSizeAndAlign()
        {
            int structSize = 0;
            int structAlign = 4;
            VariableType prev = null;
            int bitfieldPos = 0;
            foreach (VariableDeclaration varDecl in VariableDeclarations)
            {
                VariableType cur = varDecl.VarType;
                int varSize = cur.Size;
                int varAlign = 4;
                if (cur is StaticArrayType staticArrayType)
                {
                    cur = staticArrayType.ElementType;
                }
                if (cur.PropertyType == EPropertyType.Bool)
                {
                    if (prev?.PropertyType == EPropertyType.Bool)
                    {
                        varSize = 0;
                        bitfieldPos++;
                        if (bitfieldPos > 32)
                        {
                            //cannot pack more than 32 bools into a single bitfield (not that this will ever come up...)
                            cur = null;
                        }
                    }
                    else
                    {
                        bitfieldPos = 0;
                    }
                }
                else if (cur.PropertyType == EPropertyType.Byte)
                {
                    varAlign = 1;
                }
                else if (cur.PropertyType == EPropertyType.String)
                {
                    varSize = 12 * varDecl.ArrayLength; //TODO: verify this
                }
                else if (cur is DynamicArrayType)
                {
                    varSize = 12; //TODO: verify this
                }
                else if (cur is Struct curStruct)
                {
                    (varSize, varAlign) = curStruct.GetSizeAndAlign();
                    varSize *= varDecl.ArrayLength;
                }

                structSize = structSize.Align(varAlign) + varSize;

                structAlign = Math.Max(structAlign, varAlign);
                prev = cur;
            }

            structSize = structSize.Align(structAlign);

            return (structSize, structAlign);
        }

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
