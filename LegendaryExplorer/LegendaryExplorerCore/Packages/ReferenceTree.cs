using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LegendaryExplorerCore.Packages
{
    /// <inheritdoc/>
    public class ReferenceTree : ReferenceTreeBase<ReferenceTree>;

    /// <summary>
    /// Tree of references
    /// </summary>
    [DebuggerDisplay("{Entry.InstancedFullPath}")]
    public class ReferenceTreeBase<TSelf> where TSelf : ReferenceTreeBase<TSelf>, new()
    {
        /// <summary>
        /// The export this tree node represents.
        /// </summary>
        public IEntry Entry { get; set; }

        /// <summary>
        /// Items that reference this export. Reference trees do not change after creation and don't need to be ObservableCollections.
        /// </summary>
        public List<TSelf> Children { get; } = [];

        /// <summary>
        /// The next link the in the reference chain to the root item we are calculating the references for. If null, we are the root node.
        /// </summary>
        public TSelf Parent { get; set; }

        public static TSelf CalculateReferenceTree(IEntry entry)
        {
            var pcc = entry.FileRef;

            var rt = new TSelf
            {
                Entry = entry
            };

            var referenceMap = new Dictionary<int, List<IEntry>>(pcc.ImportCount + pcc.ExportCount);



            // 1. Calculate all outbound references
            foreach (ImportEntry imp in entry.FileRef.Imports)
            {
                if (imp == entry)
                    continue;
                foreach (int reference in GetObjectReferences(imp))
                {
                    referenceMap.AddToListAt(reference, imp);
                }
            }

            foreach (ExportEntry exp in entry.FileRef.Exports)
            {
                if (exp == entry)
                    continue;
                foreach (int reference in GetObjectReferences(exp))
                {
                    referenceMap.AddToListAt(reference, exp);
                }
            }

            // Draw the rest of the owl

            // Don't go too deep. There will be circular dependencies, even across levels.
            int maxDepth = 100;
            int currentDepth = 0;
            // First level
            List<TSelf> levelNodes = [rt];

            // Used to link up to a higher branch and prune duplicates
            var levels = new List<Dictionary<int, TSelf>>();

            while (currentDepth < maxDepth && !levelNodes.IsEmpty())
            {
                Debug.WriteLine($"Current depth: {currentDepth}");
                levelNodes = AddReferenceLeaves(currentDepth, levelNodes, referenceMap, levels);

                var levelDict = new Dictionary<int, TSelf>();
                foreach (TSelf node in levelNodes)
                {
                    // Only add the first one. This makes it so it favors the first ones and not the last ones
                    levelDict.TryAdd(node.Entry.UIndex, node);
                }

                levels.Add(levelDict);
                currentDepth++;
            }

            return rt;
        }

        private static List<TSelf> AddReferenceLeaves(int level, List<TSelf> levelNodes,
            Dictionary<int, List<IEntry>> referenceMap, List<Dictionary<int, TSelf>> higherLevels)
        {
            List<TSelf> subLevelNodes = [];

            foreach (TSelf node in levelNodes)
            {
                if (node.HigherLevelRef != null)
                    continue; // This is a reference to another branch
                if (referenceMap.TryGetValue(node.Entry.UIndex, out List<IEntry> entries))
                {
                    foreach (IEntry entry in entries)
                    {
                        // Check for direct circular dependency
                        if (node.Parent != null && node.Parent.Entry == entry)
                            continue; // This is a direct back and forth circular dependency

                        TSelf higherLevelRef = null;
                        foreach (var higherLevel in higherLevels)
                        {
                            if (higherLevel.TryGetValue(entry.UIndex, out higherLevelRef))
                            {
                                break;
                            }
                        }

                        //if (entry.UIndex == 603 && level == 2)
                        //    Debug.WriteLine("hi");
                        var newNode = new TSelf
                        {
                            Entry = entry,
                            Parent = node,
                            HigherLevelRef = higherLevelRef
                        };
                        subLevelNodes.Add(newNode);
                        node.Children.Add(newNode);
                    }
                }
            }

            return subLevelNodes;
        }

        /// <summary>
        /// Reference to another branch that's at a higher level. If set, this node will not have children
        /// </summary>
        public TSelf HigherLevelRef { get; set; }

        private bool HasObjectInChain(IEntry entry)
        {
            var rt = this;
            while (rt != null)
            {
                if (rt.Entry == entry)
                    return true;
                rt = rt.Parent;
            }

            return false;
        }


        /// <summary>
        /// Gets all references to other objects that this entry contains
        /// </summary>
        /// <param name="entry">Entry to compute references for.</param>
        /// <returns></returns>
        private static HashSet<int> GetObjectReferences(IEntry entry)
        {
            IMEPackage pcc = entry.FileRef;
            MEGame pccGame = pcc.Game;
            var references = new HashSet<int>();

            void addReference(int uIndex)
            {
                if (uIndex != 0 && uIndex != entry.UIndex && pcc.IsEntry(uIndex))
                {
                    references.Add(uIndex);
                }
            }

            if (entry is ImportEntry)
            {
                addReference(entry.idxLink);
                return references;
            }

            if (entry is ExportEntry exp)
            {
                try
                {
                    //find header references
                    addReference(exp.idxLink);
                    addReference(exp.idxArchetype);
                    addReference(exp.idxClass);
                    addReference(exp.idxSuperClass);


                    if (exp.HasComponentMap)
                    {
                        foreach ((_, int value) in exp.ComponentMap)
                        {
                            addReference(value);
                        }
                    }

                    //find stack references
                    if (exp.HasStack)
                    {
                        addReference(EndianReader.ToInt32(exp.DataReadOnly, 0, exp.FileRef.Endian));
                        addReference(EndianReader.ToInt32(exp.DataReadOnly, 4, exp.FileRef.Endian));
                    }
                    else if (exp.TemplateOwnerClassIdx is >= 0)
                    {
                        // Value will be -1 is unset. So we must make sure it is >= 0.
                        addReference(exp.TemplateOwnerClassIdx);
                    }

                    //find property references
                    GetObjectReferencesInProperties(exp.GetProperties(), addReference);

                    //find binary references
                    if (!exp.IsDefaultObject
                        && exp.ClassName != "AnimSequence" //has no UIndexes, and is expensive to deserialize
                        && ObjectBinary.From(exp) is ObjectBinary objBin)
                    {
                        objBin.ForEachUIndex(pccGame, new UIndexRefAdder(entry, references));
                    }
                }
                catch (Exception e) //when (!App.IsDebug)
                {
                    // Not much we can do it about it here
                }
            }

            return references;
        }

        private readonly struct UIndexRefAdder(IEntry entry, HashSet<int> references) : IUIndexAction
        {
            public void Invoke(ref int uIndex, string propName)
            {
                if (uIndex != 0 && uIndex != entry.UIndex && entry.FileRef.IsEntry(uIndex))
                {
                    references.Add(uIndex);
                }
            }
        }


        private static void GetObjectReferencesInProperties(PropertyCollection props, Action<int> addRef)
        {
            foreach (Property prop in props)
            {
                switch (prop)
                {
                    case ObjectProperty objectProperty:
                        addRef(objectProperty.Value);
                        break;
                    case DelegateProperty delegateProperty:
                        addRef(delegateProperty.Value.ContainingObjectUIndex);
                        break;
                    case StructProperty structProperty:
                        GetObjectReferencesInProperties(structProperty.Properties, addRef);
                        break;
                    case ArrayProperty<ObjectProperty> arrayProperty:
                        foreach (ObjectProperty objProp in arrayProperty)
                        {
                            addRef(objProp.Value);
                        }

                        break;
                    case ArrayProperty<StructProperty> arrayProperty:
                        foreach (StructProperty structProp in arrayProperty)
                        {
                            GetObjectReferencesInProperties(structProp.Properties, addRef);
                        }

                        break;
                }
            }
        }
    }
}
