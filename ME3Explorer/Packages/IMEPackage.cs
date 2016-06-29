using System.Collections.Generic;

namespace ME3Explorer.Packages
{
    public enum MEGame
    {
        ME1 = 1,
        ME2,
        ME3,
    }

    public enum ArrayType
    {
        Object,
        Name,
        Enum,
        Struct,
        Bool,
        String,
        Float,
        Int,
        Byte,
    }

    public class PropertyInfo
    {
        public Unreal.PropertyReader.Type type;
        public string reference;
    }

    public class ClassInfo
    {
        public Dictionary<string, PropertyInfo> properties;
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;
        public int exportIndex;

        public ClassInfo()
        {
            properties = new Dictionary<string, PropertyInfo>();
        }
    }

    public interface IMEPackage
    {
        bool bCompressed { get; set; }
        bool canReconstruct { get; }
        bool isModified { get; }
        int ExportCount { get; }
        int ImportCount { get; }
        int ImportOffset { get; }
        IReadOnlyList<IExportEntry> Exports { get; }
        IReadOnlyList<IImportEntry> Imports { get; }
        IReadOnlyList<string> Names { get; }
        MEGame game { get; }
        string fileName { get; }

        bool canClone();
        bool isExport(int index);
        IExportEntry getExport(int index);
        bool isImport(int index);
        IImportEntry getImport(int index);
        bool isName(int index);
        IEntry getEntry(int index);
        int findName(string nameToFind);
        int FindNameOrAdd(string name);
        string appendSave(string newFileName, bool attemptOverwrite = true, int HeaderNameOffset = 34);
        string getClassName(int index);
        string getNameEntry(int index);
        string getObjectClass(int index);
        string getObjectName(int index);
        void addExport(IExportEntry exportEntry);
        void addImport(IImportEntry importEntry);
        void addName(string name);
        void setNames(List<string> list);
        void save();
        void save(string path);
        void saveByReconstructing(string path);
    }
}