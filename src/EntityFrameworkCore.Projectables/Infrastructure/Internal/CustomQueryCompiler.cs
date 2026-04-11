using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using EntityFrameworkCore.Projectables.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Projectables.Infrastructure.Internal
{
    /// <summary>
    /// Foo
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Needed")]
    public sealed class CustomQueryCompiler : QueryCompiler
    {
        readonly IQueryCompiler _decoratedQueryCompiler;
        readonly ProjectableExpressionReplacer _projectableExpressionReplacer;

        // This field intentionally shadows the private field of the same name in QueryCompiler.
        // Some third-party libraries (e.g. EFCore.BulkExtensions) discover the DbContext by
        // calling obj.GetType().GetField("_queryContextFactory", BindingFlags.Instance | BindingFlags.NonPublic)
        // on the IQueryCompiler instance. Because C# reflection does not surface private fields
        // declared in a base class when searching a derived type, without this shadow field the
        // lookup returns null and causes a TargetException ("Non-static method requires a target")
        // in those libraries. Storing the same value here makes the field discoverable via
        // reflection regardless of which type the caller starts from.
#pragma warning disable IDE0052 // Remove unread private members
        private readonly IQueryContextFactory _queryContextFactory;
#pragma warning restore IDE0052

        public CustomQueryCompiler(IQueryCompiler decoratedQueryCompiler,
            IQueryContextFactory queryContextFactory,
            ICompiledQueryCache compiledQueryCache,
            ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
            IDatabase database,
            IDbContextOptions contextOptions,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            ICurrentDbContext currentContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter,
            IModel model) : base(queryContextFactory,
            compiledQueryCache,
            compiledQueryCacheKeyGenerator,
            database,
            logger,
            currentContext,
            evaluatableExpressionFilter,
            model)
        {
            _queryContextFactory = queryContextFactory;
            _decoratedQueryCompiler = decoratedQueryCompiler;
            var trackingByDefault = (contextOptions.FindExtension<CoreOptionsExtension>()?.QueryTrackingBehavior ?? QueryTrackingBehavior.TrackAll) ==
                                    QueryTrackingBehavior.TrackAll;

            _projectableExpressionReplacer = new ProjectableExpressionReplacer(new ProjectionExpressionResolver(), trackingByDefault);
        }

        public override Func<QueryContext, TResult> CreateCompiledAsyncQuery<TResult>(Expression query)
            => _decoratedQueryCompiler.CreateCompiledAsyncQuery<TResult>(Expand(query));
        public override Func<QueryContext, TResult> CreateCompiledQuery<TResult>(Expression query)
            => _decoratedQueryCompiler.CreateCompiledQuery<TResult>(Expand(query));
        public override TResult Execute<TResult>(Expression query)
            => _decoratedQueryCompiler.Execute<TResult>(Expand(query));
        public override TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken)
            => _decoratedQueryCompiler.ExecuteAsync<TResult>(Expand(query), cancellationToken);

        Expression Expand(Expression expression)
            => _projectableExpressionReplacer.Replace(expression);
    }
}
