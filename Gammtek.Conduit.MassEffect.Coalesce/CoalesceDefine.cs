using System;
using System.Globalization;
using System.Xml;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public class CoalesceDefine<T> : IDefine<T>
	{
		public string Name { get; set; }

		public T Value { get; set; }
	}

	public class CoalesceDefine : CoalesceDefine<string>
	{
		/*
		 * Bool
		 */

		public static explicit operator bool(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToBoolean(define.Value.ToLower(CultureInfo.InvariantCulture));
		}

		public static explicit operator bool?(CoalesceDefine define)
		{
			return define == null
				? new bool?()
				: XmlConvert.ToBoolean(define.Value.ToLower(CultureInfo.InvariantCulture));
		}

		/*
		 * Byte
		 */

		public static explicit operator byte(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToByte(define.Value);
		}

		public static explicit operator byte?(CoalesceDefine define)
		{
			return define == null
				? new byte?()
				: XmlConvert.ToByte(define.Value);
		}

		/*
		 * DateTime
		 */

		public static explicit operator DateTime(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return DateTime.Parse(define.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}

		public static explicit operator DateTime?(CoalesceDefine define)
		{
			return define == null
				? new DateTime?()
				: DateTime.Parse(define.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}

		/*
		 * DateTimeOffset
		 */

		public static explicit operator DateTimeOffset(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToDateTimeOffset(define.Value);
		}

		public static explicit operator DateTimeOffset?(CoalesceDefine define)
		{
			return define == null
				? new DateTimeOffset?()
				: XmlConvert.ToDateTimeOffset(define.Value);
		}

		/*
		 * Decimal
		 */

		public static explicit operator decimal(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToDecimal(define.Value);
		}

		public static explicit operator decimal?(CoalesceDefine define)
		{
			return define == null
				? new decimal?()
				: XmlConvert.ToDecimal(define.Value);
		}

		/*
		 * Double
		 */

		public static explicit operator double(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToDouble(define.Value);
		}

		public static explicit operator double?(CoalesceDefine define)
		{
			return define == null
				? new double?()
				: XmlConvert.ToDouble(define.Value);
		}

		/*
		 * Float
		 */

		public static explicit operator float(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToSingle(define.Value);
		}

		public static explicit operator float?(CoalesceDefine define)
		{
			return define == null
				? new float?()
				: XmlConvert.ToSingle(define.Value);
		}

		/*
		 * GUID
		 */

		public static explicit operator Guid(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToGuid(define.Value);
		}

		public static explicit operator Guid?(CoalesceDefine define)
		{
			return define == null
				? new Guid?()
				: XmlConvert.ToGuid(define.Value);
		}

		/*
		 * Int
		 */

		public static explicit operator int(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToInt32(define.Value);
		}

		public static explicit operator int?(CoalesceDefine define)
		{
			return define == null
				? new int?()
				: XmlConvert.ToInt32(define.Value);
		}

		/*
		 * Long
		 */

		public static explicit operator long(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToInt64(define.Value);
		}

		public static explicit operator long?(CoalesceDefine define)
		{
			return define == null
				? new long?()
				: XmlConvert.ToInt64(define.Value);
		}

		/*
		 * SByte
		 */

		public static explicit operator sbyte(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToSByte(define.Value);
		}

		public static explicit operator sbyte?(CoalesceDefine define)
		{
			return define == null
				? new sbyte?()
				: XmlConvert.ToSByte(define.Value);
		}

		/*
		 * Short
		 */

		public static explicit operator short(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToInt16(define.Value);
		}

		public static explicit operator short?(CoalesceDefine define)
		{
			return define == null
				? new short?()
				: XmlConvert.ToInt16(define.Value);
		}

		/*
		 * TimeSpan
		 */

		public static explicit operator TimeSpan(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToTimeSpan(define.Value);
		}

		public static explicit operator TimeSpan?(CoalesceDefine define)
		{
			return define == null
				? new TimeSpan?()
				: XmlConvert.ToTimeSpan(define.Value);
		}

		/*
		 * UInt
		 */

		public static explicit operator uint(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToUInt32(define.Value);
		}

		public static explicit operator uint?(CoalesceDefine define)
		{
			return define == null
				? new uint?()
				: XmlConvert.ToUInt32(define.Value);
		}

		/*
		 * ULong
		 */

		public static explicit operator ulong(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToUInt64(define.Value);
		}

		public static explicit operator ulong?(CoalesceDefine define)
		{
			return define == null
				? new ulong?()
				: XmlConvert.ToUInt64(define.Value);
		}

		/*
		 * UShort
		 */

		public static explicit operator ushort(CoalesceDefine define)
		{
			if (define == null)
			{
				throw new ArgumentNullException(nameof(define));
			}

			return XmlConvert.ToUInt16(define.Value);
		}

		public static explicit operator ushort?(CoalesceDefine define)
		{
			return define == null
				? new ushort?()
				: XmlConvert.ToUInt16(define.Value);
		}
	}
}
