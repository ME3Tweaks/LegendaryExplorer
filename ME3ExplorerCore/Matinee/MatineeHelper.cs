using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.Matinee
{
    public static class MatineeHelper
    {
        public static ExportEntry AddNewGroupToInterpData(ExportEntry interpData, string groupName) => InternalAddGroup("InterpGroup", interpData, groupName);

        public static ExportEntry AddNewGroupDirectorToInterpData(ExportEntry interpData) => InternalAddGroup("InterpGroupDirector", interpData, null);

        private static ExportEntry InternalAddGroup(string className, ExportEntry interpData, string groupName)
        {
            var properties = new PropertyCollection{new ArrayProperty<ObjectProperty>("InterpTracks")};
            if (!string.IsNullOrEmpty(groupName))
            {
                properties.Add(new NameProperty(groupName, "GroupName"));
            }
            properties.Add(CommonStructs.ColorProp(className == "InterpGroup" ? Color.Green : Color.Purple, "GroupColor"));
            ExportEntry group = CreateNewExport(className, interpData, properties);

            var props = interpData.GetProperties();
            var groupsProp = props.GetProp<ArrayProperty<ObjectProperty>>("InterpGroups") ?? new ArrayProperty<ObjectProperty>("InterpGroups");
            groupsProp.Add(new ObjectProperty(group));
            props.AddOrReplaceProp(groupsProp);
            interpData.WriteProperties(props);

            return group;
        }

        private static ExportEntry CreateNewExport(string className, ExportEntry parent, PropertyCollection properties)
        {
            IMEPackage pcc = parent.FileRef;
            var group = new ExportEntry(pcc, properties: properties)
            {
                ObjectName = pcc.GetNextIndexedName(className),
                Class = EntryImporter.EnsureClassIsInFile(pcc, className)
            };
            group.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            pcc.AddExport(group);
            group.Parent = parent;
            return group;
        }

        public static List<ClassInfo> GetInterpTracks(MEGame game) => UnrealObjectInfo.GetNonAbstractDerivedClassesOf("InterpTrack", game);

        public static ExportEntry AddNewTrackToGroup(ExportEntry interpGroup, string trackClass)
        {
            //should add the property that contains track keys at least
            ExportEntry track = CreateNewExport(trackClass, interpGroup, null);

            var props = interpGroup.GetProperties();
            var tracksProp = props.GetProp<ArrayProperty<ObjectProperty>>("InterpTracks") ?? new ArrayProperty<ObjectProperty>("InterpTracks");
            tracksProp.Add(new ObjectProperty(track));
            props.AddOrReplaceProp(tracksProp);
            interpGroup.WriteProperties(props);

            return track;
        }
    }
}
