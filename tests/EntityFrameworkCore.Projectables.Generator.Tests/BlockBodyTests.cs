using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class BlockBodyTests : ProjectionExpressionGeneratorTestsBase
{
    public BlockBodyTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public void BlockBodiedMethod_NoLongerRaisesDiagnostics()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(AllowBlockBody = true)]
        public int Foo() 
        {
            return 1;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void BlockBodiedMethod_WithoutAllowFlag_EmitsWarning()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class C {
        public int Value { get; set; }
        
        [Projectable]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void BlockBodiedMethod_WithAllowFlag_NoWarning()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class C {
        public int Value { get; set; }
        
        [Projectable(AllowBlockBody = true)]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public Task BlockBodiedMethod_SimpleReturn()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            return 42;
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
    public Task BlockBodiedMethod_WithPropertyAccess()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            return Bar + 10;
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
    public Task BlockBodiedMethod_WithIfElse()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            if (Bar > 10)
            {
                return 1;
            }
            else
            {
                return 0;
            }
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
    public Task BlockBodiedMethod_WithNestedIfElse()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public string Foo()
        {
            if (Bar > 10)
            {
                return ""High"";
            }
            else if (Bar > 5)
            {
                return ""Medium"";
            }
            else
            {
                return ""Low"";
            }
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
    public Task BlockBodiedMethod_WithLocalVariable()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            var temp = Bar * 2;
            return temp + 5;
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
    public Task BlockBodiedMethod_WithTransitiveLocalVariables()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            var a = Bar * 2;
            var b = a + 5;
            return b + 10;
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
    public Task BlockBodiedMethod_LocalInIfCondition()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            var threshold = Bar * 2;
            if (threshold > 10)
            {
                return 1;
            }
            else
            {
                return 0;
            }
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
    public Task BlockBodiedMethod_LocalInSwitchExpression()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public string Foo()
        {
            var value = Bar * 2;
            switch (value)
            {
                case 2:
                    return ""Two"";
                case 4:
                    return ""Four"";
                default:
                    return ""Other"";
            }
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
    public Task BlockBodiedMethod_LocalsInNestedBlock_ProducesDiagnostic()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            if (Bar > 10)
            {
                var temp = Bar * 2;
                return temp;
            }
            return 0;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Id == "EFP0003");

        return Verifier.Verify(result.Diagnostics.Select(d => d.ToString()));
    }

    [Fact]
    public Task BlockBodiedMethod_WithMultipleParameters()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(AllowBlockBody = true)]
        public int Add(int a, int b)
        {
            return a + b;
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
    public Task BlockBodiedMethod_WithIfElseAndCondition()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }
        public bool IsActive { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            if (IsActive && Bar > 0)
            {
                return Bar * 2;
            }
            else
            {
                return 0;
            }
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
    public Task BlockBodiedMethod_IfWithoutElse_UsesDefault()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            if (Bar > 10)
            {
                return 1;
            }
            return 0;
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
    public Task BlockBodiedMethod_IfWithoutElse_ImplicitReturn()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int? Foo()
        {
            if (Bar > 10)
            {
                return 1;
            }
            
            return null;
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
    public Task BlockBodiedMethod_SwitchStatement_Simple()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public string Foo()
        {
            switch (Bar)
            {
                case 1:
                    return ""One"";
                case 2:
                    return ""Two"";
                default:
                    return ""Other"";
            }
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
    public Task BlockBodiedMethod_SwitchStatement_WithMultipleCases()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public string Foo()
        {
            switch (Bar)
            {
                case 1:
                case 2:
                    return ""Low"";
                case 3:
                case 4:
                case 5:
                    return ""Medium"";
                default:
                    return ""High"";
            }
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
    public Task BlockBodiedMethod_SwitchStatement_WithoutDefault()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public string? Foo()
        {
            switch (Bar)
            {
                case 1:
                    return ""One"";
                case 2:
                    return ""Two"";
            }
            
            return null;
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
    public Task BlockBodiedMethod_PropertyAssignment_ReportsError()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            Bar = 10;
            return Bar;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Id == "EFP0004");

        return Verifier.Verify(result.Diagnostics.Select(d => d.ToString()));
    }

    [Fact]
    public Task BlockBodiedMethod_CompoundAssignment_ReportsError()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            Bar += 10;
            return Bar;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Id == "EFP0004");

        return Verifier.Verify(result.Diagnostics.Select(d => d.ToString()));
    }

    [Fact]
    public Task BlockBodiedMethod_IncrementOperator_ReportsError()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            var x = 5;
            x++;
            return x;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Id == "EFP0004");

        return Verifier.Verify(result.Diagnostics.Select(d => d.ToString()));
    }

    [Fact]
    public Task BlockBodiedMethod_NonProjectableMethodCall_ReportsWarning()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo()
        {
            Console.WriteLine(""test"");
            return Bar;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Id == "EFP0005");

        return Verifier.Verify(result.Diagnostics.Select(d => d.ToString()));
    }

    [Fact]
    public Task BlockBodiedMethod_WithPatternMatching()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity {
        public bool IsActive { get; set; }
        public int Value { get; set; }
    }
    
    static class Extensions {
        [Projectable(AllowBlockBody = true)]
        public static string GetComplexCategory(this Entity entity)
        {
            if (entity is { IsActive: true, Value: > 100 })
            {
                return ""Active High"";
            }
            return ""Other"";
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
    public Task BlockBodiedMethod_WithRelationalPattern()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity {
        public int Value { get; set; }
    }
    
    static class Extensions {
        [Projectable(AllowBlockBody = true)]
        public static string GetCategory(this Entity entity)
        {
            if (entity.Value is > 100)
            {
                return ""High"";
            }
            return ""Low"";
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
    public Task BlockBodiedMethod_WithConstantPattern()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity {
        public string Status { get; set; }
    }
    
    static class Extensions {
        [Projectable(AllowBlockBody = true)]
        public static bool IsNull(this Entity entity)
        {
            if (entity is null)
            {
                return true;
            }
            return false;
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
    public Task BlockBodiedMethod_WithNotPattern()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Entity {
        public string Name { get; set; }
    }
    
    static class Extensions {
        [Projectable(AllowBlockBody = true)]
        public static bool IsNotNull(this Entity entity)
        {
            if (entity is not null)
            {
                return true;
            }
            return false;
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
    public Task BlockBodiedMethod_WithAndPattern()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Value { get; set; }
    }

    static class Extensions {
        [Projectable(AllowBlockBody = true)]
        public static bool IsInRange(this Entity entity)
        {
            if (entity.Value is >= 1 and <= 100)
            {
                return true;
            }
            return false;
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
    public Task BlockBodiedMethod_WithOrPattern()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public string Status { get; set; }
    }

    static class Extensions {
        [Projectable(AllowBlockBody = true)]
        public static bool IsTerminal(this Entity entity)
        {
            if (entity.Status is ""Cancelled"" or ""Completed"")
            {
                return true;
            }
            return false;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}