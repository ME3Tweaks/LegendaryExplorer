using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LegendaryExplorerCore.Unreal
{
    public static class UnrealFlags
    {

        /// <summary>
        /// Flags describing an class instance. This code is from UEExplorer.
        ///
        /// Note:
        ///     This is valid for UE3 as well unless otherwise noted.
        ///
        /// @Redefined( Version, Clone )
        ///     The flag is redefined in (Version) as (Clone)
        ///
        /// @Removed( Version )
        ///     The flag is removed in (Version)
        ///
        /// @Moved( Version, New )
        ///     The flag was moved since (Version) to a different value (New)
        /// </summary>
        [Flags]
        public enum EClassFlags : uint
        {
            None = 0x00000000U,
            Abstract = 0x00000001U,
            Compiled = 0x00000002U,
            Config = 0x00000004U,
            Transient = 0x00000008U,
            Parsed = 0x00000010U,
            Localized = 0x00000020U,
            SafeReplace = 0x00000040U,
            Native = 0x00000080,
            NoExport = 0x00000100U,
            Placeable = 0x00000200U,
            PerObjectConfig = 0x00000400U,
            NativeReplication = 0x00000800U,
            EditInlineNew = 0x00001000U,
            CollapseCategories = 0x00002000U,
            Interface = 0x00004000,
            HasInstancedProps = 0x00200000,      // @Removed(UE3 in early but not latest)
            HasComponents = 0x00800000,      // @Redefined Class has component properties.
            Hidden = 0x01000000,      // @Redefined Don't show this class in the editor class browser or edit inline new menus.
            Deprecated = 0x02000000,      // @Redefined Don't save objects of this class when serializing
            HideDropDown = 0x04000000,
            Exported = 0x08000000,
            Intrinsic = 0x10000000,
            NativeOnly = 0x20000000U,
            PerObjectLocalized = 0x40000000,
            HasCrossLevelRefs = 0x80000000,

            Inherit = //Transient |
                      Config |
                      Localized |
                      SafeReplace |
                      PerObjectConfig |
                      PerObjectLocalized |
                      Placeable |
                      HasComponents |
                      Deprecated |
                      Intrinsic |
                      HasInstancedProps |
                      HasCrossLevelRefs
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EClassFlags enumValue, EClassFlags flag) => (enumValue & flag) == flag;

        [Flags]
        //from https://github.com/EliotVU/Unreal-Library/blob/23da0b1d42d90ccf8bc7d64051ffd38a8088ec93/src/UnrealFlags.cs#L17
        public enum EPackageFlags : uint
        {
            // 028A0009 : A cooked and compressed package
            // 00280009 : A cooked package
            // 00020001 : A ordinary package

            /// <summary>
            /// Whether clients are allowed to download the package from the server.
            /// </summary>
            AllowDownload = 0x00000001U,

            /// <summary>
            /// Whether clients can skip downloading the package but still able to join the server.
            /// </summary>
            ClientOptional = 0x00000002U,

            /// <summary>
            /// Only necessary to load on the server.
            /// </summary>
            ServerSideOnly = 0x00000004U,

            /// <summary>
            /// The package is cooked.
            /// </summary>
            Cooked = 0x00000008U,      // @Redefined

            Unsecure = 0x00000010U,

            SavedWithNewerVersion = 0x00000020U,

            /// <summary>
            /// Clients must download the package.
            /// </summary>
            Need = 0x00008000U,

            /// <summary>
            /// Package holds map data.
            /// </summary>
            Map = 0x00020000U,

            DisallowLazyLoading = 0x00080000,

            /// <summary>
            /// Package has ME3Explorer appended name table. This is an unused flag by the engine and is present only due to ME3Explorer modified files
            /// DEPRECATED! This will only be present in files saved with older versions of ME3Explorer
            /// </summary>
            ME3ExplorerAppendedNameTable = 0x00100000U,

            /// <summary>
            /// Package contains classes.
            /// </summary>
            Script = 0x00200000U,


            /// <summary>
            /// The package was build with -Debug
            /// </summary>
            Debug = 0x00400000U,
            RequireImportsAlreadyLoaded = 0x00800000U,

            SelfContainedLighting = 0x01000000U,
            Compressed = 0x02000000U,
            FullyCompressed = 0x04000000U,

            ContainsInlinedShaders = 0x08000000U,

            ContainsFaceFXData = 0x10000000U,

            /// <summary>
            /// Whether package has metadata exported(anything related to the editor).
            /// </summary>
            NoExportsData = 0x20000000U,

            /// <summary>
            /// Package's source is stripped.
            /// </summary>
            Stripped = 0x40000000U,

            Protected = 0x80000000U,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EPackageFlags enumValue, EPackageFlags flag) => (enumValue & flag) == flag;

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
            EditInline = 0x0000000000080000U,
            AlwaysInit = 0x0000000000100000U,
            DuplicateTransient = 0x0000000000200000U,
            NeedCtorLink = 0x0000000000400000U,
            NoExport = 0x0000000000800000U,
            NoImport = 0x0000000001000000U,
            NoClear = 0x0000000002000000U,
            Component = 0x0000000004000000U,
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
            CrossLevelActive = 0x0000200000000000U,

            // BioWare specific
            RsxStorage = 0x0001000000000000U,        // Property can be moved into RSX memory on the PS3
            UnkFlag1 = 0x0080000000000000U,
            LoadForCooking = 0x0100000000000000U,        // property is editoronly or notforconsole but needs to be loaded during cooking
            BioNonShip = 0x0200000000000000U,        // Property doesn't serialize to or from disk
            BioIgnorePropertyAdd = 0x0400000000000000U,        // ??????
            SortBarrier = 0x0800000000000000U,        // Inserts a barrier between the marked property and the previous property to avoid sorting properties across. 
            ClearCrossLevel = 0x1000000000000000U,        // Property should call BioClearCrossLevelReferences
            BioSave = 0x2000000000000000U,        // Property should automagically synch with a save object
            BioExpanded = 0x4000000000000000U,        // EDITOR ONLY Property should be initially expanded (arrays and structs).
            BioAutoGrow = 0x8000000000000000U,        // EDITOR ONLY Property should auto grow the array when enter hit on last entry

            CrossLevel = CrossLevelPassive | CrossLevelActive,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EPropertyFlags enumValue, EPropertyFlags flag) => (enumValue & flag) == flag;

        public static Dictionary<EPropertyFlags, string> propertyflagsdesc = new()
        {
            [EPropertyFlags.None] = "",
            [EPropertyFlags.Editable] = "",
            [EPropertyFlags.Const] = "Constant",
            [EPropertyFlags.Input] = "",
            [EPropertyFlags.ExportObject] = "",
            [EPropertyFlags.OptionalParm] = "",
            [EPropertyFlags.Net] = "",
            [EPropertyFlags.EditFixedSize] = "also EditConstArray",
            [EPropertyFlags.Parm] = "",
            [EPropertyFlags.OutParm] = "",
            [EPropertyFlags.SkipParm] = "",
            [EPropertyFlags.ReturnParm] = "",
            [EPropertyFlags.CoerceParm] = "",
            [EPropertyFlags.Native] = "",
            [EPropertyFlags.Transient] = "",
            [EPropertyFlags.Config] = "",
            [EPropertyFlags.Localized] = "",
            [EPropertyFlags.Travel] = "",
            [EPropertyFlags.EditConst] = "",
            [EPropertyFlags.GlobalConfig] = "",
            [EPropertyFlags.EditInline] = "",
            [EPropertyFlags.AlwaysInit] = "",
            [EPropertyFlags.DuplicateTransient] = "",
            [EPropertyFlags.NeedCtorLink] = "",
            [EPropertyFlags.NoExport] = "",
            [EPropertyFlags.NoImport] = "",
            [EPropertyFlags.NoClear] = "",
            [EPropertyFlags.Component] = "",
            [EPropertyFlags.EdFindable] = "",
            [EPropertyFlags.EditInlineUse] = "",
            [EPropertyFlags.Deprecated] = "",
            [EPropertyFlags.DataBinding] = "also EditInlineNotify",
            [EPropertyFlags.SerializeText] = "",
            [EPropertyFlags.RepNotify] = "",
            [EPropertyFlags.Interp] = "",
            [EPropertyFlags.NonTransactional] = "",
            [EPropertyFlags.EditorOnly] = "",
            [EPropertyFlags.NotForConsole] = "",
            [EPropertyFlags.RepRetry] = "Retries replication if replication fails",
            [EPropertyFlags.PrivateWrite] = "",
            [EPropertyFlags.ProtectedWrite] = "",
            [EPropertyFlags.Archetype] = "",
            [EPropertyFlags.EditHide] = "",
            [EPropertyFlags.EditTextBox] = "",
            [EPropertyFlags.CrossLevelPassive] = "",
            [EPropertyFlags.CrossLevelActive] = "",

            // BIOWARE SPECIFIC
            [EPropertyFlags.RsxStorage] = "Property can be moved into RSX memory (PS3)",
            [EPropertyFlags.UnkFlag1] = "",
            [EPropertyFlags.LoadForCooking] = "Property is editor only, but must be loaded during cooking",
            [EPropertyFlags.BioNonShip] = "Property does not serialize to or from disk",
            [EPropertyFlags.BioIgnorePropertyAdd] = "",
            [EPropertyFlags.SortBarrier] = "BioEditor Only",
            [EPropertyFlags.ClearCrossLevel] = "Property should call BioClearCrossLevelReferences",
            [EPropertyFlags.BioSave] = "Property should automatically sync with a save object",
            [EPropertyFlags.BioExpanded] = "EDITOR ONLY Property should be initially expanded",
            [EPropertyFlags.BioAutoGrow] = "EDITOR ONLY Property should automatially grow the array when adding an item to the end",
        };

        [Flags]
        public enum EObjectFlags : ulong
        {
            InSingularFunc = 0x0000000000000002, // In a singular function.
            StateChanged = 0x0000000000000004,   // Object did a state change.
            DebugPostLoad = 0x0000000000000008,   // For debugging Serialize calls.
            DebugSerialize = 0x0000000000000010,   // For debugging Serialize calls.
            DebugFinishDestroy = 0x0000000000000020,   // For debugging FinishDestroy calls.
            EdSelected = 0x0000000000000040,
            ZombieComponent = 0x0000000000000080,
            Protected = 0x0000000000000100, // Property can only be accessed by owning class or subclasses
            ClassDefaultObject = 0x0000000000000200, // this object is its class's default object
            ArchetypeObject = 0x0000000000000400, // this object is a template for another object - treat like a class default object
            ForceTagExp = 0x0000000000000800, //Force this object into the export table when saving
            TokenStreamAssembled = 0x0000000000001000,
            MisAlignedObject = 0x0000000000002000, // Object has desynced from c++ class (native classes, editor only)
            RootSet = 0x0000000000004000, // This object should not be garbage collected
            BeginDestroyed = 0x0000000000008000, // BeginDestroy has been called
            FinishDestroyed = 0x0000000000010000, // FinishDestroy has been called
            DebugBeginDestroyed = 0x0000000000020000, // If object is considered part of the root set (?)
            MarkedByCooker = 0x0000000000040000,
            LocalizedResource = 0x0000000000080000, // Resource object is localized
            InitializedProps = 0x0000000000100000, // have properties been initialized?
            PendingFieldPatches = 0x0000000000200000, // ScriptPatch system (not used in ME)
            IsCrossLevelReferenced = 0x0000000000400000, // This object has been pointed to by a cross-level reference, and therefore requires additional cleanup upon deletion

            Saved = 0x0000000080000000,
            Transactional = 0x0000000100000000,   // Object is transactional.
            Unreachable = 0x0000000200000000,   // Object is not reachable on the object graph.            
            Public = 0x0000000400000000, // Object is visible outside its package.
            TagImp = 0x0000000800000000, // Temporary import tag in load/save.
            TagExp = 0x0000001000000000, // Temporary export tag in load/save.
            Obsolete = 0x0000002000000000,   // Object marked as obsolete and should be replaced.
            TagGarbage = 0x0000004000000000, // Check during garbage collection.
            DisregardForGC = 0x0000008000000000,// Object is considered static // REPLACED Final = 0x0000008000000000,	// Object is not visible outside of class.
            PerObjectLocalized = 0x0000010000000000, // Object is localized by instance name, not by class.
            NeedLoad = 0x0000020000000000,   // During loading, indicates object needs loading.
            AsyncLoading = 0x0000040000000000, // Object is being async loaded
            NeedPostLoadSubobjects = 0x0000080000000000, // During load, Subobjects also need instanced
            Suppress = 0x0000100000000000,   //warning: Mirrored in UnName.h. Suppressed log name.
            InEndState = 0x0000200000000000,   // Within an EndState call.
            Transient = 0x0000400000000000,  // Don't save object.
            Cooked = 0x0000800000000000, // Content was cooked
            LoadForClient = 0x0001000000000000,  // In-file load for client.
            LoadForServer = 0x0002000000000000,  // In-file load for client.
            LoadForEdit = 0x0004000000000000,    // In-file load for client.
            Standalone = 0x0008000000000000,   // Keep object around for editing even if unreferenced.
            NotForClient = 0x0010000000000000,   // Don't load this object for the game client.
            NotForServer = 0x0020000000000000,   // Don't load this object for the game server.
            NotForEdit = 0x0040000000000000, // Don't load this object for the editor.
            // There is nothing in this slot.
            NeedPostLoad = 0x0100000000000000,   // Object needs to be postloaded.
            HasStack = 0x0200000000000000,   // Has execution stack.
            Native = 0x0400000000000000,   // Native (UClass only).
            Marked = 0x0800000000000000,   // Marked (for debugging).
            ErrorShutdown = 0x1000000000000000, // ShutdownAfterError called.
            PendingKill = 0x2000000000000000 // Object is pending destruction
                                             // Not used
                                             // Not used


            // The following are not used with updated info
            //HighlightedName = 0x0000040000000000,	// A hardcoded name which should be syntax-highlighted.
            //EliminateObject = 0x0000040000000000,   // NULL out references to this during garbage collecion.
            //RemappedName = 0x0000080000000000,   // Name is remapped.
            //Preloading = 0x0000800000000000,   // Data is being preloaded from file.
            //Destroyed = 0x0080000000000000,	// Object Destroy has already been called.




            // ORIGINAL BELOW
            //InSingularFunc = 0x0000000000000002, // In a singular function.
            //ClassDefaultObject = 0x0000000000000200, // this object is its class's default object
            //IsCrossLevelReferenced = 0x0000000000400000, // This object has been pointed to by a cross-level reference, and therefore requires additional cleanup upon deletion
            //ArchetypeObject = 0x0000000000000400, // this object is a template for another object - treat like a class default object
            //LocalizedResource = 0x0000000000080000, // Resource object is localized
            //Transactional = 0x0000000100000000,   // Object is transactional.
            //Unreachable = 0x0000000200000000,	// Object is not reachable on the object graph.
            //Public = 0x0000000400000000,	// Object is visible outside its package.
            //TagImp = 0x0000000800000000,	// Temporary import tag in load/save.
            //TagExp = 0x0000001000000000,	// Temporary export tag in load/save.
            //Obsolete = 0x0000002000000000,   // Object marked as obsolete and should be replaced.
            //TagGarbage = 0x0000004000000000,	// Check during garbage collection.
            //Final = 0x0000008000000000,	// Object is not visible outside of class.
            //PerObjectLocalized = 0x0000010000000000,	// Object is localized by instance name00000000, not by class.
            //NeedLoad = 0x0000020000000000,   // During load00000000, indicates object needs loading.
            //HighlightedName = 0x0000040000000000,	// A hardcoded name which should be syntax-highlighted.
            //EliminateObject = 0x0000040000000000,   // NULL out references to this during garbage collecion.
            //RemappedName = 0x0000080000000000,   // Name is remapped.
            //Suppress = 0x0000100000000000,	//warning: Mirrored in UnName.h. Suppressed log name.
            //StateChanged = 0x0000100000000000,   // Object did a state change.
            //InEndState = 0x0000200000000000,   // Within an EndState call.
            //Transient = 0x0000400000000000,	// Don't save object.
            //Preloading = 0x0000800000000000,   // Data is being preloaded from file.
            //LoadForClient = 0x0001000000000000,	// In-file load for client.
            //LoadForServer = 0x0002000000000000,	// In-file load for client.
            //LoadForEdit = 0x0004000000000000,	// In-file load for client.
            //Standalone = 0x0008000000000000,   // Keep object around for editing even if unreferenced.
            //NotForClient = 0x0010000000000000,	// Don't load this object for the game client.
            //NotForServer = 0x0020000000000000,	// Don't load this object for the game server.
            //NotForEdit = 0x0040000000000000,	// Don't load this object for the editor.
            //Destroyed = 0x0080000000000000,	// Object Destroy has already been called.
            //NeedPostLoad = 0x0100000000000000,   // Object needs to be postloaded.
            //HasStack = 0x0200000000000000,	// Has execution stack.
            //Native = 0x0400000000000000,   // Native (UClass only).
            //Marked = 0x0800000000000000,   // Marked (for debugging).
            //ErrorShutdown = 0x1000000000000000,	// ShutdownAfterError called.
            //DebugPostLoad = 0x2000000000000000,   // For debugging Serialize calls.
            //DebugSerialize = 0x4000000000000000,   // For debugging Serialize calls.
            //DebugDestroy = 0x8000000000000000,   // For debugging Destroy calls.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EObjectFlags enumValue, EObjectFlags flag) => (enumValue & flag) == flag;

        public static string[] flagdesc =
        {
            "Transactional , 0000000100000000,    Object is transactional.",
            "Unreachable , 0000000200000000,	 Object is not reachable on the object graph.",
            "Public , 0000000400000000,	 Object is visible outside its package.",
            "TagImp , 0000000800000000,	 Temporary import tag in loadsave.",
            "TagExp , 0000001000000000,	 Temporary export tag in loadsave.",
            "Obsolete , 0000002000000000,    Object marked as obsolete and should be replaced.",
            "TagGarbage , 0000004000000000,	 Check during garbage collection.",
            "Final , 0000008000000000,	 Object is not visible outside of class.",
            "PerObjectLocalized , 0000010000000000,	 Object is localized by instance name00000000, not by class.",
            "NeedLoad , 0000020000000000,    During load00000000, indicates object needs loading.",
            "HighlightedName , 0000040000000000,	 A hardcoded name which should be syntax-highlighted.",
            "EliminateObject , 0000040000000000,    NULL out references to this during garbage collecion.",
            "RemappedName , 0000080000000000,    Name is remapped.",
            "Suppress , 0000100000000000,	warning: Mirrored in UnName.h. Suppressed log name.",
            "StateChanged , 0000100000000000,    Object did a state change.",
            "InEndState , 0000200000000000,    Within an EndState call.",
            "Transient , 0000400000000000,	 Don't save object.",
            "Preloading , 0000800000000000,    Data is being preloaded from file.",
            "LoadForClient , 0001000000000000,	 In-file load for client.",
            "LoadForServer , 0002000000000000,	 In-file load for client.",
            "LoadForEdit , 0004000000000000,	 In-file load for client.",
            "Standalone , 0008000000000000,    Keep object around for editing even if unreferenced.",
            "NotForClient , 0010000000000000,	 Don't load this object for the game client.",
            "NotForServer , 0020000000000000,	 Don't load this object for the game server.",
            "NotForEdit , 0040000000000000,	 Don't load this object for the editor.",
            "Destroyed , 0080000000000000,	 Object Destroy has already been called.",
            "NeedPostLoad , 0100000000000000,    Object needs to be postloaded.",
            "HasStack , 0200000000000000,	 Has execution stack.",
            "Native , 0400000000000000,    Native (UClass only).",
            "Marked , 0800000000000000,    Marked (for debugging).",
            "ErrorShutdown , 1000000000000000,	 ShutdownAfterError called.",
            "DebugPostLoad , 2000000000000000,    For debugging Serialize calls.",
            "DebugSerialize , 4000000000000000,    For debugging Serialize calls.",
            "DebugDestroy , 8000000000000000,    For debugging Destroy calls."
        };

        [Flags]
        public enum EExportFlags : uint
        {
            ForcedExport = 1,
            ScriptPatcherExport = 2,
            MemberFieldPatchPending = 4,
            AllFlags = uint.MaxValue,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EExportFlags enumValue, EExportFlags flag) => (enumValue & flag) == flag;

        [Flags]
        public enum EFunctionFlags : uint
        {
            Final = 0x00000001,
            Defined = 0x00000002,
            Iterator = 0x00000004,
            Latent = 0x00000008,
            PreOperator = 0x00000010,
            Singular = 0x00000020,
            Net = 0x00000040,
            NetReliable = 0x00000080,
            Simulated = 0x00000100,
            Exec = 0x00000200,
            Native = 0x00000400,
            Event = 0x00000800,
            Operator = 0x00001000,
            Static = 0x00002000,
            HasOptionalParms = 0x00004000, //unused in ME2/1
            Const = 0x00008000, //Const is 0x00004000 in ME2/1

            Public = 0x00020000,
            Private = 0x00040000,
            Protected = 0x00080000,
            Delegate = 0x00100000,
            NetServer = 0x00200000,
            HasOutParms = 0x00400000,
            HasDefaults = 0x00800000,
            NetClient = 0x01000000,
            DLLImport = 0x02000000,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EFunctionFlags enumValue, EFunctionFlags flag) => (enumValue & flag) == flag;

        [Flags]
        public enum ScriptStructFlags : uint
        {
            Native = 0x00000001,
            Export = 0x00000002,
            HasComponents = 0x00000004,
            Transient = 0x00000008,
            Atomic = 0x00000010,
            Immutable = 0x00000020,
            StrictConfig = 0x00000040,
            ImmutableWhenCooked = 0x00000080,
            AtomicWhenCooked = 0x00000100,
            UnkStructFlag = 0x00020000,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this ScriptStructFlags enumValue, ScriptStructFlags flag) => (enumValue & flag) == flag;

        [Flags]
        public enum EStateFlags : uint
        {
            None = 0,
            Editable = 1,
            Auto = 2,
            Simulated = 4,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EStateFlags enumValue, EStateFlags flag) => (enumValue & flag) == flag;

        [Flags]
        public enum EProbeFunctions : ulong
        {
            UnusedProbe0 = 1ul << 0,
            Destroyed = 1ul << 1,
            GainedChild = 1ul << 2,
            LostChild = 1ul << 3,
            UnusedProbe4 = 1ul << 4,
            UnusedProbe5 = 1ul << 5,
            Trigger = 1ul << 6,
            UnTrigger = 1ul << 7,
            Timer = 1ul << 8,
            HitWall = 1ul << 9,
            Falling = 1ul << 10,
            Landed = 1ul << 11,
            PhysicsVolumeChange = 1ul << 12,
            Touch = 1ul << 13,
            UnTouch = 1ul << 14,
            Bump = 1ul << 15,
            BeginState = 1ul << 16,
            EndState = 1ul << 17,
            BaseChange = 1ul << 18,
            Attach = 1ul << 19,
            Detach = 1ul << 20,
            UnusedProbe21 = 1ul << 21,
            UnusedProbe22 = 1ul << 22,
            UnusedProbe23 = 1ul << 23,
            UnusedProbe24 = 1ul << 24,
            UnusedProbe25 = 1ul << 25,
            UnusedProbe26 = 1ul << 26,
            EncroachingOn = 1ul << 27,
            EncroachedBy = 1ul << 28,
            PoppedState = 1ul << 29,
            HeadVolumeChange = 1ul << 30,
            PostTouch = 1ul << 31,
            PawnEnteredVolume = 1ul << 32,
            MayFall = 1ul << 33,
            PushedState = 1ul << 34,
            PawnLeavingVolume = 1ul << 35,
            Tick = 1ul << 36,
            PlayerTick = 1ul << 37,
            ModifyVelocity = 1ul << 38,
            UnusedProbe39 = 1ul << 39,
            SeePlayer = 1ul << 40,
            EnemyNotVisible = 1ul << 41,
            HearNoise = 1ul << 42,
            UpdateEyeHeight = 1ul << 43,
            SeeMonster = 1ul << 44,
            __MISSINGPROBE = 1ul << 45,
            SpecialHandling = 1ul << 46,
            BotDesireability = 1ul << 47,
            NotifyBump = 1ul << 48,
            NotifyPhysicsVolumeChange = 1ul << 49,
            UnusedProbe50 = 1ul << 50,
            NotifyHeadVolumeChange = 1ul << 51,
            NotifyLanded = 1ul << 52,
            NotifyHitWall = 1ul << 53,
            UnusedProbe54 = 1ul << 54,
            PreBeginPlay = 1ul << 55,
            UnusedProbe56 = 1ul << 56,
            PostBeginPlay = 1ul << 57,
            UnusedProbe58 = 1ul << 58,
            PhysicsChangedFor = 1ul << 59,
            ActorEnteredVolume = 1ul << 60,
            ActorLeavingVolume = 1ul << 61,
            UnusedProbe62 = 1ul << 62,
            All = 1ul << 63,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this EProbeFunctions enumValue, EProbeFunctions flag) => (enumValue & flag) == flag;
    }
}
