using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Tree of references
    /// </summary>
    [DebuggerDisplay("{Entry.InstancedFullPath}")]
    public class ReferenceTree
    {
        /// <summary>
        /// The export this tree node represents.
        /// </summary>
        public IEntry Entry { get; set; }

        /// <summary>
        /// Items that reference this export. Reference trees do not change after creation and don't need to be ObservableCollections.
        /// </summary>
        public List<ReferenceTree> Children { get; } = new List<ReferenceTree>();

        /// <summary>
        /// The next link the in the reference chain to the root item we are calculating the references for. If null, we are the root node.
        /// </summary>
        public ReferenceTree Parent { get; set; }

        public static ReferenceTree CalculateReferenceTree(IEntry entry, Func<ReferenceTree> referenceTreeGenerator = null)
        {
            referenceTreeGenerator ??= () => new ReferenceTree();

            var pcc = entry.FileRef;

            ReferenceTree rt = referenceTreeGenerator();
            rt.Entry = entry;

            var referenceMap = new Dictionary<IEntry, int[]>(pcc.ImportCount + pcc.ExportCount);



            // 1. Calculate all outbound references
            foreach (var imp in entry.FileRef.Imports)
            {
                if (imp == entry)
                    continue;
                referenceMap[imp] = GetObjectReferences(imp);
            }

            foreach (var exp in entry.FileRef.Exports)
            {
                if (exp == entry)
                    continue;
                referenceMap[exp] = GetObjectReferences(exp);
            }

            // Draw the rest of the owl

            // Don't go too deep. There will be circular dependencies, even across levels.
            int maxDepth = 6;
            int currentDepth = 0;
            // First level
            List<ReferenceTree> levelNodes = new List<ReferenceTree>([rt]);

            // Used to link up to a higher branch and prune duplicates
            var levels = new List<Dictionary<int, ReferenceTree>>();

            while (currentDepth < maxDepth || levelNodes.IsEmpty())
            {
                Debug.WriteLine($"Current depth: {currentDepth}");
                levelNodes = AddReferenceLeaves(currentDepth, levelNodes, referenceMap, levels, referenceTreeGenerator);

                var levelDict = new Dictionary<int, ReferenceTree>();
                foreach (var node in levelNodes)
                {
                    // Only add the first one. This makes it so it favors the first ones and not the last ones
                    levelDict.TryAdd(node.Entry.UIndex, node);
                }

                levels.Add(levelDict);
                currentDepth++;
            }

            return rt;
        }

        private static List<ReferenceTree> AddReferenceLeaves(int level, List<ReferenceTree> levelNodes,
            Dictionary<IEntry, int[]> referenceMap, List<Dictionary<int, ReferenceTree>> higherLevels, Func<ReferenceTree> referenceTreeGenerator)
        {
            List<ReferenceTree> subLevelNodes = new List<ReferenceTree>();

            foreach (var node in levelNodes)
            {
                if (node.HigherLevelRef != null)
                    continue; // This is a reference to another branch

                foreach (var reference in referenceMap)
                {
                    if (reference.Value.Contains(node.Entry.UIndex))
                    {
                        // Check for direct circular dependency
                        if (node.Parent != null && node.Parent.Entry == reference.Key)
                            continue; // This is a direct back and forth circular dependency

                        ReferenceTree higherLevelRef = null;
                        foreach (var higherLevel in higherLevels)
                        {
                            if (higherLevel.TryGetValue(reference.Key.UIndex, out higherLevelRef))
                            {
                                break;
                            }
                        }

                        //if (reference.Key.UIndex == 603 && level == 2)
                        //    Debug.WriteLine("hi");
                        var newNode = referenceTreeGenerator();
                        newNode.Entry = reference.Key;
                        newNode.Parent = node;
                        newNode.HigherLevelRef = higherLevelRef;
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
        public ReferenceTree HigherLevelRef { get; set; }

        private bool HasObjectInChain(IEntry entry)
        {
            ReferenceTree rt = this;
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
        private static int[] GetObjectReferences(IEntry entry)
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
                return references.ToArray();
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
                        foreach (var comp in exp.ComponentMap)
                        {
                            addReference(comp.Value);
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
                        var indices = new List<int>();
                        if (objBin is Level levelBin)
                        {
                            //trashing a level object will automatically remove it from the Actor list
                            //so we don't care if it's referenced there
                            levelBin.ForEachUIndexExceptActorList(pccGame, new UIndexCollector(indices));
                        }
                        else
                        {
                            objBin.ForEachUIndex(pccGame, new UIndexCollector(indices));
                        }

                        foreach (int uIndex in indices)
                        {
                            addReference(uIndex);
                        }
                    }
                }
                catch (Exception e) //when (!App.IsDebug)
                {
                    // Not much we can do it about it here
                }
            }

            return references.ToArray();
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
