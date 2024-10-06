using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Lexing;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LegendaryExplorerCore.Unreal
{
    public partial class LEXJSONState
    {

        public readonly IMEPackage Pcc;
        public readonly Func<IMEPackage, string, IEntry> MissingObjectResolver;

        private LEXJSONState(IMEPackage pcc, Func<IMEPackage, string, IEntry> missingObjectResolver = null)
        {
            Pcc = pcc;
            MissingObjectResolver = missingObjectResolver;
        }

        public string UIndexToPath(int uidx)
        {
            IEntry entry = Pcc.GetEntry(uidx);
            if (entry is null)
            {
                return "None";
            }
            return $"{entry.ClassName}'{entry.InstancedFullPath}'";
        }
        [GeneratedRegex("^([^']+)'([^']+)'$")]
        private static partial Regex ObjectLiteralRegex();

        public int PathToUIndex(string objLiteral)
        {
            if (objLiteral is "None")
            {
                return 0;
            }
            Match m = ObjectLiteralRegex().Match(objLiteral);
            if (m.Groups.Count is not 3)
            {
                throw new JsonException($"Could not parse {objLiteral} as an object literal");
            }
            string ifp = m.Groups[2].Value;
            IEntry entry = Pcc.FindEntry(ifp, m.Groups[1].Value) ?? MissingObjectResolver?.Invoke(Pcc, ifp);
            return entry?.UIndex ?? 0;
        }

        public int ReadEntryValue(ref Utf8JsonReader reader) => PathToUIndex(reader.GetString());

        public static JsonSerializerOptions CreateSerializerOptions(IMEPackage pcc, Func<IMEPackage, string, IEntry> missingObjectResolver = null, JsonSerializerOptions options = null)
        {
            options = options is null ? new JsonSerializerOptions() : new JsonSerializerOptions(options);
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            options.IncludeFields = true;
            options.WriteIndented = true;
            options.SetState(new LEXJSONState(pcc, missingObjectResolver));
            return options;
        }
    }

    public static class LEXJSONExtensions
    {
        private static readonly ConditionalWeakTable<JsonSerializerOptions, LEXJSONState> StateTable = [];

        public static bool TryGetState(this JsonSerializerOptions options, out LEXJSONState state)
        {
            return StateTable.TryGetValue(options, out state);
        }

        public static void SetState(this JsonSerializerOptions options, LEXJSONState state)
        {
            StateTable.AddOrUpdate(options, state);
        }

        public static void Expect(this ref Utf8JsonReader reader, JsonTokenType tokenType)
        {
            if (reader.TokenType != tokenType)
            {
                throw new JsonException();
            }
        }

        public static void ExpectPropertyName(this ref Utf8JsonReader reader, string expectedPropertyName)
        {
            reader.Read();
            reader.Expect(JsonTokenType.PropertyName);
            string propertyName = reader.GetString();
            if (propertyName != expectedPropertyName) throw new JsonException();
        }

        //uses out instead of return so that T is inferred (having to specify it is a bug vector, as many types are implicitly convertible)
        public static void ReadNumProp<T>(this ref Utf8JsonReader reader, out T result, string propertyName) where T : INumberBase<T>
        {
            ExpectPropertyName(ref reader, propertyName);
            reader.Read();
            if (!T.TryParse(reader.ValueSpan, CultureInfo.InvariantCulture, out result))
            {
                throw new JsonException();
            }
        }

        public static bool ReadBoolProp(this ref Utf8JsonReader reader, string propertyName)
        {
            ExpectPropertyName(ref reader, propertyName);
            reader.Read();
            return reader.GetBoolean();
        }

        public static Guid ReadGuidProp(this ref Utf8JsonReader reader, string propertyName)
        {
            ExpectPropertyName(ref reader, propertyName);
            reader.Read();
            return reader.GetGuid();
        }

        public delegate T JsonValueReadDelegate<out T>(ref Utf8JsonReader reader);

        public static List<T> ReadList<T>(this ref Utf8JsonReader reader, string propName, JsonValueReadDelegate<T> readValue)
        {
            reader.ExpectPropertyName(propName);
            reader.Read();
            reader.Expect(JsonTokenType.StartArray);
            var tempList = new List<T>();
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.EndArray)
                {
                    break;
                }
                tempList.Add(readValue(ref reader));
            }
            return tempList;
        }
    }
}
