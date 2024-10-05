using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorerCore.Kismet
{
    /// <summary>
    /// Class used for designing sequences, not just making nodes.
    /// </summary>
    public static class SequenceDesigner
    {

        /// <summary>
        /// Creates a new sequence activated event and hooks it up for use in the sequence, creating the input pin.
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="inputLabel"></param>
        /// <param name="cache"></param>
        public static ExportEntry CreateInput(ExportEntry sequence, string inputLabel, PackageCache cache = null)
        {
            // Create an add activation to sequence
            var activation = SequenceObjectCreator.CreateSequenceObject(sequence, "SeqEvent_SequenceActivated", cache);
            activation.WriteProperty(new StrProperty(inputLabel, "InputLabel"));

            //// Reindex if necessary - 10/02/2024 - this should not be necessary as when generating export it indexes it automatically
            //var expCount = sequence.FileRef.Exports.Count(x => x.InstancedFullPath == activation.InstancedFullPath);
            //if (expCount > 1)
            //{
            //    // update the index
            //    activation.ObjectName = s.GetNextIndexedName(activation.ObjectName.Name);
            //}

            // Add input link to sequence
            var inputLinks = sequence.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (inputLinks == null)
            {
                inputLinks = new ArrayProperty<StructProperty>("InputLinks");
            }

            // Add struct
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(sequence.Game, "InputLinks", "Sequence");
            if (p == null)
            {
                Debugger.Break();
            }

            if (p != null)
            {
                string typeName = p.Reference;
                PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(sequence.Game, typeName, true, sequence.FileRef, packageCache: cache);
                props.AddOrReplaceProp(new NameProperty(activation.ObjectName, "LinkAction"));
                props.AddOrReplaceProp(new StrProperty(inputLabel, "LinkDesc"));
                props.AddOrReplaceProp(new ObjectProperty(activation, "LinkedOp"));
                inputLinks.Add(new StructProperty(typeName, props, isImmutable: false));
            }

            sequence.WriteProperty(inputLinks);

            return activation;
        }

        /// <summary>
        /// Create a new Finish Sequence object and hooks it up for use in the sequence, creating the output pin.
        /// </summary>
        public static ExportEntry CreateOutput(ExportEntry sequence, string outputLabel, PackageCache cache = null)
        {
            // Create an add activation to sequence
            var finished = SequenceObjectCreator.CreateSequenceObject(sequence, "SeqAct_FinishSequence", cache);
            finished.WriteProperty(new StrProperty(outputLabel, "OutputLabel"));
            // Reindex if necessary - 10/02/2024 - this should not be necessary as when generating export it indexes it automatically
            //var expCount = Pcc.Exports.Count(x => x.InstancedFullPath == finished.InstancedFullPath);
            //if (expCount > 1)
            //{
            //    // update the index
            //    finished.ObjectName = Pcc.GetNextIndexedName(finished.ObjectName.Name);
            //}

            //KismetHelper.AddObjectToSequence(finished, SelectedSequence);

            // Add output link to sequence
            var outputLinks = sequence.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outputLinks == null)
            {
                outputLinks = new ArrayProperty<StructProperty>("OutputLinks");
            }

            // Add struct
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(sequence.Game, "OutputLinks", "Sequence");
            if (p == null)
            {
                Debugger.Break();
            }

            if (p != null)
            {
                string typeName = p.Reference;
                PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(sequence.Game, typeName, true, sequence.FileRef, packageCache: cache);
                props.AddOrReplaceProp(new NameProperty(finished.ObjectName, "LinkAction"));
                props.AddOrReplaceProp(new StrProperty(outputLabel, "LinkDesc"));
                props.AddOrReplaceProp(new ObjectProperty(finished, "LinkedOp"));
                outputLinks.Add(new StructProperty(typeName, props, isImmutable: false));
            }

            sequence.WriteProperty(outputLinks);

            return finished;
        }

        /// <summary>
        /// Creates a new extern variable and hooks it up for use in the sequence, creating the variable link pin on the sequence.
        /// </summary>
        /// <returns></returns>
        public static ExportEntry CreateExtern(ExportEntry sequence, string externName, string externDataType, bool writable = false, PackageCache cache = null)
        {
            // Create a new extern
            var externalVar = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_External");
            externalVar.idxLink = sequence.UIndex;

            var expectedDataTypeClass =
                EntryImporter.EnsureClassIsInFile(sequence.FileRef, externDataType, new RelinkerOptionsPackage());
            externalVar.WriteProperty(new StrProperty(externName, "VariableLabel"));
            externalVar.WriteProperty(new ObjectProperty(expectedDataTypeClass, "ExpectedType"));
            // Reindex if necessary - 
            //var expCount = sequence.FileRef.Exports.Count(x => x.InstancedFullPath == externalVar.InstancedFullPath);
            //if (expCount > 1)
            //{
            //    // update the index
            //    externalVar.ObjectName = sequence.FileRef.GetNextIndexedName(externalVar.ObjectName.Name);
            //}

            //KismetHelper.AddObjectToSequence(externalVar, sequence);

            // Add input link to sequence
            var variableLinks = sequence.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (variableLinks == null)
            {
                variableLinks = new ArrayProperty<StructProperty>("VariableLinks");
            }

            // Add struct to VariableLinks
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(sequence.FileRef.Game, "VariableLinks", "Sequence");
            if (p == null)
            {
                Debugger.Break();
            }

            if (p != null)
            {
                string typeName = p.Reference;
                PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(sequence.FileRef.Game, typeName, true, sequence.FileRef, packageCache: cache);
                props.AddOrReplaceProp(new NameProperty(externalVar.ObjectName, "LinkVar"));
                props.AddOrReplaceProp(new StrProperty(externName, "LinkDesc"));
                props.AddOrReplaceProp(new ObjectProperty(expectedDataTypeClass, "ExpectedType"));
                props.AddOrReplaceProp(new BoolProperty(writable, "bWritable"));
                variableLinks.Add(new StructProperty(typeName, props, isImmutable: false));
            }

            sequence.WriteProperty(variableLinks);

            return externalVar;
        }
    }
}
