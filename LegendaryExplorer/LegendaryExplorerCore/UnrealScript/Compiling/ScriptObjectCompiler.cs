using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    public static class ScriptObjectCompiler
    {
        public static void Compile(ASTNode node, IEntry parent, UField existingObject = null)
        {
            switch (node)
            {
                case Class classAST:
                    if (existingObject is null or UClass)
                    {
                        UClass uClass = (UClass)existingObject;
                        Compile(classAST, parent, ref uClass);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UClass)}!");
                    }
                case Const constAST:
                    if (existingObject is null or UConst)
                    {
                        UConst uConst = (UConst)existingObject;
                        Compile(constAST, parent, ref uConst);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UConst)}!");
                    }
                case Enumeration enumAST:
                    if (existingObject is null or UEnum)
                    {
                        UEnum uEnum = (UEnum)existingObject;
                        Compile(enumAST, parent, ref uEnum);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UEnum)}!");
                    }
                case Function funcAST:
                    if (existingObject is null or UFunction)
                    {
                        UFunction uFunction = (UFunction)existingObject;
                        Compile(funcAST, parent, ref uFunction);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UFunction)}!");
                    }
                case State stateAST:
                    if (existingObject is null or UState)
                    {
                        UState uState = (UState)existingObject;
                        Compile(stateAST, parent, ref uState);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UState)}!");
                    }
                case Struct structAST:
                    if (existingObject is null or UScriptStruct)
                    {
                        UScriptStruct uScriptStruct = (UScriptStruct)existingObject;
                        Compile(structAST, parent, ref uScriptStruct);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UScriptStruct)}!");
                    }
                case VariableDeclaration varDeclAST:
                    if (existingObject is null or UProperty)
                    {
                        UProperty uProp = (UProperty)existingObject;
                        Compile(varDeclAST, parent, ref uProp);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UProperty)}!");
                    }
            }

            throw new ArgumentOutOfRangeException(nameof(node));
        }

        public static void Compile(Class classAST, IEntry parent, ref UClass classObj)
        {
            throw new NotImplementedException();
        }

        public static void Compile(State stateAST, IEntry parent, ref UState stateObj)
        {
            IEntry super = null;
            if (stateAST.Parent is not null)
            {
                super = CompilerUtils.ResolveState(stateAST.Parent, parent.FileRef);
            }

            var stateName = NameReference.FromInstancedString(stateAST.Name);
            ExportEntry stateExport;
            if (stateObj is null)
            {
                stateExport = CreateNewExport(stateName, "State", parent, new UState { ScriptBytes = Array.Empty<byte>(), LocalFunctionMap = new() }, super);
                stateObj = stateExport.GetBinaryData<UState>();
            }
            else
            {
                stateExport = stateObj.Export;
                if (stateExport.SuperClass != super)
                {
                    stateExport.SuperClass = super;
                }

                if (stateExport.ObjectName != stateName)
                {
                    stateExport.ObjectName = stateName;
                }
            }

            stateObj.StateFlags = stateAST.Flags;
            stateObj.ProbeMask = 0;
            stateObj.IgnoreMask = stateAST.IgnoreMask;
            stateObj.SuperClass = super?.UIndex ?? 0;


            //calculate probemask
            State curState = stateAST;
            while (curState is not null)
            {
                foreach (Function stateFunc in curState.Functions)
                {
                    if (Enum.TryParse(stateFunc.Name, true, out EProbeFunctions enumVal))
                    {
                        stateObj.ProbeMask |= enumVal;
                    }
                }

                curState = curState.Parent;
            }

            stateObj.LocalFunctionMap.Clear();

            UFunction prevFunc = null;
            var existingFuncs = GetMembers<UFunction>(stateObj).ToDictionary(uFunc => uFunc.Export.ObjectName.Instanced);
            stateObj.Children = 0;
            foreach (Function member in stateAST.Functions)
            {
                existingFuncs.Remove(member.Name, out UFunction childFunc);
                Compile(member, stateExport, ref childFunc);
                if (prevFunc is null)
                {
                    stateObj.Children = childFunc.Export;
                }
                else
                {
                    prevFunc.Next = childFunc.Export;
                    prevFunc.Export.WriteBinary(prevFunc);
                }

                stateObj.LocalFunctionMap.Add(childFunc.Export.ObjectName, childFunc.Export.UIndex);
                prevFunc = childFunc;
            }

            foreach (UFunction removedFunc in existingFuncs.Values)
            {
                EntryPruner.TrashEntryAndDescendants(removedFunc.Export);
            }

            if (prevFunc is not null)
            {
                prevFunc.Next = 0;
                prevFunc.Export.WriteBinary(prevFunc);
            }

            ByteCodeCompilerVisitor.Compile(stateAST, stateObj);
        }

        public static void Compile(Function funcAST, IEntry parent, ref UFunction funcObj)
        {
            IEntry super = null;
            if (funcAST.SuperFunction is not null)
            {
                super = CompilerUtils.ResolveFunction(funcAST.SuperFunction, parent.FileRef);
            }

            var functionName = NameReference.FromInstancedString(funcAST.Name);
            ExportEntry funcExport;
            if (funcObj is null)
            {
                funcExport = CreateNewExport(functionName, "Function", parent, new UFunction { ScriptBytes = Array.Empty<byte>(), FriendlyName = functionName }, super);
                funcObj = funcExport.GetBinaryData<UFunction>();
            }
            else
            {
                funcExport = funcObj.Export;
                if (funcExport.SuperClass != super)
                {
                    funcExport.SuperClass = super;
                }

                if (funcExport.ObjectName != functionName)
                {
                    funcExport.ObjectName = functionName;
                }
            }

            funcObj.FriendlyName = functionName;
            funcObj.FunctionFlags = funcAST.Flags;
            funcObj.NativeIndex = (ushort)funcAST.NativeIndex;
            funcObj.OperatorPrecedence = funcAST.OperatorPrecedence;
            funcObj.SuperClass = super?.UIndex ?? 0;

            var newMembers = new List<VariableDeclaration>();
            newMembers.AddRange(funcAST.Parameters);
            if (funcAST.ReturnValueDeclaration is not null)
            {
                newMembers.Add(funcAST.ReturnValueDeclaration);
            }

            newMembers.AddRange(funcAST.Locals);

            UProperty prevProp = null;
            using (var existingEnumerator = GetMembers<UField>(funcObj).GetEnumerator())
            {
                funcObj.Children = 0;
                foreach (VariableDeclaration member in newMembers)
                {
                    UProperty childProp = null;
                    while (existingEnumerator.MoveNext())
                    {
                        UField current = existingEnumerator.Current;
                        if (current.Export.ClassName == ByteCodeCompilerVisitor.PropertyTypeName(member.VarType))
                        {
                            childProp = (UProperty)current;
                            break;
                        }

                        EntryPruner.TrashEntryAndDescendants(current.Export);
                    }

                    Compile(member, funcExport, ref childProp);
                    if (prevProp is null)
                    {
                        funcObj.Children = childProp.Export;
                    }
                    else
                    {
                        prevProp.Next = childProp.Export;
                        prevProp.Export.WriteBinary(prevProp);
                    }

                    prevProp = childProp;
                }

                while (existingEnumerator.MoveNext())
                {
                    EntryPruner.TrashEntryAndDescendants(existingEnumerator.Current.Export);
                }
            }

            if (prevProp is not null)
            {
                prevProp.Next = 0;
                prevProp.Export.WriteBinary(prevProp);
            }

            ByteCodeCompilerVisitor.Compile(funcAST, funcObj);
        }

        public static void Compile(Struct structAST, IEntry parent, ref UScriptStruct structObj)
        {
            throw new NotImplementedException();
        }

        public static void Compile(Enumeration enumAST, IEntry parent, ref UEnum enumObj)
        {
            throw new NotImplementedException();
        }

        public static void Compile(VariableDeclaration varDeclAST, IEntry parent, ref UProperty propObj)
        {
            IMEPackage pcc = parent.FileRef;
            VariableType varType = varDeclAST.VarType;

            NameReference propName = NameReference.FromInstancedString(varDeclAST.Name);
            if (propObj is null)
            {
                string className = ByteCodeCompilerVisitor.PropertyTypeName(varType);
                UProperty tmp = className switch
                {
                    "BioMask4Property" => new UBioMask4Property(),
                    "ByteProperty" => new UByteProperty(),
                    "IntProperty" => new UIntProperty(),
                    "BoolProperty" => new UBoolProperty(),
                    "FloatProperty" => new UFloatProperty(),
                    "ClassProperty" => new UClassProperty(),
                    "ComponentProperty" => new UComponentProperty(),
                    "ObjectProperty" => new UObjectProperty(),
                    "NameProperty" => new UNameProperty(),
                    "DelegateProperty" => new UDelegateProperty(),
                    "InterfaceProperty" => new UInterfaceProperty(),
                    "StructProperty" => new UStructProperty(),
                    "StrProperty" => new UStrProperty(),
                    "MapProperty" => new UMapProperty(),
                    "StringRefProperty" => new UStringRefProperty(),
                    "ArrayProperty" => new UArrayProperty(),
                    _ => throw new ArgumentOutOfRangeException(nameof(className), className, "")
                };
                tmp.Category = "None";
                propObj = (UProperty)ObjectBinary.From(CreateNewExport(propName, className, parent, tmp));
            }
            else
            {
                if (propObj.Export.ObjectName != propName)
                {
                    propObj.Export.ObjectName = propName;
                }
            }

            propObj.ArraySize = varDeclAST.ArrayLength;
            propObj.PropertyFlags = varDeclAST.Flags;
            propObj.Category = NameReference.FromInstancedString(varDeclAST.Category);


            switch (propObj)
            {
                case UByteProperty uByteProperty:
                    uByteProperty.Enum = varType is Enumeration ? CompilerUtils.ResolveSymbol(varType, pcc).UIndex : 0;
                    break;
                case UClassProperty uClassProperty:
                    uClassProperty.ObjectRef = pcc.getEntryOrAddImport("Core.Class").UIndex;
                    uClassProperty.ClassRef = CompilerUtils.ResolveSymbol(((ClassType)varType).ClassLimiter, pcc).UIndex;
                    break;
                case UDelegateProperty uDelegateProperty:
                    uDelegateProperty.Function = CompilerUtils.ResolveFunction(((DelegateType)varType).DefaultFunction, pcc).UIndex;
                    string parentClassName = parent.ClassName;
                    if (parentClassName.CaseInsensitiveEquals("ArrayProperty"))
                    {
                        parentClassName = parent.Parent.ClassName;
                    }

                    uDelegateProperty.Delegate = parentClassName.CaseInsensitiveEquals("Function") ? uDelegateProperty.Function : 0;
                    break;
                case UMapProperty uMapProperty:
                    uMapProperty.KeyType = 0;
                    uMapProperty.ValueType = 0;
                    break;
                case UObjectProperty uObjectProperty:
                    uObjectProperty.ObjectRef = CompilerUtils.ResolveSymbol(varType, pcc).UIndex;
                    break;
                case UStructProperty uStructProperty:
                    uStructProperty.Struct = CompilerUtils.ResolveSymbol(varType, pcc).UIndex;
                    break;
                case UArrayProperty uArrayProperty:
                    UProperty child = null;
                    var dynArrType = (DynamicArrayType)varType;
                    VariableType elementType = dynArrType.ElementType;
                    if (pcc.TryGetUExport(uArrayProperty.ElementType ?? 0, out ExportEntry childExp))
                    {
                        if (childExp.ClassName == ByteCodeCompilerVisitor.PropertyTypeName(elementType))
                        {
                            child = (UProperty)ObjectBinary.From(pcc.GetUExport(uArrayProperty.ElementType));
                        }
                        else
                        {
                            EntryPruner.TrashEntryAndDescendants(childExp);
                        }
                    }

                    Compile(new VariableDeclaration(elementType, dynArrType.ElementPropertyFlags, propName), uArrayProperty.Export, ref child);
                    child.Export.WriteBinary(child);
                    uArrayProperty.ElementType = child.Export.UIndex;
                    break;

                    //have no properties beyond the base class
                    //case UIntProperty uIntProperty:
                    //    break;
                    //case UBoolProperty uBoolProperty:
                    //    break;
                    //case UFloatProperty uFloatProperty:
                    //    break;
                    //case UNameProperty uNameProperty:
                    //    break;
                    //case UStringRefProperty uStringRefProperty:
                    //    break;
                    //case UStrProperty uStrProperty:
                    //    break;

                    //have no properties of their own, handled by their base classes above
                    //case UComponentProperty uComponentProperty:
                    //    break;
                    //case UInterfaceProperty uInterfaceProperty:
                    //    break;
                    //case UBioMask4Property uBioMask4Property:
                    //    break;
            }

        }

        public static void Compile(Const constAST, IEntry parent, ref UConst constObj)
        {
            throw new NotImplementedException();
        }

        public static List<T> GetMembers<T>(UStruct obj) where T : UField
        {
            IMEPackage pcc = obj.Export.FileRef;

            var members = new List<T>();

            var nextItem = obj.Children;

            while (nextItem is not null && pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case T field:
                        nextItem = field.Next;
                        members.Add(field);
                        break;
                    default:
                        nextItem = null;
                        break;
                }
            }

            return members;
        }

        private static ExportEntry CreateNewExport(NameReference name, string className, IEntry parent, UField binary = null, IEntry super = null)
        {
            IMEPackage pcc = parent.FileRef;

            //reuse trash exports
            if (pcc.TryGetTrash(out ExportEntry trashExport))
            {
                trashExport.ObjectName = name;
                trashExport.Class = EntryImporter.EnsureClassIsInFile(pcc, className, new RelinkerOptionsPackage() { ImportExportDependencies = true });
                trashExport.SuperClass = super;
                trashExport.Parent = parent;
                trashExport.WritePrePropsAndPropertiesAndBinary(new byte[4], new PropertyCollection(), (ObjectBinary)binary ?? new GenericObjectBinary(new byte[0]));
                return trashExport;
            }

            var exp = new ExportEntry(pcc, parent, name, binary: binary, isClass: binary is UClass)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className, new RelinkerOptionsPackage() { ImportExportDependencies = true }),
                SuperClass = super
            };
            pcc.AddExport(exp);
            return exp;
        }

        private static IEntry ResolveSymbol(ASTNode node, IMEPackage pcc) =>
            node switch
            {
                Class cls => ResolveClass(cls, pcc),
                Struct strct => ResolveStruct(strct, pcc),
                State state => ResolveState(state, pcc),
                Function func => ResolveFunction(func, pcc),
                Enumeration @enum => ResolveEnum(@enum, pcc),
                StaticArrayType statArr => ResolveSymbol(statArr.ElementType, pcc),
                _ => throw new ArgumentOutOfRangeException(nameof(node))
            };

        private static IEntry ResolveEnum(Enumeration e, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(e.Outer, pcc).InstancedFullPath}.{e.Name}", "Enum");

        private static IEntry ResolveStruct(Struct s, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(s.Outer, pcc).InstancedFullPath}.{s.Name}", "ScriptStruct");

        private static IEntry ResolveFunction(Function f, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(f.Outer, pcc).InstancedFullPath}.{f.Name}", "Function");

        private static IEntry ResolveState(State s, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(s.Outer, pcc).InstancedFullPath}.{s.Name}", "State");

        private static IEntry ResolveClass(Class c, IMEPackage pcc)
        {
            var rop = new RelinkerOptionsPackage() { ImportExportDependencies = true }; // Might need to disable cache here depending on if that is desirable
            var entry = EntryImporter.EnsureClassIsInFile(pcc, c.Name, rop);
            if (rop.RelinkReport.Any())
            {
                throw new Exception($"Unable to resolve class '{c.Name}'! There were relinker errors: {string.Join("\n\t", rop.RelinkReport.Select(pair => pair.Message))}");
            }
            return entry;
        }
    }
}
