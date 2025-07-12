using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using System;
using System.Diagnostics;
using System.IO;

namespace LegendaryExplorerCore.Unreal.BinaryConverters;

// This class must still take pcc so subclasses that can use pcc will pass it up
// Callers using straight packageless should pass null for pcc

/// <summary>
/// Serializer used by the GlobalShaderCache and, to serialize name references as strings. This cannot serialize objects.
/// </summary>
/// <param name="stream"></param>
/// <param name="isLoading"></param>
/// <param name="offset"></param>
/// <param name="packageCache"></param>
public class PackagelessSerializingContainer(Stream stream, IMEPackage pcc, bool isLoading = false, int offset = 0, PackageCache packageCache = null) : SerializingContainer(stream, pcc, isLoading, offset, packageCache)
{
    // Name references are directly written.
    public override void Serialize(ref NameReference name)
    {
        if (IsLoading)
        {
            name = NameReference.FromInstancedString(ms.ReadUnrealString());
        }
        else
        {
            ms.Writer.WriteUnrealString(name.Instanced, Game);
        }
    }

    public override void Serialize(ref NameReference? name)
    {
        if (IsLoading)
        {
            name = NameReference.FromInstancedString(ms.ReadUnrealString());
        }
        else if (name.HasValue)
        {
            ms.Writer.WriteUnrealString(name.Value.Instanced, Game);
        }
        else
        {
            // Can't serialize null name
            Debugger.Break();
        }
    }

    public override void SerializeObjectRef(ref int val)
    {
        throw new Exception(nameof(PackagelessSerializingContainer) + " cannot serialize object references.");
    }
}

/// <summary>
/// SPECIAL SERIALIZER - Serializes names as strings, serializes objects in special format. This does NOT support serializing, only deserializing!
/// </summary>
/// <param name="stream"></param>
/// <param name="pcc"></param>
/// <param name="isLoading"></param>
/// <param name="offset"></param>
/// <param name="packageCache"></param>
public class PackagelessWithObjectsSerializingContainer(Stream stream, IMEPackage pcc, bool isLoading = false, int offset = 0, PackageCache packageCache = null) : PackagelessSerializingContainer(stream, pcc, isLoading, offset, packageCache)
{
    // Special deserialization method.
    // Does NOT support writing serialization!

    public override void SerializeObjectRef(ref int val)
    {
        if (!IsLoading)
        {
            throw new Exception(nameof(PackagelessSerializingContainer) + " cannot be used to serialize object references when saving.");
        }

        var objectIfp = stream.ReadUnrealString();
        var foundEntry = pcc.FindEntry(objectIfp);
        if (foundEntry == null)
        {
            Debug.WriteLine($"Could not find {objectIfp} in package, value will be set to null");
            val = 0;
        }
        else
        {
            val = foundEntry.UIndex;
        }

    }
}
