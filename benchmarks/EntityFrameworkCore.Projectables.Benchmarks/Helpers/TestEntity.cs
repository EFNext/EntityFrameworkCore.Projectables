using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFrameworkCore.Projectables.Benchmarks.Helpers
{
    public class TestEntity
    {
        public int Id { get; set; }

        [Projectable]
        public int IdPlus1 => Id + 1;

        [Projectable]
        public int IdPlus1Method() => Id + 1;

        [Projectable]
        public int IdPlusDelta(int delta) => Id + delta;
    }
}
