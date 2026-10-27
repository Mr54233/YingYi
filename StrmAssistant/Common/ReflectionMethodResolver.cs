using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StrmAssistant.Common
{
    internal static class ReflectionMethodResolver
    {
        public static MethodInfo FindExactMethod(Type targetType, IEnumerable<string> names, Type returnType,
            params Type[][] acceptedSignatures)
        {
            if (targetType is null) throw new ArgumentNullException(nameof(targetType));
            if (names is null) throw new ArgumentNullException(nameof(names));
            if (returnType is null) throw new ArgumentNullException(nameof(returnType));

            var methodNames = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var signature in acceptedSignatures)
            {
                var method = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(candidate =>
                    {
                        if (!methodNames.Contains(candidate.Name) || candidate.ReturnType != returnType) return false;
                        var parameters = candidate.GetParameters();
                        return parameters.Length == signature.Length &&
                               parameters.Select(p => p.ParameterType).SequenceEqual(signature);
                    });

                if (method != null) return method;
            }

            return null;
        }

        /// <summary>
        ///     Resolves a method by name and parameter-count only, requiring that each supplied
        ///     concrete parameter type is assignable from the declared parameter type. Parameters
        ///     whose expected type is unknown at compile time (e.g. Emby.Naming.NamingOptions)
        ///     are passed as <c>null</c> and skipped in the comparison. Return type is not compared.
        ///     Use when the target signature varies across Emby builds (4.8 / 4.9 / 4.10).
        /// </summary>
        public static MethodInfo FindByNameAndArity(Type targetType, string name, int parameterCount,
            params Type[] knownParameterTypes)
        {
            if (targetType is null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            return targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != name) return false;
                    var parameters = candidate.GetParameters();
                    if (parameters.Length != parameterCount) return false;

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var declared = parameters[i].ParameterType;
                        var known = i < knownParameterTypes.Length ? knownParameterTypes[i] : null;
                        if (known is null) continue; // unknown at compile time, accept as-is
                        if (declared.IsAssignableFrom(known)) continue;
                        return false;
                    }

                    return true;
                });
        }
    }
}
