using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.Projectables.Benchmarks.Helpers;
using EntityFrameworkCore.Projectables.Services;

namespace EntityFrameworkCore.Projectables.Benchmarks
{
    /// <summary>
    /// Micro-benchmarks <see cref="ProjectionExpressionResolver.FindGeneratedExpression"/> in
    /// isolation (no EF Core overhead) to directly compare the static registry path against
    /// the reflection-based path (<see cref="ProjectionExpressionResolver.FindGeneratedExpressionViaReflection"/>).
    /// </summary>
    [MemoryDiagnoser]
    public class ExpressionResolverBenchmark
    {
        private static readonly MemberInfo _propertyMember =
            typeof(TestEntity).GetProperty(nameof(TestEntity.IdPlus1))!;

        private static readonly MemberInfo _methodMember =
            typeof(TestEntity).GetMethod(nameof(TestEntity.IdPlus1Method))!;

        private static readonly MemberInfo _methodWithParamMember =
            typeof(TestEntity).GetMethod(nameof(TestEntity.IdPlusDelta), new[] { typeof(int) })!;

        private readonly ProjectionExpressionResolver _resolver = new();

        // ── Registry (source-generated) path ─────────────────────────────────

        [Benchmark(Baseline = true)]
        public LambdaExpression? ResolveProperty_Registry()
            => _resolver.FindGeneratedExpression(_propertyMember);

        [Benchmark]
        public LambdaExpression? ResolveMethod_Registry()
            => _resolver.FindGeneratedExpression(_methodMember);

        [Benchmark]
        public LambdaExpression? ResolveMethodWithParam_Registry()
            => _resolver.FindGeneratedExpression(_methodWithParamMember);

        // ── Reflection path ───────────────────────────────────────────────────

        [Benchmark]
        public LambdaExpression? ResolveProperty_Reflection()
            => ProjectionExpressionResolver.FindGeneratedExpressionViaReflection(_propertyMember);

        [Benchmark]
        public LambdaExpression? ResolveMethod_Reflection()
            => ProjectionExpressionResolver.FindGeneratedExpressionViaReflection(_methodMember);

        [Benchmark]
        public LambdaExpression? ResolveMethodWithParam_Reflection()
            => ProjectionExpressionResolver.FindGeneratedExpressionViaReflection(_methodWithParamMember);
    }
}
