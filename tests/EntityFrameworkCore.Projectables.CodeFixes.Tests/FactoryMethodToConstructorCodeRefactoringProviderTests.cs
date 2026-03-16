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
public class FactoryMethodToConstructorCodeRefactoringProviderTests : RefactoringTestBase
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
}
