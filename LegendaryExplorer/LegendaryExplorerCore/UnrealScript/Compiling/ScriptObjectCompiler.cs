using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    public static class ScriptObjectCompiler
    {
        public static void Compile(ASTNode node, IMEPackage pcc, IEntry parent, UField existingObject = null, PackageCache packageCache = null, string gameRootOverride = null)
        {
            switch (node)
            {
                case Class classAST:
                    if (existingObject is null or UClass)
                    {
                        var uClass = (UClass)existingObject;
                        CompileClass(classAST, pcc, parent, ref uClass, packageCache, gameRootOverride: gameRootOverride);
                        uClass.Export.WriteBinary(uClass);
                        return;
                    }
                    throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UClass)}!");
                case Const constAST:
                    if (existingObject is null or UConst)
                    {
                        var uConst = (UConst)existingObject;
                        CompileConst(constAST, parent, ref uConst);
                        uConst.Export.WriteBinary(uConst);
                        return;
                    }
                    throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UConst)}!");
                case Enumeration enumAST:
                    if (existingObject is null or UEnum)
                    {
                        var uEnum = (UEnum)existingObject;
                        CompileEnum(enumAST, parent, ref uEnum);
                        uEnum.Export.WriteBinary(uEnum);
                        return;
                    }
                    throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UEnum)}!");
                case Function funcAST:
                    if (existingObject is null or UFunction)
                    {
                        var uFunction = (UFunction)existingObject;
                        CompileFunction(funcAST, parent, ref uFunction);
                        uFunction.Export.WriteBinary(uFunction);
                        return;
                    }
                    throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UFunction)}!");
                case State stateAST:
                    if (existingObject is null or UState)
                    {
                        var uState = (UState)existingObject;
                        CompileState(stateAST, parent, ref uState);
                        uState.Export.WriteBinary(uState);
                        return;
                    }
                    throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UState)}!");
                case Struct structAST:
                    if (existingObject is null or UScriptStruct)
                    {
                        var uScriptStruct = (UScriptStruct)existingObject;
                        CompileStruct(structAST, parent, ref uScriptStruct, packageCache);
                        uScriptStruct.Export.WriteBinary(uScriptStruct);
                        return;
                    }
                    throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UScriptStruct)}!");
                case VariableDeclaration varDeclAST:
                    if (existingObject is null or UProperty)
                    {
                        var uProp = (UProperty)existingObject;
                        CompileProperty(varDeclAST, parent, ref uProp);
                        uProp.Export.WriteBinary(uProp);
                        return;
                    }
                    throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UProperty)}!");
            }
            throw new ArgumentOutOfRangeException(nameof(node));
        }

        private static void CompileClass(Class classAST, IMEPackage pcc, IEntry parent, ref UClass classObj, PackageCache packageCache = null, string gameRootOverride = null)
        {
            var finishClassCompilation = CreateClassStub(classAST, pcc, parent, ref classObj, packageCache, gameRootOverride);

            finishClassCompilation();
        }

        public static Action CreateClassStub(Class classAST, IMEPackage pcc, IEntry parent, ref UClass refClassObj, PackageCache packageCache = null, string gameRootOverride = null, Func<IMEPackage, string, IEntry> missingObjectResolver = null)
        {
            var className = NameReference.FromInstancedString(classAST.Name);
            ExportEntry classExport;
            if (refClassObj is null)
            {
                classExport = CreateNewExport(pcc, className, "Class", parent, UClass.Create(), useTrash: false);
                refClassObj = classExport.GetBinaryData<UClass>();
                classExport.ObjectFlags = EObjectFlags.Public | EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit | EObjectFlags.Standalone;
            }
            else
            {
                classExport = refClassObj.Export;
                classExport.ObjectName = className;
            }
            UClass classObj = refClassObj;

            classObj.IgnoreMask = (EProbeFunctions)ulong.MaxValue;
            classObj.LabelTableOffset = ushort.MaxValue;
            classObj.ClassConfigName = NameReference.FromInstancedString(classAST.ConfigName);

            classObj.ClassFlags = classAST.Flags;
            if (classAST.Parent is Class parentClass)
            {
                //loop in case we are compiling multiple classes at once and our direct parent has not inherited flags yet
                do
                {
                    classObj.ClassFlags |= parentClass.Flags & EClassFlags.Inherit;
                    if (classObj.ClassFlags.Has(EClassFlags.Config) && classObj.ClassConfigName.Name.CaseInsensitiveEquals("None"))
                    {
                        classObj.ClassConfigName = NameReference.FromInstancedString(parentClass.ConfigName);
                    }
                    parentClass = parentClass.Parent as Class;
                } while (parentClass is not null);
            }

            if (classObj.ClassFlags.Has(EClassFlags.Native))
            {
                classExport.ObjectFlags |= EObjectFlags.Native;
            }

            //calculate probemask
            classObj.ProbeMask = 0;
            Class curClass = classAST;
            while (curClass is not null)
            {
                foreach (Function func in curClass.Functions)
                {
                    if (func.IsDefined && Enum.TryParse(func.Name, true, out EProbeFunctions enumVal))
                    {
                        classObj.ProbeMask |= enumVal;
                    }
                }
                curClass = curClass.Parent as Class;
            }

            //set StateFlags to Auto if there is no Auto state in the class
            curClass = classAST;
            bool hasAutoState = false;
            while (curClass is not null)
            {
                foreach (State state in curClass.States)
                {
                    hasAutoState |= state.Flags.Has(EStateFlags.Auto);
                }
                curClass = curClass.Parent as Class;
            }
            classObj.StateFlags = hasAutoState ? EStateFlags.None : EStateFlags.Auto;

            (CaseInsensitiveDictionary<UConst> existingConsts, CaseInsensitiveDictionary<UEnum> existingEnums, CaseInsensitiveDictionary<UScriptStruct> existingStructs,
                CaseInsensitiveDictionary<UProperty> existingProperties, CaseInsensitiveDictionary<UFunction> existingFunctions, CaseInsensitiveDictionary<UState> existingStates)
                = GetClassMembers(classObj);

            //Stub out all the child exports, and trash existing ones that don't get re-used

            var completions = new List<Action>();
            var compiledConsts = new List<UConst>();
            var compiledEnums = new List<UEnum>();
            var compiledStructs = new List<UScriptStruct>();
            bool childrenHaveBeenTrashed = false;
            bool childrenHaveBeenAdded = false;
            foreach (VariableType typeDeclaration in classAST.TypeDeclarations)
            {
                switch (typeDeclaration)
                {
                    case Const @const:
                        existingConsts.Remove(@const.Name, out UConst uConst);
                        childrenHaveBeenAdded |= uConst is null;
                        CompileConst(@const, classExport, ref uConst);
                        compiledConsts.Add(uConst);
                        break;
                    case Enumeration enumeration:
                        existingEnums.Remove(enumeration.Name, out UEnum uEnum);
                        childrenHaveBeenAdded |= uEnum is null;
                        CompileEnum(enumeration, classExport, ref uEnum);
                        compiledEnums.Add(uEnum);
                        break;
                    case Struct @struct:
                        existingStructs.Remove(@struct.Name, out UScriptStruct uScriptStruct);
                        childrenHaveBeenAdded |= uScriptStruct is null;
                        completions.Add(CreateStructStub(@struct, classExport, ref uScriptStruct, packageCache));
                        compiledStructs.Add(uScriptStruct);
                        break;
                }
            }
            foreach (UField unusedField in existingConsts.Values.Cast<UField>().Concat(existingEnums.Values).Concat(existingStructs.Values))
            {
                childrenHaveBeenTrashed = true;
                EntryPruner.TrashEntryAndDescendants(unusedField.Export);
            }

            //UMap instead of a Dictionary, since UMap is guaranteed to enumerate in insertion order if nothing has been removed
            var compiledProperties = new UMap<string, UProperty>();
            foreach (VariableDeclaration property in classAST.VariableDeclarations)
            {
                if (existingProperties.Remove(property.Name, out UProperty uProperty) && !uProperty.Export.ClassName.CaseInsensitiveEquals(ByteCodeCompilerVisitor.PropertyTypeName(property.VarType)))
                {
                    //do not mess with mapproperties
                    if (uProperty is not UMapProperty)
                    {
                        //whoops, it's a different type now! We cannot reuse this. Put it back so that it can be trashed.
                        existingProperties.Add(property.Name, uProperty);
                        uProperty = null;
                    }
                }
                childrenHaveBeenAdded |= uProperty is null;
                completions.Add(CreatePropertyStub(property, classExport, ref uProperty));
                compiledProperties.Add(property.Name, uProperty);
            }
            foreach (UProperty unusedField in existingProperties.Values)
            {
                childrenHaveBeenTrashed = true;
                EntryPruner.TrashEntryAndDescendants(unusedField.Export);
            }

            var compiledFunctions = new List<UFunction>();
            classObj.LocalFunctionMap.Clear();
            foreach (Function function in classAST.Functions)
            {
                existingFunctions.Remove(function.Name, out UFunction uFunction);
                childrenHaveBeenAdded |= uFunction is null;
                completions.Add(CreateFunctionStub(function, classExport, ref uFunction, missingObjectResolver));
                compiledFunctions.Add(uFunction);
                classObj.LocalFunctionMap.Add(uFunction.Export.ObjectName, uFunction.Export.UIndex);
            }
            foreach (UFunction unusedField in existingFunctions.Values)
            {
                childrenHaveBeenTrashed = true;
                EntryPruner.TrashEntryAndDescendants(unusedField.Export);
            }

            var compiledStates = new List<UState>();
            foreach (State state in classAST.States)
            {
                existingStates.Remove(state.Name, out UState uState);
                childrenHaveBeenAdded |= uState is null;
                completions.Add(CreateStateStub(state, classExport, ref uState, missingObjectResolver));
                compiledStates.Add(uState);
            }
            foreach (UState unusedField in existingStates.Values)
            {
                childrenHaveBeenTrashed = true;
                EntryPruner.TrashEntryAndDescendants(unusedField.Export);
            }

            //make defaults stub
            if (classObj.Defaults is 0)
            {
                var defaultsExportObjectName = new NameReference($"Default__{classExport.ObjectNameString}", classExport.indexValue);
                //do not reuse trash for new defaults export. This is to ensure the defaults ends up right after the new class in the tree view
                var defaultsExport = new ExportEntry(pcc, classExport.Parent, defaultsExportObjectName);
                pcc.AddExport(defaultsExport);
                classObj.Defaults = defaultsExport.UIndex;
            }

            return FinishClassCompilation;

            void FinishClassCompilation()
            {
                IEntry superClass = classAST.Parent is Class super ? CompilerUtils.ResolveClass(super, pcc) : null;
                classExport.SuperClass = superClass;
                classObj.SuperClass = superClass?.UIndex ?? 0;
                classObj.OuterClass = classAST.OuterClass is Class outerClass ? CompilerUtils.ResolveClass(outerClass, pcc).UIndex : 0;

                if (pcc.Game.IsGame3())
                {
                    classObj.VirtualFunctionTable = classAST.VirtualFunctionTable.Select(func => CompilerUtils.ResolveFunction(func, pcc).UIndex).ToArray();
                }

                if (classAST.ReplicationBlock.Statements.Count is 0)
                {
                    classObj.ScriptBytecodeSize = 0;
                    classObj.ScriptBytes = [];
                }
                else
                {
                    //must occur before the property stubs finish compiling so that replication offsets can be set
                    ByteCodeCompilerVisitor.Compile(classAST, classObj, missingObjectResolver);
                }

                //finish compiling all the stubs
                foreach (Action completion in completions)
                {
                    completion();
                }

                IEnumerable<UField> allChildren = compiledProperties.Values.Cast<UField>().Concat(compiledFunctions).Concat(compiledStates).Concat(compiledStructs).Concat(compiledEnums).Concat(compiledConsts);
                if (childrenHaveBeenAdded || childrenHaveBeenTrashed)
                {
                    classObj.Children = 0;
                    UField prev = null;
                    foreach (UField current in allChildren)
                    {
                        AdvanceField(ref prev, current, classObj);
                    }
                    if (prev is not null)
                    {
                        prev.Next = 0;
                        prev.Export.WriteBinary(prev);
                    }
                }
                else
                {
                    foreach (UField field in allChildren)
                    {
                        field.Export.WriteBinary(field);
                    }
                }

                //todo: figure out what these are. Might be able to get away with not touching them. Existing classes will retain their values, and custom ones will be 0/empty
                //classObj.unk2
                //classObj.le2ps3me2Unknown
                //I think these two are editor information. Categories of properties to automatically hide/expand?
                //classObj.unkNameList1
                //classObj.unkNameList2

                classObj.Interfaces.Clear();
                foreach (Class interfaceClass in classAST.Interfaces.OfType<Class>())
                {
                    var vfTablePropertyUIndex = 0;
                    if (interfaceClass.IsNative)
                    {
                        if (!compiledProperties.TryGetValue($"VfTable_I{interfaceClass.Name}", out UProperty vfTableProperty))
                        {
                            throw new Exception($"Missing VfTable_I{interfaceClass.Name} property for native interface '{interfaceClass.Name}' in class '{className}'");
                        }
                        vfTablePropertyUIndex = vfTableProperty.Export.UIndex;
                    }
                    classObj.Interfaces.Add(new UClass.ImplementedInterface(CompilerUtils.ResolveClass(interfaceClass, pcc).UIndex, vfTablePropertyUIndex));
                }

                classObj.DLLBindName = "None";

                var defaultsExport = pcc.GetEntry(classObj.Defaults) as ExportEntry;
                ScriptPropertiesCompiler.CompileDefault__Object(classAST.DefaultProperties, classExport, ref defaultsExport, packageCache, gameRootOverride, missingObjectResolver);
                classObj.Defaults = defaultsExport.UIndex;

                //classObj.ComponentNameToDefaultObjectMap.Clear();
                //foreach (Subobject subobject in classAST.DefaultProperties.Statements.OfType<Subobject>())
                //{
                //    if (subobject.Class.IsComponent)
                //    {
                //        var componentName = NameReference.FromInstancedString(subobject.NameDeclaration.Name);
                //        classObj.ComponentNameToDefaultObjectMap.Add(componentName, pcc.FindExport($"{defaultsExport.InstancedFullPath}.{componentName}"));
                //    }
                //}

                GlobalUnrealObjectInfo.AddOrReplaceClassInDB(classObj);
            }
        }

        private static void CompileState(State stateAST, IEntry parent, ref UState stateObj)
        {
            var finishStateCompilation = CreateStateStub(stateAST, parent, ref stateObj);

            finishStateCompilation();
        }

        private static Action CreateStateStub(State stateAST, IEntry parent, ref UState refStateObj, Func<IMEPackage, string, IEntry> missingObjectResolver = null)
        {
            var stateName = NameReference.FromInstancedString(stateAST.Name);
            ExportEntry stateExport;
            if (refStateObj is null)
            {
                stateExport = CreateNewExport(parent.FileRef, stateName, "State", parent, UState.Create());
                refStateObj = stateExport.GetBinaryData<UState>();
            }
            else
            {
                stateExport = refStateObj.Export;
                if (stateExport.ObjectName != stateName)
                {
                    stateExport.ObjectName = stateName;
                }
            }
            UState stateObj = refStateObj;

            stateObj.StateFlags = stateAST.Flags;
            stateObj.ProbeMask = 0;
            stateObj.IgnoreMask = stateAST.IgnoreMask;

            //calculate probemask
            State curState = stateAST;
            while (curState is not null)
            {
                foreach (Function stateFunc in curState.Functions)
                {
                    if (stateFunc.IsDefined && Enum.TryParse(stateFunc.Name, true, out EProbeFunctions enumVal))
                    {
                        stateObj.ProbeMask |= enumVal;
                    }
                }
                curState = curState.Parent;
            }

            stateObj.LocalFunctionMap.Clear();

            var existingFuncs = GetMembers<UFunction>(stateObj).ToDictionary(uFunc => uFunc.Export.ObjectName.Instanced);
            stateObj.Children = 0;
            var functionCompletions = new List<Action>();
            var functions = new List<UFunction>();
            foreach (Function member in stateAST.Functions)
            {
                existingFuncs.Remove(member.Name, out UFunction childFunc);
                functionCompletions.Add(CreateFunctionStub(member, stateObj.Export, ref childFunc, missingObjectResolver));
                functions.Add(childFunc);
                stateObj.LocalFunctionMap.Add(childFunc.Export.ObjectName, childFunc.Export.UIndex);
            }
            foreach (UFunction removedFunc in existingFuncs.Values)
            {
                EntryPruner.TrashEntryAndDescendants(removedFunc.Export);
            }

            return FinishStateCompilation;

            void FinishStateCompilation()
            {
                IEntry super = null;
                if (stateAST.Parent is not null)
                {
                    super = CompilerUtils.ResolveState(stateAST.Parent, stateObj.Export.FileRef);
                }
                stateObj.SuperClass = super?.UIndex ?? 0;
                stateObj.Export.SuperClass = super;

                foreach (Action functionCompletion in functionCompletions)
                {
                    functionCompletion();
                }

                UField prevFunc = null;
                foreach (UFunction childFunc in functions)
                {
                    AdvanceField(ref prevFunc, childFunc, stateObj);
                }
                if (prevFunc is not null)
                {
                    prevFunc.Next = 0;
                    prevFunc.Export.WriteBinary(prevFunc);
                }

                ByteCodeCompilerVisitor.Compile(stateAST, stateObj, missingObjectResolver);
            }
        }

        private static void CompileFunction(Function funcAST, IEntry parent, ref UFunction funcObj)
        {
            Action finishFunctionCompilation = CreateFunctionStub(funcAST, parent, ref funcObj);

            finishFunctionCompilation();
        }

        private static Action CreateFunctionStub(Function funcAST, IEntry parent, ref UFunction refFuncObj, Func<IMEPackage, string, IEntry> missingObjectResolver = null)
        {
            var functionName = NameReference.FromInstancedString(funcAST.Name);
            ExportEntry funcExport;

            if (refFuncObj is null)
            {
                funcExport = CreateNewExport(parent.FileRef, functionName, "Function", parent, new UFunction { ScriptBytes = [], FriendlyName = functionName });
                refFuncObj = funcExport.GetBinaryData<UFunction>();
            }
            else
            {
                funcExport = refFuncObj.Export;
                if (funcExport.ObjectName != functionName)
                {
                    funcExport.ObjectName = functionName;
                }
            }
            UFunction funcObj = refFuncObj;

            funcObj.FriendlyName = functionName;
            funcObj.FunctionFlags = funcAST.Flags;
            funcObj.NativeIndex = (ushort)funcAST.NativeIndex;
            funcObj.OperatorPrecedence = funcAST.OperatorPrecedence;

            var newMembers = new List<VariableDeclaration>();
            newMembers.AddRange(funcAST.Parameters);
            if (funcAST.ReturnValueDeclaration is not null)
            {
                newMembers.Add(funcAST.ReturnValueDeclaration);
            }
            newMembers.AddRange(funcAST.Locals);

            var properties = new List<UProperty>();
            var propertyCompletions = new List<Action>();

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
                    propertyCompletions.Add(CreatePropertyStub(member, funcObj.Export, ref childProp));
                    properties.Add(childProp);
                }
                while (existingEnumerator.MoveNext())
                {
                    EntryPruner.TrashEntryAndDescendants(existingEnumerator.Current.Export);
                }
            }

            return FinishFunctionCompilation;

            void FinishFunctionCompilation()
            {
                IEntry super = null;
                if (funcAST.SuperFunction is not null)
                {
                    super = CompilerUtils.ResolveFunction(funcAST.SuperFunction, parent.FileRef);
                }
                if (funcExport.SuperClass != super)
                {
                    funcExport.SuperClass = super;
                }
                funcObj.SuperClass = super?.UIndex ?? 0;

                UField prevProp = null;

                foreach (Action propertyCompletion in propertyCompletions)
                {
                    propertyCompletion();
                }

                foreach (UProperty childProp in properties)
                {
                    AdvanceField(ref prevProp, childProp, funcObj);
                }

                if (prevProp is not null)
                {
                    prevProp.Next = 0;
                    prevProp.Export.WriteBinary(prevProp);
                }

                ByteCodeCompilerVisitor.Compile(funcAST, funcObj, missingObjectResolver);
            }
        }

        private static void CompileStruct(Struct structAST, IEntry parent, ref UScriptStruct structObj, PackageCache packageCache = null)
        {
            Action finishStructCompilation = CreateStructStub(structAST, parent, ref structObj, packageCache);

            finishStructCompilation();
        }

        private static Action CreateStructStub(Struct structAST, IEntry parent, ref UScriptStruct refStructObj, PackageCache packageCache = null)
        {
            var structName = NameReference.FromInstancedString(structAST.Name);
            ExportEntry structExport;
            if (refStructObj is null)
            {
                structExport = CreateNewExport(parent.FileRef, structName, "ScriptStruct", parent, UScriptStruct.Create());
                refStructObj = structExport.GetBinaryData<UScriptStruct>();
            }
            else
            {
                structExport = refStructObj.Export;
                structExport.ObjectName = structName;
            }
            refStructObj.StructFlags = structAST.Flags;
            UScriptStruct structObj = refStructObj;

            (CaseInsensitiveDictionary<UScriptStruct> existingSubStructs, CaseInsensitiveDictionary<UProperty> existingProps) = GetMembers<UScriptStruct, UProperty>(structObj);
            structObj.Children = 0;

            var subStructs = new List<UField>();
            var compilationCompletions = new List<Action>();
            foreach (Struct subStructAST in structAST.TypeDeclarations.OfType<Struct>())
            {
                existingSubStructs.Remove(subStructAST.Name, out UScriptStruct subStruct);
                compilationCompletions.Add(CreateStructStub(subStructAST, structObj.Export, ref subStruct, packageCache));
                subStructs.Add(subStruct);
            }
            foreach (UScriptStruct removedSubStruct in existingSubStructs.Values)
            {
                EntryPruner.TrashEntryAndDescendants(removedSubStruct.Export);
            }
            var properties = new List<UField>();
            foreach (VariableDeclaration member in structAST.VariableDeclarations)
            {
                existingProps.Remove(member.Name, out UProperty current);
                if (current is not null && !current.Export.ClassName.CaseInsensitiveEquals(ByteCodeCompilerVisitor.PropertyTypeName(member.VarType)))
                {
                    EntryPruner.TrashEntryAndDescendants(current.Export);
                    current = null;
                }
                compilationCompletions.Add(CreatePropertyStub(member, structObj.Export, ref current));
                properties.Add(current);
            }
            foreach (UProperty removedProp in existingProps.Values)
            {
                EntryPruner.TrashEntryAndDescendants(removedProp.Export);
            }

            return FinishStructCompilation;

            void FinishStructCompilation()
            {
                IMEPackage pcc = structObj.Export.FileRef;
                IEntry super = null;
                if (structAST.Parent is Struct parentStruct)
                {
                    super = CompilerUtils.ResolveStruct(parentStruct, pcc);
                }
                structObj.SuperClass = super?.UIndex ?? 0;
                structObj.Export.SuperClass = super;

                foreach (Action completion in compilationCompletions)
                {
                    completion();
                }

                UField prevField = null;
                var allfields = pcc.Game <= MEGame.ME2 ? subStructs.Concat(properties) : properties.Concat(subStructs);
                foreach (UField currentField in allfields)
                {
                    AdvanceField(ref prevField, currentField, structObj);
                }
                if (prevField is not null)
                {
                    prevField.Next = 0;
                    prevField.Export.WriteBinary(prevField);
                }

                PropertyCollection fullDefaults = structAST.GetDefaultPropertyCollection(pcc, false, packageCache);
                //normal structs have all their properties in their defaults
                if (structAST.Parent is null)
                {
                    structObj.Defaults = fullDefaults;
                }
                //structs with a parent have only the properties which differ from their default values
                //(for StructProperties, the difference is calculated from the default values of their members, NOT the structdefaultproperties values)
                else
                {
                    var baseDefaults = structAST.MakeBaseProps(pcc, packageCache, false);
                    structObj.Defaults = fullDefaults.Diff(baseDefaults);
                }
            }
        }

        private static void CompileProperty(VariableDeclaration varDeclAST, IEntry parent, ref UProperty propObj)
        {
            Action finishPropertyCompilation = CreatePropertyStub(varDeclAST, parent, ref propObj);

            finishPropertyCompilation();
        }

        private static Action CreatePropertyStub(VariableDeclaration varDeclAST, IEntry parent, ref UProperty refPropObj)
        {
            IMEPackage pcc = parent.FileRef;
            VariableType varType = varDeclAST.VarType;
            if (varType is StaticArrayType staticArrayType)
            {
                varType = staticArrayType.ElementType;
            }

            var propName = NameReference.FromInstancedString(varDeclAST.Name);
            if (refPropObj is null)
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
                refPropObj = (UProperty)ObjectBinary.From(CreateNewExport(parent.FileRef, propName, className, parent, tmp));
            }
            else
            {
                if (refPropObj.Export.ObjectName != propName)
                {
                    refPropObj.Export.ObjectName = propName;
                }
            }
            UProperty propObj = refPropObj;

            propObj.ArraySize = varDeclAST.ArrayLength;
            propObj.PropertyFlags = varDeclAST.Flags;
            propObj.Category = NameReference.FromInstancedString(varDeclAST.Category);

            return FinishPropertyCompilation;

            void FinishPropertyCompilation()
            {
                propObj.ReplicationOffset = varDeclAST.ReplicationOffset;
                switch (propObj)
                {
                    case UByteProperty uByteProperty:
                        uByteProperty.Enum = varType is Enumeration ? CompilerUtils.ResolveSymbol(varType, pcc).UIndex : 0;
                        break;
                    case UClassProperty uClassProperty:
                        uClassProperty.ObjectRef = pcc.GetEntryOrAddImport("Core.Class", "Class").UIndex;
                        uClassProperty.ClassRef = CompilerUtils.ResolveSymbol(((ClassType)varType).ClassLimiter, pcc).UIndex;
                        break;
                    case UDelegateProperty uDelegateProperty:
                        uDelegateProperty.Function = CompilerUtils.ResolveFunction(((DelegateType)varType).DefaultFunction, pcc).UIndex;
                        uDelegateProperty.Delegate = varDeclAST.Name.EndsWith("__Delegate") ? 0 : uDelegateProperty.Function;
                        break;
                    case UMapProperty uMapProperty:
                        uMapProperty.KeyType = 0;
                        uMapProperty.ValueType = 0;
                        break;
                    case UObjectProperty uObjectProperty:
                        if (propObj.PropertyFlags.Has(EPropertyFlags.EditInline) && propObj.PropertyFlags.Has(EPropertyFlags.ExportObject) && !propObj.PropertyFlags.Has(EPropertyFlags.Component))
                        {
                            propObj.PropertyFlags |= EPropertyFlags.NeedCtorLink;
                        }
                        uObjectProperty.ObjectRef = CompilerUtils.ResolveSymbol(varType, pcc).UIndex;
                        break;
                    case UStructProperty uStructProperty:
                        uStructProperty.Struct = CompilerUtils.ResolveSymbol(varType, pcc).UIndex;
                        break;
                    case UArrayProperty uArrayProperty:
                        UProperty child = null;
                        var dynArrType = (DynamicArrayType)varType;
                        VariableType elementType = dynArrType.ElementType;
                        if (pcc.TryGetUExport(uArrayProperty.ElementType, out ExportEntry childExp))
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
                        CompileProperty(new VariableDeclaration(elementType, dynArrType.ElementPropertyFlags, propName), uArrayProperty.Export, ref child);
                        //propogate certain flags to inner prop
                        child.PropertyFlags |= uArrayProperty.PropertyFlags & (EPropertyFlags.ExportObject | EPropertyFlags.EditInline | EPropertyFlags.EditInlineUse | EPropertyFlags.Localized
                                                                               | EPropertyFlags.Component | EPropertyFlags.Config | EPropertyFlags.EditConst | EPropertyFlags.AlwaysInit
                                                                               | EPropertyFlags.Deprecated | EPropertyFlags.SerializeText | EPropertyFlags.CrossLevel);
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
        }

        private static void CompileEnum(Enumeration enumAST, IEntry parent, ref UEnum enumObj)
        {
            var enumName = NameReference.FromInstancedString(enumAST.Name);
            ExportEntry enumExport;
            if (enumObj is null)
            {
                enumExport = CreateNewExport(parent.FileRef, enumName, "Enum", parent, UEnum.Create());
                enumObj = enumExport.GetBinaryData<UEnum>();
            }
            else
            {
                enumExport = enumObj.Export;
                enumExport.ObjectName = enumName;
            }
            var values = enumAST.Values.Select(ev => NameReference.FromInstancedString(ev.Name)).ToList();
            values.Add(enumAST.GenerateMaxName());
            enumObj.Names = values.ToArray();
        }

        private static void CompileConst(Const constAST, IEntry parent, ref UConst constObj)
        {
            var constName = NameReference.FromInstancedString(constAST.Name);
            ExportEntry constExport;
            if (constObj is null)
            {
                constExport = CreateNewExport(parent.FileRef, constName, "Const", parent, UConst.Create());
                constObj = constExport.GetBinaryData<UConst>();
            }
            else
            {
                constExport = constObj.Export;
                constExport.ObjectName = constName;
            }
            constObj.Value = constAST.Value;
        }

        private static void AdvanceField(ref UField field, UField current, UStruct uStruct)
        {
            if (field is null)
            {
                uStruct.Children = current.Export.UIndex;
            }
            else
            {
                field.Next = current.Export.UIndex;
                field.Export.WriteBinary(field);
            }
            field = current;
        }

        private static List<T> GetMembers<T>(UStruct obj) where T : UField
        {
            IMEPackage pcc = obj.Export.FileRef;

            var members = new List<T>();

            var nextItem = obj.Children;

            while (nextItem is not 0 && pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case T field:
                        nextItem = field.Next;
                        members.Add(field);
                        break;
                    case UField field:
                        nextItem = field.Next;
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }
            return members;
        }

        private static (CaseInsensitiveDictionary<T>, CaseInsensitiveDictionary<U>) GetMembers<T, U>(UStruct obj) where T : UField where U : UField
        {
            IMEPackage pcc = obj.Export.FileRef;

            var membersT = new CaseInsensitiveDictionary<T>();
            var membersU = new CaseInsensitiveDictionary<U>();

            var nextItem = obj.Children;

            while (nextItem is not 0 && pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case T tField:
                        nextItem = tField.Next;
                        membersT.Add(tField.Export.ObjectName.Instanced, tField);
                        break;
                    case U uField:
                        nextItem = uField.Next;
                        membersU.Add(uField.Export.ObjectName.Instanced, uField);
                        break;
                    case UField field:
                        nextItem = field.Next;
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }
            return (membersT, membersU);
        }

        private static (CaseInsensitiveDictionary<UConst>, CaseInsensitiveDictionary<UEnum>, CaseInsensitiveDictionary<UScriptStruct>,
            CaseInsensitiveDictionary<UProperty>, CaseInsensitiveDictionary<UFunction>, CaseInsensitiveDictionary<UState>)
            GetClassMembers(UClass obj)
        {
            IMEPackage pcc = obj.Export.FileRef;

            var constMembers = new CaseInsensitiveDictionary<UConst>();
            var enumMembers = new CaseInsensitiveDictionary<UEnum>();
            var structMembers = new CaseInsensitiveDictionary<UScriptStruct>();
            var propMembers = new CaseInsensitiveDictionary<UProperty>();
            var funcMembers = new CaseInsensitiveDictionary<UFunction>();
            var stateMembers = new CaseInsensitiveDictionary<UState>();

            var nextItem = obj.Children;

            while (nextItem is not 0 && pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                string objName = objBin?.Export.ObjectName.Instanced;
                if (objBin is UField uField)
                {
                    nextItem = uField.Next;
                    switch (objBin)
                    {
                        case UConst uConst:
                            constMembers.Add(objName, uConst);
                            break;
                        case UEnum uEnum:
                            enumMembers.Add(objName, uEnum);
                            break;
                        case UScriptStruct uScriptStruct:
                            structMembers.Add(objName, uScriptStruct);
                            break;
                        case UProperty uProperty:
                            propMembers.Add(objName, uProperty);
                            break;
                        case UFunction uFunction:
                            funcMembers.Add(objName, uFunction);
                            break;
                        case UState uState:
                            stateMembers.Add(objName, uState);
                            break;
                    }
                }
                else
                {
                    break;
                }
            }
            return (constMembers, enumMembers, structMembers, propMembers, funcMembers, stateMembers);
        }

        private static ExportEntry CreateNewExport(IMEPackage pcc, NameReference name, string className, IEntry parent, UField binary = null, IEntry super = null, bool useTrash = true)
        {
            IEntry classEntry = className.CaseInsensitiveEquals("Class") ? null : EntryImporter.EnsureClassIsInFile(pcc, className, new RelinkerOptionsPackage());

            //reuse trash exports
            if (useTrash && pcc.TryGetTrash(out ExportEntry trashExport))
            {
                trashExport.ObjectName = name;
                trashExport.Class = classEntry;
                trashExport.SuperClass = super;
                trashExport.Parent = parent;
                trashExport.Archetype = null;
                trashExport.WritePrePropsAndPropertiesAndBinary(new byte[4], className == "Class" ? null : new PropertyCollection(), (ObjectBinary)binary ?? new GenericObjectBinary([]));
                return trashExport;
            }

            var exp = new ExportEntry(pcc, parent, name, binary: binary, isClass: binary is UClass)
            {
                Class = classEntry,
                SuperClass = super
            };

            // This is set after object initialization and looks at the parent.
            exp.ExportFlags = exp.IsForcedExport ? EExportFlags.ForcedExport : 0;
            pcc.AddExport(exp);
            return exp;
        }
    }
}
