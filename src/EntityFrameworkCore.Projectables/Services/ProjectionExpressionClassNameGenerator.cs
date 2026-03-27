using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFrameworkCore.Projectables.Services
{
    public static class ProjectionExpressionClassNameGenerator
    {
        public const string Namespace = "EntityFrameworkCore.Projectables.Generated";

        public static string GenerateName(string? namespaceName, IEnumerable<string>? nestedInClassNames, string memberName)
        {
            return GenerateName(namespaceName, nestedInClassNames, memberName, null);
        }

        public static string GenerateName(string? namespaceName, IEnumerable<string>? nestedInClassNames, string memberName, IEnumerable<string>? parameterTypeNames)
        {
            var stringBuilder = new StringBuilder();

            return GenerateNameImpl(stringBuilder, namespaceName, nestedInClassNames, memberName, parameterTypeNames);
        }

        public static string GenerateFullName(string? namespaceName, IEnumerable<string>? nestedInClassNames, string memberName)
        {
            return GenerateFullName(namespaceName, nestedInClassNames, memberName, null);
        }

        public static string GenerateFullName(string? namespaceName, IEnumerable<string>? nestedInClassNames, string memberName, IEnumerable<string>? parameterTypeNames)
        {
            var stringBuilder = new StringBuilder(Namespace);
            stringBuilder.Append('.');

            return GenerateNameImpl(stringBuilder, namespaceName, nestedInClassNames, memberName, parameterTypeNames);
        }

        static string GenerateNameImpl(StringBuilder stringBuilder, string? namespaceName, IEnumerable<string>? nestedInClassNames, string memberName, IEnumerable<string>? parameterTypeNames)
        {
            // Append namespace, replacing '.' separators with '_' in a single pass (no intermediate string).
            if (namespaceName is not null)
            {
                foreach (var c in namespaceName)
                {
                    stringBuilder.Append(c == '.' ? '_' : c);
                }
            }

            stringBuilder.Append('_');
            var arity = 0;
            
            if (nestedInClassNames is not null)
            {

                foreach (var className in nestedInClassNames)
                {
                    var arityCharacterIndex = className.IndexOf('`');
                    if (arityCharacterIndex is -1)
                    {
                        stringBuilder.Append(className);
                    }
                    else
                    {
#if NETSTANDARD2_0
                        arity += int.Parse(className.Substring(arityCharacterIndex + 1));
#else
                        arity += int.Parse(className.AsSpan().Slice(arityCharacterIndex + 1));
#endif
                        stringBuilder.Append(className, 0, arityCharacterIndex);
                    }

                    stringBuilder.Append('_');
                }

            }

            // Append member name; only allocate a replacement string for the rare explicit-interface case.
            if (memberName.IndexOf('.') >= 0)
            {
                stringBuilder.Append(memberName.Replace(".", "__"));
            }
            else
            {
                stringBuilder.Append(memberName);
            }

            // Add parameter types to make method overloads unique
            if (parameterTypeNames is not null)
            {
                var parameterIndex = 0;
                foreach (var parameterTypeName in parameterTypeNames)
                {
                    stringBuilder.Append("_P");
                    stringBuilder.Append(parameterIndex);
                    stringBuilder.Append('_');
                    // Single-pass sanitization: replace invalid identifier characters with '_',
                    // stripping the "global::" prefix on the fly — avoids 9 intermediate string allocations.
                    AppendSanitizedTypeName(stringBuilder, parameterTypeName);
                    parameterIndex++;
                }
            }

            // Add generic arity at the very end (after parameter types)
            // This matches how the CLR names generic types
            if (arity > 0)
            {
                stringBuilder.Append('`');
                stringBuilder.Append(arity);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Appends <paramref name="typeName"/> to <paramref name="sb"/>, stripping every
        /// <c>global::</c> occurrence (leading and those inside generic type argument lists)
        /// and replacing every character that is invalid in a C# identifier with <c>'_'</c>.
        /// <para>
        /// The multi-occurrence stripping is necessary so that fully-qualified generic types
        /// such as <c>global::Foo.Wrapper&lt;global::Foo.Entity&gt;</c> — produced by Roslyn's
        /// <c>FullyQualifiedFormat</c> — yield the same sanitised name as the runtime resolver,
        /// which never includes <c>global::</c>.
        /// </para>
        /// </summary>
        private static void AppendSanitizedTypeName(StringBuilder sb, string typeName)
        {
            const string GlobalPrefix = "global::";
            const int PrefixLength = 8; // "global::".Length

            var i = 0;
            while (i < typeName.Length)
            {
                // Skip every "global::" occurrence — both the leading prefix and any that
                // appear inside generic type argument lists (e.g. "Wrapper<global::Inner>").
                if (typeName[i] == 'g'
                    && i + PrefixLength <= typeName.Length
                    && string.CompareOrdinal(typeName, i, GlobalPrefix, 0, PrefixLength) == 0)
                {
                    i += PrefixLength;
                    continue;
                }

                var c = typeName[i];
                sb.Append(IsInvalidIdentifierChar(c) ? '_' : c);
                i++;
            }
        }

        private static bool IsInvalidIdentifierChar(char c) =>
            c == '.' || c == '<' || c == '>' || c == ',' || c == ' ' ||
            c == '[' || c == ']' || c == '`' || c == ':' || c == '?';
    }
}
