using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public sealed class Struct : ObjectType
    {
        public ScriptStructFlags Flags;
        public override List<VariableDeclaration> VariableDeclarations { get; }
        public override List<VariableType> TypeDeclarations { get; }
        public override DefaultPropertiesBlock DefaultProperties { get; set; }

        public bool IsAtomic => Flags.Has(ScriptStructFlags.Atomic) || Flags.Has(ScriptStructFlags.AtomicWhenCooked);

        public bool IsImmutable => Flags.Has(ScriptStructFlags.Immutable) || Flags.Has(ScriptStructFlags.ImmutableWhenCooked);
        public bool IsNative => Flags.Has(ScriptStructFlags.Native);

        public Struct(string name, VariableType parent, ScriptStructFlags flags,
                      List<VariableDeclaration> variableDeclarations = null,
                      List<VariableType> typeDeclarations = null,
                      DefaultPropertiesBlock defaults = null,
                      PropertyCollection defaultPropertyCollection = null,
                      int start = -1, int end = -1)
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
            if (defaultPropertyCollection is not null)
            {
                DefaultPropertyCollection = defaultPropertyCollection;
                if (parent is not null)
                {
                    defaultPropCollectionNeedsFixing = true;
                }
            }

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

        public override int Size(MEGame game)
        {
            (int structSize, _) = GetSizeAndAlign(game);
            return structSize;
        }

        private (int structSize, int structAlign) GetSizeAndAlign(MEGame game)
        {
            int structSize = 0;
            int structAlign = 4;
            VariableType prev = null;
            int bitfieldPos = 0;
            foreach (VariableDeclaration varDecl in VariableDeclarations)
            {
                VariableType cur = varDecl.VarType;
                int varSize = cur.Size(game);
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
                    if (game.IsLEGame())
                    {
                        varSize = 16 * varDecl.ArrayLength;
                        varAlign = 8;
                    }
                    else
                    {
                        varSize = 12 * varDecl.ArrayLength; //TODO: verify this
                    }
                }
                else if (cur is DynamicArrayType)
                {
                    if (game.IsLEGame())
                    {
                        varSize = 16;
                        varAlign = 8;
                    }
                    else
                    {
                        varSize = 12;
                    }
                }
                else if (cur is Struct curStruct)
                {
                    (varSize, varAlign) = curStruct.GetSizeAndAlign(game);
                    varSize *= varDecl.ArrayLength;
                }
                else if (cur.PropertyType is EPropertyType.Object or EPropertyType.Delegate && game.IsLEGame())
                {
                    varAlign = 8;
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

        public override string GetScope()
        {
            Struct targetStruct = this;
            string classScope = NodeUtils.GetContainingClass(targetStruct).GetInheritanceString();
            var scopes = new Stack<string>();
            scopes.Push(Name);
            while (targetStruct.Outer is Struct lhsStructOuter)
            {
                scopes.Push(lhsStructOuter.Name);
                targetStruct = lhsStructOuter;
            }
            scopes.Push(classScope);

            return string.Join(".", scopes);
        }

        private PropertyCollection DefaultPropertyCollection;
        private bool defaultPropCollectionNeedsFixing;
        public PropertyCollection GetDefaultPropertyCollection(IMEPackage pcc, UnrealScriptOptionsPackage usop, bool stripTransients = false)
        {
            if (DefaultPropertyCollection is null)
            {
                DefaultPropertyCollection = WriteDefaultsOntoProps(MakeBaseProps(pcc, usop), pcc, usop);
            }
            else if (defaultPropCollectionNeedsFixing)
            {
                var props = MakeBaseProps(pcc, usop);
                foreach (Property property in DefaultPropertyCollection)
                {
                    props.AddOrReplaceProp(property);
                }
                DefaultPropertyCollection = props;
                defaultPropCollectionNeedsFixing = false;
            }

            return stripTransients ? StripTransients(pcc, usop) : DefaultPropertyCollection.DeepClone();
        }

        private PropertyCollection WriteDefaultsOntoProps(PropertyCollection props, IMEPackage pcc, UnrealScriptOptionsPackage usop)
        {
            if (DefaultProperties?.Statements is not null && DefaultProperties.Statements.Any())
            {
                ScriptPropertiesCompiler.CompileStructDefaults(this, props, pcc, usop);
            }
            return props;
        }

        public PropertyCollection MakeBaseProps(IMEPackage pcc, UnrealScriptOptionsPackage usop, bool useStructDefaultsForStructProperties = true)
        {
            var props = new PropertyCollection();
            foreach (VariableDeclaration varDeclAST in VariableDeclarations)
            {
                if (varDeclAST.Flags.Has(EPropertyFlags.Native))
                {
                    continue;
                }
                var propName = NameReference.FromInstancedString(varDeclAST.Name);
                VariableType targetType = varDeclAST.VarType is StaticArrayType staticArrayType ? staticArrayType.ElementType : varDeclAST.VarType;
                int staticArrayLength = varDeclAST.IsStaticArray ? varDeclAST.ArrayLength : 1;
                for (int i = 0; i < staticArrayLength; i++)
                {
                    Property prop;
                    switch (targetType)
                    {
                        case Class cls:
                            prop = new ObjectProperty(0, propName)
                            {
                                InternalPropType = cls.IsInterface ? Unreal.PropertyType.InterfaceProperty : Unreal.PropertyType.ObjectProperty
                            };
                            break;
                        case ClassType targetClassLimiter:
                            prop = new ObjectProperty(0, propName);
                            break;
                        case DelegateType delegateType:
                            prop = new DelegateProperty("None", 0, propName);
                            break;
                        case DynamicArrayType dynArrType:
                            var elementType = dynArrType.ElementType;
                            switch (elementType)
                            {
                                case ClassType:
                                case Class:
                                    prop = new ArrayProperty<ObjectProperty>(propName);
                                    break;
                                case DelegateType:
                                    prop = new ArrayProperty<DelegateProperty>(propName);
                                    break;
                                case Enumeration:
                                    prop = new ArrayProperty<EnumProperty>(propName);
                                    break;
                                case Struct:
                                    prop = new ArrayProperty<StructProperty>(propName);
                                    break;
                                default:
                                    switch (elementType.PropertyType)
                                    {
                                        case EPropertyType.Byte:
                                            prop = new ArrayProperty<ByteProperty>(propName);
                                            break;
                                        case EPropertyType.Int:
                                            prop = new ArrayProperty<IntProperty>(propName);
                                            break;
                                        case EPropertyType.Bool:
                                            prop = new ArrayProperty<BoolProperty>(propName);
                                            break;
                                        case EPropertyType.Float:
                                            prop = new ArrayProperty<FloatProperty>(propName);
                                            break;
                                        case EPropertyType.Name:
                                            prop = new ArrayProperty<NameProperty>(propName);
                                            break;
                                        case EPropertyType.String:
                                            prop = new ArrayProperty<StrProperty>(propName);
                                            break;
                                        case EPropertyType.StringRef:
                                            prop = new ArrayProperty<StringRefProperty>(propName);
                                            break;
                                        default:
                                            throw new ArgumentOutOfRangeException(nameof(elementType.PropertyType), elementType.PropertyType, $"{elementType.PropertyType} is not a valid array element type!");
                                    }
                                    break;
                            }
                            break;
                        case Enumeration enumeration:
                            prop = new EnumProperty(NameReference.FromInstancedString(enumeration.Values[0].Name), NameReference.FromInstancedString(enumeration.Name), pcc.Game, propName);
                            break;
                        case Struct structType:
                            prop = new StructProperty(NameReference.FromInstancedString(structType.Name),
                                useStructDefaultsForStructProperties ? structType.GetDefaultPropertyCollection(pcc, usop, false) : structType.MakeBaseProps(pcc, usop, useStructDefaultsForStructProperties),
                                propName, structType.IsImmutable);
                            break;
                        default:
                            switch (targetType.PropertyType)
                            {
                                case EPropertyType.Byte:
                                    prop = new ByteProperty(0, propName);
                                    break;
                                case EPropertyType.Int:
                                    prop = new IntProperty(0, propName);
                                    break;
                                case EPropertyType.Bool:
                                    prop = new BoolProperty(false, propName);
                                    break;
                                case EPropertyType.Float:
                                    prop = new FloatProperty(0f, propName);
                                    break;
                                case EPropertyType.Name:
                                    prop = new NameProperty("None", propName);
                                    break;
                                case EPropertyType.String:
                                    prop = new StrProperty("", propName);
                                    break;
                                case EPropertyType.StringRef:
                                    prop = new StringRefProperty(0, propName);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(targetType.PropertyType), targetType.PropertyType, $"{targetType.PropertyType} is not a property type!");
                            }
                            break;
                    }
                    prop.StaticArrayIndex = i;
                    props.Add(prop);
                }
            }
            if (Parent is Struct parentStruct)
            {
                foreach (Property property in parentStruct.GetDefaultPropertyCollection(pcc, usop, false))
                {
                    props.Add(property);
                }
            }
            return props;
        }

        private PropertyCollection StripTransients(IMEPackage pcc, UnrealScriptOptionsPackage usop)
        {
            var props = new PropertyCollection();
            foreach ((VariableDeclaration varDecl, Property prop) in VariableDeclarations.Where(varDecl => !varDecl.Flags.Has(EPropertyFlags.Native)).Zip(DefaultPropertyCollection))
            {
                if (varDecl.Flags.Has(EPropertyFlags.Transient))
                {
                    continue;
                }
                if (prop is StructProperty structProp)
                {
                    var strct = (Struct)varDecl.VarType;
                    props.Add(new StructProperty(structProp.StructType, strct.GetDefaultPropertyCollection(pcc, usop, true), structProp.Name, strct.IsImmutable));
                }
                else
                {
                    props.Add(prop.DeepClone());
                }
            }
            return props;
        }

        public bool IsNativeCompatibleWith(Struct other, MEGame game, UnrealScriptOptionsPackage usop)
        {
            if (VariableDeclarations.Count != other.VariableDeclarations.Count)
            {
                return false;
            }
            foreach ((VariableDeclaration ours, VariableDeclaration theirs) in VariableDeclarations.Zip(other.VariableDeclarations))
            {
                if (ours.Name != theirs.Name
                    || !string.Equals(ours.VarType.Name, theirs.VarType.Name, StringComparison.OrdinalIgnoreCase)
                    || ours.ArrayLength != theirs.ArrayLength)
                {
                    return false;
                }
            }
            if (TypeDeclarations.Count != other.TypeDeclarations.Count)
            {
                return false;
            }
            foreach ((VariableType ours, VariableType theirs) in TypeDeclarations.Zip(other.TypeDeclarations))
            {
                if (!((Struct)ours).IsNativeCompatibleWith((Struct)theirs, game, usop))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
