using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Tests for <see cref="MissingParameterlessConstructorCodeFixProvider"/> (EFP0008).
/// The fix inserts a <c>public ClassName() { }</c> constructor at the top of the class body.
/// </summary>
[UsesVerify]
public class MissingParamLessConstructorCodeFixProviderTests : CodeFixTestBase
{
    private readonly static MissingParameterlessConstructorCodeFixProvider _provider = new();

    // Locates the first constructor identifier — the code fix walks up to TypeDeclarationSyntax.
    private static TextSpan FirstConstructorIdentifierSpan(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .First()
            .Identifier
            .Span;

    [Fact]
    public void FixableDiagnosticIds_ContainsEFP0008() =>
        Assert.Contains("EFP0008", _provider.FixableDiagnosticIds);

    [Fact]
    public async Task RegistersCodeFix_WithTitleContainingClassName()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class MyClass {
        [Projectable]
        public MyClass(int value) { }
    }
}",
            "EFP0008",
            FirstConstructorIdentifierSpan,
            _provider);

        var action = Assert.Single(actions);
        Assert.Contains("MyClass", action.Title, StringComparison.Ordinal);
    }

    [Fact]
    public Task AddParamLessConstructor_ToClassWithSingleParamConstructor() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class Person {
        [Projectable]
        public Person(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}",
                "EFP0008",
                FirstConstructorIdentifierSpan,
                _provider));

    [Fact]
    public Task AddParamLessConstructor_IsInsertedBeforeExistingMembers() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class Person {
        public string Name { get; set; }
        public int Age { get; set; }

        [Projectable]
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }
    }
}",
                "EFP0008",
                FirstConstructorIdentifierSpan,
                _provider));

    [Fact]
    public Task AddParamLessConstructor_ToClassWithNoOtherMembers() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class Empty {
        [Projectable]
        public Empty(int value) { }
    }
}",
                "EFP0008",
                FirstConstructorIdentifierSpan,
                _provider));

    // ── Partial class — private constructor fix ───────────────────────────────

    [Fact]
    public async Task PartialClass_OffersBothPublicAndPrivateConstructorFix()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    partial class MyClass {
        [Projectable]
        public MyClass(int value) { }
    }
}",
            "EFP0008",
            FirstConstructorIdentifierSpan,
            _provider);

        Assert.Equal(2, actions.Count);
        Assert.Contains(actions, a => a.Title.Contains("private", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, a => !a.Title.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NonPartialClass_OffersOnlyPublicConstructorFix()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class MyClass {
        [Projectable]
        public MyClass(int value) { }
    }
}",
            "EFP0008",
            FirstConstructorIdentifierSpan,
            _provider);

        var action = Assert.Single(actions);
        Assert.DoesNotContain("private", action.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public Task AddPrivateParamLessConstructor_ToPartialClass() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    partial class PersonDto {
        public string Name { get; set; }

        [Projectable]
        public PersonDto(string name)
        {
            Name = name;
        }
    }
}",
                "EFP0008",
                FirstConstructorIdentifierSpan,
                _provider,
                actionIndex: 1));   // index 1 = private fix

    [Fact]
    public async Task PartialClass_NestedInsideNonPartialOuter_OffersOnlyPublicFix()
    {
        // Inner is partial but outer is not → inline generation won't be used
        // → private constructor would still trigger EFP0008 → only offer public fix.
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class Outer {
        partial class Inner {
            [Projectable]
            public Inner(int value) { }
        }
    }
}",
            "EFP0008",
            FirstConstructorIdentifierSpan,
            _provider);

        var action = Assert.Single(actions);
        Assert.DoesNotContain("private", action.Title, StringComparison.OrdinalIgnoreCase);
    }
}


