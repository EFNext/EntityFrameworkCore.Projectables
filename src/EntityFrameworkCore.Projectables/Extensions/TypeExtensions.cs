using System.Reflection;

namespace EntityFrameworkCore.Projectables.Extensions;

public static class TypeExtensions
{
    public static Type[] GetNestedTypePath(this Type type)
    {
        // First pass: count the nesting depth so we can size the array exactly.
        var depth = 0;
        var current = type;
        while (true)
        {
            depth++;
            if (!current.IsNested || current.DeclaringType is null)
            {
                break;
            }

            current = current.DeclaringType;
        }

        // Second pass: fill the array outermost-first by walking back from the leaf.
        var path = new Type[depth];
        current = type;
        for (var i = depth - 1; i >= 0; i--)
        {
            path[i] = current;
            current = current.DeclaringType!;
        }

        return path;
    }

    private static bool CanHaveOverridingMethod(this Type derivedType, MethodInfo methodInfo)
    {
        // We only need to search for virtual instance methods who are not declared on the derivedType
        if (derivedType == methodInfo.DeclaringType || methodInfo.IsStatic || !methodInfo.IsVirtual)
        {
            return false;
        }

        if (!derivedType.IsAssignableTo(methodInfo.DeclaringType))
        {
            throw new ArgumentException("MethodInfo needs to be declared on the type hierarchy", nameof(methodInfo));
        }

        return true;
    }

    private static bool IsOverridingMethodOf(this MethodInfo methodInfo, MethodInfo baseDefinition)
        => methodInfo.GetBaseDefinition() == baseDefinition;

    public static MethodInfo GetOverridingMethod(this Type derivedType, MethodInfo methodInfo)
    {
        if (!derivedType.CanHaveOverridingMethod(methodInfo))
        {
            return methodInfo;
        }
            
        var derivedMethods = derivedType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        MethodInfo? overridingMethod = null;
        if (derivedMethods is { Length: > 0 })
        {
            var baseDefinition = methodInfo.GetBaseDefinition();
            overridingMethod = derivedMethods.FirstOrDefault(derivedMethodInfo
                => derivedMethodInfo.IsOverridingMethodOf(baseDefinition));
        }

        return overridingMethod ?? methodInfo; // If no derived methods were found, return the original methodInfo
    }

    private static PropertyInfo GetOverridingProperty(this Type derivedType, PropertyInfo propertyInfo)
    {
        var accessor = propertyInfo.GetAccessors(true).FirstOrDefault(derivedType.CanHaveOverridingMethod);
        if (accessor is null)
        {
            return propertyInfo;
        }

        var isGetAccessor = propertyInfo.GetMethod == accessor;
            
        var derivedProperties = derivedType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        PropertyInfo? overridingProperty = null;
        if (derivedProperties is { Length: > 0 })
        {
            var baseDefinition = accessor.GetBaseDefinition();
            overridingProperty = derivedProperties.FirstOrDefault(p
                => (isGetAccessor ? p.GetMethod : p.SetMethod)?.IsOverridingMethodOf(baseDefinition) == true);
        }

        return overridingProperty ?? propertyInfo; // If no derived methods were found, return the original methodInfo
    }

    private static MethodInfo GetImplementingMethod(this Type derivedType, MethodInfo methodInfo)
    {
        var interfaceType = methodInfo.DeclaringType;
        // We only need to search for interface methods
        if (interfaceType?.IsInterface != true || derivedType.IsInterface || methodInfo.IsStatic || !methodInfo.IsVirtual)
        {
            return methodInfo;
        }

        if (!derivedType.IsAssignableTo(interfaceType))
        {
            throw new ArgumentException("MethodInfo needs to be declared on the type hierarchy", nameof(methodInfo));
        }

        var interfaceMap = derivedType.GetInterfaceMap(interfaceType);
        for (var i = 0; i < interfaceMap.InterfaceMethods.Length; i++)
        {
            if (interfaceMap.InterfaceMethods[i] == methodInfo)
            {
                return interfaceMap.TargetMethods[i];
            }
        }

        throw new ApplicationException(
            $"The interface map for {derivedType} doesn't contain the implemented method for {methodInfo}!");
    }

    public static PropertyInfo GetImplementingProperty(this Type derivedType, PropertyInfo propertyInfo)
    {
        var accessor = propertyInfo.GetAccessors()[0];

        var implementingAccessor = derivedType.GetImplementingMethod(accessor);
        if (implementingAccessor == accessor)
        {
            return propertyInfo;
        }

        var implementingType = implementingAccessor.DeclaringType 
                               // This should only be null if it is a property accessor on the global module,
                               // which should never happen since we found it from derivedType
                               ?? throw new ApplicationException("The property accessor has no declaring type!");

        var derivedProperties = implementingType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        return derivedProperties.FirstOrDefault(propertyInfo.GetMethod == accessor
            ? p => MethodInfosEqual(p.GetMethod, implementingAccessor)
            : p => MethodInfosEqual(p.SetMethod, implementingAccessor)) ?? propertyInfo;
    }

    /// <summary>
    /// The built-in <see cref="MethodInfo.op_Equality(System.Reflection.MethodInfo?,System.Reflection.MethodInfo?)"/>
    /// does not work if the <see cref="MemberInfo.ReflectedType"/>s don't agree.
    /// </summary>
    private static bool MethodInfosEqual(MethodInfo? first, MethodInfo second)
        => first?.ReflectedType == second.ReflectedType
            ? first             == second
            : first is not null
              && first.DeclaringType == second.DeclaringType
              && first.Name          == second.Name
              && first.GetParameters().Select(p => p.ParameterType)
                  .SequenceEqual(second.GetParameters().Select(p => p.ParameterType))
              && first.GetGenericArguments().SequenceEqual(second.GetGenericArguments());

    public static MethodInfo GetConcreteMethod(this Type derivedType, MethodInfo methodInfo)
        => methodInfo.DeclaringType?.IsInterface == true
            ? derivedType.GetImplementingMethod(methodInfo)
            : derivedType.GetOverridingMethod(methodInfo);

    public static PropertyInfo GetConcreteProperty(this Type derivedType, PropertyInfo propertyInfo)
        => propertyInfo.DeclaringType?.IsInterface == true
            ? derivedType.GetImplementingProperty(propertyInfo)
            : derivedType.GetOverridingProperty(propertyInfo);
}
