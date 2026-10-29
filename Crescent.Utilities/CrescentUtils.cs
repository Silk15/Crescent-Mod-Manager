using System.Reflection;

namespace Crescent.Utilities
{
    public static class CrescentUtils
    {
        public static List<Type> GetDerivedTypes(this Type baseType)
        {
            List<Type> derivedTypes = new();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type derivedType in assembly.GetTypes())
                    if (derivedType.IsSubclassOf(baseType) && !derivedType.IsAbstract)
                        derivedTypes.Add(derivedType);
            }

            return derivedTypes;
        }
    }
}