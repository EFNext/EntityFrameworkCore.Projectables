using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Tests for <see cref="FactoryMethodToConstructorCodeRefactoringProvider"/>.
/// Each test verifies via Verify.Xunit snapshots (.verified.txt files).
/// </summary>
[UsesVerify]
public class FactoryMethodToCtorCodeRefProviderTests : RefactoringTestBase
{
    private readonly static FactoryMethodToConstructorCodeRefactoringProvider _provider = new();

    // ────────────────────────────────────────────────────────────────────────────
    // Action 0 — convert factory method to constructor (document only)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public Task ConvertToConstructor_SimpleFactoryMethod() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                FactoryMethodToCtorSources.SimpleStaticFactoryMethod,
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_PreservesProjectableOptions() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                FactoryMethodToCtorSources.PreservesProjectableOptions,
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_MultipleInitializerAssignments() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class Src { public int A { get; set; } public int B { get; set; } }
    class Dest {
        public int A { get; set; }
        public int B { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest { A = src.A, B = src.B };
    }
}",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_AddsParamLessCtor_WhenNoneExists() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                FactoryMethodToCtorSources.AddsParameterlessConstructor,
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_DoesNotAddParamLessCtor_WhenAlreadyPresent() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                FactoryMethodToCtorSources.ParameterlessConstructorAlreadyPresent,
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    /// <summary>
    /// When the class already has at least one explicit constructor (but no parameterless one),
    /// the C# compiler did NOT generate an implicit default constructor — so the transformation
    /// must NOT insert one, which would unintentionally widen the public surface area.
    /// </summary>
    [Fact]
    public Task ConvertToConstructor_DoesNotAddParamLessCtor_WhenOtherExplicitCtorExists() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                FactoryMethodToCtorSources.OtherExplicitCtorExists,
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    /// <summary>
    /// The implicit default constructor is always public (C# spec §10.11.4) regardless of the
    /// factory method's accessibility.  The inserted explicit parameterless ctor must therefore
    /// be public too, even when the factory method is internal or protected.
    /// </summary>
    [Fact]
    public Task ConvertToConstructor_InsertedParameterlessCtorIsAlwaysPublic() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                FactoryMethodToCtorSources.InsertedParameterlessCtorIsAlwaysPublic,
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_WithExistingMembers_PreservesMemberOrder() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class Input { public int Value { get; set; } }
    class Output {
        public int Value { get; set; }
        public string Name { get; set; }
        [Projectable]
        public static Output From(Input i) => new Output { Value = i.Value };
    }
}",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    // ────────────────────────────────────────────────────────────────────────────
    // Guard: no refactoring should be offered in inapplicable situations
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoRefactoring_WhenMethodHasNoProjectableAttribute()
    {
        var actions = await GetRefactoringActionsAsync(
            @"
namespace Foo {
    class MyObj {
        public MyObj Create() => new MyObj { };
    }
}",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task NoRefactoring_WhenReturnTypeDoesNotMatchContainingClass()
    {
        var actions = await GetRefactoringActionsAsync(
            @"
namespace Foo {
    class Other { }
    class MyObj {
        [Projectable]
        public Other Create() => new Other { };
    }
}",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task NoRefactoring_WhenBodyHasConstructorArguments()
    {
        var actions = await GetRefactoringActionsAsync(
            @"
namespace Foo {
    class MyObj {
        [Projectable]
        public MyObj Create(int x) => new MyObj(x) { };
    }
}",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task NoRefactoring_WhenBodyIsNotObjectCreation()
    {
        var actions = await GetRefactoringActionsAsync(
            @"
namespace Foo {
    class MyObj {
        [Projectable]
        public MyObj Create() => default;
    }
}",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task NoRefactoring_WhenBodyHasNoInitializer()
    {
        var actions = await GetRefactoringActionsAsync(
            @"
namespace Foo {
    class MyObj {
        [Projectable]
        public MyObj Create() => new MyObj();
    }
}",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    /// <summary>
    /// Regression: <c>new Other.MyObj { }</c> has a qualified type name that cannot be
    /// confirmed as the containing type without a semantic model.  The pattern must reject
    /// it to avoid a false-positive transformation that would corrupt the class.
    /// </summary>
    [Fact]
    public async Task NoRefactoring_WhenCreatedTypeIsQualifiedName()
    {
        var actions = await GetRefactoringActionsAsync(
            @"
namespace Other { class MyObj { } }
namespace Foo {
    class MyObj {
        [Projectable]
        public static MyObj Create() => new Other.MyObj { };
    }
}",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    /// <summary>
    /// Regression: <c>new global::Other.MyObj { }</c> uses an alias-qualified name that
    /// cannot be confirmed as the containing type without a semantic model.
    /// </summary>
    [Fact]
    public async Task NoRefactoring_WhenCreatedTypeIsAliasQualifiedName()
    {
        var actions = await GetRefactoringActionsAsync(
            @"
namespace Other { class MyObj { } }
namespace Foo {
    class MyObj {
        [Projectable]
        public static MyObj Create() => new global::Other.MyObj { };
    }
}",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Action titles
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoActionsAreOffered_WithCorrectTitles()
    {
        var actions = await GetRefactoringActionsAsync(
            FactoryMethodToCtorSources.TwoActionsSource,
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Equal(2, actions.Count);
        Assert.Contains("constructor", actions[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("callers", actions[1].Title, StringComparison.OrdinalIgnoreCase);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Action 1 — convert factory method to constructor AND update callers
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Regression test: when the declaring document also contains call sites,
    /// BuildRootWithConstructor shifts all spans (it removes the factory method and
    /// inserts a constructor). Only <c>Dest.Map(…)</c> must be rewritten —
    /// unrelated invocations such as <c>Other.Compute()</c> must be left intact.
    /// </summary>
    [Fact]
    public Task UpdateCallers_SameDocument_OnlyReplacesFactoryCallSite() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                CreateDocumentWithReferences(@"
namespace Foo {
    class Src { public int A { get; set; } public int B { get; set; } }
    class Dest {
        public Dest() { }
        public int A { get; set; }
        public int B { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest { A = src.A, B = src.B };
    }
    class Other {
        public static int Compute() => 42;
    }
    class Consumer {
        void Setup() {
            var d = Dest.Map(new Src { A = 1, B = 2 });
            var x = Other.Compute();
        }
    }
}"),
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 1));

    [Fact]
    public Task UpdateCallers_PreservesCallSiteTrivia() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                CreateDocumentWithReferences(@"
namespace Foo {
    class Src { public int A { get; set; } }
    class Dest {
        public Dest() { }
        public int A { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest { A = src.A };
    }
    class Consumer {
        Dest Use(Src src) {
            // map the source
            return Dest.Map(src); // inline comment
        }
    }
}"),
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 1));

    // ────────────────────────────────────────────────────────────────────────────
    // Action 1 — method group callers (e.g. .Select(Dest.Map))
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single-parameter method group passed to <c>Select</c> must be rewritten to a
    /// simple lambda: <c>Dest.Map</c> → <c>src => new Dest(src)</c>.
    /// </summary>
    [Fact]
    public Task UpdateCallers_MethodGroup_SingleParameter() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                CreateDocumentWithReferences(@"
using System.Collections.Generic;
using System.Linq;
namespace Foo {
    class Src { public int A { get; set; } }
    class Dest {
        public Dest() { }
        public int A { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest { A = src.A };
    }
    class Consumer {
        Dest[] Use(IEnumerable<Src> items) => items.Select(Dest.Map).ToArray();
    }
}"),
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 1));

    /// <summary>
    /// A multi-parameter method group assigned to a delegate variable must be rewritten
    /// to a parenthesised lambda: <c>Dest.Map</c> → <c>(src, offset) => new Dest(src, offset)</c>.
    /// </summary>
    [Fact]
    public Task UpdateCallers_MethodGroup_MultipleParameters() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                CreateDocumentWithReferences(@"
using System;
namespace Foo {
    class Src { public int A { get; set; } }
    class Dest {
        public Dest() { }
        public int A { get; set; }
        public int Offset { get; set; }
        [Projectable]
        public static Dest Map(Src src, int offset) => new Dest { A = src.A, Offset = offset };
    }
    class Consumer {
        void Register(Func<Src, int, Dest> factory) { }
        void Setup() => Register(Dest.Map);
    }
}"),
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 1));

    /// <summary>
    /// When the same document has both a direct invocation <em>and</em> a method-group
    /// reference to the same factory method, both must be rewritten correctly and
    /// independently.
    /// </summary>
    [Fact]
    public Task UpdateCallers_MixedDirectInvocationAndMethodGroup() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                CreateDocumentWithReferences(@"
using System.Collections.Generic;
using System.Linq;
namespace Foo {
    class Src { public int A { get; set; } }
    class Dest {
        public Dest() { }
        public int A { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest { A = src.A };
    }
    class Consumer {
        void Setup(IEnumerable<Src> items) {
            var d = Dest.Map(new Src { A = 1 });         // direct invocation
            var all = items.Select(Dest.Map).ToArray();  // method group
        }
    }
}"),
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 1));

    /// <summary>
    /// <c>nameof(Dest.Map)</c> is returned by <c>SymbolFinder.FindReferencesAsync</c> as a
    /// reference location. It must NOT be rewritten to a lambda — it should be left unchanged
    /// (producing a compile-time error that the user can fix manually), rather than generating
    /// invalid C# like <c>p => new Dest(p)</c> inside a <c>nameof</c> argument.
    /// </summary>
    [Fact]
    public Task UpdateCallers_NameOfReference_IsNotRewritten() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                CreateDocumentWithReferences(@"
namespace Foo {
    class Src { public int A { get; set; } }
    class Dest {
        public Dest() { }
        public int A { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest { A = src.A };
    }
    class Consumer {
        string GetMethodName() => nameof(Dest.Map);
    }
}"),
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 1));

    // ────────────────────────────────────────────────────────────────────────────
    // Complex initializer expressions and trivia preservation (action 0)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public Task ConvertToConstructor_ComplexPropertyExpressions_ArePreserved() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class Src {
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsActive { get; set; }
        public string Name { get; set; }
    }
    class Dest {
        public int Sum { get; set; }
        public int Toggle { get; set; }
        public string Label { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest {
            Sum = src.X + src.Y,
            Toggle = src.IsActive ? 1 : 0,
            Label = src.Name ?? ""unknown""
        };
    }
}",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_PreservesLeadingXmlDocComment() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class Src { public int A { get; set; } }
    class Dest {
        public int A { get; set; }
        /// <summary>Creates a new <see cref=""Dest""/> from a <see cref=""Src""/>.</summary>
        /// <param name=""src"">The source object.</param>
        [Projectable]
        public static Dest Map(Src src) => new Dest { A = src.A };
    }
}",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    /// <summary>
    /// Regression test: implicit object creation (<c>new() { … }</c>) must not throw
    /// an <see cref="InvalidCastException"/> — <see cref="FactoryMethodTransformationHelper"/>
    /// must treat it as <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.BaseObjectCreationExpressionSyntax"/>
    /// rather than casting to the explicit <c>ObjectCreationExpressionSyntax</c>.
    /// </summary>
    [Fact]
    public Task ConvertToConstructor_ImplicitObjectCreation() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                FactoryMethodToCtorSources.ImplicitObjectCreation,
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_PreservesInitializerInlineComments() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class Src { public int A { get; set; } public int B { get; set; } }
    class Dest {
        public int A { get; set; }
        public int B { get; set; }
        [Projectable]
        public static Dest Map(Src src) => new Dest {
            // primary field
            A = src.A,
            B = src.B // secondary field
        };
    }
}",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));
}

