using LegendaryExplorerCore.Gammtek.Extensions.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LegendaryExplorerCore.UnrealScript.Documentation
{
    /// <summary>
    /// Serializes and deserializes a binary version of a DocuDB object, with less information, as it is used in tools that have access to that info already.
    /// </summary>
    public static class BinaryDocuDB
    {
        /// <summary>
        /// Bump this when the format changes.
        /// </summary>
        public const int BINDOCUDB_CURRENT_VERSION = 1;

        public const string BINDOCUDB_MAGIC = @"DOCU";

        #region SERIALIZATION
        /// <summary>
        /// Serializes a source DocuDB to binary format.
        /// </summary>
        /// <param name="db"></param>
        public static MemoryStream Serialize(DocuDB db)
        {
            MemoryStream bin = new MemoryStream();

            // Write Magic
            bin.WriteStringASCII(BINDOCUDB_MAGIC);

            // Write Version
            bin.WriteInt32(BINDOCUDB_CURRENT_VERSION);


            // Class Documentation
            // Only serialize classes that have documentation to reduce size
            var classesToWrite = db.ClassDocumentation.Where(x =>
                x.Value.ClassDocumentation != null ||
                x.Value.Variables.Any(x => x.Value.MemberDocumentation != null) || // Variables
                x.Value.Functions.Any(x => x.Value.MemberDocumentation != null || x.Value.FunctionMembers.Any(y => y.Value.MemberDocumentation != null)) || // Variables
                x.Value.States.Any(x => x.Value.MemberDocumentation != null || x.Value.Functions.Any(y => y.Value.MemberDocumentation != null)) // States
            ).ToList();
            bin.WriteInt32(classesToWrite.Count);
            foreach (var classDoc in classesToWrite)
            {
                WriteClass(bin, classDoc);
            }

            // Enum Documentation
            // Only serialize enums that have documentation to reduce size
            var enumsToWrite = db.EnumDocumentation.Where(x => x.Value.MemberDocumentation != null).ToList();
            bin.WriteInt32(enumsToWrite.Count);
            foreach (var enumDoc in enumsToWrite)
            {
                WriteEnum(bin, enumDoc);
            }

            var structsToWrite = db.StructDocumentation.Where(x =>
                       x.Value.MemberDocumentation != null ||
                       x.Value.Members.Any(x => x.Value.MemberDocumentation != null) // Variables
                   ).ToList();
            bin.WriteInt32(structsToWrite.Count);
            foreach (var structDoc in structsToWrite)
            {
                WriteStruct(bin, structDoc);
            }


            bin.Position = 0;
            return bin;
        }

        private static void WriteClass(MemoryStream bin, KeyValuePair<string, DocuClassEntry> classDoc)
        {
            bin.WriteUnrealStringUnicode(classDoc.Key);
            bin.WriteUnrealStringUnicode(classDoc.Value.ClassDocumentation);

            // Variables
            // Only serialize variables that have documentation to reduce size
            var varsToWrite = classDoc.Value.Variables.Where(x => x.Value.MemberDocumentation != null).ToList();
            bin.WriteInt32(varsToWrite.Count);
            foreach (var variable in varsToWrite)
            {
                WriteMember(bin, variable);
            }


            // Functions
            // Only serialize functions that have documentation to reduce size
            WriteFunctions(bin, classDoc.Value.Functions);

            // States
            var statesToWrite = classDoc.Value.States.Where(x => x.Value.MemberDocumentation != null || x.Value.Functions.Any(y => y.Value.MemberDocumentation != null)).ToList();
            bin.WriteInt32(classDoc.Value.States.Count);
            foreach (var state in statesToWrite)
            {
                WriteState(bin, state);
            }
        }

        private static void WriteStruct(MemoryStream bin, KeyValuePair<string, DocuStructEntry> structDoc)
        {
            bin.WriteUnrealStringUnicode(structDoc.Key);
            bin.WriteUnrealStringUnicode(structDoc.Value.MemberDocumentation);

            // Serialize only documented members values
            var valuesToWrite = structDoc.Value.Members.Where(x => x.Value.MemberDocumentation != null).ToList();
            bin.WriteInt32(valuesToWrite.Count);
            foreach (var val in valuesToWrite)
            {
                WriteMember(bin, val);
            }
        }

        private static void WriteEnum(MemoryStream bin, KeyValuePair<string, DocuEnumEntry> enumDoc)
        {
            bin.WriteUnrealStringUnicode(enumDoc.Key);
            bin.WriteUnrealStringUnicode(enumDoc.Value.MemberDocumentation);

            // Serialize only documented enum values
            var valuesToWrite = enumDoc.Value.EnumValues.Where(x => x.Value.MemberDocumentation != null).ToList();
            bin.WriteInt32(valuesToWrite.Count);
            foreach (var val in valuesToWrite)
            {
                bin.WriteUnrealStringUnicode(val.Key);
                bin.WriteUnrealStringUnicode(val.Value.MemberDocumentation);
            }
        }

        private static void WriteState(MemoryStream bin, KeyValuePair<string, DocuStateEntry> state)
        {
            bin.WriteUnrealStringUnicode(state.Key);
            bin.WriteUnrealStringUnicode(state.Value.MemberDocumentation);
            WriteFunctions(bin, state.Value.Functions);
        }

        private static void WriteFunctions(MemoryStream bin, CaseInsensitiveDictionary<DocuFunctionEntry> functions)
        {
            var funcsToWrite = functions.Where(x => x.Value.MemberDocumentation != null || x.Value.FunctionMembers.Any(y => y.Value.MemberDocumentation != null)).ToList();
            bin.WriteInt32(funcsToWrite.Count);
            foreach (var func in funcsToWrite)
            {
                bin.WriteUnrealStringUnicode(func.Key);
                bin.WriteUnrealStringUnicode(func.Value.MemberDocumentation);

                // Only serialize function members that have documentation to reduce size
                var funcMembersToWrite = func.Value.FunctionMembers.Where(x => x.Value.MemberDocumentation != null).ToList();
                bin.WriteInt32(funcMembersToWrite.Count);
                foreach (var variable in funcMembersToWrite)
                {
                    WriteMember(bin, variable);
                }
            }
        }

        private static void WriteMember(MemoryStream bin, KeyValuePair<string, DocuMemberEntry> variable)
        {
            bin.WriteUnrealStringUnicode(variable.Key);
            bin.WriteUnrealStringUnicode(variable.Value.MemberDocumentation);
        }

        #endregion

        #region DESERIALIZATION
        public static DocuDB Deserialize(string path)
        {
            using var fs = File.OpenRead(path);
            return Deserialize(fs);
        }

        public static DocuDB Deserialize(Stream inStream)
        {
            var magic = inStream.ReadStringASCII(4);
            if (magic != BINDOCUDB_MAGIC)
            {
                throw new Exception("Input stream is not a binary DocuDB.");
            }

            var version = inStream.ReadInt32();
            if (version > BINDOCUDB_CURRENT_VERSION)
            {
                throw new Exception($"Unsupported DocuDB version: {version}. Is this tool outdated?");
            }


            DocuDB db = new DocuDB();
            ReadClassDocumentation(db, inStream);
            ReadEnumDocumentation(db, inStream);
            ReadStructDocumentation(db, inStream);
            return db;
        }

        private static void ReadStructDocumentation(DocuDB db, Stream inStream)
        {
            var structCount = inStream.ReadInt32();
            db.StructDocumentation = new(structCount);
            while (structCount > 0)
            {
                ReadStruct(db, inStream);
                structCount--;
            }
        }

        private static void ReadStruct(DocuDB db, Stream inStream)
        {
            var structName = inStream.ReadUnrealString();
            var structDoc = inStream.ReadUnrealString();
            var structEntry = new DocuStructEntry() { MemberDocumentation = structDoc };
            db.StructDocumentation[structName] = structEntry;

            var memberCount = inStream.ReadInt32();
            structEntry.Members = new(memberCount);
            while (memberCount > 0)
            {
                var vari = ReadMember(inStream);
                structEntry.Members[vari.Key] = new DocuMemberEntry() { MemberDocumentation = vari.Value };
                memberCount--;
            }
        }

        private static void ReadEnumDocumentation(DocuDB db, Stream inStream)
        {
            var numEnums = inStream.ReadInt32();
            db.EnumDocumentation = new(numEnums);

            while (numEnums > 0)
            {
                ReadEnum(db, inStream);
                numEnums--;
            }
        }

        private static void ReadEnum(DocuDB db, Stream inStream)
        {
            var enumName = inStream.ReadUnrealString();
            var enumDoc = inStream.ReadUnrealString();
            var entry = new DocuEnumEntry() { MemberDocumentation = enumDoc };

            var enumValuesCount = inStream.ReadInt32();
            entry.EnumValues = new(enumValuesCount);
            while (enumValuesCount > 0)
            {
                var enumValue = ReadMember(inStream);
                entry.EnumValues[enumValue.Key] = new DocuEnumValueEntry() { MemberDocumentation = enumValue.Value };
                enumValuesCount--;
            }

            db.EnumDocumentation[enumName] = entry;
        }

        private static void ReadClassDocumentation(DocuDB db, Stream inStream)
        {
            var numClasses = inStream.ReadInt32();
            db.ClassDocumentation = new(numClasses);

            while (numClasses > 0)
            {
                ReadClass(db, inStream);
                numClasses--;
            }
        }

        private static void ReadClass(DocuDB db, Stream inStream)
        {
            var className = inStream.ReadUnrealString();
            var classDoc = inStream.ReadUnrealString();
            var classEntry = new DocuClassEntry() { ClassDocumentation = classDoc };
            db.ClassDocumentation[className] = classEntry;

            {
                var variableCount = inStream.ReadInt32();
                classEntry.Variables = new(variableCount);
                while (variableCount > 0)
                {
                    var vari = ReadMember(inStream);
                    classEntry.Variables[vari.Key] = new DocuMemberEntry() { MemberDocumentation = vari.Value };
                    variableCount--;
                }
            }

            {
                var functionCount = inStream.ReadInt32();
                classEntry.Functions = new(functionCount);
                while (functionCount > 0)
                {
                    var func = ReadFunction(inStream);
                    classEntry.Functions[func.Key] = func.Value;
                    functionCount--;
                }
            }

            {
                var stateCount = inStream.ReadInt32();
                classEntry.States = new(stateCount);
                while (stateCount > 0)
                {
                    var state = ReadState(inStream);
                    classEntry.States[state.Key] = state.Value;
                    stateCount--;
                }
            }
        }

        private static KeyValuePair<string, DocuStateEntry> ReadState(Stream inStream)
        {
            var stateName = inStream.ReadUnrealString();
            var stateDoc = inStream.ReadUnrealString();
            var state = new DocuStateEntry() { MemberDocumentation = stateDoc };

            var functionCount = inStream.ReadInt32();
            state.Functions = new(functionCount);
            while (functionCount > 0)
            {
                var func = ReadFunction(inStream);
                state.Functions[func.Key] = func.Value;
                functionCount--;
            }

            return new KeyValuePair<string, DocuStateEntry>(stateName, state);
        }

        private static KeyValuePair<string, DocuFunctionEntry> ReadFunction(Stream inStream)
        {
            var funcName = inStream.ReadUnrealString();
            var funcDoc = inStream.ReadUnrealString();
            DocuFunctionEntry func = new DocuFunctionEntry() { MemberDocumentation = funcDoc };

            var memberCount = inStream.ReadInt32();
            func.FunctionMembers = new(memberCount);
            while (memberCount > 0)
            {
                var member = ReadMember(inStream);
                func.FunctionMembers[member.Key] = new DocuMemberEntry() { MemberDocumentation = member.Value };
                memberCount--;
            }

            return new KeyValuePair<string, DocuFunctionEntry>(funcName, func);
        }

        private static KeyValuePair<string, string> ReadMember(Stream inStream)
        {
            return new KeyValuePair<string, string>(inStream.ReadUnrealString(), inStream.ReadUnrealString());
        }

        #endregion
    }
}
