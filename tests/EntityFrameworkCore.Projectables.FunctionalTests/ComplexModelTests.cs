using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace EntityFrameworkCore.Projectables.FunctionalTests
{

    public class ComplexModelTests
    {
        public class User
        {
            public int Id { get; set; }

            public string DisplayName { get; set; }

            public ICollection<Order> Orders { get; set; }

            [Projectable]
            public Order LastOrder =>
                Orders.OrderByDescending(x => x.RecordDate).FirstOrDefault();

            [Projectable]
            [NotMapped]
            public IEnumerable<Order> Last2Orders =>
                Orders.OrderByDescending(x => x.RecordDate).Take(2);

            [Projectable]
            public Order GetLastOrderFromExternalDbContext(DbContext dbContext)
                => dbContext.Set<Order>().Where(x => x.UserId == Id).OrderByDescending(x => x.RecordDate).FirstOrDefault();

        }

        public class Order
        {
            public int Id { get; set; }

            public int UserId { get; set; }

            public DateTime RecordDate { get; set; }
        } 
        
        public class GenericObject<T>
        {
            public T Id { get; set; }
        } 

        [Fact]
        public Task ProjectOverNavigationProperty()
        {
            using var dbContext = new SampleDbContext<User>();

            var query = dbContext.Set<User>()
                .Select(x => x.LastOrder.RecordDate);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverCollectionNavigationProperty()
        {
            using var dbContext = new SampleDbContext<User>();

            var query = dbContext.Set<User>()
                .SelectMany(x => x.Last2Orders)
                .Select(x => x.RecordDate);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverMethodTakingDbContext()
        {
            using var dbContext = new SampleDbContext<User>(Infrastructure.CompatibilityMode.Full);

            var query = dbContext.Set<User>()
                .Select(x => x.GetLastOrderFromExternalDbContext(dbContext));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectQueryFilters()
        {
            using var dbContext = new SampleUserWithGlobalQueryFilterDbContext();

            var query = dbContext.Set<User>()
                .SelectMany(x => x.Last2Orders)
                .Select(x => x.RecordDate);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverGenericType()
        {
            using var dbContext = new SampleDbContext<GenericObject<int>>();
            
            var query = dbContext.Set<GenericObject<int>>()
                .Select(x => x.Id);
            
            return Verifier.Verify(query.ToQueryString());
        }
    }
}
