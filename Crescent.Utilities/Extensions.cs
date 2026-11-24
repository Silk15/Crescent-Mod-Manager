using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Crescent.Utilities
{
    public static class Extensions
    {
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
    }
}