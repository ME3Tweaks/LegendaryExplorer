using LegendaryExplorer.DialogueEditor.DialogueEditorExperiments;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Linq;

using static LegendaryExplorer.Misc.ExperimentsTools.SharedMethods;

namespace LegendaryExplorer.Misc.ExperimentsTools
{
    public static class PackageAutomations
    {
        /// <summary>
        /// Create a LevelStreamingKismet with the filename in the file.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="filename">Name of file to stream.</param>
        /// <param name="inPersistentlevel">Whether to add it in the PersistentLevel or in TheWorld.</param>
        public static ExportEntry AddStreamingKismet(IMEPackage pcc, string filename, bool inPersistentlevel = false)
        {
            ExportEntry kismet = CreateExport(
                pcc, pcc.GetNextIndexedName("LevelStreamingKismet"), "LevelStreamingKismet",
                inPersistentlevel ? GetPersistentLevel(pcc) : GetTheWorld(pcc),
                new PropertyCollection() { new NameProperty(filename, "PackageName") }
                );

            RebuildStreamingLevels(pcc);

            return kismet;
        }

        /// <summary>
        /// Set the loading and streaming of the given file name in all the BioTriggerStreams where the conditional file is present.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="filename">Name of file to stream.</param>
        /// <param name="conditionalFile"></param>
        public static void StreamFile(IMEPackage pcc, string filename, string conditionalFile)
        {
            List<ExportEntry> triggerStreams = GetExports(pcc, "BioTriggerStream");

            foreach (ExportEntry triggerStream in triggerStreams)
            {
                ArrayProperty<StructProperty> streamingLevels = triggerStream.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
                if (streamingLevels == null) { continue; }

                // Iterate through all the streaming levels
                foreach (StructProperty streamingLevel in streamingLevels)
                {
                    ArrayProperty<NameProperty> visibleChunkNames = streamingLevel.GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames");
                    ArrayProperty<NameProperty> loadChunkNames = streamingLevel.GetProp<ArrayProperty<NameProperty>>("LoadChunkNames");
                    ArrayProperty<NameProperty> reference = null;

                    // Check if the conditional file is in the visible chunks
                    foreach (NameProperty visibleChunkName in visibleChunkNames)
                    {
                        if (StringExtensions.CaseInsensitiveEquals(visibleChunkName.Value, conditionalFile))
                        {
                            reference = visibleChunkNames;
                            break;
                        }
                    }
                    // If the conditional file was not in the visible chunks, check in the load chunks
                    if (reference == null)
                    {
                        foreach (NameProperty loadChunkName in loadChunkNames)
                        {
                            if (StringExtensions.CaseInsensitiveEquals(loadChunkName.Value, conditionalFile))
                            {
                                reference = loadChunkNames;
                                break;
                            }
                        }
                    }

                    // If neither contained the conditional file, skip this level
                    if (reference == null) { continue; }

                    // Add the filename
                    reference.Add(new NameProperty(filename));
                }

                triggerStream.WriteProperty(streamingLevels);
            }
        }

        /// <summary>
        /// Replace the old name with the new name, if the old one exists.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="oldName">Name to edit.</param>
        /// <param name="newName">Name to edit in.</param>
        /// <exception cref="Exception">If name not found.</exception>
        public static void EditName(IMEPackage pcc, string oldName, string newName)
        {
            int oldIdx = pcc.findName(oldName);
            if (oldIdx < 0) { throw new Exception(oldName); }
            pcc.replaceName(oldIdx, newName);
        }
    }
}
