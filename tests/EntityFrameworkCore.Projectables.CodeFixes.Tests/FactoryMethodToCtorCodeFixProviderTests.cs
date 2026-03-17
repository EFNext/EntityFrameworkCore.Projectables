namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Tests for <see cref="FactoryMethodToCtorCodeFixProvider"/> (EFP0012).
/// Verifies the code fix output via Verify.Xunit snapshots.
/// </summary>
[UsesVerify]
public class FactoryMethodToCtorCodeFixProviderTests : CodeFixTestBase
{
    private readonly static FactoryMethodToCtorCodeFixProvider _provider = new();

    [Fact]
    public void FixableDiagnosticIds_ContainsEFP0012() =>
        Assert.Contains("EFP0012", _provider.FixableDiagnosticIds);

    [Fact]
    public Task CodeFix_SimpleStaticFactoryMethod() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                FactoryMethodToCtorSources.SimpleStaticFactoryMethod,
                "EFP0012",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task CodeFix_PreservesProjectableOptions() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                FactoryMethodToCtorSources.PreservesProjectableOptions,
                "EFP0012",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task CodeFix_AddsParameterlessConstructor_WhenNoneExists() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                FactoryMethodToCtorSources.AddsParameterlessConstructor,
                "EFP0012",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task CodeFix_DoesNotAddParamLessCtor_WhenAlreadyPresent() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                FactoryMethodToCtorSources.ParameterlessConstructorAlreadyPresent,
                "EFP0012",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    /// <summary>
    /// When the class already has at least one explicit constructor (but no parameterless one),
    /// the C# compiler did NOT generate an implicit default constructor — so the transformation
    /// must NOT insert one, which would unintentionally widen the public surface area.
    /// </summary>
    [Fact]
    public Task CodeFix_DoesNotAddParamLessCtor_WhenOtherExplicitCtorExists() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                FactoryMethodToCtorSources.OtherExplicitCtorExists,
                "EFP0012",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    /// <summary>
    /// The implicit default constructor is always public (C# spec §10.11.4) regardless of the
    /// factory method's accessibility.  The inserted explicit parameterless ctor must therefore
    /// be public too, even when the factory method is internal or protected.
    /// </summary>
    [Fact]
    public Task CodeFix_InsertedParameterlessCtorIsAlwaysPublic() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                FactoryMethodToCtorSources.InsertedParameterlessCtorIsAlwaysPublic,
                "EFP0012",
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
    public Task CodeFix_ImplicitObjectCreation() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                FactoryMethodToCtorSources.ImplicitObjectCreation,
                "EFP0012",
                FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public async Task TwoCodeFixActionsAreOffered()
    {
        var actions = await GetCodeFixActionsAsync(
            FactoryMethodToCtorSources.TwoActionsSource,
            "EFP0012",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Equal(2, actions.Count);
        Assert.Contains("constructor", actions[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("callers", actions[1].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoCodeFix_WhenPatternDoesNotMatch()
    {
        // Without [Projectable] the pattern check returns false → no fix registered.
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class MyObj {
        public static MyObj Create() => new MyObj { };
    }
}",
            "EFP0012",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    /// <summary>
    /// <c>Items = { 1, 2 }</c> in an object initializer is an
    /// <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.AssignmentExpressionSyntax"/>
    /// whose RHS is an <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.InitializerExpressionSyntax"/>.
    /// Converting it to a statement produces invalid C# (<c>Items = { 1, 2 };</c>),
    /// so the code fix must not be offered for this pattern.
    /// </summary>
    [Fact]
    public async Task NoCodeFix_WhenInitializerHasNestedCollectionInitializer()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
using System.Collections.Generic;
namespace Foo {
    class MyObj {
        public List<int> Items { get; set; }
        [Projectable]
        public static MyObj Create() => new MyObj { Items = { 1, 2 } };
    }
}",
            "EFP0012",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }

    /// <summary>
    /// A mixed initializer that combines a simple assignment with a nested collection
    /// initializer (<c>Items = { 1, 2 }</c>) must also be rejected.
    /// </summary>
    [Fact]
    public async Task NoCodeFix_WhenMixedSimpleAndNestedCollectionInitializer()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
using System.Collections.Generic;
namespace Foo {
    class MyObj {
        public int Value { get; set; }
        public List<int> Items { get; set; }
        [Projectable]
        public static MyObj Create(int v) => new MyObj { Value = v, Items = { 1, 2 } };
    }
}",
            "EFP0012",
            FactoryMethodToCtorSources.FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }
}
