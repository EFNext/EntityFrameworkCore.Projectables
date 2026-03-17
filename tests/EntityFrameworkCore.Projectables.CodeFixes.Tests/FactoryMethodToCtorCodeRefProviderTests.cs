using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
    private static readonly FactoryMethodToConstructorCodeRefactoringProvider _provider = new();

    // Locates the span of the first method identifier — the provider walks up to MethodDeclarationSyntax.
    private static TextSpan FirstMethodIdentifierSpan(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First()
            .Identifier
            .Span;

    // ────────────────────────────────────────────────────────────────────────────
    // Action 0 — convert factory method to constructor (document only)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public Task ConvertToConstructor_SimpleFactoryMethod() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class OtherObj { public string Prop1 { get; set; } }
    class MyObj {
        public string Prop1 { get; set; }
        [Projectable]
        public static MyObj Create(OtherObj obj) => new MyObj { Prop1 = obj.Prop1 };
    }
}",
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_PreservesProjectableOptions() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class OtherObj { }
    class MyObj {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static MyObj Create(OtherObj obj) => new MyObj { };
    }
}",
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_AddsParameterlessConstructor_WhenNoneExists() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class Input { }
    class Output {
        [Projectable]
        public static Output Create(Input i) => new Output { };
    }
}",
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task ConvertToConstructor_DoesNotAddParameterlessConstructor_WhenAlreadyPresent() =>
        Verifier.Verify(
            ApplyRefactoringAsync(
                @"
namespace Foo {
    class Input { }
    class Output {
        public Output() { }
        [Projectable]
        public static Output Create(Input i) => new Output { };
    }
}",
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
            FirstMethodIdentifierSpan,
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
            FirstMethodIdentifierSpan,
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
            FirstMethodIdentifierSpan,
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
            FirstMethodIdentifierSpan,
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
            FirstMethodIdentifierSpan,
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
            @"
namespace Foo {
    class MyObj {
        [Projectable]
        public static MyObj Create() => new MyObj { };
    }
}",
            FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
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
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));
}
