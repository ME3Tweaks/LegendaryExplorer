using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PropertyInfo = System.Reflection.PropertyInfo;

namespace LegendaryExplorerCore.Tests;

internal static class LE3ParityInspection
{
    private const int MaxDepth = 6;
    private const int MaxItems = 512;

    internal static void Write(string directory, string stage, ExportEntry export, params object[] details)
    {
        if (string.IsNullOrEmpty(directory)) return;

        Directory.CreateDirectory(directory);
        string packageName = Path.GetFileNameWithoutExtension(export?.FileRef?.FilePath) ?? "package";
        string stem = string.Join("_", Safe(packageName), Safe(export?.InstancedFullPath), Safe(stage));
        string jsonPath = Path.Combine(directory, stem + ".json");

        var document = new JObject
        {
            ["package"] = packageName,
            ["exportPath"] = export?.InstancedFullPath,
            ["class"] = export?.ClassName,
            ["archetype"] = export?.Archetype?.InstancedFullPath,
            ["stage"] = stage,
            ["properties"] = DumpExportProperties(export),
            ["effectiveProperties"] = export == null || export.ClassName?.StartsWith("Texture", StringComparison.Ordinal) == true ? new JArray() :
                DumpProperties(export.GetPropertiesIncludingInherited(), export.FileRef, 0)
        };

        var detailDump = new JObject();
        if (details != null)
        {
            for (int i = 0; i < details.Length; i++)
            {
                object detail = details[i];
                if (detail == null) continue;
                string detailName = detail.GetType().Name;
                detailDump[detailName + (details.Take(i).Count(d => d?.GetType() == detail.GetType()) == 0
                    ? string.Empty
                    : i.ToString(CultureInfo.InvariantCulture))] = DumpObject(detail, 0, new System.Collections.Generic.HashSet<object>(ReferenceComparer.Instance), out string hlsl);
                if (!string.IsNullOrEmpty(hlsl))
                    File.WriteAllText(Path.Combine(directory, stem + ".usf"), hlsl);
            }
        }

        document["details"] = detailDump;
        if (export != null && string.Equals(stage, "decooked", StringComparison.OrdinalIgnoreCase))
        {
            var expressions = new JArray();
            foreach (ExportEntry expression in export.FileRef.Exports
                         .Where(e => e.ClassName?.StartsWith("MaterialExpression", StringComparison.Ordinal) == true)
                         .OrderBy(e => e.UIndex))
            {
                expressions.Add(new JObject
                {
                    ["uindex"] = expression.UIndex,
                    ["path"] = expression.InstancedFullPath,
                    ["class"] = expression.ClassName,
                    ["properties"] = DumpExportProperties(expression)
                });
            }
            document["materialExpressions"] = expressions;
        }

        File.WriteAllText(jsonPath, document.ToString(Formatting.Indented));
    }

    private static JArray DumpExportProperties(ExportEntry export) =>
        export == null || export.ClassName?.StartsWith("Texture", StringComparison.Ordinal) == true
            ? new JArray()
            : DumpProperties(export.GetProperties(), export.FileRef, 0);

    private static JArray DumpProperties(IEnumerable properties, IMEPackage package, int depth)
    {
        var result = new JArray();
        if (properties == null || depth > MaxDepth) return result;
        int count = 0;
        foreach (object item in properties)
        {
            if (count++ >= MaxItems) break;
            if (item is not Property property) continue;

            var node = new JObject
            {
                ["name"] = property.Name.Instanced,
                ["type"] = property.GetType().Name
            };
            IMEPackage valuePackage = property.InheritedFrom == null ? package : null;
            if (property.InheritedFrom != null)
                node["inheritedFrom"] = DumpObject(property.InheritedFrom, 0,
                    new System.Collections.Generic.HashSet<object>(ReferenceComparer.Instance), out _);
            if (property is StructProperty structure)
            {
                node["structType"] = structure.StructType;
                node["properties"] = DumpProperties(structure.Properties, valuePackage, depth + 1);
            }
            else if (property is ArrayPropertyBase array)
            {
                var values = new JArray();
                int itemCount = 0;
                foreach (object value in array)
                {
                    if (itemCount++ >= MaxItems) break;
                    values.Add(DumpPropertyValue(value, valuePackage, depth + 1));
                }
                node["items"] = values;
            }
            else if (property is ObjectProperty objectProperty)
            {
                int uIndex = objectProperty.Value;
                IEntry entry = valuePackage?.GetEntry(uIndex);
                node["uindex"] = uIndex;
                node["resolvedPath"] = entry?.InstancedFullPath;
                node["resolvedClass"] = entry?.ClassName;
            }
            else
            {
                PropertyInfo valueProperty = property.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProperty?.CanRead == true)
                    node["value"] = DumpObject(valueProperty.GetValue(property), depth + 1,
                        new System.Collections.Generic.HashSet<object>(ReferenceComparer.Instance), out _);
            }
            result.Add(node);
        }
        return result;
    }

    private static JToken DumpPropertyValue(object value, IMEPackage package, int depth)
    {
        if (value is Property property)
        {
            var wrapper = new JObject
            {
                ["name"] = property.Name.Instanced,
                ["type"] = property.GetType().Name
            };
            if (property is StructProperty structure)
                wrapper["properties"] = DumpProperties(structure.Properties, package, depth + 1);
            else if (property is ArrayPropertyBase array)
            {
                var items = new JArray();
                foreach (object item in array.Cast<object>().Take(MaxItems)) items.Add(DumpPropertyValue(item, package, depth + 1));
                wrapper["items"] = items;
            }
            else if (property is ObjectProperty objectProperty)
            {
                int uIndex = objectProperty.Value;
                IEntry entry = package?.GetEntry(uIndex);
                wrapper["uindex"] = uIndex;
                wrapper["resolvedPath"] = entry?.InstancedFullPath;
                wrapper["resolvedClass"] = entry?.ClassName;
            }
            else
            {
                PropertyInfo valueProperty = property.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProperty?.CanRead == true)
                    wrapper["value"] = DumpObject(valueProperty.GetValue(property), depth + 1,
                        new System.Collections.Generic.HashSet<object>(ReferenceComparer.Instance), out _);
            }
            return wrapper;
        }
        return DumpObject(value, depth, new System.Collections.Generic.HashSet<object>(ReferenceComparer.Instance), out _);
    }

    private static JToken DumpObject(object value, int depth, System.Collections.Generic.HashSet<object> seen, out string hlsl)
    {
        hlsl = null;
        if (value == null) return JValue.CreateNull();
        Type type = value.GetType();
        if (value is NameReference name) return new JValue(name.Instanced);
        if (value is Guid guid) return new JValue(guid.ToString());
        if (value is IEntry entry) return new JValue(entry.InstancedFullPath);
        if (value is IMEPackage package) return new JValue(package.FilePath);
        if (value is string text) return new JValue(text);
        if (type.IsEnum) return new JValue(value.ToString());
        if (type.IsPrimitive || type == typeof(decimal)) return new JValue(value);
        if (depth >= MaxDepth) return new JValue("<max-depth>");

        bool track = !type.IsValueType;
        if (track && !seen.Add(value)) return new JValue("<cycle>");
        try
        {
            if (value is IEnumerable enumerable)
            {
                var array = new JArray();
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count++ >= MaxItems) break;
                    array.Add(DumpObject(item, depth + 1, seen, out _));
                }
                return array;
            }

            var result = new JObject { ["type"] = type.FullName };
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (ShouldSkip(property.Name, property.PropertyType)) continue;
                object member;
                try { member = property.GetValue(value); }
                catch (Exception ex) { result[property.Name] = "<getter-error: " + ex.GetType().Name + ">"; continue; }
                if (string.Equals(property.Name, "MaterialHlsl", StringComparison.Ordinal) && member is string source)
                {
                    hlsl = source;
                    result[property.Name] = "<written to sibling .usf>";
                }
                else
                {
                    result[property.Name] = DumpObject(member, depth + 1, seen, out string nestedHlsl);
                    hlsl ??= nestedHlsl;
                }
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal))
                if (!result.ContainsKey(field.Name) && !ShouldSkip(field.Name, field.FieldType))
                {
                    result[field.Name] = DumpObject(field.GetValue(value), depth + 1, seen, out string nestedHlsl);
                    hlsl ??= nestedHlsl;
                }
            return result;
        }
        finally
        {
            if (track) seen.Remove(value);
        }
    }

    private static bool ShouldSkip(string name, Type type) =>
        name is "Graph" or "SourceSnapshot" or "CompilationInput" or "FileRef" ||
        name.Contains("Shader", StringComparison.OrdinalIgnoreCase) &&
        (name.Contains("Map", StringComparison.OrdinalIgnoreCase) || typeof(IDictionary).IsAssignableFrom(type));

    private static string Safe(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) || c == '/' || c == '\\' ? '_' : c));
    }

    private sealed class ReferenceComparer : System.Collections.Generic.IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
