using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.Projectables.Benchmarks.Helpers;
using EntityFrameworkCore.Projectables.Services;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.Benchmarks
{
    /// <summary>
    /// Benchmarks two closure-capture scenarios that exercise
    /// <c>ProjectableExpressionReplacer.VisitMember</c>:
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <term>Value closure</term>
    ///     <description>
    ///       A local <c>int</c> is captured per iteration (<c>delta = i % 10</c>).
    ///       In Full mode, <c>VisitMember</c> evaluates the closure field on <b>every</b>
    ///       query execution to check whether it is an inlinable <c>IQueryable</c>.
    ///       Previously this used <c>Expression.Lambda(...).Compile().Invoke()</c>
    ///       (one JIT compilation per field per iteration); now it uses
    ///       <c>FieldInfo.GetValue()</c> which is significantly cheaper.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Sub-query closure</term>
    ///     <description>
    ///       An <c>IQueryable</c> sub-query is captured in a closure and inlined into
    ///       the outer query at expansion time.  Full mode pays this cost on every
    ///       iteration; Limited mode pays it only on the first EF Core cache miss.
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// <para>
    ///   Three benchmark groups are provided, from least to most accurate:
    ///   <list type="number">
    ///     <item><c>*_ValueClosure</c> / <c>*_SubQueryClosure</c> — end-to-end via
    ///       <c>ToQueryString()</c> (includes EF Core cache lookup + SQL formatting)</item>
    ///     <item><c>Isolated_Replace_*</c> — calls <c>ProjectableExpressionReplacer.Replace()</c>
    ///       directly with a pre-built expression tree; zero EF Core pipeline overhead</item>
    ///   </list>
    /// </para>
    ///
    /// Run with:
    /// <code>dotnet run -c Release -- --filter "*ClosureCapture*"</code>
    /// </summary>
    [MemoryDiagnoser]
    public class ClosureCaptureBenchmark
    {
        // ── DbContexts — created once in GlobalSetup, NOT per benchmark invocation ──
        // The original benchmark constructed a new TestDbContext inside each benchmark
        // method, adding ~N ms of DI/EF startup noise to every invocation.
        private TestDbContext _ctxBaseline = null!;
        private TestDbContext _ctxFull     = null!;
        private TestDbContext _ctxLimited  = null!;

        // ── Sub-queries for Scenario B ────────────────────────────────────────
        // Stored as fields so DbContext lifetime is controlled by GlobalSetup/Cleanup.
        // Each benchmark method re-captures them into a *local variable* so the C#
        // compiler generates a <>c__DisplayClass closure — the exact type that
        // VisitMember checks for (NestedPrivate + CompilerGenerated).
        // Capturing a field directly would embed 'this' instead and skip the code path.
        private IQueryable<TestEntity> _subBaseline = null!;
        private IQueryable<TestEntity> _subFull     = null!;
        private IQueryable<TestEntity> _subLimited  = null!;

        // ── Replacer + pre-built trees for isolated benchmarks ────────────────
        // The isolated benchmarks call Replace() directly, bypassing:
        //   • EF Core compiled-query cache lookup
        //   • SQL generation and string formatting
        //   • ParameterExtractingExpressionVisitor
        // This isolates the pure expression-tree-rewrite cost.
        private ProjectableExpressionReplacer _replacer      = null!;
        private Expression                    _valueClosureExpr  = null!;
        private Expression                    _subQueryExpr      = null!;

        [GlobalSetup]
        public void Setup()
        {
            _ctxBaseline = new TestDbContext(false);
            _ctxFull     = new TestDbContext(true, useFullCompatibiltyMode: true);
            _ctxLimited  = new TestDbContext(true, useFullCompatibiltyMode: false);

            _subBaseline = _ctxBaseline.Entities.Where(x => x.Id > 5);
            _subFull     = _ctxFull.Entities.Where(x => x.IdPlus1 > 5);
            _subLimited  = _ctxLimited.Entities.Where(x => x.IdPlus1 > 5);

            // Build expression trees used by the isolated benchmarks.
            // Using local captures so the compiler generates the <>c__DisplayClass
            // closures that VisitMember expects.
            var delta = 5;
            _valueClosureExpr = _ctxFull.Entities.Select(e => e.IdPlusDelta(delta)).Expression;

            var sub = _ctxFull.Entities.Where(x => x.IdPlus1 > 5);
            _subQueryExpr = _ctxFull.Entities.Where(e => sub.Any(s => s.Id == e.Id)).Expression;

            _replacer = new ProjectableExpressionReplacer(new ProjectionExpressionResolver(), trackByDefault: false);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _ctxBaseline.Dispose();
            _ctxFull.Dispose();
            _ctxLimited.Dispose();
        }

        // ── Scenario A: value closure (int captured per call) ─────────────────
        // A fixed delta=5 is used so the EF Core compiled-query cache is warm after
        // BDN's own warmup phase.  All results are then per-single-query overhead,
        // directly comparable with the Isolated_Replace_* µs numbers below.

        /// <summary>Baseline: no projectables, int closure captured per call.</summary>
        [Benchmark(Baseline = true)]
        public string Baseline_ValueClosure()
        {
            var delta = 5;
            return _ctxBaseline.Entities.Select(e => e.Id + delta).ToQueryString();
        }

        /// <summary>
        /// Full mode: projectable method call with a closure-captured int argument.
        /// <c>VisitMember</c> evaluates the 'delta' field via reflection on every call.
        /// </summary>
        [Benchmark]
        public string Full_ValueClosure()
        {
            var delta = 5;
            return _ctxFull.Entities.Select(e => e.IdPlusDelta(delta)).ToQueryString();
        }

        /// <summary>
        /// Limited mode: expansion runs only on EF Core cache miss (first call per
        /// query shape); per-call closure evaluation overhead is near zero.
        /// </summary>
        [Benchmark]
        public string Limited_ValueClosure()
        {
            var delta = 5;
            return _ctxLimited.Entities.Select(e => e.IdPlusDelta(delta)).ToQueryString();
        }

        // ── Scenario B: IQueryable sub-query closure ──────────────────────────

        /// <summary>Baseline: no projectables, sub-IQueryable captured in a closure.</summary>
        [Benchmark]
        public string Baseline_SubQueryClosure()
        {
            var sub = _subBaseline;
            return _ctxBaseline.Entities.Where(e => sub.Any(s => s.Id == e.Id)).ToQueryString();
        }

        /// <summary>
        /// Full mode: sub-IQueryable captured in a closure; the sub-query itself uses
        /// a projectable property so both the inlining path and the projectable
        /// expansion path are exercised on every call.
        /// </summary>
        [Benchmark]
        public string Full_SubQueryClosure()
        {
            var sub = _subFull;
            return _ctxFull.Entities.Where(e => sub.Any(s => s.Id == e.Id)).ToQueryString();
        }

        /// <summary>
        /// Limited mode: expansion occurs only on the first EF Core cache miss;
        /// subsequent calls hit the compiled-query cache directly.
        /// </summary>
        [Benchmark]
        public string Limited_SubQueryClosure()
        {
            var sub = _subLimited;
            return _ctxLimited.Entities.Where(e => sub.Any(s => s.Id == e.Id)).ToQueryString();
        }

        // ── Isolated: pure ProjectableExpressionReplacer.Replace() cost ───────
        // No EF Core pipeline. No SQL generation. No string formatting.
        // Now on the same µs scale as the ToQueryString benchmarks above, so you
        // can directly subtract to get: EF Core pipeline cost = Full_* − Isolated_Replace_*

        /// <summary>
        /// Pure replacer cost for a value-closure query (no EF Core involved).
        /// </summary>
        [Benchmark]
        public Expression Isolated_Replace_ValueClosure()
            => _replacer.Replace(_valueClosureExpr);

        /// <summary>
        /// Pure replacer cost for a sub-query-closure query, including the recursive
        /// visit of the inlined sub-query expression (no EF Core involved).
        /// </summary>
        [Benchmark]
        public Expression Isolated_Replace_SubQueryClosure()
            => _replacer.Replace(_subQueryExpr);
    }
}

