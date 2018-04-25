using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME3Explorer.Unreal
{
    public static class UnrealFlags
    {

        [Flags]
        public enum EPropertyFlags : ulong
        {
            None = 0,
            Editable = 0x0000000000000001U,
            Const = 0x0000000000000002U,
            Input = 0x0000000000000004U,
            ExportObject = 0x0000000000000008U,
            OptionalParm = 0x0000000000000010U,
            Net = 0x0000000000000020U,
            EditFixedSize = 0x0000000000000040U, // also EditConstArray
            Parm = 0x0000000000000080U,
            OutParm = 0x0000000000000100U,
            SkipParm = 0x0000000000000200U,
            ReturnParm = 0x0000000000000400U,
            CoerceParm = 0x0000000000000800U,
            Native = 0x0000000000001000U,
            Transient = 0x0000000000002000U,
            Config = 0x0000000000004000U,
            Localized = 0x0000000000008000U,
            Travel = 0x0000000000010000U,
            EditConst = 0x0000000000020000U,
            GlobalConfig = 0x0000000000040000U,
            Component = 0x0000000000080000U,
            Init = 0x0000000000100000U,
            DuplicateTransient = 0x0000000000200000U,
            NeedCtorLink = 0x0000000000400000U,
            NoExport = 0x0000000000800000U,
            NoImport = 0x0000000001000000U,
            NoClear = 0x0000000002000000U,
            EditInline = 0x0000000004000000U,
            EdFindable = 0x0000000008000000U,
            EditInlineUse = 0x0000000010000000U,
            Deprecated = 0x0000000020000000U,
            DataBinding = 0x0000000040000000U, // also EditInlineNotify
            SerializeText = 0x0000000080000000U,
            RepNotify = 0x0000000100000000U,
            Interp = 0x0000000200000000U,
            NonTransactional = 0x0000000400000000U,
            EditorOnly = 0x0000000800000000U,
            NotForConsole = 0x0000001000000000U,
            RepRetry = 0x0000002000000000U,
            PrivateWrite = 0x0000004000000000U,
            ProtectedWrite = 0x0000008000000000U,
            Archetype = 0x0000010000000000U,
            EditHide = 0x0000020000000000U,
            EditTextBox = 0x0000040000000000U,
            CrossLevelPassive = 0x0000100000000000U,
            CrossLevelActive = 0x0000200000000000U
        }

        public static string propertyflagscraw = @"None, 0000000000000000,
Editable, 0000000000000001, 
Const, 0000000000000002, Constant
Input, 0000000000000004, 
ExportObject, 0000000000000008, 
OptionalParm, 0000000000000010, 
Net, 0000000000000020, 
EditFixedSize, 0000000000000040,
Parm, 0000000000000080, 
OutParm, 0000000000000100, 
SkipParm, 0000000000000200, 
ReturnParm, 0000000000000400, 
CoerceParm, 0000000000000800, 
Native, 0000000000001000, 
Transient, 0000000000002000, 
Config, 0000000000004000, 
Localized, 0000000000008000, 
Travel, 0000000000010000, 
EditConst, 0000000000020000, 
GlobalConfig, 0000000000040000, 
Component, 0000000000080000, 
Init, 0000000000100000, 
DuplicateTransient, 0000000000200000, 
NeedCtorLink, 0000000000400000, 
NoExport, 0000000000800000, 
NoImport, 0000000001000000, 
NoClear, 0000000002000000, 
EditInline, 0000000004000000, 
EdFindable, 0000000008000000, 
EditInlineUse, 0000000010000000, 
Deprecated, 0000000020000000, 
DataBinding, 0000000040000000,
SerializeText, 0000000080000000, 
RepNotify, 0000000100000000, 
Interp, 0000000200000000, 
NonTransactional, 0000000400000000, 
EditorOnly, 0000000800000000, 
NotForConsole, 0000001000000000, 
RepRetry, 0000002000000000, Retries replication if replication fails
PrivateWrite, 0000004000000000, 
ProtectedWrite, 0000008000000000, 
Archetype, 0000010000000000, 
EditHide, 0000020000000000, 
EditTextBox, 0000040000000000, 
CrossLevelPassive, 0000100000000000, 
CrossLevelActive, 0000200000000000";
        public static string[] propertyflags = propertyflagscraw.Split('\n');

        public enum EObjectFlags : ulong
        {
            Transactional = 0x0000000100000000,   // Object is transactional.
            Unreachable = 0x0000000200000000,	// Object is not reachable on the object graph.
            Public = 0x0000000400000000,	// Object is visible outside its package.
            TagImp = 0x0000000800000000,	// Temporary import tag in load/save.
            TagExp = 0x0000001000000000,	// Temporary export tag in load/save.
            Obsolete = 0x0000002000000000,   // Object marked as obsolete and should be replaced.
            TagGarbage = 0x0000004000000000,	// Check during garbage collection.
            Final = 0x0000008000000000,	// Object is not visible outside of class.
            PerObjectLocalized = 0x0000010000000000,	// Object is localized by instance name00000000, not by class.
            NeedLoad = 0x0000020000000000,   // During load00000000, indicates object needs loading.
            HighlightedName = 0x0000040000000000,	// A hardcoded name which should be syntax-highlighted.
            EliminateObject = 0x0000040000000000,   // NULL out references to this during garbage collecion.
            InSingularFunc = 0x0000080000000000,	// In a singular function.
            RemappedName = 0x0000080000000000,   // Name is remapped.
            Suppress = 0x0000100000000000,	//warning: Mirrored in UnName.h. Suppressed log name.
            StateChanged = 0x0000100000000000,   // Object did a state change.
            InEndState = 0x0000200000000000,   // Within an EndState call.
            Transient = 0x0000400000000000,	// Don't save object.
            Preloading = 0x0000800000000000,   // Data is being preloaded from file.
            LoadForClient = 0x0001000000000000,	// In-file load for client.
            LoadForServer = 0x0002000000000000,	// In-file load for client.
            LoadForEdit = 0x0004000000000000,	// In-file load for client.
            Standalone = 0x0008000000000000,   // Keep object around for editing even if unreferenced.
            NotForClient = 0x0010000000000000,	// Don't load this object for the game client.
            NotForServer = 0x0020000000000000,	// Don't load this object for the game server.
            NotForEdit = 0x0040000000000000,	// Don't load this object for the editor.
            Destroyed = 0x0080000000000000,	// Object Destroy has already been called.
            NeedPostLoad = 0x0100000000000000,   // Object needs to be postloaded.
            HasStack = 0x0200000000000000,	// Has execution stack.
            Native = 0x0400000000000000,   // Native (UClass only).
            Marked = 0x0800000000000000,   // Marked (for debugging).
            ErrorShutdown = 0x1000000000000000,	// ShutdownAfterError called.
            DebugPostLoad = 0x2000000000000000,   // For debugging Serialize calls.
            DebugSerialize = 0x4000000000000000,   // For debugging Serialize calls.
            DebugDestroy = 0x8000000000000000,   // For debugging Destroy calls.
        }

        public static string flagdescraw = @"Transactional , 0000000100000000,    Object is transactional.
Unreachable , 0000000200000000,	 Object is not reachable on the object graph.
Public , 0000000400000000,	 Object is visible outside its package.
TagImp , 0000000800000000,	 Temporary import tag in loadsave.
TagExp , 0000001000000000,	 Temporary export tag in loadsave.
Obsolete , 0000002000000000,    Object marked as obsolete and should be replaced.
TagGarbage , 0000004000000000,	 Check during garbage collection.
Final , 0000008000000000,	 Object is not visible outside of class.
PerObjectLocalized , 0000010000000000,	 Object is localized by instance name00000000, not by class.
NeedLoad , 0000020000000000,    During load00000000, indicates object needs loading.
HighlightedName , 0000040000000000,	 A hardcoded name which should be syntax-highlighted.
EliminateObject , 0000040000000000,    NULL out references to this during garbage collecion.
InSingularFunc , 0000080000000000,	 In a singular function.
RemappedName , 0000080000000000,    Name is remapped.
Suppress , 0000100000000000,	warning: Mirrored in UnName.h. Suppressed log name.
StateChanged , 0000100000000000,    Object did a state change.
InEndState , 0000200000000000,    Within an EndState call.
Transient , 0000400000000000,	 Don't save object.
Preloading , 0000800000000000,    Data is being preloaded from file.
LoadForClient , 0001000000000000,	 In-file load for client.
LoadForServer , 0002000000000000,	 In-file load for client.
LoadForEdit , 0004000000000000,	 In-file load for client.
Standalone , 0008000000000000,    Keep object around for editing even if unreferenced.
NotForClient , 0010000000000000,	 Don't load this object for the game client.
NotForServer , 0020000000000000,	 Don't load this object for the game server.
NotForEdit , 0040000000000000,	 Don't load this object for the editor.
Destroyed , 0080000000000000,	 Object Destroy has already been called.
NeedPostLoad , 0100000000000000,    Object needs to be postloaded.
HasStack , 0200000000000000,	 Has execution stack.
Native , 0400000000000000,    Native (UClass only).
Marked , 0800000000000000,    Marked (for debugging).
ErrorShutdown , 1000000000000000,	 ShutdownAfterError called.
DebugPostLoad , 2000000000000000,    For debugging Serialize calls.
DebugSerialize , 4000000000000000,    For debugging Serialize calls.
DebugDestroy , 8000000000000000,    For debugging Destroy calls.";
        public static string[] flagdesc = flagdescraw.Split('\n');
    }
}
