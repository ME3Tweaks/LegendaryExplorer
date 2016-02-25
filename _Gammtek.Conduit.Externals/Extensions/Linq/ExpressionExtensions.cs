using System;
using System.Linq.Expressions;

namespace Gammtek.Conduit.Extensions.Linq
{
	/// <summary>
	///     Extension for <see cref="Expression" />.
	/// </summary>
	public static class ExpressionExtensions
	{
		/// <summary>
		///     Gets the parameter info for the expression.
		/// </summary>
		/// <typeparam name="T">The type of the parameter.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="ParameterInfo{T}" />.</returns>
		public static ParameterInfo<T> GetParameterInfo<T>(this Expression<Func<T>> expression)
		{
			Argument.IsNotNull(nameof(expression), expression);

			var parameterExpression = (MemberExpression)expression.Body;
			var parameterInfo = new ParameterInfo<T>(parameterExpression.Member.Name, expression.Compile().Invoke());

			return parameterInfo;
		}

		/// <summary>
		///     The parameter info.
		/// </summary>
		public class ParameterInfo<T>
		{
			/// <summary>
			///     Initializes a new instance of the <see cref="ParameterInfo{T}" /> class.
			/// </summary>
			/// <param name="name">The parameter name.</param>
			/// <param name="value">The parameter value.</param>
			public ParameterInfo(string name, T value)
			{
				Name = name;
				Value = value;
			}

			/// <summary>
			///     Gets the parameter name.
			/// </summary>
			public string Name { get; }

			/// <summary>
			///     Gets the parameter value.
			/// </summary>
			public T Value { get; }
		}
	}
}
