using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using EntityFrameworkCore.Projectables.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.Projectables.Services
{
    public sealed class ProjectableExpressionReplacer : ExpressionVisitor
    {
        private readonly IProjectionExpressionResolver _resolver;
        private readonly ExpressionArgumentReplacer _expressionArgumentReplacer = new();
        private readonly Dictionary<MemberInfo, LambdaExpression?> _projectableMemberCache = new();
        private readonly HashSet<ConstructorInfo> _expandingConstructors = new();
        private IQueryProvider? _currentQueryProvider;
        private bool _disableRootRewrite = false;
        private readonly bool _trackingByDefault;
        private IEntityType? _entityType;

        internal readonly static bool _polymorphicDispatchGlobal = ((bool?)AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return [];
                }
            })
            .FirstOrDefault(t => t.FullName == "EntityFrameworkCore.Projectables.Generated.ProjectableGlobalOptions")
            ?.GetProperty("PolymorphicDispatch", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null))
                ?? false;

        // Extract MethodInfo via expression trees (trim-safe; computed once per AppDomain)
        private readonly static MethodInfo _select =
            ((MethodCallExpression)((Expression<Func<IQueryable<object>, IQueryable<object>>>)
                (q => q.Select(x => x))).Body).Method.GetGenericMethodDefinition();

        private readonly static MethodInfo _where =
            ((MethodCallExpression)((Expression<Func<IQueryable<object>, IQueryable<object>>>)
                (q => q.Where(x => true))).Body).Method.GetGenericMethodDefinition();

        // Static caches — keyed by CLR type, shared across all instances for the AppDomain lifetime.
        // ConditionalWeakTable uses "ephemeron" semantics: the Type key is not kept alive by the
        // cache entry, so types from collectible AssemblyLoadContexts can still be unloaded.
        private readonly static ConditionalWeakTable<Type, StrongBox<bool>> _compilerGeneratedClosureCache = new();
        private readonly static ConditionalWeakTable<Type, PropertyInfo[]> _projectablePropertiesCache = new();
        private readonly static ConditionalWeakTable<Type, MethodInfo> _closedSelectCache = new();
        private readonly static ConditionalWeakTable<Type, MethodInfo> _closedWhereCache = new();

        public ProjectableExpressionReplacer(IProjectionExpressionResolver projectionExpressionResolver, bool trackByDefault = false)
        {
            _trackingByDefault = trackByDefault;
            _resolver = projectionExpressionResolver;
        }

        bool TryGetReflectedExpression(MemberInfo memberInfo, [NotNullWhen(true)] out LambdaExpression? reflectedExpression)
        {
            if (!_projectableMemberCache.TryGetValue(memberInfo, out reflectedExpression))
            {
                var projectableAttribute = memberInfo.GetCustomAttribute<ProjectableAttribute>(false);

                reflectedExpression = projectableAttribute is not null
                    ? _resolver.FindGeneratedExpression(memberInfo, projectableAttribute)
                    : null;

                _projectableMemberCache.Add(memberInfo, reflectedExpression);
            }

            return reflectedExpression is not null;
        }

        [return: NotNullIfNotNull(nameof(node))]
        public Expression? Replace(Expression? node)
        {
            _disableRootRewrite = _trackingByDefault;
            _currentQueryProvider = null;
            _entityType = null;

            var ret = Visit(node);

            if (_disableRootRewrite)
            {
                // This boolean is enabled when a "Select" is encountered 
                return ret;
            }

            switch (ret)
            {
                // Probably a First() or ToList()
                case MethodCallExpression { Arguments.Count: > 0, Object: null } call when _entityType != null:
                {
                    // if return type != IQueryable {
                    //     if return type is IEnuberable {
                    //         // case of a ToList()
                    //         return (ret.arg[0]).Select(...).ToList() or the other method
                    //     } else {
                    //         // case of a Max() 
                    //         return ret;
                    //     }
                    // } else if retrun type == entitytype {
                    //     // case of a first()
                    //     return obj.MyMap(x => new Obj {});
                    // }
                    
                    if (call.Method.ReturnType.IsAssignableTo(typeof(IQueryable)))
                    {
                        // Generic case where the return type is still a IQueryable<T>
                        return _AddProjectableSelect(call, _entityType);
                    }

                    if (call.Method.ReturnType == _entityType.ClrType)
                    {
                        // case of a .First(), .SingleAsync()
                        if (call.Arguments.Count != 1 && true /* Add && arg.count == 1 exist */)
                        {
                            // .First(x => whereCondition), since we need to add a select after the last condition but
                            // before the query become executed by EF (before the .First()), we rewrite the .First(where)
                            // as .Where(where).Select(x => ...).First()
            
                            var whereMethod = _closedWhereCache.GetValue(_entityType.ClrType, t => _where.MakeGenericMethod(t));
                            var where = Expression.Call(null, whereMethod, call.Arguments);
                            // The call instance is based on the wrong polymorphied method.
                            var first  = call.Method.DeclaringType?.GetMethods()
                                .FirstOrDefault(x => x.Name == call.Method.Name && x.GetParameters().Length == 1);
                            if (first == null)
                            {
                                // Unknown case that should not happen.
                                return call;
                            }

                            return Expression.Call(null, first.MakeGenericMethod(_entityType.ClrType), _AddProjectableSelect(where, _entityType));
                        }
                        
                        // .First() without arguments is the same case as bellow so we let it fallthrough
                    }
                    else if (!call.Method.ReturnType.IsAssignableTo(typeof(IEnumerable)))
                    {
                        // case of something like a .Max(), .Sum()
                        return call;
                    }
                    
                    // return type is IEnumerable<EntityType> or EntityType (in case of fallthrough from a .First())
                    
                    // case of something like .ToList(), .ToArrayAsync()
                    var self = _AddProjectableSelect(call.Arguments.First(), _entityType);
                    return call.Update(null, call.Arguments.Skip(1).Prepend(self));
                }
                case QueryRootExpression root when _entityType != null:
                    return _AddProjectableSelect(root, _entityType);
                default:
                    return ret;
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Replace MethodGroup arguments with their reflected expressions.
            // No-alloc fast-path: scan args without allocating; only copy the array and call
            // Update() when a replacement is actually found (method-group arguments are rare).
            Expression[]? updatedArgs = null;
            for (var i = 0; i < node.Arguments.Count; i++)
            {
                if (node.Arguments[i] is UnaryExpression {
                        NodeType: ExpressionType.Convert,
                        Operand: MethodCallExpression {
                            NodeType: ExpressionType.Call,
                            Method: { Name: nameof(MethodInfo.CreateDelegate), DeclaringType.Name: nameof(MethodInfo) },
                            Object: ConstantExpression { Value: MethodInfo capturedMethodInfo }
                        }
                    } && TryGetReflectedExpression(capturedMethodInfo, out var expressionArg))
                {
                    (updatedArgs ??= [.. node.Arguments])[i] = expressionArg;
                }
            }
            if (updatedArgs is not null)
            {
                node = node.Update(node.Object, updatedArgs);
            }

            // Get the overriding methodInfo based on te type of the received of this expression
            var methodInfo = node.Object?.Type.GetConcreteMethod(node.Method) ?? node.Method;

            if (methodInfo.Name == nameof(Queryable.Select))
            {
                _disableRootRewrite = true;
            }

            if (methodInfo.Name == nameof(EntityFrameworkQueryableExtensions.AsTracking))
            {
                _disableRootRewrite = true;
            }
            if (methodInfo.Name is nameof(EntityFrameworkQueryableExtensions.AsNoTracking) or nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution))
            {
                _disableRootRewrite = false;
            }

            // Check if we are rewriting a base invocation ((BaseType)@this).MyMethod(...) or ((BaseBaseType)(BaseType)@this).MyMethod(...)
            // We are only checking for a type cast from a type to its immediate parent,
            // unwrapping nested casts, because the original parameter might have been replaced
            var isBase = (node.Object is UnaryExpression u && UnwrapUnaryConvert(u) != u);

            var polymorphicDispatch = !isBase && IsPolymorphic(methodInfo) &&
                methodInfo.GetCustomAttribute<ProjectableAttribute>() is ProjectableAttribute projectable &&
                (projectable.PolymorphicDispatch || _polymorphicDispatchGlobal);

            if ((TryGetReflectedExpression(methodInfo, out var reflectedExpression) && reflectedExpression != null) || polymorphicDispatch)
            {
                if (polymorphicDispatch)
                {
                    var derivedTypes = RetrieveTypes(methodInfo.DeclaringType!, methodInfo);
                    if (derivedTypes.Count > 0)
                    {
                        var arguments = node.Arguments.ToArray();

                        // Check if the method has an implementation or if it is abstract, if it is not abstract it will be added
                        // as the last result in the if/else if/else chain, otherwise the last type will be used instead
                        Expression body;
                        if (reflectedExpression != null)
                        {
                            for (var parameterIndex = 0; parameterIndex < reflectedExpression.Parameters.Count; parameterIndex++)
                            {
                                var parameterExpression = reflectedExpression.Parameters[parameterIndex];
                                var mappedArgumentExpression = (parameterIndex, node.Object) switch {
                                    (0, not null) => node.Object,
                                    (_, not null) => node.Arguments[parameterIndex - 1],
                                    (_, null) => node.Arguments.Count > parameterIndex ? node.Arguments[parameterIndex] : null
                                };

                                if (mappedArgumentExpression is not null)
                                {
                                    _expressionArgumentReplacer.ParameterArgumentMapping.Add(parameterExpression, mappedArgumentExpression);
                                }
                            }

                            var updatedBody = _expressionArgumentReplacer.Visit(reflectedExpression.Body);
                            _expressionArgumentReplacer.ParameterArgumentMapping.Clear();

                            // @this is Type1 ? ((Type1)@this).Method(...) : ...
                            // ... ? ... :
                            // @this is TypeN ? ((TypeN)@this).Method(...) : ...
                            // virtualImplementation
                            body = derivedTypes.AsEnumerable()
                                .Reverse()
                                .Aggregate(updatedBody, AggregateTypes);
                        }
                        else
                        {
                            // DEV: handle generic types
                            var lastType = derivedTypes[derivedTypes.Count - 1];

                            // @this is Type1 ? ((Type1)@this).Method(...) : ...
                            // ... ? ... :
                            // ((TypeN)@this).Method(...)
                            body = derivedTypes.AsEnumerable()
                                .Reverse()
                                .Skip(1)
                                .Aggregate((Expression)Expression.Call(Expression.Convert(node.Object!, lastType), methodInfo.Name, null, arguments), AggregateTypes);
                        }

                        return Visit(body);


                        Expression AggregateTypes(Expression expr, Type type)
                        {
                            return Expression.Condition(
                                Expression.TypeIs(node.Object!, type),
                                Expression.Call(Expression.Convert(node.Object!, type), methodInfo.Name, null, arguments),
                                expr);
                        }
                    }
                }

                if (reflectedExpression != null)
                {
                    for (var parameterIndex = 0; parameterIndex < reflectedExpression.Parameters.Count; parameterIndex++)
                    {
                        var parameterExpression = reflectedExpression.Parameters[parameterIndex];
                        var mappedArgumentExpression = (parameterIndex, node.Object) switch {
                            (0, not null) => node.Object,
                            (_, not null) => node.Arguments[parameterIndex - 1],
                            (_, null) => node.Arguments.Count > parameterIndex ? node.Arguments[parameterIndex] : null
                        };

                        if (mappedArgumentExpression is not null)
                        {
                            // If base we can remove the cast
                            if (isBase && mappedArgumentExpression is UnaryExpression u2)
                            {
                                mappedArgumentExpression = UnwrapUnaryConvert(u2);
                            }

                            _expressionArgumentReplacer.ParameterArgumentMapping.Add(parameterExpression, mappedArgumentExpression);
                        }
                    }

                    var updatedBody = _expressionArgumentReplacer.Visit(reflectedExpression.Body);
                    _expressionArgumentReplacer.ParameterArgumentMapping.Clear();

                    return Visit(updatedBody);
                }
            }

            return base.VisitMethodCall(node);
        }

        internal static bool IsPolymorphic(MethodInfo? method)
        {
            return method != null && (method.IsAbstract || method.IsVirtual || method.GetBaseDefinition() != method);
        }

        private static Expression UnwrapUnaryConvert(UnaryExpression node)
        {
            if (node.NodeType != ExpressionType.Convert || node.Type != node.Operand.Type.BaseType)
            {
                return node;
            }

            if (node.Operand is UnaryExpression u)
            {
                return UnwrapUnaryConvert(u);
            }
            else
            {
                return node.Operand;
            }
        }

        private static List<Type> RetrieveTypes(Type baseType, MemberInfo member)
        {
            Func<Type, MemberInfo?> memberGetter;
            if (member is MethodInfo method)
            {
                var parameters = method.GetParameters()
                    .Select(p => p.ParameterType)
                    .ToArray();
                memberGetter = t => t.GetMethod(member.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, parameters);
            }
            else
            {
                memberGetter = t => t.GetProperty(member.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, ((PropertyInfo)member).PropertyType, Array.Empty<Type>(), null);
            }

            // Retrieve all the derived types which have an override of the member
            // DEV: maybe cache?
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        return [];
                    }
                })
                .Where(t => t != baseType && t.IsAssignableTo(baseType) && memberGetter.Invoke(t) != null)
                .OrderByDescending(GetDepth) // More specific types first
                .ThenBy(t => t.Name)
                .ToList();

            // Remove types which are derived from another type in the list which has the declared symbol
            // with the Projectable attribute (generation will be delegated to them)
            var typesToRemove = types.Where(t => types.Any(tt => t != tt && t.IsAssignableTo(tt) &&
                    memberGetter.Invoke(t)?.GetCustomAttribute<ProjectableAttribute>() != null))
                .ToList();

            foreach (var type in typesToRemove)
            {
                types.Remove(type);
            }

            return types;


            static int GetDepth(Type type)
            {
                var depth = 0;
                while (type.BaseType != null)
                {
                    depth++;
                    type = type.BaseType;
                }

                return depth;
            }
        }

        protected override Expression VisitNew(NewExpression node)
        {
            var constructor = node.Constructor;
            if (constructor is not null &&
                !_expandingConstructors.Contains(constructor) &&
                TryGetReflectedExpression(constructor, out var reflectedExpression))
            {
                _expandingConstructors.Add(constructor);
                try
                {
                    for (var parameterIndex = 0; parameterIndex < reflectedExpression.Parameters.Count; parameterIndex++)
                    {
                        var parameterExpression = reflectedExpression.Parameters[parameterIndex];
                        if (parameterIndex < node.Arguments.Count)
                        {
                            _expressionArgumentReplacer.ParameterArgumentMapping.Add(parameterExpression, node.Arguments[parameterIndex]);
                        }
                    }

                    var updatedBody = _expressionArgumentReplacer.Visit(reflectedExpression.Body);
                    return base.Visit(updatedBody);
                }
                finally
                {
                    _expressionArgumentReplacer.ParameterArgumentMapping.Clear();
                    _expandingConstructors.Remove(constructor);
                }
            }

            return base.VisitNew(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // Evaluate captured variables in closures that contain EF queries to inline them into the main query
            if (node.Expression is ConstantExpression constant &&
                IsCompilerGeneratedClosure(constant.Type))
            {
                try
                {
                    // Cheap type check first: only call GetValue() when the declared type
                    // could possibly hold an IQueryable at runtime.  We use IEnumerable as
                    // the gate (rather than IQueryable) because a variable legitimately
                    // declared as IEnumerable<T> may hold an EF Core IQueryable<T> at
                    // runtime — both interfaces share the same assignability chain.
                    // FieldType / PropertyType are free property reads on already-
                    // materialised MemberInfo objects, so this check is cheap.
                    var memberType = node.Member switch {
                        FieldInfo field => field.FieldType,
                        PropertyInfo prop => prop.PropertyType,
                        _ => null
                    };

                    if (memberType is not null && typeof(IEnumerable).IsAssignableFrom(memberType))
                    {
                        var value = node.Member switch {
                            FieldInfo field => field.GetValue(constant.Value),
                            PropertyInfo prop => prop.GetValue(constant.Value),
                            _ => null
                        };

                        if (value is IQueryable queryable && ReferenceEquals(queryable.Provider, _currentQueryProvider))
                        {
                            return Visit(queryable.Expression);
                        }
                    }
                }
                catch
                {
                    // Ignore evaluation exceptions - continue with normal processing
                }
            }

            var nodeExpression = node.Expression switch {
                UnaryExpression { NodeType: ExpressionType.Convert, Type: { IsInterface: true } type, Operand: { } operand }
                    when type.IsAssignableFrom(operand.Type)
                    // This is an interface member. Operand contains the concrete (or at least more concrete) expression,
                    // from which we can try to find the concrete member.
                    => operand,
                _ => node.Expression
            };
            var nodeMember = node.Member switch {
                PropertyInfo property when nodeExpression is not null
                    => nodeExpression.Type.GetConcreteProperty(property),
                _ => node.Member
            };

            // Check if we are rewriting a base property ((BaseType)@this).MyProp or ((BaseBaseType)(BaseType)@this).MyProp
            // We are only checking for a type cast from a type to its immediate parent,
            // unwrapping nested casts, because the original parameter might have been replaced
            var isBase = (node.Expression is UnaryExpression u && UnwrapUnaryConvert(u) != u);

            // If we don't have an expression we might have an abstract property with Projectable attribute
            // and PolymorphicDispatch set to true, so we check that
            var polymorphicDispatch = !isBase && nodeMember is PropertyInfo p && IsPolymorphic(p.GetGetMethod()) &&
                nodeMember.GetCustomAttribute<ProjectableAttribute>() is ProjectableAttribute projectable &&
                (projectable.PolymorphicDispatch || _polymorphicDispatchGlobal);

            if ((TryGetReflectedExpression(nodeMember, out var reflectedExpression) && reflectedExpression != null) || polymorphicDispatch)
            {
                if (polymorphicDispatch)
                {
                    var derivedTypes = RetrieveTypes(nodeMember.DeclaringType!, nodeMember);
                    if (derivedTypes.Count > 0)
                    {
                        // Check if the method has an implementation or if it is abstract, if it is not abstract it will be added
                        // as the last result in the if/else if/else chain, otherwise the last type will be used instead
                        Expression body;
                        if (reflectedExpression != null)
                        {
                            // @this is Type1 ? ((Type1)@this).Property : ...
                            // ... ? ... :
                            // @this is TypeN ? ((TypeN)@this).Property : ...
                            // virtualImplementation
                            body = derivedTypes.AsEnumerable()
                                .Reverse()
                                .Aggregate(reflectedExpression.Body, AggregateTypes);
                        }
                        else
                        {
                            // DEV: handle generic types
                            var lastType = derivedTypes[derivedTypes.Count - 1];

                            // @this is Type1 ? ((Type1)@this).Property : ...
                            // ... ? ... :
                            // ((TypeN)@this).Property
                            body = derivedTypes.AsEnumerable()
                                .Reverse()
                                .Skip(1)
                                .Aggregate((Expression)Expression.Property(Expression.Convert(node.Expression!, lastType), nodeMember.Name), AggregateTypes);
                        }

                        return Visit(body);


                        Expression AggregateTypes(Expression expr, Type type)
                        {
                            return Expression.Condition(
                                Expression.TypeIs(node.Expression!, type),
                                Expression.Property(Expression.Convert(node.Expression!, type), nodeMember.Name),
                                expr);
                        }
                    }
                }

                if (reflectedExpression != null)
                {
                    if (nodeExpression is not null)
                    {
                        // If base we can remove the cast
                        if (isBase && nodeExpression is UnaryExpression u2)
                        {
                            nodeExpression = UnwrapUnaryConvert(u2);
                        }

                        _expressionArgumentReplacer.ParameterArgumentMapping.Add(reflectedExpression.Parameters[0], nodeExpression);
                        var updatedBody = _expressionArgumentReplacer.Visit(reflectedExpression.Body);
                        _expressionArgumentReplacer.ParameterArgumentMapping.Clear();

                        return Visit(updatedBody);
                    }

                    return Visit(reflectedExpression.Body);
                }
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is EntityQueryRootExpression root)
            {
                _entityType = root.EntityType;
                _currentQueryProvider = root.QueryProvider;
            }

            return base.VisitExtension(node);
        }

        private Expression _AddProjectableSelect(Expression node, IEntityType entityType)
        {
            var projectableProperties = _projectablePropertiesCache.GetValue(
                entityType.ClrType,
                static t => t.GetProperties()
                    .Where(x => x.IsDefined(typeof(ProjectableAttribute), false) && x.CanWrite)
                    .ToArray());

            if (projectableProperties.Length == 0)
            {
                return node;
            }

            var members = entityType.GetProperties()
                .Where(x => !x.IsShadowProperty())
                .Cast<IPropertyBase>()
                .Concat(entityType.GetNavigations()
                    .Where(x => !x.IsShadowProperty()))
                .Concat(entityType.GetSkipNavigations()
                    .Where(x => !x.IsShadowProperty()))
                .Select(x => x.GetMemberInfo(false, false))
                // Remove projectable properties from the ef properties. Since properties returned here for auto
                // properties (like `public string Test {get;set;}`) are generated fields, we also need to take them into account.
                .Where(x => projectableProperties.All(y => x.Name != y.Name && x.Name != $"<{y.Name}>k__BackingField"));

            // Replace db.Entities to db.Entities.Select(x => new Entity { Property1 = x.Property1, Rewritted = rewrittedProperty })
            var select = _closedSelectCache.GetValue(entityType.ClrType, t => _select.MakeGenericMethod(t, t));
            var xParam = Expression.Parameter(entityType.ClrType);
            return Expression.Call(
                null,
                select,
                node,
                Expression.Lambda(
                    Expression.MemberInit(
                        Expression.New(entityType.ClrType),
                        members.Select(x => _MakeBinding(xParam, x))
                            .Concat(projectableProperties
                                .Select(x => Expression.Bind(x, _GetAccessor(x, xParam)))
                            )
                    ),
                    xParam
                )
            );
        }

        // Builds the member binding used to copy an EF-mapped member into the re-projected entity.
        //
        // EF's member metadata resolves auto-properties to their compiler-generated backing field.
        // Reading a mapped scalar or navigation through that backing field prevents EF's navigation
        // expansion from recognizing it as a plain column/navigation, so it instead materializes the
        // whole source entity — including any Include-ed collection navigations — once for every
        // rewritten member. That produces one duplicate JOIN per member (see issue #217).
        //
        // Reading through the CLR property lets EF treat scalars as columns and collapse a collection
        // navigation into a single join. When the property is also settable we bind to the property
        // itself so the generated projection keeps the natural member/column names instead of the
        // backing-field name.
        private static MemberAssignment _MakeBinding(ParameterExpression para, MemberInfo member)
        {
            var property = _ResolveClrProperty(member);
            if (property is not null && property.CanRead)
            {
                var read = Expression.MakeMemberAccess(para, property);
                return property.CanWrite
                    ? Expression.Bind(property, read)
                    : Expression.Bind(member, read);
            }

            return Expression.Bind(member, Expression.MakeMemberAccess(para, member));
        }

        // Resolves the CLR property backing an EF member, whether EF handed us the property directly
        // or the compiler-generated "<Name>k__BackingField" for an auto-property.
        private static PropertyInfo? _ResolveClrProperty(MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo property:
                    return property;
                case FieldInfo { Name: ['<', ..] } field when field.DeclaringType is { } declaringType:
                    var closingBracket = field.Name.IndexOf('>');
                    if (closingBracket > 1)
                    {
                        var propertyName = field.Name.Substring(1, closingBracket - 1);
                        var property = declaringType.GetProperty(propertyName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (property is not null && property.PropertyType == field.FieldType)
                        {
                            return property;
                        }
                    }

                    return null;
                default:
                    return null;
            }
        }

        private Expression _GetAccessor(PropertyInfo property, ParameterExpression para)
        {
            var lambda = _resolver.FindGeneratedExpression(property)!;
            _expressionArgumentReplacer.ParameterArgumentMapping.Add(lambda.Parameters[0], para);
            var updatedBody = _expressionArgumentReplacer.Visit(lambda.Body);
            _expressionArgumentReplacer.ParameterArgumentMapping.Clear();
            return base.Visit(updatedBody);
        }

        private static bool IsCompilerGeneratedClosure(Type type) =>
            // TypeAttributes.NestedPrivate is a cheap flag check that rules out most types before
            // touching the attribute cache.
            type.Attributes.HasFlag(TypeAttributes.NestedPrivate) &&
            _compilerGeneratedClosureCache.GetValue(type, static t =>
                new StrongBox<bool>(Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute), inherit: true))).Value;
    }
}
