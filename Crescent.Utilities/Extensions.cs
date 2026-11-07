using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Crescent.Utilities
{
    public static class Extensions
    {
        public static readonly List<char> BlacklistedCharacters = new()
        {
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9',
            '0',
            '.',
            ','
        };

        public static readonly List<string> Archives = new()
        {
            ".zip",
            ".rar",
            ".7z"
        };

        public static string WithPlural(this int value) => value == 1 ? "" : "s";

        public static string WithoutNumbers(this string value)
        {
            StringBuilder stringBuilder = new();
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!BlacklistedCharacters.Contains(character)) stringBuilder.Append(character);
            }

            return stringBuilder.ToString();
        }

        public static string WithoutSpaces(this string value)
        {
            value = value.Replace(" ", string.Empty);
            return value;
        }

        public static string Cleaned(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = Regex.Replace(value, @"(?<=[a-z])([A-Z])", " $1");
            value = Regex.Replace(value, @"\b(v?\d+(?:\.\d+)*)\b", string.Empty);
            value = Regex.Replace(value, @"\b[eEkK]\b", string.Empty);
            value = Regex.Replace(value, @"\s+", " ");
            return value.Trim();
        }
        
        public static bool IsArchive(this string fileName) => Archives.Any(a => fileName.EndsWith(a, StringComparison.OrdinalIgnoreCase));

        public static bool IsNativeBinary(this string fileName) =>
            fileName.StartsWith("clr", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("host", StringComparison.OrdinalIgnoreCase) || fileName.Contains("native", StringComparison.OrdinalIgnoreCase) || fileName.Equals("msquic.dll", StringComparison.OrdinalIgnoreCase) || fileName.Equals("sni.dll", StringComparison.OrdinalIgnoreCase) || fileName.Equals("vcruntime140_app.dll", StringComparison.OrdinalIgnoreCase) || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

        public static void TryOpen(this string value)
        {
            Process.Start(new ProcessStartInfo(value)
            {
                UseShellExecute = true
            });
        }

        public static bool AnyEquals(this IEnumerable<string> collection, string value)
        {
            foreach (string item in collection)
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static bool TryGetCustomAttribute<T>(this MemberInfo memberInfo, out T? attribute) where T : Attribute
        {
            object[] customAttributes = memberInfo.GetCustomAttributes(typeof(T), false);
            for (int i = 0; i < customAttributes.Length; i++)
                if (customAttributes[i] is T)
                {
                    attribute = (T)customAttributes[i];
                    return true;
                }

            attribute = null;
            return false;
        }

        public static async Task InitialiseOnLoadTypesAsync(this Assembly assembly)
        {
            IEnumerable<MethodInfo> methods = assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).Where(m => m.TryGetCustomAttribute(out InitialiseOnLoadAttribute _));
            foreach (MethodInfo methodInfo in methods)
            {
                try
                {
                    object? result = methodInfo.Invoke(null, null);

                    if (result is Task task) await task;
                    else if (result is ValueTask valueTask) await valueTask;
                }
                catch (Exception ex)
                {
                }
            }
        }
    }
}