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
            if (groupName is not null)
            {
                properties.Add(new NameProperty(groupName, "GroupName"));
            }
            properties.Add(CommonStructs.ColorProp(className == "InterpGroup" ? Color.Green : Color.Purple, "GroupColor"));
            IMEPackage pcc = interpData.FileRef;
            var group = new ExportEntry(pcc, properties: properties)
            {
                ObjectName = pcc.GetNextIndexedName(className),
                Class = EntryImporter.EnsureClassIsInFile(pcc, className)
            };
            group.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            pcc.AddExport(group);

            var props = interpData.GetProperties();
            var groupsProp = props.GetProp<ArrayProperty<ObjectProperty>>("InterpGroups") ?? new ArrayProperty<ObjectProperty>("InterpGroups");
            groupsProp.Add(new ObjectProperty(group));
            props.AddOrReplaceProp(groupsProp);

            return group;
        }


    }
}
