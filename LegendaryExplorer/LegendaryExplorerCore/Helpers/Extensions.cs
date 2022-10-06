using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Helpers
{
    public static class TaskExtensions
    {
        //no argument passed to continuation
        public static Task ContinueWithOnUIThread(this Task task, Action<Task> continuationAction, TaskContinuationOptions continuationOptions = default)
        {
            return task.ContinueWith(continuationAction, default, continuationOptions, LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT);
        }

        //no argument passed to and result returned from continuation
        public static Task<TNewResult> ContinueWithOnUIThread<TNewResult>(this Task task, Func<Task, TNewResult> continuationAction, TaskContinuationOptions continuationOptions = default)
        {
            return task.ContinueWith(continuationAction, default, continuationOptions, LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT);
        }

        //argument passed to continuation
        public static Task ContinueWithOnUIThread<TResult>(this Task<TResult> task, Action<Task<TResult>> continuationAction, TaskContinuationOptions continuationOptions = default)
        {
            return task.ContinueWith(continuationAction, default, continuationOptions, LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT);
        }

        //argument passed to and result returned from continuation
        public static Task<TNewResult> ContinueWithOnUIThread<TResult, TNewResult>(this Task<TResult> task, Func<Task<TResult>, TNewResult> continuationAction, TaskContinuationOptions continuationOptions = default)
        {
            return task.ContinueWith(continuationAction, default, continuationOptions, LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT);
        }
    }
    public static class EnumerableExtensions
    {
        public static int FindOrAdd<T>(this List<T> list, T element)
        {
            int idx = list.IndexOf(element);
            if (idx == -1)
            {
                list.Add(element);
                idx = list.Count - 1;
            }
            return idx;
        }

        /// <summary>
        /// Searches for the specified object and returns the index of its first occurence, or -1 if it is not found
        /// </summary>
        /// <param name="array">The one-dimensional array to search</param>
        /// <param name="value">The object to locate in <paramref name="array" /></param>
        /// <typeparam name="T">The type of the elements of the array.</typeparam>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="array" /> is null.</exception>
        public static int IndexOf<T>(this T[] array, T value)
        {
            return Array.IndexOf(array, value);
        }

        public static int IndexOf<T>(this LinkedList<T> list, LinkedListNode<T> node)
        {
            LinkedListNode<T> temp = list.First;
            for (int i = 0; i < list.Count; i++)
            {
                if (node == temp)
                {
                    return i;
                }
                temp = temp.Next;
            }
            return -1;
        }

        public static int IndexOf<T>(this LinkedList<T> list, T node)
        {
            LinkedListNode<T> temp = list.First;
            for (int i = 0; i < list.Count; i++)
            {
                if (node.Equals(temp.Value))
                {
                    return i;
                }
                temp = temp.Next;
            }
            return -1;
        }

        public static LinkedListNode<T> Node<T>(this LinkedList<T> list, T node)
        {
            LinkedListNode<T> temp = list.First;
            for (int i = 0; i < list.Count; i++)
            {
                if (node.Equals(temp.Value))
                {
                    return temp;
                }
                temp = temp.Next;
            }
            return null;
        }

        public static void RemoveAt<T>(this LinkedList<T> list, int index)
        {
            list.Remove(list.NodeAt(index));
        }

        public static LinkedListNode<T> NodeAt<T>(this LinkedList<T> list, int index)
        {
            LinkedListNode<T> temp = list.First;
            for (int i = 0; i < list.Count; i++)
            {
                if (i == index)
                {
                    return temp;
                }
                temp = temp.Next;
            }
            throw new ArgumentOutOfRangeException();
        }

        public static T First<T>(this LinkedList<T> list)
        {
            return list.First.Value;
        }
        public static T Last<T>(this LinkedList<T> list)
        {
            return list.Last.Value;
        }

        /// <summary>
        /// Overwrites a portion of an array starting at offset with the contents of another array.
        /// Accepts negative indexes
        /// </summary>
        /// <typeparam name="T">Content of array.</typeparam>
        /// <param name="dest">Array to write to</param>
        /// <param name="offset">Start index in dest. Can be negative (eg. last element is -1)</param>
        /// <param name="source">data to write to dest</param>
        public static void OverwriteRange<T>(this IList<T> dest, int offset, IList<T> source)
        {
            if (offset < 0)
            {
                offset = dest.Count + offset;
                if (offset < 0)
                {
                    throw new IndexOutOfRangeException("Attempt to write before the beginning of the array.");
                }
            }
            if (offset + source.Count > dest.Count)
            {
                throw new IndexOutOfRangeException("Attempt to write past the end of the array.");
            }
            for (int i = 0; i < source.Count; i++)
            {
                dest[offset + i] = source[i];
            }
        }

        public static T[] Slice<T>(this T[] src, int start, int length)
        {
            var slice = new T[length];
            src.AsSpan(start, length).CopyTo(slice);
            return slice;
        }

        public static T[] ArrayClone<T>(this T[] src)
        {
            var copy = new T[src.Length];
            src.AsSpan().CopyTo(copy);
            return copy;
        }

        /// <summary>
        /// Creates a shallow copy
        /// </summary>
        public static List<T> Clone<T>(this IEnumerable<T> src)
        {
            return new List<T>(src);
        }

        //https://stackoverflow.com/a/26880541
        /// <summary>
        /// Searches for the specified array and returns the index of its first occurence, or -1 if it is not found
        /// </summary>
        /// <param name="haystack">The one-dimensional array to search</param>
        /// <param name="needle">The object to locate in <paramref name="haystack" /></param>
        /// <param name="start">The index to start searching at</param>
        public static int IndexOfArray<T>(this T[] haystack, T[] needle, int start = 0) where T : IEquatable<T>
        {
            var len = needle.Length;
            var limit = haystack.Length - len;
            for (var i = start; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (!needle[k].Equals(haystack[i + k])) break;
                }
                if (k == len) return i;
            }
            return -1;
        }

        public static bool IsEmpty<T>(this ICollection<T> list) => list.Count == 0;

        public static bool IsEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.Any();
        public static bool Any<T>(this ICollection<T> collection) => collection.Count > 0;

        public static bool HasExactly<T>(this IEnumerable<T> src, int count)
        {
            if (count < 0) return false;
            foreach (var _ in src)
            {
                if (count <= 0)
                {
                    return false;
                }

                --count;
            }

            return count == 0;
        }

        public static IEnumerable<T> NonNull<T>(this IEnumerable<T> src) where T : class
        {
            return src.Where(obj => obj != null);
        }

        public static string StringJoin<T>(this IEnumerable<T> values, string separator)
        {
            return string.Join(separator, values);
        }

        public static bool SubsetOf<T>(this IList<T> src, IList<T> compare)
        {
            return src.All(compare.Contains);
        }

        /// <summary>
        /// Removes all items that match the predicate
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static bool RemoveAll<T>(this IList<T> list, Predicate<T> predicate)
        {
            bool removed = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    list.RemoveAt(i);
                    i--;
                    removed = true;
                }
            }

            return removed;
        }

        /// <summary>
        /// Tries to remove an item that matches the predicate
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="predicate"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static bool TryRemove<T>(this IList<T> list, Predicate<T> predicate, out T item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    item = list[i];
                    list.RemoveAt(i);
                    return true;
                }
            }
            item = default;
            return false;
        }

        //IEnumerable containing everything after item
        public static IEnumerable<T> After<T>(this IEnumerable<T> src, T item)
        {
            using IEnumerator<T> enumerator = src.GetEnumerator();
            while (enumerator.MoveNext() && enumerator.Current?.Equals(item) != true) { } //Explicit comparison to true necessary because it's a nullable bool

            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }


        //IEnumerable containing everything before item
        public static IEnumerable<T> Before<T>(this IEnumerable<T> src, T item)
        {
            return src.TakeWhile(t => !t.Equals(item));
        }

        //IEnumerable containing everything after item, then everything before item
        //useful for looparound search starting in the middle of a list
        public static IEnumerable<T> AfterThenBefore<T>(this IEnumerable<T> src, T item)
        {
            return src.After(item).Concat(src.Before(item));
        }

        public static void Add<T, U>(this IList<(T, U)> list, T item1, U item2) => list.Add((item1, item2));

        public static void Add<T, U, V>(this IList<(T, U, V)> list, T item1, U item2, V item3) => list.Add((item1, item2, item3));

        public static void Add<T, U, V, W>(this IList<(T, U, V, W)> list, T item1, U item2, V item3, W item4) => list.Add((item1, item2, item3, item4));

        public static void Add<T>(this Stack<T> stack, T item) => stack.Push(item);

        //This allows a partially enumerated IEnumerator to be further enumerated in a foreach
        public static IEnumerator<T> GetEnumerator<T>(this IEnumerator<T> enumerator) => enumerator;
    }

    public static class DictionaryExtensions
    {
        /// <summary>
        /// Adds <paramref name="value"/> to List&lt;<typeparamref name="TValue"/>&gt; associated with <paramref name="key"/>. Creates List&lt;<typeparamref name="TValue"/>&gt; if neccesary.
        /// </summary>
        public static void AddToListAt<TKey, TValue>(this IDictionary<TKey, List<TValue>> dict, TKey key, TValue value)
        {
            if (!dict.TryGetValue(key, out List<TValue> list))
            {
                list = new List<TValue>();
                dict[key] = list;
            }
            list.Add(value);
        }
        public static void AddToListAt<TKey, TValue>(this IDictionary<TKey, ObservableCollectionExtended<TValue>> dict, TKey key, TValue value)
        {
            if (!dict.TryGetValue(key, out ObservableCollectionExtended<TValue> list))
            {
                list = new ObservableCollectionExtended<TValue>();
                dict[key] = list;
            }
            list.Add(value);
        }

        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict.ContainsKey(key))
            {
                return false;
            }
            dict.Add(key, value);
            return true;
        }
    }

    public static class StringExtensions
    {
        /// <summary>
        /// Splits on Environment.Newline
        /// </summary>
        /// <param name="s"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static string[] SplitLines(this string s, StringSplitOptions options = StringSplitOptions.None)
        {
            return s.Split(new[] { Environment.NewLine }, options);
        }

        /// <summary>
        /// Capitalizes the first letter in the string.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string UpperFirst(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;
            return char.ToUpper(str[0]) + str.Substring(1);
        }
        public static bool RepresentsPackageFilePath(this string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(@".pcc", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".sfm", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".u", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".upk", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".udk", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".xxx", StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }

        public static bool IsNumericallyEqual(this string first, string second)
        {
            return double.TryParse(first, out double a)
                && double.TryParse(second, out double b)
                && (Math.Abs(a - b) < double.Epsilon);
        }

        //based on algorithm described here: http://www.codeproject.com/Articles/13525/Fast-memory-efficient-Levenshtein-algorithm
        public static int LevenshteinDistance(this string a, string b)
        {
            int n = a.Length;
            int m = b.Length;
            if (n == 0)
            {
                return m;
            }
            if (m == 0)
            {
                return n;
            }

            int[] v1 = new int[m + 1];
            for (int i = 0; i <= m; i++)
            {
                v1[i] = i;
            }

            for (int i = 1; i <= n; i++)
            {
                int[] v0 = v1;
                v1 = new int[m + 1];
                v1[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int above = v1[j - 1] + 1;
                    int left = v0[j] + 1;
                    int cost;
                    if (j > m || j > n)
                    {
                        cost = 1;
                    }
                    else
                    {
                        cost = a[j - 1] == b[j - 1] ? 0 : 1;
                    }
                    cost += v0[j - 1];
                    v1[j] = Math.Min(above, Math.Min(left, cost));
                }
            }

            return v1[m];
        }

        public static bool FuzzyMatch(this IEnumerable<string> words, string word, double threshold = 0.75)
        {
            foreach (string s in words)
            {
                int dist = s.LevenshteinDistance(word);
                if (1 - (double)dist / Math.Max(s.Length, word.Length) > threshold)
                {
                    return true;
                }
            }
            return false;
        }

        public static Guid ToGuid(this string src) //Do not edit this function!
        {
            byte[] stringbytes = Encoding.UTF8.GetBytes(src);
            byte[] hashedBytes = System.Security.Cryptography.SHA1.Create().ComputeHash(stringbytes);
            Array.Resize(ref hashedBytes, 16);
            return new Guid(hashedBytes);
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CaseInsensitiveEquals(this string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        public static string GetPathWithoutInvalids(this string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }
        
        public static bool IsLatin1(this string value)
        {
            return string.Equals(value, Encoding.Latin1.GetString(Encoding.Latin1.GetBytes(value)));
        }
    }


    public static class IOExtensions
    {
        public static string ReadUnrealString(this Stream stream)
        {
            int length = stream.ReadInt32();
            if (length == 0)
            {
                return "";
            }
            return length < 0 ? stream.ReadStringUnicodeNull(length * -2) : stream.ReadStringLatin1Null(length);
        }

        public static void WriteUnrealString(this Stream stream, string value, MEGame game)
        {
            if (game.IsGame3() || game is MEGame.LE1 or MEGame.LE2 && !value.IsLatin1())
            {
                stream.WriteUnrealStringUnicode(value);
            }
            else
            {
                stream.WriteUnrealStringLatin1(value);
            }
        }

        public static void WriteUnrealString(this EndianWriter stream, string value, MEGame game)
        {
            if (game.IsGame3() || game is MEGame.LE1 or MEGame.LE2 && !value.IsLatin1())
            {
                stream.WriteUnrealStringUnicode(value);
            }
            else
            {
                stream.WriteUnrealStringLatin1(value);
            }
        }

        public static void WriteUnrealStringLatin1(this Stream stream, string value)
        {
            if (value?.Length > 0)
            {
                stream.WriteInt32(value.Length + 1);
                stream.WriteStringLatin1Null(value);
            }
            else
            {
                stream.WriteInt32(0);
            }
        }

        public static void WriteUnrealStringUnicode(this Stream stream, string value)
        {
            if (value?.Length > 0)
            {
                stream.WriteInt32(-(value.Length + 1));
                stream.WriteStringUnicodeNull(value);
            }
            else
            {
                stream.WriteInt32(0);
            }
        }

        public static void WriteUnrealStringLatin1(this EndianWriter stream, string value)
        {
            if (value?.Length > 0)
            {
                stream.WriteInt32(value.Length + 1);
                stream.WriteStringLatin1Null(value);
            }
            else
            {
                stream.WriteInt32(0);
            }
        }

        public static void WriteUnrealStringUnicode(this EndianWriter stream, string value)
        {
            if (value?.Length > 0)
            {
                stream.WriteInt32(-(value.Length + 1));
                stream.WriteStringUnicodeNull(value);
            }
            else
            {
                stream.WriteInt32(0);
            }
        }

        public static void WriteStream(this Stream stream, Stream value)
        {
            value.Position = 0;
            value.CopyTo(stream);
        }

        /// <summary>
        /// Copies the inputstream to the outputstream, for the specified amount of bytes
        /// </summary>
        /// <param name="input">Stream to copy from</param>
        /// <param name="output">Stream to copy to</param>
        /// <param name="bytes">The number of bytes to copy</param>
        public static void CopyToEx(this Stream input, Stream output, int bytes)
        {
            var bufSize = 32768;
            var buffer = MemoryManager.GetByteArray(bufSize);
            int read;
            while (bytes > 0 &&
                   (read = input.Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
            {
                output.Write(buffer, 0, read);
                bytes -= read;
            }
            MemoryManager.ReturnByteArray(buffer);
        }

        public static NameReference ReadNameReference(this Stream stream, IMEPackage pcc)
        {
            return new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32());
        }

        public static NameReference ReadNameReference(this EndianReader stream, IMEPackage pcc)
        {
            return new NameReference(pcc.GetNameEntry(stream.ReadInt32()), stream.ReadInt32());
        }

        public static void WriteNameReference(this Stream stream, NameReference name, IMEPackage pcc)
        {
            stream.WriteInt32(pcc.FindNameOrAdd(name.Name));
            stream.WriteInt32(name.Number);
        }

        public static void WriteNameReference(this EndianWriter stream, NameReference name, IMEPackage pcc)
        {
            stream.WriteInt32(pcc.FindNameOrAdd(name.Name));
            stream.WriteInt32(name.Number);
        }
    }

    public static class ByteArrayExtensions
    {
        private static readonly int[] Empty = Array.Empty<int>();

        public static int[] Locate(this byte[] self, byte[] candidate)
        {
            if (IsEmptyLocate(self, candidate))
                return Empty;

            var list = new List<int>();

            for (int i = 0; i < self.Length; i++)
            {
                if (!IsMatch(self, i, candidate))
                    continue;

                list.Add(i);
            }

            return list.Count == 0 ? Empty : list.ToArray();
        }

        static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            if (candidate.Length > (array.Length - position))
                return false;

            for (int i = 0; i < candidate.Length; i++)
                if (array[position + i] != candidate[i])
                    return false;

            return true;
        }

        static bool IsEmptyLocate(byte[] array, byte[] candidate)
        {
            return array == null
                   || candidate == null
                   || array.Length == 0
                   || candidate.Length == 0
                   || candidate.Length > array.Length;
        }
    }

    public static class UnrealExtensions
    {
        /// <summary>
        /// Converts an ME3Explorer index to an unreal index. (0 and up get incremented, less than zero stays the same)
        /// </summary>
        public static int ToUnrealIdx(this int i)
        {
            return i >= 0 ? i + 1 : i;
        }

        /// <summary>
        /// Converts an unreal index to an ME3Explorer index. (greater than zero gets decremented, less than zero stays the same,
        /// and 0 returns null)
        /// </summary>
        public static int? FromUnrealIdx(this int i)
        {
            if (i == 0)
            {
                return null;
            }
            return i > 0 ? i - 1 : i;
        }

        /// <summary>
        /// Converts Degrees to Unreal rotation units
        /// </summary>
        public static int DegreesToUnrealRotationUnits(this float degrees) => Convert.ToInt32(degrees * 65536f / 360f);

        /// <summary>
        /// Converts Radians to Unreal rotation units
        /// </summary>
        public static int RadiansToUnrealRotationUnits(this float radians) => Convert.ToInt32(radians * 180 / MathF.PI * 65536f / 360f);

        /// <summary>
        /// Converts Unreal rotation units to Degrees
        /// </summary>
        public static float UnrealRotationUnitsToDegrees(this int unrealRotationUnits) => unrealRotationUnits * 360f / 65536f;

        /// <summary>
        /// Converts Unreal rotation units to Radians
        /// </summary>
        public static float UnrealRotationUnitsToRadians(this int unrealRotationUnits) => unrealRotationUnits * 360.0f / 65536.0f * MathF.PI / 180.0f;

        /// <summary>
        /// Checks if this object is of a specific generic type (e.g. List&lt;IntProperty&gt;)
        /// </summary>
        /// <param name="typeToCheck">typeof() of the item you are checking</param>
        /// <param name="genericType">typeof() of the value you are checking against</param>
        /// <returns>True if type matches, false otherwise</returns>
        public static bool IsOfGenericType(this Type typeToCheck, Type genericType)
        {
            return typeToCheck.IsOfGenericType(genericType, out Type _);
        }

        /// <summary>
        /// Checks if this object is of a specific generic type (e.g. List&lt;IntProperty&gt;)
        /// </summary>
        /// <param name="typeToCheck">typeof() of the item you are checking</param>
        /// <param name="genericType">typeof() of the value you are checking against</param>
        /// <param name="concreteGenericType">Concrete type output if this result is true</param>
        /// <returns>True if type matches, false otherwise</returns>
        public static bool IsOfGenericType(this Type typeToCheck, Type genericType, out Type concreteGenericType)
        {
            while (true)
            {
                concreteGenericType = null;

                if (genericType == null)
                    throw new ArgumentNullException(nameof(genericType));

                if (!genericType.IsGenericTypeDefinition)
                    throw new ArgumentException("The definition needs to be a GenericTypeDefinition", nameof(genericType));

                if (typeToCheck == null || typeToCheck == typeof(object))
                    return false;

                if (typeToCheck == genericType)
                {
                    concreteGenericType = typeToCheck;
                    return true;
                }

                if ((typeToCheck.IsGenericType ? typeToCheck.GetGenericTypeDefinition() : typeToCheck) == genericType)
                {
                    concreteGenericType = typeToCheck;
                    return true;
                }

                if (genericType.IsInterface)
                    foreach (var i in typeToCheck.GetInterfaces())
                        if (i.IsOfGenericType(genericType, out concreteGenericType))
                            return true;

                typeToCheck = typeToCheck.BaseType;
            }
        }

        public static float AsFloat16(this ushort float16bits)
        {
            int sign = (float16bits >> 15) & 0x00000001;
            int exp = (float16bits >> 10) & 0x0000001F;
            int mant = float16bits & 0x000003FF;
            switch (exp)
            {
                case 0:
                    return 0f;
                case 31:
                    return 65504f;
            }
            exp += (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
        }

        public static ushort ToFloat16bits(this float f)
        {
            byte[] bytes = BitConverter.GetBytes((double)f);
            ulong bits = BitConverter.ToUInt64(bytes, 0);
            ulong exponent = bits & 0x7ff0000000000000L;
            ulong mantissa = bits & 0x000fffffffffffffL;
            ulong sign = bits & 0x8000000000000000L;
            int placement = (int)((exponent >> 52) - 1023);
            if (placement is > 15 or < -14)
                return 0;
            ushort exponentBits = (ushort)((15 + placement) << 10);
            ushort mantissaBits = (ushort)(mantissa >> 42);
            ushort signBits = (ushort)(sign >> 48);
            return (ushort)(exponentBits | mantissaBits | signBits);
        }

        /// <summary>
        /// expects a float in range -1 to 1, anything outside that will be clamped to it.
        /// </summary>
        public static byte PackToByte(this float f)
        {
            return (byte)((int)(f * 127.5f + 128.0f)).Clamp(0, 255);
        }

        public static uint bits(this uint word, byte from, byte to)
        {
            Contract.Assert(from < 32);
            Contract.Assert(to < 32);
            Contract.Assert(to < from);

            return (word << (31 - from)) >> (31 - from + to);
        }

        public static bool IsBinarilyIdentical(this float f1, float f2)
        {
            return BitConverter.SingleToInt32Bits(f1) == BitConverter.SingleToInt32Bits(f2);
        }

        public static Rotator GetRotator(this Matrix4x4 m)
        {
            var xAxis = new Vector3(m.M11, m.M12, m.M13);
            var yAxis = new Vector3(m.M21, m.M22, m.M23);
            var zAxis = new Vector3(m.M31, m.M32, m.M33);

            var pitch = Math.Atan2(xAxis.Z, Math.Sqrt(Math.Pow(xAxis.X, 2) + Math.Pow(xAxis.Y, 2)));
            var yaw = Math.Atan2(xAxis.Y, xAxis.X);

            var sy = Math.Sin(yaw);
            var cy = Math.Cos(yaw);


            var syAxis = new Vector3((float)-sy, (float)cy, 0f);

            var roll = Math.Atan2(Vector3.Dot(zAxis, syAxis), Vector3.Dot(yAxis, syAxis));

            return new Rotator(RadToURR(pitch), RadToURR(yaw), RadToURR(roll));

            static int RadToURR(double d)
            {
                return ((float)(d * (180.0 / Math.PI))).DegreesToUnrealRotationUnits();
            }
        }

        public static (Vector3 translation, Vector3 scale, Rotator rotation) UnrealDecompose(this Matrix4x4 m)
        {
            Vector3 translation = m.Translation;
            Vector3 scale = new Vector3(MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12 + m.M13 * m.M13),
                                        MathF.Sqrt(m.M21 * m.M21 + m.M22 * m.M22 + m.M23 * m.M23),
                                        MathF.Sqrt(m.M31 * m.M31 + m.M32 * m.M32 + m.M33 * m.M33));

            if (SharpDX.MathUtil.IsZero(scale.X) ||
                SharpDX.MathUtil.IsZero(scale.Y) ||
                SharpDX.MathUtil.IsZero(scale.Z))
            {
                return (translation, scale, default);
            }

            m.M11 /= scale.X;
            m.M12 /= scale.X;
            m.M13 /= scale.X;

            m.M21 /= scale.Y;
            m.M22 /= scale.Y;
            m.M23 /= scale.Y;

            m.M31 /= scale.Z;
            m.M32 /= scale.Z;
            m.M33 /= scale.Z;
            return (translation, scale, m.GetRotator());
        }

        public static uint ReinterpretAsUint(this int i) => BitConverter.ToUInt32(BitConverter.GetBytes(i), 0);

        public static int ReinterpretAsInt(this uint i) => BitConverter.ToInt32(BitConverter.GetBytes(i), 0);
    }

    public static class Enums
    {
        public static T[] GetValues<T>() where T : Enum
        {
            return (T[])Enum.GetValues(typeof(T));
        }
        public static string[] GetNames<T>() where T : Enum
        {
            return Enum.GetNames(typeof(T));
        }

        public static T Parse<T>(string val) where T : Enum
        {
            return (T)Enum.Parse(typeof(T), val);
        }
        public static T[] MaskToList<T>(this T mask, bool ignoreDefault = true) where T : Enum
        {
            var q = GetValues<T>().Where(t => mask.Has(t));
            if (ignoreDefault)
            {
                q = q.Where(v => !v.Equals(default(T)));
            }
            return q.ToArray();
        }

        //The Enum.HasFlag method boxes both enumValue and flag!
        //It is best to define a specific override of this method for each flagged Enum, with this implementation:
        // (enumValue & flag) == flag
        public static bool Has<T>(this T enumValue, T flag) where T : Enum
        {
            return enumValue.HasFlag(flag);
        }
    }

    public static class TypeExtension
    {
        public static object InvokeGenericMethod(this Type type, string methodName, Type genericType, object invokeOn, params object[] parameters)
        {
            return type.GetMethod(methodName).MakeGenericMethod(genericType).Invoke(invokeOn, parameters);
        }
    }

    public static class ExceptionExtensions
    {
        /// <summary>
        /// Flattens an exception into a printable string
        /// </summary>
        /// <param name="exception">Exception to flatten</param>
        /// <returns>Printable string</returns>
        public static string FlattenException(this Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }
    }

    /// <summary>
    /// For use with List initializers
    /// </summary>
    /// <example>
    /// <code>
    /// var intList = new List&lt;int&gt;
    /// {
    ///     1,
    ///     2,
    ///     3,
    ///     InitializerHelper.ConditionalAdd(shouldAdd465, () =&gt; new[]
    ///     {
    ///         4,
    ///         5,
    ///         6
    ///     }),
    ///     7,
    ///     8
    /// }
    /// //intList would only contain 4,5, and 6 if shouldAdd456 was true 
    /// </code>
    /// </example>
    public static class ListInitHelper
    {
        public class InitCollection<T> : List<T>
        {
            public InitCollection(IEnumerable<T> collection) : base(collection) { }
            public InitCollection() { }
        }

        public static InitCollection<T> ConditionalAddOne<T>(bool condition, Func<T> elem) => condition ? new InitCollection<T> { elem() } : null;

        public static InitCollection<T> ConditionalAddOne<T>(bool condition, Func<T> ifTrue, Func<T> ifFalse) => new InitCollection<T> { condition ? ifTrue() : ifFalse() };

        public static InitCollection<T> ConditionalAdd<T>(bool condition, Func<IEnumerable<T>> elems) => condition ? new InitCollection<T>(elems()) : null;

        public static InitCollection<T> ConditionalAdd<T>(bool condition, Func<IEnumerable<T>> ifTrue, Func<IEnumerable<T>> ifFalse) =>
            new InitCollection<T>(condition ? ifTrue() : ifFalse());

        //this may appear to have no references, but it is implicitly called whenever ConditionalAdd is used in a List initializer
        //VS's "Find All References" can't figure this out, but Resharper's "Find Usages" can 
        public static void Add<T>(this List<T> list, InitCollection<T> range)
        {
            if (range != null) list.AddRange(range);
        }
    }

    public static class MiscExtensions
    {
        public static bool bit(this uint word, byte index)
        {
            Contract.Assert(index < 32);

            return (word << (31 - index)) >> 31 == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumDigits(this int n)
        {
            return n < 100000 ? n < 100 ? n < 10 ? 1 : 2 : n < 1000 ? 3 : n < 10000 ? 4 : 5 : n < 10000000 ? n < 1000000 ? 6 : 7 : n < 100000000 ? 8 : n < 1000000000 ? 9 : 10;
        }
    }
}
