using KFreonLib.Helpers.LiquidEngine;
using KFreonLib.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFreonLib.PCCObjects
{
    public interface IPCCObject
    {
        ITexture2D CreateTexture2D(int expID, string pathBIOGame, uint hash = 0);
        int GameVersion { get; set; }
        byte[] header { get; set; }
        int expDataBegOffset { get; set; }
        int nameSize { get; set; }
        uint flags { get; set; }
        bool bCompressed { get; set; }
        int NameCount { get; set; }
        int NameOffset { get; set; }
        int ExportCount { get; set; }
        int ExportOffset { get; set; }
        int ImportCount { get; set; }
        int ImportOffset { get; set; }
        int Generator { get; set; }
        int Compression { get; set; }
        int ExportDataEnd { get; set; }
        uint PackageFlags { get; set; }
        int NumChunks { get; set; }
        MemoryTributary listsStream { get; set; }
        List<string> Names { get; set; }
        List<IImportEntry> Imports { get; set; }
        List<IExportEntry> Exports { get; set; }
        int _HeaderOff { get; set; }
        MemoryStream m { get; set; }
        string fullname { get; set; }
        string pccFileName { get; set; }
        void SaveToFile(string path);
        bool isName(int Index);
        bool isImport(int Index);
        bool isExport(int Index);
        string GetClass(int Index);
        string FollowLink(int Link);
        string GetName(int Index);
        int AddName(string newName);
        void DumpPCC(string path);
        int FindExp(string name);
        int FindExp(string name, string className);
        string getObjectName(int index);
        string getNameEntry(int index);
        void saveToFile(string newFileName = null, bool WriteToMemoryStream = false);
        string getClassName(int classname);
        void addExport(IExportEntry entry);

        long expDataEndOffset { get; set; }

        IExportEntry LastExport { get; set; }

        bool bDLCStored { get; set; }

        int findName(string name);
    }
}
