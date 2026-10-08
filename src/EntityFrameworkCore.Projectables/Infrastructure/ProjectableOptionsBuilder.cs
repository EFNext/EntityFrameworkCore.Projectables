using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.Projectables.Infrastructure
{
    public class ProjectableOptionsBuilder
    {
        readonly DbContextOptionsBuilder _optionsBuilder;

        public ProjectableOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            _optionsBuilder = optionsBuilder ?? throw new ArgumentNullException(nameof(optionsBuilder));
        }

        /// <summary>
        /// Change the default CompatibilityMode
        /// </summary>
        public ProjectableOptionsBuilder CompatibilityMode(CompatibilityMode mode)
            => WithOption(x => x.WithCompatibilityMode(mode));

        /// <summary>
        /// Controls whether projectable properties that have a setter are filled in with their
        /// database-computed value when a query loads whole entities (issue #84). Enabled by
        /// default.
        /// <para>
        /// Disabling it leaves projectables usable inside queries -- in Select, Where,
        /// OrderBy and so on -- while a loaded entity keeps whatever its CLR member
        /// returns. That is the behaviour of versions from before #84, and is what you want
        /// when entities are only ever projected explicitly.
        /// </para>
        /// </summary>
        public ProjectableOptionsBuilder PopulateSettableProperties(bool enabled)
            => WithOption(x => x.WithPopulateSettableProperties(enabled));

        /// <summary>
        ///     Sets an option by cloning the extension used to store the settings. This ensures the builder
        ///     does not modify options that are already in use elsewhere.
        /// </summary>
        /// <param name="setAction"> An action to set the option. </param>
        /// <returns> The same builder instance so that multiple calls can be chained. </returns>
        protected virtual ProjectableOptionsBuilder WithOption(Func<ProjectionOptionsExtension, ProjectionOptionsExtension> setAction)
        {
            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder).AddOrUpdateExtension(
                setAction(_optionsBuilder.Options.FindExtension<ProjectionOptionsExtension>() ?? new ProjectionOptionsExtension()));

            return this;
        }
    }
}
