using System.Collections.Generic;

namespace ME3Explorer.Packages
{
    public interface IMEPackage
    {
        bool bCompressed { get; set; }
        bool canReconstruct { get; }
        List<IExportEntry> IExports { get; }
        string fileName { get; }
        int ImportOffset { get; set; }
        List<IImportEntry> IImports { get; }
        bool isModified { get; }

        void addExport(IExportEntry exportEntry);
        void addImport(IImportEntry importEntry);
        void addName(string name);
        bool canClone();
        int findName(string nameToFind);
        int FindNameOrAdd(string name);
        string getClassName(int index);
        IEntry getEntry(int index);
        string getNameEntry(int index);
        string getObjectClass(int index);
        string getObjectName(int index);
        bool isExport(int index);
        bool isImport(int index);
        bool isName(int index);
        void save();
        void save(string path);
    }
}