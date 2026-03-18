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
        /// Appends <paramref name="typeName"/> to <paramref name="sb"/>, stripping the
        /// <c>global::</c> prefix and replacing every character that is invalid in a C# identifier
        /// with <c>'_'</c> — all in a single pass with no intermediate string allocations.
        /// </summary>
        private static void AppendSanitizedTypeName(StringBuilder sb, string typeName)
        {
            const string GlobalPrefix = "global::";
            var start = typeName.StartsWith(GlobalPrefix, StringComparison.Ordinal) ? GlobalPrefix.Length : 0;

            for (var i = start; i < typeName.Length; i++)
            {
                var c = typeName[i];
                sb.Append(IsInvalidIdentifierChar(c) ? '_' : c);
            }
        }

        private static bool IsInvalidIdentifierChar(char c) =>
            c == '.' || c == '<' || c == '>' || c == ',' || c == ' ' ||
            c == '[' || c == ']' || c == '`' || c == ':' || c == '?';
    }
}
