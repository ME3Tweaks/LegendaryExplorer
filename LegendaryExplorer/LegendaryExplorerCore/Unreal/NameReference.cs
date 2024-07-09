using System;
using System.Diagnostics;
using System.Globalization;
using LegendaryExplorerCore.Gammtek.Extensions;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Unreal
{
    [DebuggerDisplay("NameReference - Name: {Name} Number: {Number} Instanced: {Instanced}")]
    public readonly struct NameReference : IEquatable<NameReference>, IComparable<NameReference>, IComparable
    {
        private readonly int _number;
        private readonly string _name;
        
        // ReSharper disable once ConvertToAutoPropertyWhenPossible
        public string Name => _name;
        // ReSharper disable once ConvertToAutoPropertyWhenPossible
        public int Number => _number;

        public NameReference(string name, int number = 0)
        {
            _name = name;
            _number = number;
        }

        //https://api.unrealengine.com/INT/API/Runtime/Core/UObject/FName/index.html
        //heavily optimized version of this: _number > 0 ? $"{_name}_{_number - 1}" : _name;
        [JsonIgnore]
        public string Instanced
        {
            get
            {
                if (_number > 0)
                {
                    int n = _number - 1;
                    int numChars = _name.Length + 1 + NumDigitsOfPositiveInt(n);
                    return string.Create(numChars, this, (span, nameRef) =>
                    {
                        ReadOnlySpan<char> nameSpan = nameRef._name.AsSpan();
                        nameSpan.CopyTo(span);
                        int nameLength = nameSpan.Length;
                        span[nameLength] = '_';
                        ((uint)nameRef._number - 1).ToStrInPlace(span.Slice(nameLength + 1));
                    });
                }

                return _name;
            }
        }

        public int GetInstancedLength()
        {
            if (_number > 0)
            {
                int n = _number - 1;
                return _name.Length + 1 + NumDigitsOfPositiveInt(n);
            }

            return _name.Length;
        }

        /// <summary>
        /// Compares this name reference to an instanced string.
        /// </summary>
        /// <param name="instancedString"></param>
        /// <returns></returns>
        public bool EqualsInstancedString(ReadOnlySpan<char> instancedString)
        {
            Span<char> numberPortion = stackalloc char[11];
            if (_number > 0)
            {
                if (!instancedString.StartsWith(_name))
                {
                    return false;
                }
                int n = _number - 1;
                int numPortionLength = 1 + NumDigitsOfPositiveInt(n);
                if (instancedString.Length != _name.Length + numPortionLength)
                {
                    return false;
                }
                numberPortion = numberPortion[..numPortionLength];
                numberPortion[0] = '_';
                ((uint)_number).ToStrInPlace(numberPortion[1..]);
                return instancedString.EndsWith(numberPortion, StringComparison.Ordinal);
            }
            return instancedString.Equals(_name, StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Adds instanced name to end of <paramref name="parentPath"/>, after a '.'
        /// </summary>
        /// <param name="parentPath"></param>
        /// <returns></returns>
        public string AddToPath(string parentPath)
        {
            int n = _number - 1;
            int length = parentPath.Length + _name.Length + 1;
            if (_number > 0)
            {
                length += 1 + NumDigitsOfPositiveInt(n);
            }
            return string.Create(length, (parentPath, _name, n), (span, tuple) =>
            {
                ReadOnlySpan<char> parentSpan = tuple.parentPath.AsSpan();
                int parentLength = parentSpan.Length;
                parentSpan.CopyTo(span);
                span[parentLength] = '.';
                ReadOnlySpan<char> nameSpan = tuple._name.AsSpan();
                nameSpan.CopyTo(span.Slice(parentLength + 1));
                int nameLength = parentLength + 1 + nameSpan.Length;
                if (tuple.n >= 0)
                {
                    span[nameLength] = '_';
                    ((uint)tuple.n).ToStrInPlace(span.Slice(nameLength + 1));
                }
            });
        }

        /// <summary>
        /// Formats the instanced string onto the provided span. SPAN MUST BE THE EXACT LENGTH NEEDED! Use <see cref="GetInstancedLength"/> to get the correct length.
        /// </summary>
        /// <param name="span"></param>
        public void FormatInstanced(Span<char> span)
        {
            _name.CopyTo(span);
            if (_number > 0)
            {
                int n = _number - 1;
                int i = _name.Length;
                span[i] = '_';
                ((uint)n).ToStrInPlace(span.Slice(i + 1));
            }
        }

        private static int NumDigitsOfPositiveInt(int n) => (n < 100000 ? n < 100 ? n < 10 ? 1 : 2 : n < 1000 ? 3 : n < 10000 ? 4 : 5 : n < 10000000 ? n < 1000000 ? 6 : 7 : n < 100000000 ? 8 : n < 1000000000 ? 9 : 10);

        public static implicit operator NameReference(string s)
        {
            return new NameReference(s);
        }

        public static implicit operator string(NameReference n)
        {
            return n._name;
        }

        public override string ToString()
        {
            return _name ?? string.Empty;
        }

        public static bool operator ==(NameReference r, string s)
        {
            return string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(NameReference r, string s)
        {
            return !string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator ==(string s, NameReference r)
        {
            return string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(string s, NameReference r)
        {
            return !string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }

        public static NameReference FromInstancedString(string s)
        {
            int num = 0;
            int _Idx = s.LastIndexOf('_');
            if (_Idx > 0)
            {
                var numComponent = s.AsSpan(_Idx + 1);
                if (numComponent.Length > 0 
                    && !(numComponent.Length > 1 && numComponent[0] == '0') //if there's more than one character and a leading zero, it's just part of the string
                    && int.TryParse(numComponent, NumberStyles.None, null, out num))
                {
                    s = s.Substring(0, _Idx);
                    num += 1;
                }
            }
            return new NameReference(s, num);
        }

        #region IEquatable
        public static bool operator ==(NameReference n1, NameReference n2)
        {
            return n1.Equals(n2);
        }

        public static bool operator !=(NameReference n1, NameReference n2)
        {
            return !n1.Equals(n2);
        }
        public bool Equals(NameReference other)
        {
            return string.Equals(_name, other._name, StringComparison.OrdinalIgnoreCase) && _number == other._number;
        }

        public override bool Equals(object obj)
        {
            return obj is NameReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_name.GetHashCode() * 397) ^ _number;
            }
        }
        #endregion

        #region IComparable
        public int CompareTo(NameReference other)
        {
            int nameComparison = string.Compare(_name, other._name, StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0) return nameComparison;
            return _number.CompareTo(other._number);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            return obj is NameReference other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(NameReference)}");
        }

        public static bool operator <(NameReference left, NameReference right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(NameReference left, NameReference right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(NameReference left, NameReference right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(NameReference left, NameReference right)
        {
            return left.CompareTo(right) >= 0;
        } 
        #endregion

        public static readonly NameReference None = new("None");
    }
}