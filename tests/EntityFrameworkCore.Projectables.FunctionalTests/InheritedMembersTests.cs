using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class InheritedMembersTests
    {
        public abstract class Animal
        {
            public int Id { get; set; }
            public double AverageLifespan { get; set; }
            public int Age { get; set; }

            [Projectable]
            public double LifeProgression
                => Age / AverageLifespan;
        }

        public class Cat : Animal
        {
            public int MentalAge { get; set; }

            [Projectable]
            public double MentalLifeProgression
                => MentalAge / AverageLifespan;
        }


        [Fact]
        public Task ProjectOverMethodTakingDbContext()
        {
            using var dbContext = new SampleDbContext<Cat>();

            var query = dbContext.Set<Cat>()
                .Select(x => new { x.LifeProgression, x.MentalLifeProgression });

            return Verifier.Verify(query.ToQueryString());
        }
    }
}
