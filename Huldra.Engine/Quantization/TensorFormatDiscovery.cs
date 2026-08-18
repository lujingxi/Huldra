using Huldra.Engine.Quantization;
using System.Reflection;

namespace Huldra.Engine.Quantization;

/// <summary>
/// Performs the one-time reflection scan used to discover format types.
/// </summary>
internal static class TensorFormatDiscovery
{
    private static readonly Type FormatInterface = typeof(IQuantizationFormat<>);

    internal static IEnumerable<Type> Discover(
        IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies.Distinct())
        {
            foreach (Type? type in GetLoadableTypes(assembly))
            {
                if (type is null || !IsFormatType(type))
                {
                    continue;
                }

                yield return type;
            }
        }
    }

    private static bool IsFormatType(Type type)
    {
        if (!type.IsValueType ||
            type.IsEnum ||
            type.IsGenericTypeDefinition ||
            type.IsAbstract)
        {
            return false;
        }

        foreach (Type implementedInterface in type.GetInterfaces())
        {
            if (!implementedInterface.IsGenericType ||
                implementedInterface.GetGenericTypeDefinition() != FormatInterface)
            {
                continue;
            }

            Type formatType = implementedInterface.GetGenericArguments()[0];

            // Require:
            //
            // Q4_0 : IQuantizationFormat<Q4_0>
            //
            // rather than accepting:
            //
            // SomeType : IQuantizationFormat<Q4_0>
            //
            return formatType == type;
        }

        return false;
    }

    private static IEnumerable<Type?> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types;
        }
        catch (FileLoadException)
        {
            return [];
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }
}
