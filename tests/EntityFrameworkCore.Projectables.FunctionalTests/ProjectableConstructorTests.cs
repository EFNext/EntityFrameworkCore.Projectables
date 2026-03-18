using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

#nullable disable

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class ProjectableConstructorTests
    {
        public class PersonEntity
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public int Score { get; set; }
        }

        /// <summary>DTO built from scalar entity fields.</summary>
        public class PersonSummaryDto
        {
            public int Id { get; set; }
            public string FullName { get; set; }

            public PersonSummaryDto() { }   // required: EF Core uses the parameterless ctor

            [Projectable]
            public PersonSummaryDto(int id, string firstName, string lastName)
            {
                Id = id;
                FullName = firstName + " " + lastName;
            }
        }

        /// <summary>DTO built by passing the whole entity instance as the constructor argument.</summary>
        public class PersonFromEntityDto
        {
            public int Id { get; set; }
            public string FullName { get; set; }

            public PersonFromEntityDto() { }  // required: EF Core uses the parameterless ctor

            [Projectable]
            public PersonFromEntityDto(PersonEntity entity)
            {
                Id = entity.Id;
                FullName = entity.FirstName + " " + entity.LastName;
            }
        }

        public class BaseDto
        {
            public int Id { get; set; }

            public BaseDto() { }              // required
            public BaseDto(int id) { Id = id; }
        }

        public class DerivedDto : BaseDto
        {
            public string FullName { get; set; }

            public DerivedDto() { }            // required: EF Core uses the parameterless ctor

            /// <summary>
            /// <c>Id</c> is automatically included from <c>: base(id)</c> — no need to repeat it here.
            /// </summary>
            [Projectable]
            public DerivedDto(int id, string firstName, string lastName) : base(id)
            {
                FullName = firstName + " " + lastName;
            }
        }

        public class PersonOverloadedDto
        {
            public int Id { get; set; }
            public string FullName { get; set; }

            public PersonOverloadedDto() { }   // required: EF Core uses the parameterless ctor

            [Projectable]
            public PersonOverloadedDto(int id, string firstName, string lastName)
            {
                Id = id;
                FullName = firstName + " " + lastName;
            }

            [Projectable]
            public PersonOverloadedDto(string firstName, string lastName)
            {
                // Id deliberately not set → should not appear as a DB column in the query
                FullName = firstName + " " + lastName;
            }
        }

        // ── Partial / unmapped property ───────────────────────────────────────────

        /// <summary>
        /// DTO with a <c>Nickname</c> property that is intentionally NOT assigned
        /// in the <c>[Projectable]</c> constructor body.
        /// The generated SQL must NOT include a column for <c>Nickname</c>.
        /// </summary>
        public class PersonPartialDto
        {
            public int Id { get; set; }
            public string FullName { get; set; }
            public string Nickname { get; set; }  // intentionally unmapped in the constructor

            public PersonPartialDto() { }          // required

            [Projectable]
            public PersonPartialDto(int id, string firstName, string lastName)
            {
                Id = id;
                FullName = firstName + " " + lastName;
                // Nickname is intentionally NOT assigned here
            }
        }

        /// <summary>DTO with if/else logic in the constructor body.</summary>
        public class PersonGradeDto
        {
            public int Id { get; set; }
            public string Grade { get; set; }

            public PersonGradeDto() { }

            [Projectable]
            public PersonGradeDto(int id, int score)
            {
                Id = id;
                if (score >= 90)
                {
                    Grade = "A";
                }
                else
                {
                    Grade = "B";
                }
            }
        }

        /// <summary>DTO using a local variable in the constructor body.</summary>
        public class PersonLocalVarDto
        {
            public string FullName { get; set; }

            public PersonLocalVarDto() { }

            [Projectable]
            public PersonLocalVarDto(string first, string last)
            {
                var full = first + " " + last;
                FullName = full;
            }
        }

        public class PersonBaseWithExprDto
        {
            public string Code { get; set; }

            public PersonBaseWithExprDto() { }
            public PersonBaseWithExprDto(string code) { Code = code; }
        }

        public class PersonDerivedWithExprDto : PersonBaseWithExprDto
        {
            public string Name { get; set; }

            public PersonDerivedWithExprDto() { }

            [Projectable]
            public PersonDerivedWithExprDto(string name, string rawCode) : base(rawCode.ToUpper())
            {
                Name = name;
            }
        }

        public class PersonBaseWithLogicDto
        {
            public int Id { get; set; }

            public PersonBaseWithLogicDto() { }
            public PersonBaseWithLogicDto(int id)
            {
                if (id < 0)
                {
                    Id = 0;
                }
                else
                {
                    Id = id;
                }
            }
        }

        public class PersonDerivedWithBaseLogicDto : PersonBaseWithLogicDto
        {
            public string FullName { get; set; }

            public PersonDerivedWithBaseLogicDto() { }

            [Projectable]
            public PersonDerivedWithBaseLogicDto(int id, string firstName, string lastName) : base(id)
            {
                FullName = firstName + " " + lastName;
            }
        }

        // ── Referencing previously-assigned property ──────────────────────────────

        public class PersonWithCompositeDto
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string FullName { get; set; }

            public PersonWithCompositeDto() { }

            [Projectable]
            public PersonWithCompositeDto(string firstName, string lastName)
            {
                FirstName = firstName;
                LastName = lastName;
                FullName = FirstName + " " + LastName;   // references previously-assigned props
            }
        }

        // ── Referencing base property in derived body ─────────────────────────────

        public class PersonBaseCodeDto
        {
            public string Code { get; set; }
            public PersonBaseCodeDto() { }
            public PersonBaseCodeDto(string code) { Code = code; }
        }

        public class PersonDerivedLabelDto : PersonBaseCodeDto
        {
            public string Label { get; set; }

            public PersonDerivedLabelDto() { }

            [Projectable]
            public PersonDerivedLabelDto(string code) : base(code)
            {
                Label = "[" + Code + "]";   // Code was set by base ctor → should inline it
            }
        }

        // ── Static / const member ─────────────────────────────────────────────────

        public class PersonWithConstSeparatorDto
        {
            internal const string Separator = " - ";

            public string FullName { get; set; }

            public PersonWithConstSeparatorDto() { }

            [Projectable]
            public PersonWithConstSeparatorDto(string firstName, string lastName)
            {
                FullName = firstName + Separator + lastName;
            }
        }

        // ── this() overload – simple delegation ───────────────────────────────────

        public class PersonThisSimpleDto
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }

            public PersonThisSimpleDto() { }

            public PersonThisSimpleDto(string firstName, string lastName)
            {
                FirstName = firstName;
                LastName = lastName;
            }

            /// <summary>Delegates to the 2-arg ctor using a split on the full name.</summary>
            [Projectable]
            public PersonThisSimpleDto(string fullName) : this(fullName, "")
            {
            }
        }

        // ── this() overload – with additional body after delegation ───────────────

        public class PersonThisBodyAfterDto
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string FullName { get; set; }

            public PersonThisBodyAfterDto() { }

            public PersonThisBodyAfterDto(string firstName, string lastName)
            {
                FirstName = firstName;
                LastName = lastName;
            }

            [Projectable]
            public PersonThisBodyAfterDto(string firstName, string lastName, bool upper) : this(firstName, lastName)
            {
                FullName = upper ? (FirstName + " " + LastName).ToUpper() : FirstName + " " + LastName;
            }
        }

        // ── this() overload – if/else logic in the delegated constructor ──────────

        public class PersonThisIfElseDto
        {
            public string Grade { get; set; }
            public int Score { get; set; }

            public PersonThisIfElseDto() { }

            public PersonThisIfElseDto(int score)
            {
                Score = score;
                if (score >= 90)
                    Grade = "A";
                else
                    Grade = "B";
            }

            [Projectable]
            public PersonThisIfElseDto(int score, string prefix) : this(score)
            {
                Grade = prefix + Grade;
            }
        }

        // ── this() → base() chain ─────────────────────────────────────────────────

        public class PersonChainBase
        {
            public int Id { get; set; }
            public PersonChainBase() { }
            public PersonChainBase(int id) { Id = id; }
        }

        public class PersonChainChild : PersonChainBase
        {
            public string Name { get; set; }

            public PersonChainChild() { }

            public PersonChainChild(int id, string name) : base(id)
            {
                Name = name;
            }

            [Projectable]
            public PersonChainChild(int id, string name, string suffix) : this(id, name)
            {
                Name = Name + suffix;
            }
        }

        [Fact]
        public Task Select_ScalarFieldsToDto()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonSummaryDto(p.Id, p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_EntityInstanceToDto()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonFromEntityDto(p));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_DerivedDtoWithBaseConstructor()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new DerivedDto(p.Id, p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_OverloadedConstructor_WithThreeArgs()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonOverloadedDto(p.Id, p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_OverloadedConstructor_WithTwoArgs()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonOverloadedDto(p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        /// <summary>
        /// Verifies that a property not assigned in the [Projectable] constructor body
        /// does NOT appear as a column in the generated SQL query.
        /// </summary>
        [Fact]
        public Task Select_UnassignedPropertyNotInQuery()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonPartialDto(p.Id, p.FirstName, p.LastName));

            var sql = query.ToQueryString();

            // Nickname is not assigned in the constructor → must not appear in SQL
            Assert.DoesNotContain("Nickname", sql, System.StringComparison.OrdinalIgnoreCase);

            return Verifier.Verify(sql);
        }

        [Fact]
        public Task Select_ConstructorWithIfElseLogic()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonGradeDto(p.Id, p.Score));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ConstructorWithLocalVariable()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonLocalVarDto(p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ConstructorWithBaseInitializerExpression()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonDerivedWithExprDto(p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ConstructorWithBaseInitializerAndIfElse()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonDerivedWithBaseLogicDto(p.Id, p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ConstructorReferencingPreviouslyAssignedProperty()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonWithCompositeDto(p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ConstructorReferencingBasePropertyInDerivedBody()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonDerivedLabelDto(p.FirstName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ConstructorReferencingStaticConstMember()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonWithConstSeparatorDto(p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ThisOverload_SimpleDelegate()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonThisSimpleDto(p.FirstName));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ThisOverload_WithBodyAfterDelegation()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonThisBodyAfterDto(p.FirstName, p.LastName, false));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ThisOverload_WithIfElseInDelegated()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonThisIfElseDto(p.Score, "Grade:"));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task Select_ThisOverload_ChainedThisAndBase()
        {
            using var dbContext = new SampleDbContext<PersonEntity>();

            var query = dbContext.Set<PersonEntity>()
                .Select(p => new PersonChainChild(p.Id, p.FirstName, "-" + p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

