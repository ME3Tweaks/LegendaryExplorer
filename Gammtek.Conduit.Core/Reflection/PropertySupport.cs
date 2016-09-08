using System;
using System.Linq.Expressions;
using System.Reflection;
using Gammtek.Conduit.Properties;

namespace Gammtek.Conduit.Reflection
{
	/// <summary>
	///     Provides support for extracting property information based on a property expression.
	/// </summary>
	public static class PropertySupport
	{
		/// <summary>
		///     Extracts the property name from a property expression.
		/// </summary>
		/// <typeparam name="T">The object type containing the property specified in the expression.</typeparam>
		/// <param name="propertyExpression">The property expression (e.g. p => p.PropertyName)</param>
		/// <returns>The name of the property.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="propertyExpression" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     Thrown when the expression is:<br />
		///     Not a <see cref="MemberExpression" /><br />
		///     The <see cref="MemberExpression" /> does not represent a property.<br />
		///     Or, the property is static.
		/// </exception>
		public static string ExtractPropertyName<T>(Expression<Func<T>> propertyExpression)
		{
			if (propertyExpression == null)
			{
				throw new ArgumentNullException(nameof(propertyExpression));
			}

			var memberExpression = propertyExpression.Body as MemberExpression;

			if (memberExpression == null)
			{
				throw new ArgumentException(Resources.PropertySupport_NotMemberAccessExpression_Exception, nameof(propertyExpression));
			}

			var property = memberExpression.Member as PropertyInfo;

			if (property == null)
			{
				throw new ArgumentException(Resources.PropertySupport_ExpressionNotProperty_Exception, nameof(propertyExpression));
			}

			var getMethod = property.GetMethod;

			if (getMethod.IsStatic)
			{
				throw new ArgumentException(Resources.PropertySupport_StaticExpression_Exception, nameof(propertyExpression));
			}

			return memberExpression.Member.Name;
		}
	}
}
