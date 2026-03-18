using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.FunctionalTests.ExtensionMethods
{
    public static class EntityExtensions
    {
        [Projectable]
        public static int Squared(this int i) => i * i;

        [Projectable]
        public static int Foo(this Entity entity) => entity.Id + 1;

        [Projectable]
        public static int Foo2(this Entity entity) => entity.Foo() + 1;

        [Projectable]
        public static Entity? LeadingEntity(this Entity entity, DbContext dbContext)
            => dbContext.Set<Entity>().Where(y => y.Id > entity.Id).FirstOrDefault();

        [Projectable(UseMemberBody = nameof(NameEqualsExpr))]
        public static bool NameEquals(this Entity a, Entity b) => a.Name == b.Name;

        private static Expression<Func<Entity, Entity, bool>> NameEqualsExpr => (a, b) => a.Name == b.Name;
    }
}
