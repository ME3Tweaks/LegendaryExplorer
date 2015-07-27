using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME1Explorer.Unreal
{
    public static class UnrealFlags
    {
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
