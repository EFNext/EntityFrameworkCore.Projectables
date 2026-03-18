using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

/// <summary>
/// Tests for [Projectable(UseMemberBody = ...)] — both valid cases (code is generated correctly)
/// and invalid cases (EFP0010 / EFP0011 diagnostics are emitted).
/// </summary>
[UsesVerify]
public class UseMemberBodyTests : ProjectionExpressionGeneratorTestsBase
{
    public UseMemberBodyTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    // ── Valid cases ────────────────────────────────────────────────────────────

    [Fact]
    public Task Method_UsesMethodBody_SameReturnType()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(UseMemberBody = nameof(ComputedImpl))]
        public int GetComputed() => Bar;

        private int ComputedImpl() => Bar * 2;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_StaticExtension()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity { public string Name { get; set; } }

    static class EntityExtensions {
        [Projectable(UseMemberBody = nameof(NameEqualsExpr))]
        public static bool NameEquals(this Entity a, Entity b) => a.Name == b.Name;

        private static Expression<Func<Entity, Entity, bool>> NameEqualsExpr =>
            (a, b) => a.Name == b.Name;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_InstanceMethod()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;

        private static Expression<Func<C, bool>> IsPositiveExpr => @this => @this.Value > 0;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_BlockBodiedGetterLambda()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;

        private static Expression<Func<C, bool>> IsPositiveExpr {
            get { return @this => @this.Value > 0; }
        }
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_StoredField_ExpressionBodied()
    {
        // Expression-bodied property that returns a stored private field.
        // The generator follows the field reference to inline the underlying lambda body,
        // so no code is emitted that would reference the private field from the generated class.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;

        private static Expression<Func<C, bool>> IsPositiveExpr => _isPositiveExpr;
        private static readonly Expression<Func<C, bool>> _isPositiveExpr = @this => @this.Value > 0;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_StoredField_GetterExpressionBodied()
    {
        // get => storedField: the generator follows the reference into the field initializer.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;

        private static Expression<Func<C, bool>> IsPositiveExpr { get => _isPositiveExpr; }
        private static readonly Expression<Func<C, bool>> _isPositiveExpr = @this => @this.Value > 0;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_StoredField_BlockBodiedGetter()
    {
        // get { return storedField; }: block-bodied getter returning a stored private field.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;

        private static Expression<Func<C, bool>> IsPositiveExpr {
            get { return _isPositiveExpr; }
        }
        private static readonly Expression<Func<C, bool>> _isPositiveExpr = @this => @this.Value > 0;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Property_UsesPropertyBody_SameType()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }

        [Projectable(UseMemberBody = nameof(IdDoubled))]
        public int Computed => Id;

        private int IdDoubled => Id * 2;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task StaticMethod_UsesStaticMethodBody()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(UseMemberBody = nameof(AddImpl))]
        public static int Add(int a, int b) => a + b;

        private static int AddImpl(int a, int b) => a + b + 1;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Property_UsesExpressionPropertyBody_InstanceProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }

        [Projectable(UseMemberBody = nameof(IdDoubledExpr))]
        public int Computed => Id;

        private static Expression<Func<C, int>> IdDoubledExpr => @this => @this.Id * 2;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public void Property_UsesExpressionPropertyBody_IncompatibleReturnType_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }

        [Projectable(UseMemberBody = nameof(WrongExpr))]
        public int Computed => Id;

        // Return type string does not match int
        private static Expression<Func<C, string>> WrongExpr => @this => @this.Id.ToString();
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Property_UsesExpressionPropertyBody_IncompatibleParameterCount_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }

        [Projectable(UseMemberBody = nameof(WrongExpr))]
        public int Computed => Id;

        // Func<int> has 1 arg but instance property needs Func<C, int> (2 args)
        private static Expression<Func<int>> WrongExpr => () => 42;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }



    [Fact]
    public void UseMemberBody_MemberNotFound_EmitsEFP0010()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(UseMemberBody = ""DoesNotExist"")]
        public int Foo() => 1;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0010", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_MemberNotFound_OnProperty_EmitsEFP0010()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }

        [Projectable(UseMemberBody = ""NoSuchMember"")]
        public int Computed => Id;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0010", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_IncompatibleReturnType_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(UseMemberBody = nameof(WrongType))]
        public int Foo() => 1;

        // Different return type — incompatible
        private string WrongType() => ""hello"";
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_IncompatiblePropertyType_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }

        [Projectable(UseMemberBody = nameof(WrongProp))]
        public int Computed => Id;

        // Different type — incompatible
        private string WrongProp => ""x"";
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_MethodPointsToField_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        private int _field = 42;

        [Projectable(UseMemberBody = ""_field"")]
        public int Foo() => 1;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_ExtensionMethod_MemberNotFound_EmitsEFP0010()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity { public string Name { get; set; } }

    static class EntityExtensions {
        [Projectable(UseMemberBody = ""NoSuchExpr"")]
        public static bool NameEquals(this Entity a, Entity b) => a.Name == b.Name;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0010", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_IncompatibleParameterCount_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        // Two parameters
        [Projectable(UseMemberBody = nameof(WrongParams))]
        public int Add(int a, int b) => a + b;

        // Same return type but only one parameter — count mismatch
        private int WrongParams(int a) => a * 2;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_IncompatibleParameterTypes_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(UseMemberBody = nameof(WrongParamTypes))]
        public int Add(int a, int b) => a + b;

        // Same return type and parameter count but different parameter types
        private int WrongParamTypes(int a, string b) => a;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_ExtensionMethod_IncompatibleExpressionProperty_EmitsEFP0011()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity { public string Name { get; set; } }

    static class EntityExtensions {
        [Projectable(UseMemberBody = nameof(WrongSignatureExpr))]
        public static bool NameEquals(this Entity a, Entity b) => a.Name == b.Name;

        // Wrong signature: Func<Entity, bool> instead of Func<Entity, Entity, bool>
        private static Expression<Func<Entity, bool>> WrongSignatureExpr => a => a.Name == null;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_ExpressionProperty_IncompatibleReturnType_EmitsEFP0011()
    {
        // Parameter count matches (Func<Entity, Entity, ???>) but return type is int instead of bool.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity { public string Name { get; set; } }

    static class EntityExtensions {
        [Projectable(UseMemberBody = nameof(NameEqualsExpr))]
        public static bool NameEquals(this Entity a, Entity b) => a.Name == b.Name;

        // Return type int does not match the projectable method's bool
        private static Expression<Func<Entity, Entity, int>> NameEqualsExpr => (a, b) => 1;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UseMemberBody_ExpressionProperty_IncompatibleParameterType_EmitsEFP0011()
    {
        // Parameter count and return type match, but the second parameter type is wrong (Other vs Entity).
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity { public string Name { get; set; } }
    class Other  { public string Name { get; set; } }

    static class EntityExtensions {
        [Projectable(UseMemberBody = nameof(NameEqualsExpr))]
        public static bool NameEquals(this Entity a, Entity b) => a.Name == b.Name;

        // Second parameter is Other, not Entity
        private static Expression<Func<Entity, Other, bool>> NameEqualsExpr => (a, b) => a.Name == b.Name;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_InstanceMethod_WithAlternativeReceiverName()
    {
        // Regression test: when the Expression<TDelegate> property lambda uses a different
        // parameter name for the receiver (e.g. 'c' instead of '@this'), the generated
        // code must still reference @this — not the original lambda parameter name which
        // would be undefined in the generated lambda.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;

        // Uses 'c' instead of '@this' as the lambda receiver parameter name
        private static Expression<Func<C, bool>> IsPositiveExpr => c => c.Value > 0;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_InstanceMethod_WithParamsAndAlternativeReceiverName()
    {
        // Same regression: method with extra parameters, receiver lambda param renamed to @this,
        // remaining explicit parameters keep their names from the method declaration.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(ExceedsThresholdExpr))]
        public bool ExceedsThreshold(int threshold) => Value > threshold;

        // Uses 'entity' instead of '@this' as the receiver parameter name
        private static Expression<Func<C, int, bool>> ExceedsThresholdExpr =>
            (entity, threshold) => entity.Value > threshold;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_InstanceMethod_NestedLambdaShadowingReceiverName_NotRenamed()
    {
        // Regression: the scope-unaware VariableReplacementRewriter used to rename every
        // occurrence of the outer lambda's receiver parameter — including identifiers in
        // nested lambdas that re-declare the same name (shadowing).
        // Only the outer receiver parameter should be renamed to @this; the inner lambda's
        // parameter with the same name must remain unchanged.
        var compilation = CreateCompilation(@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public IEnumerable<int> Items { get; set; }

        [Projectable(UseMemberBody = nameof(DoubledItemsExpr))]
        public IEnumerable<int> DoubledItems() => Items.Select(x => x * 2);

        // Outer parameter is named 'c'; nested lambda also uses 'c' (shadows outer).
        private static Expression<Func<C, IEnumerable<int>>> DoubledItemsExpr =>
            c => c.Items.Select(c => c * 2);
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_InstanceMethod_NestedLambdaCapturesRenamedReceiver()
    {
        // Complement to the shadowing test: when the nested lambda does NOT reuse the outer
        // parameter name but captures the outer receiver, references to the outer variable
        // inside the inner lambda body MUST still be renamed to @this.
        var compilation = CreateCompilation(@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public IEnumerable<int> Items { get; set; }
        public int Multiplier { get; set; }

        [Projectable(UseMemberBody = nameof(ScaledItemsExpr))]
        public IEnumerable<int> ScaledItems() => Items.Select(x => x * Multiplier);

        // Nested lambda uses 'item' (different from outer 'c'), but references the outer 'c'
        // inside its body via c.Multiplier — that reference must be renamed to @this.
        private static Expression<Func<C, IEnumerable<int>>> ScaledItemsExpr =>
            c => c.Items.Select(item => item * c.Multiplier);
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_InstanceMethod_WithDifferentExplicitParamNames()
    {
        // Regression test (Issue 2): when ALL lambda parameter names differ from the method's
        // parameter names (not just the receiver), the generated code must rename them all.
        // Here 'c' → '@this' and 't' → 'threshold'.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(ExceedsThresholdExpr))]
        public bool ExceedsThreshold(int threshold) => Value > threshold;

        // Uses 'c' for the receiver and 't' for the parameter — both differ from method names
        private static Expression<Func<C, int, bool>> ExceedsThresholdExpr =>
            (c, t) => c.Value > t;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Method_UsesExpressionPropertyBody_ExpressionPropertyInDifferentFile()
    {
        // Regression test (Issue 1): the Expression<TDelegate> backing property is declared
        // in a different file (separate SyntaxTree) than the [Projectable] method, as is
        // typical for split partial classes.  Previously this caused EFP0011; now the
        // generator should inline the lambda and emit the expression tree.
        var compilation = CreateCompilation(
            @"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    partial class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;
    }
}",
            @"
using System;
using System.Linq.Expressions;
namespace Foo {
    partial class C {
        private static Expression<Func<C, bool>> IsPositiveExpr => @this => @this.Value > 0;
    }
}");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Property_UsesExpressionPropertyBody_ExpressionPropertyInDifferentFile()
    {
        // Regression test (Issue 1): same as the method variant above but for a projectable
        // property.  The Expression<Func<C, int>> backing property lives in a second file.
        var compilation = CreateCompilation(
            @"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    partial class C {
        public int Id { get; set; }

        [Projectable(UseMemberBody = nameof(IdDoubledExpr))]
        public int Computed => Id;
    }
}",
            @"
using System;
using System.Linq.Expressions;
namespace Foo {
    partial class C {
        private static Expression<Func<C, int>> IdDoubledExpr => @this => @this.Id * 2;
    }
}");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public void UseMemberBody_InstanceMethod_ExpressionProperty_IncompatibleReturnType_EmitsEFP0011()
    {
        // Instance method: Expression<Func<C, ???>> — return type mismatch.
        var compilation = CreateCompilation(@"
using System;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }

        [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
        public bool IsPositive() => Value > 0;

        // Return type int does not match bool
        private static Expression<Func<C, int>> IsPositiveExpr => @this => @this.Value;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }
}

