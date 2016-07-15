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
        bool bCompressed { get; }
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

        //reading
        bool canClone();
        bool isExport(int index);
        bool isImport(int index);
        bool isName(int index);
        IEntry getEntry(int index);
        IExportEntry getExport(int index);
        IImportEntry getImport(int index);
        int findName(string nameToFind);
        string getClassName(int index);
        string getNameEntry(int index);
        string getObjectClass(int index);
        string getObjectName(int index);

        //editing
        void addName(string name);
        int FindNameOrAdd(string name);
        void setNames(List<string> list);
        void addExport(IExportEntry exportEntry);
        void addImport(IImportEntry importEntry);

        //saving
        void save();
        void save(string path);
        void saveByReconstructing(string path);
        string appendSave(string newFileName, bool attemptOverwrite = true, int HeaderNameOffset = 34);
    }
}