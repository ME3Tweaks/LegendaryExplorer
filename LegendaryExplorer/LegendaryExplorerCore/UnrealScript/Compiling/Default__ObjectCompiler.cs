using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    public class Default__ObjectCompiler
    {
        private readonly IMEPackage Pcc;
        private ExportEntry Default__Export;
        private ExportEntry Default__Archetype;

        public Default__ObjectCompiler(IMEPackage pcc, ExportEntry default__Export)
        {
            Pcc = pcc;
            Default__Export = default__Export;
        }


        public static void Compile(DefaultPropertiesBlock defaultsAST, ExportEntry classExport, ref ExportEntry defaultsExport)
        {
            IMEPackage pcc = classExport.FileRef;
            var defaultsExportObjectName = new NameReference($"Default__{classExport.ObjectNameString}", classExport.indexValue);
            if (defaultsExport is null)
            {
                if (pcc.TryGetTrash(out defaultsExport))
                {
                    defaultsExport.Parent = classExport.Parent;
                }
                else
                {
                    defaultsExport = new ExportEntry(pcc, classExport.Parent, defaultsExportObjectName);
                    pcc.AddExport(defaultsExport);
                }
            }

            var compiler = new Default__ObjectCompiler(pcc, defaultsExport);

            defaultsExport.SuperClass = null;
            defaultsExport.Class = classExport;
            defaultsExport.ObjectName = defaultsExportObjectName;
            defaultsExport.ObjectFlags = UnrealFlags.EObjectFlags.ClassDefaultObject | UnrealFlags.EObjectFlags.Public | UnrealFlags.EObjectFlags.LoadForClient | UnrealFlags.EObjectFlags.LoadForServer | UnrealFlags.EObjectFlags.LoadForEdit;
            defaultsExport.Archetype = compiler.GetClassDefaultObject(classExport.Parent);

            compiler.Default__Archetype = defaultsExport.Archetype switch
            {
                ImportEntry defaultArchetypeImport => EntryImporter.ResolveImport(defaultArchetypeImport),
                ExportEntry defaultArchetypeExport => defaultArchetypeExport,
                _ => null
            };

            var props = compiler.ConvertStatementsToPropertyCollection(defaultsExport, defaultsAST.Statements, new Dictionary<NameReference, ExportEntry>());
            
            defaultsExport.WriteProperties(props);
        }

        private Property ConvertToProperty(AssignStatement assignStatement, Dictionary<NameReference, ExportEntry> subObjectDict)
        {
            var nameRef = (SymbolReference) assignStatement.Target;
            int staticArrayIndex = 0;
            if (nameRef is ArraySymbolRef staticArrayRef)
            {
                staticArrayIndex = ((IntegerLiteral) staticArrayRef.Index).Value;
                nameRef = (SymbolReference) staticArrayRef.Array;
            }

            var type = nameRef.ResolveType();
            var literal = assignStatement.Value;
            var propName = NameReference.FromInstancedString(nameRef.Name);
            Property prop = MakeProperty(propName, type, literal, subObjectDict);
            prop.StaticArrayIndex = staticArrayIndex;
            return prop;
        }

        private Property MakeProperty(NameReference propName, VariableType type, Expression literal, Dictionary<NameReference, ExportEntry> subObjectDict)
        {
            Property prop;
            switch (type)
            {
                case ClassType classType:
                    prop = new ObjectProperty(literal is NoneLiteral ? null : CompilerUtils.ResolveClass((Class)((ClassType)((ObjectLiteral)literal).Class).ClassLimiter, Pcc), propName)
                    {
                        InternalPropType = ((Class)classType.ClassLimiter).IsInterface ? PropertyType.InterfaceProperty : PropertyType.ObjectProperty
                    };
                    break;
                case Class @class:
                    IEntry entry;
                    if (literal is NoneLiteral)
                    {
                        entry = null;
                    }
                    else if (literal is SymbolReference { Node: Subobject subobject })
                    {
                        entry = subObjectDict[NameReference.FromInstancedString(subobject.Name.Name)];
                    }
                    else
                    {
                        entry = Pcc.FindEntry(((ObjectLiteral)literal).Name.Value);
                    }

                    prop = new ObjectProperty(entry, propName)
                    {
                        InternalPropType = @class.IsComponent ? PropertyType.ComponentProperty : PropertyType.ObjectProperty
                    };
                    break;
                case DelegateType:
                    int objUIndex = 0;
                    NameReference funcName;
                    if (literal is NoneLiteral)
                    {
                        funcName = "None";
                    }
                    else
                    {
                        if (literal is CompositeSymbolRef csf)
                        {
                            objUIndex = GetClassDefaultObject(CompilerUtils.ResolveClass((Class)((ClassType)((ObjectLiteral)csf.OuterSymbol).Class).ClassLimiter, Pcc)).UIndex;
                            literal = csf.InnerSymbol;
                        }

                        funcName = NameReference.FromInstancedString(((SymbolReference) literal).Name);
                    }
                    prop = new DelegateProperty(objUIndex, funcName, propName);
                    break;
                case DynamicArrayType dynamicArrayType:
                    VariableType elementType = dynamicArrayType.ElementType;
                    var properties = ((DynamicArrayLiteral)literal).Values.Select(lit => MakeProperty(null, elementType, lit, subObjectDict));
                    switch (elementType)
                    {
                        case ClassType:
                        case Class:
                            prop = new ArrayProperty<ObjectProperty>(properties.Cast<ObjectProperty>().ToList(), propName);
                            break;
                        case DelegateType:
                            prop = new ArrayProperty<DelegateProperty>(properties.Cast<DelegateProperty>().ToList(), propName);
                            break;
                        case Enumeration:
                            prop = new ArrayProperty<EnumProperty>(properties.Cast<EnumProperty>().ToList(), propName);
                            break;
                        case Struct:
                            prop = new ArrayProperty<StructProperty>(properties.Cast<StructProperty>().ToList(), propName);
                            break;
                        default:
                            switch (type.PropertyType)
                            {
                                case EPropertyType.Byte:
                                    prop = new ArrayProperty<ByteProperty>(properties.Cast<ByteProperty>().ToList(), propName);
                                    break;
                                case EPropertyType.Int:
                                    prop = new ArrayProperty<IntProperty>(properties.Cast<IntProperty>().ToList(), propName);
                                    break;
                                case EPropertyType.Bool:
                                    prop = new ArrayProperty<BoolProperty>(properties.Cast<BoolProperty>().ToList(), propName);
                                    break;
                                case EPropertyType.Float:
                                    prop = new ArrayProperty<FloatProperty>(properties.Cast<FloatProperty>().ToList(), propName);
                                    break;
                                case EPropertyType.Name:
                                    prop = new ArrayProperty<NameProperty>(properties.Cast<NameProperty>().ToList(), propName);
                                    break;
                                case EPropertyType.String:
                                    prop = new ArrayProperty<StrProperty>(properties.Cast<StrProperty>().ToList(), propName);
                                    break;
                                case EPropertyType.StringRef:
                                    prop = new ArrayProperty<StringRefProperty>(properties.Cast<StringRefProperty>().ToList(), propName);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            break;
                    }
                    break;
                case Enumeration enumeration:
                    NameReference value;
                    if (literal is NoneLiteral)
                    {
                        value = "None";
                    }
                    else
                    {
                        value = NameReference.FromInstancedString(((EnumValue) ((SymbolReference) literal).Node).Name);
                    }
                    prop = new EnumProperty(value, NameReference.FromInstancedString(enumeration.Name), Pcc.Game, propName);
                    break;
                case Struct @struct:
                    //todo: Spec says that unspecified properties on a struct value should be inherited from base class's default for that property
                    var structProps = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, @struct.Name, true);
                    foreach (Statement statement in ((StructLiteral)literal).Statements)
                    {
                        structProps.AddOrReplaceProp(ConvertToProperty((AssignStatement)statement, subObjectDict));
                    }
                    prop = new StructProperty(@struct.Name, structProps, propName, GlobalUnrealObjectInfo.IsImmutable(@struct.Name, Pcc.Game));
                    break;
                default:
                    switch (type.PropertyType)
                    {
                        case EPropertyType.Byte:
                            prop = new ByteProperty((byte) ((IntegerLiteral) literal).Value, propName);
                            break;
                        case EPropertyType.Int:
                            prop = new IntProperty(((IntegerLiteral) literal).Value, propName);
                            break;
                        case EPropertyType.Bool:
                            prop = new BoolProperty(((BooleanLiteral) literal).Value, propName);
                            break;
                        case EPropertyType.Float:
                            prop = new FloatProperty(((FloatLiteral) literal).Value, propName);
                            break;
                        case EPropertyType.Name:
                            prop = new NameProperty(NameReference.FromInstancedString(((NameLiteral) literal).Value), propName);
                            break;
                        case EPropertyType.String:
                            prop = new StrProperty(((StringLiteral) literal).Value, propName);
                            break;
                        case EPropertyType.StringRef:
                            prop = new StringRefProperty(((StringRefLiteral) literal).Value, propName);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
            }

            return prop;
        }

        private void CreateSubObject(Subobject subObject, ExportEntry parent, Dictionary<NameReference, ExportEntry> parentSubObjectDict, ref ExportEntry subExport)
        {
            var objName = NameReference.FromInstancedString(subObject.Name.Name);
            IEntry classEntry = EntryImporter.EnsureClassIsInFile(Pcc, subObject.Class.Name);
            if (subExport is null)
            {
                if (Pcc.TryGetTrash(out subExport))
                {
                    subExport.Parent = parent;
                }
                else
                {
                    subExport = new ExportEntry(Pcc, parent, objName);
                    Pcc.AddExport(subExport);
                }
            }
            subExport.SuperClass = null;
            subExport.Class = classEntry;
            subExport.ObjectName = objName;
            subExport.Class = classEntry;

            if (Default__Archetype is not null)
            {
                string defaultArchetypePath = Default__Archetype.InstancedFullPath;
                string defaultObjectPath = Default__Export.InstancedFullPath;
                string subObjPath = subExport.InstancedFullPath;
                string subPath = subObjPath[defaultObjectPath.Length..];
                ExportEntry subObjArchetype = Default__Archetype.FileRef.FindExport($"{defaultArchetypePath}.{subPath}");
                if (subObjArchetype == null)
                {
                    //sometimes the subobjects have a flat structure under the Default__
                    subPath = objName.Instanced;
                    subObjArchetype = Default__Archetype.FileRef.FindExport($"{defaultArchetypePath}.{subPath}");
                }

                if (subObjArchetype is not null && subObjArchetype.ClassName.CaseInsensitiveEquals(subExport.ClassName))
                {
                    subExport.Archetype = ReferenceEquals(Default__Archetype.FileRef, Pcc)
                        ? subObjArchetype
                        : Pcc.getEntryOrAddImport($"{Default__Export.Archetype.InstancedFullPath}.{subPath}", classEntry.ObjectName.Instanced, classEntry.ParentName);
                }
            }

            PropertyCollection props = ConvertStatementsToPropertyCollection(subExport, subObject.Statements, new(parentSubObjectDict));

            if (subObject.Class.IsComponent)
            {
                Span<byte> preProps = stackalloc byte[16];
                const int templateOwnerClass = 0; //todo: When is this not 0?
                const int netIndex = 0; //should this be set to something?
                
                EndianBitConverter.WriteAsBytes(templateOwnerClass, preProps, Pcc.Endian);
                EndianBitConverter.WriteAsBytes(Pcc.FindNameOrAdd(subObject.Class.Name), preProps[4..], Pcc.Endian);
                EndianBitConverter.WriteAsBytes(0, preProps[8..], Pcc.Endian);
                EndianBitConverter.WriteAsBytes(netIndex, preProps[12..], Pcc.Endian);
                subExport.WritePrePropsAndPropertiesAndBinary(preProps.ToArray(), props, new GenericObjectBinary(Array.Empty<byte>()));
            }
            else
            {
                subExport.WriteProperties(props);
            }
        }

        private PropertyCollection ConvertStatementsToPropertyCollection(ExportEntry export, List<Statement> statements, Dictionary<NameReference, ExportEntry> subObjectDict)
        {
            var existingSubObjects = export.GetChildren<ExportEntry>().ToList();
            var props = new PropertyCollection();
            foreach (Statement subobjectStatement in statements)
            {
                switch (subobjectStatement)
                {
                    case Subobject subObj:
                        var subObjName = NameReference.FromInstancedString(subObj.Name.Name);
                        existingSubObjects.TryRemove(exp => exp.ObjectName == subObjName, out ExportEntry existingSubObject);
                        CreateSubObject(subObj, export, subObjectDict, ref existingSubObject);
                        subObjectDict.Add(subObjName, existingSubObject);
                        break;
                    case AssignStatement assignStatement:
                        Property prop = ConvertToProperty(assignStatement, subObjectDict);
                        props.AddOrReplaceProp(prop);
                        break;
                    default:
                        throw new Exception($"Unexpected statement type: {subobjectStatement.GetType().Name}");
                }
            }
            if (existingSubObjects.Any())
            {
                EntryPruner.TrashEntriesAndDescendants(existingSubObjects);
            }
            return props;
        }

        private IEntry GetClassDefaultObject(IEntry classEntry)
        {
            if (classEntry is ExportEntry export)
            {
                var classObj = export.GetBinaryData<UClass>();
                return classObj.Defaults.GetEntry(Pcc);
            }
            string parentPath = classEntry.ParentInstancedFullPath;
            return Pcc.getEntryOrAddImport($"{parentPath}.Default__{classEntry.ObjectName.Instanced}", classEntry.ObjectName.Instanced, classEntry.ParentName);
        }
    }
}