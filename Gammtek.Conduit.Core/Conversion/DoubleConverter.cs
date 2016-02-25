using System;
using System.Globalization;

namespace Gammtek.Conduit.Conversion
{
	public class DoubleConverter
	{
		public static string ToExactString(double d)
		{
			if (double.IsPositiveInfinity(d))
			{
				return "+Infinity";
			}

			if (double.IsNegativeInfinity(d))
			{
				return "-Infinity";
			}

			if (double.IsNaN(d))
			{
				return "NaN";
			}

			var bits = BitConverter.DoubleToInt64Bits(d);
			var negative = (bits < 0);
			var exponent = (int) ((bits >> 52) & 0x7ffL);
			var mantissa = bits & 0xfffffffffffffL;

			if (exponent == 0)
			{
				exponent++;
			}
			else
			{
				mantissa = mantissa | (1L << 52);
			}

			exponent -= 1075;

			if (mantissa == 0)
			{
				return "0";
			}

			while ((mantissa & 1) == 0)
			{
				mantissa >>= 1;
				exponent++;
			}

			var ad = new ArbitraryDecimal(mantissa);

			if (exponent < 0)
			{
				for (var i = 0; i < -exponent; i++)
				{
					ad.MultiplyBy(5);
				}
				ad.Shift(-exponent);
			}
			else
			{
				for (var i = 0; i < exponent; i++)
				{
					ad.MultiplyBy(2);
				}
			}

			if (negative)
			{
				return "-" + ad;
			}

			return ad.ToString();
		}

		#region Nested type: ArbitraryDecimal

		private class ArbitraryDecimal
		{
			private int _decimalPoint;
			private byte[] _digits;

			internal ArbitraryDecimal(long x)
			{
				var tmp = x.ToString(CultureInfo.InvariantCulture);
				_digits = new byte[tmp.Length];

				for (var i = 0; i < tmp.Length; i++)
				{
					_digits[i] = (byte) (tmp[i] - '0');
				}

				Normalize();
			}

			internal void MultiplyBy(int amount)
			{
				if (_digits == null)
				{
					throw new NullReferenceException("_digits");
				}

				var result = new byte[_digits.Length + 1];

				for (var i = _digits.Length - 1; i >= 0; i--)
				{
					var resultDigit = _digits[i] * amount + result[i + 1];
					result[i] = (byte) (resultDigit / 10);
					result[i + 1] = (byte) (resultDigit % 10);
				}

				if (result[0] != 0)
				{
					_digits = result;
				}
				else
				{
					Array.Copy(result, 1, _digits, 0, _digits.Length);
				}
				Normalize();
			}

			internal void Shift(int amount)
			{
				_decimalPoint += amount;
			}

			private void Normalize()
			{
				if (_digits == null)
				{
					throw new NullReferenceException("_digits");
				}

				int first;

				for (first = 0; first < _digits.Length; first++)
				{
					if (_digits[first] != 0)
					{
						break;
					}
				}

				int last;

				for (last = _digits.Length - 1; last >= 0; last--)
				{
					if (_digits[last] != 0)
					{
						break;
					}
				}

				if (first == 0 && last == _digits.Length - 1)
				{
					return;
				}

				var tmp = new byte[last - first + 1];
				for (var i = 0; i < tmp.Length; i++)
				{
					tmp[i] = _digits[i + first];
				}

				_decimalPoint -= _digits.Length - (last + 1);
				_digits = tmp;
			}

			public override String ToString()
			{
				if (_digits == null)
				{
					throw new NullReferenceException("_digits");
				}

				var digitString = new char[_digits.Length];

				for (var i = 0; i < _digits.Length; i++)
				{
					digitString[i] = (char) (_digits[i] + '0');
				}

				if (_decimalPoint == 0)
				{
					return new string(digitString);
				}

				if (_decimalPoint < 0)
				{
					return new string(digitString) +
						   new string('0', -_decimalPoint);
				}

				if (_decimalPoint >= digitString.Length)
				{
					return "0." +
						   new string('0', (_decimalPoint - digitString.Length)) +
						   new string(digitString);
				}

				return new string(digitString, 0,
					digitString.Length - _decimalPoint) +
					   "." +
					   new string(digitString,
						   digitString.Length - _decimalPoint,
						   _decimalPoint);
			}
		}

		#endregion
	}
}
