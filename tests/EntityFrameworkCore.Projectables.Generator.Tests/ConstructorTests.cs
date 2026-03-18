using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

[UsesVerify]
public class ConstructorTests : ProjectionExpressionGeneratorTestsBase
{
    public ConstructorTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task ProjectableConstructor_BodyAssignments()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PointDto {
        public int X { get; set; }
        public int Y { get; set; }

        public PointDto() { }

        [Projectable]
        public PointDto(int x, int y) {
            X = x;
            Y = y;
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
    public Task ProjectableConstructor_WithBaseInitializer()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Base {
        public int Id { get; set; }
        public Base(int id) { Id = id; }

        protected Base() { }
    }

    class Child : Base {
        public string Name { get; set; }

        public Child() { }

        [Projectable]
        public Child(int id, string name) : base(id) {
            Name = name;
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
    public Task ProjectableConstructor_Overloads()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(string firstName, string lastName) {
            FirstName = firstName;
            LastName = lastName;
        }

        [Projectable]
        public PersonDto(string fullName) {
            FirstName = fullName;
            LastName = string.Empty;
        }
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedTrees.Length);

        return Verifier.Verify(result.GeneratedTrees.OrderBy(t => t.FilePath).Select(t => t.ToString()));
    }

    [Fact]
    public Task ProjectableConstructor_WithClassArgument()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class SourceEntity {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    class PersonDto {
        public int Id { get; set; }
        public string Name { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(SourceEntity source) {
            Id = source.Id;
            Name = source.Name;
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
    public Task ProjectableConstructor_WithMultipleClassArguments()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class NamePart {
        public string Value { get; set; }
    }

    class PersonDto {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(NamePart first, NamePart last) {
            FirstName = first.Value;
            LastName = last.Value;
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
    public Task ProjectableConstructor_WithIfElseLogic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string Label { get; set; }
        public int Score { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(int score) {
            Score = score;
            if (score >= 90) {
                Label = ""A"";
            } else {
                Label = ""B"";
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
    public Task ProjectableConstructor_WithLocalVariable()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string FullName { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(string first, string last) {
            var full = first + "" "" + last;
            FullName = full;
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
    public Task ProjectableConstructor_WithBaseInitializerExpression()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Base {
        public string Code { get; set; }
        public Base(string code) { Code = code; }

        protected Base() { }
    }

    class Child : Base {
        public string Name { get; set; }

        public Child() { }

        [Projectable]
        public Child(string name, string rawCode) : base(rawCode.ToUpper()) {
            Name = name;
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
    public Task ProjectableConstructor_WithBaseInitializerAndIfElse()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Base {
        public int Id { get; set; }
        public Base(int id) {
            if (id < 0) {
                Id = 0;
            } else {
                Id = id;
            }
        }

        protected Base() { }
    }

    class Child : Base {
        public string Name { get; set; }

        public Child() { }

        [Projectable]
        public Child(int id, string name) : base(id) {
            Name = name;
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
    public Task ProjectableConstructor_WithIfNoElse()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string Label { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(int score) {
            Label = ""none"";
            if (score >= 90) {
                Label = ""A"";
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
    public Task ProjectableConstructor_ReferencingPreviouslyAssignedProperty()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(string firstName, string lastName) {
            FirstName = firstName;
            LastName = lastName;
            FullName = FirstName + "" "" + LastName;
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
    public Task ProjectableConstructor_ReferencingBasePropertyInDerivedBody()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Base {
        public string Code { get; set; }
        public Base(string code) { Code = code; }

        protected Base() { }
    }

    class Child : Base {
        public string Label { get; set; }

        public Child() { }

        [Projectable]
        public Child(string code) : base(code) {
            Label = ""[""  + Code + ""]"";
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
    public Task ProjectableConstructor_ReferencingStaticConstMember()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        internal const string Separator = "" - "";
        public string FullName { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(string first, string last) {
            FullName = first + Separator + last;
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
    public Task ProjectableConstructor_ReferencingPreviouslyAssignedInBaseCtor()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Base {
        public int X { get; set; }
        public int Y { get; set; }
        public Base(int x, int y) {
            X = x;
            Y = x + y;
        }

        protected Base() { }
    }

    class Child : Base {
        public int Sum { get; set; }

        public Child() { }

        [Projectable]
        public Child(int a, int b) : base(a, b) {
            Sum = X + Y;
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
    public Task ProjectableConstructor_ThisInitializer_SimpleOverload()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public PersonDto() { }

        public PersonDto(string firstName, string lastName) {
            FirstName = firstName;
            LastName = lastName;
        }

        [Projectable]
        public PersonDto(string fullName) : this(fullName.Split(' ')[0], fullName.Split(' ')[1]) {
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
    public Task ProjectableConstructor_ThisInitializer_WithBodyAfter()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }

        public PersonDto() { }

        public PersonDto(string firstName, string lastName) {
            FirstName = firstName;
            LastName = lastName;
        }

        [Projectable]
        public PersonDto(string fn, string ln, bool upper) : this(fn, ln) {
            FullName = upper ? (FirstName + "" "" + LastName).ToUpper() : FirstName + "" "" + LastName;
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
    public Task ProjectableConstructor_ThisInitializer_WithIfElseInDelegated()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string Label { get; set; }
        public int Score { get; set; }

        public PersonDto() { }

        public PersonDto(int score) {
            Score = score;
            if (score >= 90) {
                Label = ""A"";
            } else {
                Label = ""B"";
            }
        }

        [Projectable]
        public PersonDto(int score, string prefix) : this(score) {
            Label = prefix + Label;
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
    public Task ProjectableConstructor_ThisInitializer_ChainedThisAndBase()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Base {
        public int Id { get; set; }
        public Base(int id) { Id = id; }
    }

    class Child : Base {
        public string Name { get; set; }

        public Child() : base(0) { }

        public Child(int id, string name) : base(id) {
            Name = name;
        }

        [Projectable]
        public Child(int id, string name, string suffix) : this(id, name) {
            Name = Name + suffix;
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
    public Task ProjectableConstructor_ThisInitializer_RefPreviouslyAssignedProperty()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }

        public PersonDto() { }

        public PersonDto(string firstName, string lastName) {
            FirstName = firstName;
            LastName = lastName;
            FullName = FirstName + "" "" + LastName;
        }

        [Projectable]
        public PersonDto(string firstName) : this(firstName, ""Doe"") {
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
    public void ProjectableConstructor_WithoutParameterlessConstructor_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string Name { get; set; }

        [Projectable]
        public PersonDto(string name) {
            Name = name;
        }
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public Task ProjectableConstructor_WithExplicitParameterlessConstructor_Succeeds()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PersonDto {
        public string Name { get; set; }

        public PersonDto() { }

        [Projectable]
        public PersonDto(string name) {
            Name = name;
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
    public Task ProjectableConstructor_WithElseIfChain()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class GradeDto {
        public string Grade { get; set; }

        public GradeDto() { }

        [Projectable]
        public GradeDto(int score) {
            if (score >= 90) {
                Grade = ""A"";
            } else if (score >= 75) {
                Grade = ""B"";
            } else if (score >= 60) {
                Grade = ""C"";
            } else {
                Grade = ""F"";
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
    public Task ProjectableConstructor_WithNestedIfElse()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class StatusDto {
        public string Status { get; set; }

        public StatusDto() { }

        [Projectable]
        public StatusDto(bool isActive, bool isPremium) {
            if (isActive) {
                if (isPremium) {
                    Status = ""Active Premium"";
                } else {
                    Status = ""Active Free"";
                }
            } else {
                Status = ""Inactive"";
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
    public void ProjectableConstructor_WithEarlyReturn_GuardClause_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class ItemDto {
        public string Name { get; set; }
        public string Category { get; set; }

        public ItemDto() { }

        [Projectable]
        public ItemDto(string name, string category) {
            Name = name;
            if (string.IsNullOrEmpty(category)) {
                Category = ""Unknown"";
                return;
            }
            Category = category;
        }
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void ProjectableConstructor_WithMultipleEarlyReturns_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class PriorityDto {
        public string Level { get; set; }

        public PriorityDto() { }

        [Projectable]
        public PriorityDto(int value) {
            if (value < 0) {
                Level = ""Invalid"";
                return;
            }
            if (value == 0) {
                Level = ""None"";
                return;
            }
            if (value <= 5) {
                Level = ""Low"";
                return;
            }
            Level = ""High"";
        }
    }
}
");
        var result = RunGenerator(compilation);

        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, d => Assert.Equal("EFP0003", d.Id));
        Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
    }

    [Fact]
    public Task ProjectableConstructor_WithSequentialIfs()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class FlagDto {
        public string Tag { get; set; }
        public bool IsVerified { get; set; }
        public bool IsAdmin { get; set; }

        public FlagDto() { }

        [Projectable]
        public FlagDto(string role, bool verified) {
            Tag = role;
            if (verified) {
                IsVerified = true;
            }
            if (role == ""admin"") {
                IsAdmin = true;
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
    public Task ProjectableConstructor_WithTernaryAssignment()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class LabelDto {
        public string Label { get; set; }
        public string Display { get; set; }

        public LabelDto() { }

        [Projectable]
        public LabelDto(string name, bool uppercase) {
            Label = name;
            Display = uppercase ? name.ToUpper() : name.ToLower();
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
    public Task ProjectableConstructor_WithNullCoalescing()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class ProductDto {
        public string Name { get; set; }
        public string Description { get; set; }

        public ProductDto() { }

        [Projectable]
        public ProductDto(string name, string description) {
            Name = name ?? ""Unnamed"";
            Description = description ?? string.Empty;
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
    public Task ProjectableConstructor_WithSwitchExpression()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class SeasonDto {
        public string Name { get; set; }
        public string Description { get; set; }

        public SeasonDto() { }

        [Projectable]
        public SeasonDto(int month) {
            Name = month switch {
                12 or 1 or 2 => ""Winter"",
                3 or 4 or 5  => ""Spring"",
                6 or 7 or 8  => ""Summer"",
                _            => ""Autumn""
            };
            Description = ""Month: "" + month;
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
    public Task ProjectableConstructor_WithMultipleLocalVariables()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class AddressDto {
        public string Street { get; set; }
        public string City { get; set; }
        public string Full { get; set; }

        public AddressDto() { }

        [Projectable]
        public AddressDto(string street, string city, string country) {
            var trimmedStreet = street.Trim();
            var trimmedCity   = city.Trim();
            Street = trimmedStreet;
            City   = trimmedCity;
            Full   = trimmedStreet + "", "" + trimmedCity + "", "" + country;
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
    public Task ProjectableConstructor_WithNullableParameter()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class MeasurementDto {
        public double Value { get; set; }
        public string Unit { get; set; }

        public MeasurementDto() { }

        [Projectable]
        public MeasurementDto(double? value, string unit) {
            Value = value ?? 0.0;
            Unit  = unit ?? ""m"";
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
    public Task ProjectableConstructor_WithLocalVariableUsedInCondition()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class RangeDto {
        public int Min { get; set; }
        public int Max { get; set; }
        public bool IsValid { get; set; }

        public RangeDto() { }

        [Projectable]
        public RangeDto(int a, int b) {
            var lo = a < b ? a : b;
            var hi = a < b ? b : a;
            Min = lo;
            Max = hi;
            if (hi - lo > 0) {
                IsValid = true;
            } else {
                IsValid = false;
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
    public Task ProjectableConstructor_WithBaseInitializer_AndIfElse_InDerivedBody()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Animal {
        public string Species { get; set; }
        public Animal(string species) { Species = species; }
        protected Animal() { }
    }

    class Pet : Animal {
        public string Name { get; set; }
        public string Nickname { get; set; }

        public Pet() { }

        [Projectable]
        public Pet(string species, string name, bool useShortName) : base(species) {
            Name = name;
            if (useShortName) {
                Nickname = name.Length > 3 ? name.Substring(0, 3) : name;
            } else {
                Nickname = name;
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
    public void ProjectableConstructor_WithBaseInitializer_AndEarlyReturn_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Vehicle {
        public string Type { get; set; }
        public Vehicle(string type) { Type = type; }
        protected Vehicle() { }
    }

    class Car : Vehicle {
        public string Model { get; set; }
        public int Year { get; set; }

        public Car() { }

        [Projectable]
        public Car(string model, int year) : base(""Car"") {
            Model = model;
            if (year <= 0) {
                Year = 2000;
                return;
            }
            Year = year;
        }
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public Task ProjectableConstructor_WithDeepNestedIf()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class AccessDto {
        public string Access { get; set; }

        public AccessDto() { }

        [Projectable]
        public AccessDto(bool isLoggedIn, bool isVerified, bool isAdmin) {
            if (isLoggedIn) {
                if (isVerified) {
                    if (isAdmin) {
                        Access = ""Full"";
                    } else {
                        Access = ""Verified"";
                    }
                } else {
                    Access = ""Unverified"";
                }
            } else {
                Access = ""Guest"";
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
    public Task ProjectableConstructor_WithIfInsideLocalScope_AndElse()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class OrderDto {
        public string Status { get; set; }
        public string Note { get; set; }
        public bool NeedsReview { get; set; }

        public OrderDto() { }

        [Projectable]
        public OrderDto(int amount, bool flagged) {
            if (flagged) {
                Status = ""Flagged"";
                Note = ""Requires manual review"";
                NeedsReview = true;
            } else {
                Status = amount > 1000 ? ""Large"" : ""Normal"";
                Note = string.Empty;
                NeedsReview = false;
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
    public Task ProjectableConstructor_WithThisInitializer_AndElseIfInBody()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class EventDto {
        public string Title { get; set; }
        public string Tag { get; set; }
        public string Priority { get; set; }

        public EventDto() { }

        public EventDto(string title, string tag) {
            Title = title;
            Tag   = tag;
        }

        [Projectable]
        public EventDto(string title, string tag, int urgency) : this(title, tag) {
            if (urgency >= 10) {
                Priority = ""Critical"";
            } else if (urgency >= 5) {
                Priority = ""High"";
            } else {
                Priority = ""Normal"";
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
    public Task ProjectableConstructor_WithSwitchExpression_AndExtraProperty()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class ShapeDto {
        public string ShapeType { get; set; }
        public int Sides { get; set; }
        public string Description { get; set; }

        public ShapeDto() { }

        [Projectable]
        public ShapeDto(int sides) {
            Sides = sides;
            ShapeType = sides switch {
                3 => ""Triangle"",
                4 => ""Rectangle"",
                5 => ""Pentagon"",
                _ => ""Polygon""
            };
            Description = ShapeType + "" with "" + sides + "" sides"";
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
    public Task ProjectableConstructor_WithFullObject()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
using System.Collections.Generic;
using System.Linq;

namespace Foo {
    public class Customer {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; }
        public ICollection<Order> Orders { get; set; }
    }
    
    public class Order {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    public class CustomerDto {
        public int Id { get; set; }
        public string FullName { get; set; }
        public bool IsActive { get; set; }
        public int OrderCount { get; set; }

        public CustomerDto() { }   // required parameterless ctor

        [Projectable]
        public CustomerDto(Customer customer)
        {
            Id = customer.Id;
            FullName = customer.FirstName + "" "" + customer.LastName;
            IsActive = customer.IsActive;
            OrderCount = customer.Orders.Count();
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