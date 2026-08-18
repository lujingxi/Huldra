using Huldra.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Huldra.Engine.Backends;

internal static class BackendDiscovery
{
    internal static IReadOnlyCollection<BackendDescriptor> Discover(
        IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var result = new List<BackendDescriptor>();

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type backendType in GetLoadableTypes(assembly))
            {
                if (!ValidateBackendType(backendType))
                    continue;

                IBackend backend;

                try
                {
                    backend = (IBackend)Activator.CreateInstance(backendType)!;
                }
                catch (Exception ex)
                {
                    Logger.Log(
                        LogLevel.Error,
                        $"Failed to create backend '{backendType.FullName}': {ex}");
                    continue;
                }

                string name = backend.Name;

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (backend.Priority < 0)
                    continue;

                result.Add(
                    new BackendDescriptor(
                        backendType,
                        name,
                        backend.Priority,
                        backend));
            }
        }

        return result;
    }

    private static bool ValidateBackendType(Type backendType)
    {
        if (!typeof(IBackend).IsAssignableFrom(backendType))
            return false;

        if (backendType.IsAbstract)
            return false;

        if (backendType.IsInterface)
            return false;

        if (backendType.IsGenericTypeDefinition)
            return false;

        if (backendType.ContainsGenericParameters)
            return false;

        ConstructorInfo? constructor =
            backendType.GetConstructor(Type.EmptyTypes);

        if (constructor is null)
            return false;

        if (!constructor.IsPublic)
            return false;

        return true;
    }

    private static IEnumerable<Type> GetLoadableTypes(
        Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types
                .Where(static type => type is not null)
                .Cast<Type>();
        }
    }
}
