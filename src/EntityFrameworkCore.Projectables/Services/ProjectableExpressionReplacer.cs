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

            if (TryGetReflectedExpression(methodInfo, out var reflectedExpression))
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

                return base.Visit(
                    updatedBody
                );
            }

            return base.VisitMethodCall(node);
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

            if (TryGetReflectedExpression(nodeMember, out var reflectedExpression))
            {
                if (nodeExpression is not null)
                {
                    _expressionArgumentReplacer.ParameterArgumentMapping.Add(reflectedExpression.Parameters[0], nodeExpression);
                    var updatedBody = _expressionArgumentReplacer.Visit(reflectedExpression.Body);
                    _expressionArgumentReplacer.ParameterArgumentMapping.Clear();

                    return base.Visit(updatedBody);
                }

                return base.Visit(reflectedExpression.Body);
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

            var properties = entityType.GetProperties()
                .Where(x => !x.IsShadowProperty())
                .Select(x => x.GetMemberInfo(false, false))
                .Concat(entityType.GetNavigations()
                    .Where(x => !x.IsShadowProperty())
                    .Select(x => x.GetMemberInfo(false, false)))
                .Concat(entityType.GetSkipNavigations()
                    .Where(x => !x.IsShadowProperty())
                    .Select(x => x.GetMemberInfo(false, false)))
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
                        properties.Select(x => Expression.Bind(x, Expression.MakeMemberAccess(xParam, x)))
                            .Concat(projectableProperties
                                .Select(x => Expression.Bind(x, _GetAccessor(x, xParam)))
                            )
                    ),
                    xParam
                )
            );
        }

        private Expression _GetAccessor(PropertyInfo property, ParameterExpression para)
        {
            var lambda = _resolver.FindGeneratedExpression(property);
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
