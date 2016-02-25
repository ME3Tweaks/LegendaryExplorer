using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using MassEffect.Windows.Extensions;

namespace MassEffect.Windows
{
	/// <summary>
    /// Provides helper methods for raising exceptions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>ExceptionHelper</c> class provides a centralized mechanism for creating and throwing exceptions. This helps to
    /// keep exception messages and types consistent.
    /// </para>
    /// <para>
    /// Exception information is stored in an embedded resource with a default name of <c>[assemblyName].Properties.ExceptionHelper.xml</c>.
    /// The type passed into the constructor is used to determine the assembly name. If, for any reason, the default naming scheme
    /// does not work (such as when the assembly name does not match the root namespace name), there is a constructor overload that
    /// allows you to specify a custom resource name.
    /// </para>
    /// <para>
    /// The format for the exception information XML includes a grouping mechanism such that exception keys are scoped to the
    /// type throwing the exception. Thus, different types can use the same exception key because they have different scopes in
    /// the XML structure. An example of the format for the exception XML can be seen below.
    /// </para>
    /// <note type="implementation">
    /// This class is designed to be efficient in the common case (i.e. no exception thrown) but is quite inefficient if an
    /// exception is actually thrown. This is not considered a problem, however, since an exception usually indicates that
    /// execution cannot reliably continue.
    /// </note>
    /// </remarks>
    /// <example>
    /// The following example shows how an exception can thrown:
    /// <code>
    /// var exceptionHelper = new ExceptionHelper(typeof(Bar));
    /// throw exceptionHelper.Resolve("myKey", "hello");
    /// </code>
    /// Assuming this code resides in a class called <c>Foo.Bar</c>, the XML configuration might look like this:
    /// <code>
    /// <![CDATA[
    /// <?xml version="1.0" encoding="utf-8" ?> 
    /// 
    /// <exceptionHelper>
    ///     <exceptionGroup type="Foo.Bar">
    ///         <exception key="myKey" type="System.NullReferenceException">
    ///             Foo is null but I'll say '{0}' anyway.
    ///         </exception>
    ///     </exceptionGroup>
    /// </exceptionHelper>
    /// ]]>
    /// </code>
    /// With this configuration, a <see cref="NullReferenceException"/> will be thrown. The exception message will be
    /// "Foo is null but I'll say 'hello' anyway.".
    /// </example>
    /// <example>
    /// The following example shows how an exception can be conditionally thrown:
    /// <code>
    /// var exceptionHelper = new ExceptionHelper(typeof(Bar));
    /// exceptionHelper.ResolveAndThrowIf(foo == null, "myKey", "hello");
    /// </code>
    /// Assuming this code resides in a class called <c>Foo.Bar</c>, the XML configuration might look like this:
    /// <code>
    /// <![CDATA[
    /// <?xml version="1.0" encoding="utf-8" ?> 
    /// 
    /// <exceptionHelper>
    ///     <exceptionGroup type="Foo.Bar">
    ///         <exception key="myKey" type="System.NullReferenceException">
    ///             Foo is null but I'll say '{0}' anyway.
    ///         </exception>
    ///     </exceptionGroup>
    /// </exceptionHelper>
    /// ]]>
    /// </code>
    /// With this configuration, a <see cref="NullReferenceException"/> will be thrown if <c>foo</c> is <see langword="null"/>.
    /// The exception message will be "Foo is null but I'll say 'hello' anyway.".
    /// </example>
    public class ExceptionHelper
    {
        private const string typeAttributeName = "type";
        private static readonly IDictionary<ExceptionInfoKey, XDocument> exceptionInfos = new Dictionary<ExceptionInfoKey, XDocument>();
        private static readonly object exceptionInfosLock = new object();
        private readonly Type forType;
        private readonly string resourceName;

        /// <summary>
        /// Initializes a new instance of the ExceptionHelper class for the specified type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A default resource name of <c>[assemblyName].Properties.ExceptionHelper.xml</c> is used, where <c>assemblyName</c> is the name of <paramref name="forType"/>'s containing assembly.
        /// </para>
        /// </remarks>
        /// <param name="forType">
        /// The type for which exceptions will be resolved.
        /// </param>
        public ExceptionHelper(Type forType)
            : this(forType, null, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ExceptionHelper class for the specified type and with the specified resource name.
        /// </summary>
        /// <param name="forType">
        /// The type for which exceptions will be resolved.
        /// </param>
        /// <param name="resourceName">
        /// The name of the resource in which to find <c>ExceptionHelper</c> configuration.
        /// </param>
        public ExceptionHelper(Type forType, string resourceName)
            : this(forType, resourceName, 0)
        {
            resourceName.AssertNotNullOrWhiteSpace("resource");
        }

        private ExceptionHelper(Type forType, string resourceName, int dummy)
        {
            forType.AssertNotNull("forType");
            this.forType = forType;

            if (resourceName != null)
            {
                this.resourceName = resourceName;
            }
            else
            {
                // here we determine the default name for the resource
                // NOTE: PCL does not have Assembly.GetName()
                this.resourceName = string.Concat(new AssemblyName(forType.Assembly.FullName).Name, ".Properties.ExceptionHelper.xml");
            }
        }

        /// <summary>
        /// Resolves an exception.
        /// </summary>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        /// <returns>
        /// The resolved exception.
        /// </returns>
        [DebuggerHidden]
        public Exception Resolve(string exceptionKey, params object[] messageArgs)
        {
            return this.Resolve(exceptionKey, null, null, messageArgs);
        }

        /// <summary>
        /// Resolves an exception.
        /// </summary>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="innerException">
        /// The inner exception of the resolved exception.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        /// <returns>
        /// The resolved exception.
        /// </returns>
        [DebuggerHidden]
        public Exception Resolve(string exceptionKey, Exception innerException, params object[] messageArgs)
        {
            return this.Resolve(exceptionKey, null, innerException, messageArgs);
        }

        /// <summary>
        /// Resolves an exception.
        /// </summary>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="constructorArgs">
        /// Additional arguments for the resolved exception's constructor.
        /// </param>
        /// <param name="innerException">
        /// The inner exception of the resolved exception.
        /// </param>
        /// <returns>
        /// The resolved exception.
        /// </returns>
        [DebuggerHidden]
        public Exception Resolve(string exceptionKey, object[] constructorArgs, Exception innerException)
        {
            return this.Resolve(exceptionKey, constructorArgs, innerException, null);
        }

        /// <summary>
        /// Resolves an exception.
        /// </summary>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="constructorArgs">
        /// Additional arguments for the resolved exception's constructor.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        /// <returns>
        /// The resolved exception.
        /// </returns>
        [DebuggerHidden]
        public Exception Resolve(string exceptionKey, object[] constructorArgs, params object[] messageArgs)
        {
            return this.Resolve(exceptionKey, constructorArgs, null, messageArgs);
        }

        /// <summary>
        /// Resolves an exception.
        /// </summary>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="constructorArgs">
        /// Additional arguments for the resolved exception's constructor.
        /// </param>
        /// <param name="innerException">
        /// The inner exception of the resolved exception.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        /// <returns>
        /// The resolved exception.
        /// </returns>
        [DebuggerHidden]
        public Exception Resolve(string exceptionKey, object[] constructorArgs, Exception innerException, params object[] messageArgs)
        {
            exceptionKey.AssertNotNull("exceptionKey");

            var exceptionInfo = GetExceptionInfo(this.forType.Assembly, this.resourceName);
            var exceptionNode = (from exceptionGroup in exceptionInfo.Element("exceptionHelper").Elements("exceptionGroup")
                                 from exception in exceptionGroup.Elements("exception")
                                 where string.Equals(exceptionGroup.Attribute("type").Value, this.forType.FullName, StringComparison.Ordinal) && string.Equals(exception.Attribute("key").Value, exceptionKey, StringComparison.Ordinal)
                                 select exception).FirstOrDefault();

            if (exceptionNode == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "The exception details for key '{0}' could not be found at /exceptionHelper/exceptionGroup[@type'{1}']/exception[@key='{2}'].", exceptionKey, this.forType, exceptionKey));
            }

            var typeAttribute = exceptionNode.Attribute(typeAttributeName);

            if (typeAttribute == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "The '{0}' attribute could not be found for exception with key '{1}'", typeAttributeName, exceptionKey));
            }

            var type = Type.GetType(typeAttribute.Value);

            if (type == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Type '{0}' could not be loaded for exception with key '{1}'", typeAttribute.Value, exceptionKey));
            }

            if (!typeof(Exception).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Type '{0}' for exception with key '{1}' does not inherit from '{2}'", type.FullName, exceptionKey, typeof(Exception).FullName));
            }

            var message = exceptionNode.Value.Trim();

            if ((messageArgs != null) && (messageArgs.Length > 0))
            {
                message = string.Format(CultureInfo.InvariantCulture, message, messageArgs);
            }

            var constructorArgsList = new List<object>();

            // message is always first
            constructorArgsList.Add(message);

            // next, any additional constructor args
            if (constructorArgs != null)
            {
                constructorArgsList.AddRange(constructorArgs);
            }

            // finally, the inner exception, if any
            if (innerException != null)
            {
                constructorArgsList.Add(innerException);
            }

            // find the most suitable constructor given the parameters and available constructors
            var constructorArgsArr = constructorArgsList.ToArray();
            var constructor = (from candidateConstructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                               let rank = RankArgumentsAgainstParameters(constructorArgsArr, candidateConstructor.GetParameters())
                               where rank > 0
                               orderby rank descending
                               select candidateConstructor).FirstOrDefault();

            if (constructor == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "An appropriate constructor could not be found for exception type '{0}, for exception with key '{1}'", type.FullName, exceptionKey));
            }

            return (Exception)constructor.Invoke(constructorArgsArr);
        }

        /// <summary>
        /// Resolves and throws the specified exception if the given condition is met.
        /// </summary>
        /// <param name="condition">
        /// The condition that determines whether the exception will be resolved and thrown.
        /// </param>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        [DebuggerHidden]
        public void ResolveAndThrowIf(bool condition, string exceptionKey, params object[] messageArgs)
        {
            if (condition)
            {
                throw this.Resolve(exceptionKey, messageArgs);
            }
        }

        /// <summary>
        /// Resolves and throws the specified exception if the given condition is met.
        /// </summary>
        /// <param name="condition">
        /// The condition that determines whether the exception will be resolved and thrown.
        /// </param>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="innerException">
        /// The inner exception of the resolved exception.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        [DebuggerHidden]
        public void ResolveAndThrowIf(bool condition, string exceptionKey, Exception innerException, params object[] messageArgs)
        {
            if (condition)
            {
                throw this.Resolve(exceptionKey, innerException, messageArgs);
            }
        }

        /// <summary>
        /// Resolves and throws the specified exception if the given condition is met.
        /// </summary>
        /// <param name="condition">
        /// The condition that determines whether the exception will be resolved and thrown.
        /// </param>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="constructorArgs">
        /// Additional arguments for the resolved exception's constructor.
        /// </param>
        /// <param name="innerException">
        /// The inner exception of the resolved exception.
        /// </param>
        [DebuggerHidden]
        public void ResolveAndThrowIf(bool condition, string exceptionKey, object[] constructorArgs, Exception innerException)
        {
            if (condition)
            {
                throw this.Resolve(exceptionKey, constructorArgs, innerException);
            }
        }

        /// <summary>
        /// Resolves and throws the specified exception if the given condition is met.
        /// </summary>
        /// <param name="condition">
        /// The condition that determines whether the exception will be resolved and thrown.
        /// </param>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="constructorArgs">
        /// Additional arguments for the resolved exception's constructor.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        [DebuggerHidden]
        public void ResolveAndThrowIf(bool condition, string exceptionKey, object[] constructorArgs, params object[] messageArgs)
        {
            if (condition)
            {
                throw this.Resolve(exceptionKey, constructorArgs, messageArgs);
            }
        }

        /// <summary>
        /// Resolves and throws the specified exception if the given condition is met.
        /// </summary>
        /// <param name="condition">
        /// The condition that determines whether the exception will be resolved and thrown.
        /// </param>
        /// <param name="exceptionKey">
        /// The exception key.
        /// </param>
        /// <param name="constructorArgs">
        /// Additional arguments for the resolved exception's constructor.
        /// </param>
        /// <param name="innerException">
        /// The inner exception of the resolved exception.
        /// </param>
        /// <param name="messageArgs">
        /// Arguments to be substituted into the resolved exception's message.
        /// </param>
        [DebuggerHidden]
        public void ResolveAndThrowIf(bool condition, string exceptionKey, object[] constructorArgs, Exception innerException, params object[] messageArgs)
        {
            if (condition)
            {
                throw this.Resolve(exceptionKey, constructorArgs, innerException, messageArgs);
            }
        }

        [DebuggerHidden]
        private static XDocument GetExceptionInfo(Assembly assembly, string resourceName)
        {
            var retVal = (XDocument)null;
            var exceptionInfoKey = new ExceptionInfoKey(assembly, resourceName);

            lock (exceptionInfosLock)
            {
                if (exceptionInfos.ContainsKey(exceptionInfoKey))
                {
                    retVal = exceptionInfos[exceptionInfoKey];
                }
                else
                {
                    var stream = assembly.GetManifestResourceStream(resourceName);

                    if (stream == null)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "XML resource file '{0}' could not be found in assembly '{1}'.", resourceName, assembly.FullName));
                    }

                    using (var streamReader = new StreamReader(stream))
                    {
                        retVal = XDocument.Load(streamReader);
                    }

                    exceptionInfos[exceptionInfoKey] = retVal;
                }
            }

            return retVal;
        }

        // the higher the rank, the more suited the arguments are to fitting into the given parameters
        // a rank of zero means completely unsuitable and should be ignored
        private static int RankArgumentsAgainstParameters(object[] arguments, ParameterInfo[] parameters)
        {
            if (arguments.Length != parameters.Length)
            {
                return 0;
            }

            var runningRank = 0;

            for (var i = 0; i < arguments.Length; ++i)
            {
                var parameterRank = RankArgumentAgainstParameter(arguments[i], parameters[i]);

                if (parameterRank == 0)
                {
                    return 0;
                }

                runningRank += parameterRank;
            }

            return runningRank;
        }

        // rank an individual argument's suitability to fit the given parameter
        // a rank of zero means completely unsuitable
        private static int RankArgumentAgainstParameter(object argument, ParameterInfo parameter)
        {
            if (argument == null)
            {
                // limited what we can do when we have no type for the argument
                if (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) == null)
                {
                    // parameter is not nullable, but argument is null
                    return 0;
                }

                // null fits into this parameter
                return 1;
            }

            if (!parameter.ParameterType.IsAssignableFrom(argument.GetType()))
            {
                // argument is not assignable to parameter type
                return 0;
            }

            return 2;
        }

        private struct ExceptionInfoKey : IEquatable<ExceptionInfoKey>
        {
            private readonly Assembly assembly;
            private readonly string resourceName;

            public ExceptionInfoKey(Assembly assembly, string resourceName)
            {
                this.assembly = assembly;
                this.resourceName = resourceName;
            }

            public bool Equals(ExceptionInfoKey other)
            {
                return other.assembly.Equals(this.assembly) && string.Equals(other.resourceName, this.resourceName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is ExceptionInfoKey))
                {
                    return false;
                }

                return this.Equals((ExceptionInfoKey)obj);
            }

            public override int GetHashCode()
            {
                var hash = 17;
                hash = (hash * 23) + this.assembly.GetHashCode();
                hash = (hash * 23) + this.resourceName.GetHashCode();
                return hash;
            }
        }
    }
}
