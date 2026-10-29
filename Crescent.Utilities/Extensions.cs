using System.Reflection;

namespace Crescent.Utilities
{
    public static class Extensions
    {
        public static string AsPlural(this int value) => value == 1 ? "" : "s";
        
        public static bool IsArchive(this string fileName) => fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase);

        public static bool IsNativeBinary(this string fileName) => fileName.StartsWith("clr", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("host", StringComparison.OrdinalIgnoreCase) || fileName.Contains("native", StringComparison.OrdinalIgnoreCase) || fileName.Equals("msquic.dll", StringComparison.OrdinalIgnoreCase) || fileName.Equals("sni.dll", StringComparison.OrdinalIgnoreCase) || fileName.Equals("vcruntime140_app.dll", StringComparison.OrdinalIgnoreCase) || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

        public static bool AnyEquals(this IEnumerable<string> collection, string value)
        {
            foreach (string item in collection)
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase)) return true;
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
                catch (Exception ex) { }
            }
        }
    }
}