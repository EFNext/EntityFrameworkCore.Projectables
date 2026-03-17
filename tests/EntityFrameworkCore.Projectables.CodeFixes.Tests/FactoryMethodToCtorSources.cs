using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Source code snippets shared between <see cref="FactoryMethodToCtorCodeFixProviderTests"/>
/// and <see cref="FactoryMethodToCtorCodeRefProviderTests"/>.
/// </summary>
static internal class FactoryMethodToCtorSources
{
    static internal TextSpan FirstMethodIdentifierSpan(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First()
            .Identifier
            .Span;

    [StringSyntax("csharp")]
    internal const string SimpleStaticFactoryMethod = @"
namespace Foo {
    class OtherObj { public string Prop1 { get; set; } }
    class MyObj {
        public string Prop1 { get; set; }
        [Projectable]
        public static MyObj Create(OtherObj obj) => new MyObj { Prop1 = obj.Prop1 };
    }
}";

    [StringSyntax("csharp")]
    internal const string PreservesProjectableOptions = @"
namespace Foo {
    class OtherObj { }
    class MyObj {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static MyObj Create(OtherObj obj) => new MyObj { };
    }
}";

    [StringSyntax("csharp")]
    internal const string AddsParameterlessConstructor = @"
namespace Foo {
    class Input { }
    class Output {
        [Projectable]
        public static Output Create(Input i) => new Output { };
    }
}";

    [StringSyntax("csharp")]
    internal const string ParameterlessConstructorAlreadyPresent = @"
namespace Foo {
    class Input { }
    class Output {
        public Output() { }
        [Projectable]
        public static Output Create(Input i) => new Output { };
    }
}";

    [StringSyntax("csharp")]
    internal const string OtherExplicitCtorExists = @"
namespace Foo {
    class Input { public int Value { get; set; } }
    class Output {
        public int Value { get; set; }
        public Output(string name) { }
        [Projectable]
        public static Output Create(Input i) => new Output { Value = i.Value };
    }
}";

    [StringSyntax("csharp")]
    internal const string InsertedParameterlessCtorIsAlwaysPublic = @"
namespace Foo {
    class Input { }
    class Output {
        public int Value { get; set; }
        [Projectable]
        internal static Output Create(Input i) => new Output { };
    }
}";

    [StringSyntax("csharp")]
    internal const string ImplicitObjectCreation = @"
namespace Foo {
    class OtherObj { public string Prop1 { get; set; } }
    class MyObj {
        public string Prop1 { get; set; }
        [Projectable]
        public static MyObj Create(OtherObj obj) => new() { Prop1 = obj.Prop1 };
    }
}";

    [StringSyntax("csharp")]
    internal const string TwoActionsSource = @"
namespace Foo {
    class MyObj {
        [Projectable]
        public static MyObj Create() => new MyObj { };
    }
}";
}