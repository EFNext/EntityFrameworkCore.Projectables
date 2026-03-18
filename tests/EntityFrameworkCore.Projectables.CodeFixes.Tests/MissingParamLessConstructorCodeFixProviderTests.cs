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
}


