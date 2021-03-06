﻿using Microsoft.EntityFrameworkCore;
using NetFusion.Common;
using NetFusion.Common.Extensions.Collection;
using System.Linq;

namespace NetFusion.EntityFramework
{
    /// <summary>
    /// Simple class deriving from the base Entity Framework database context
    /// allowing common implementation and conformance to an interface.
    /// </summary>
    public abstract class EntityDbContext : DbContext,
        IEntityDbContext
    {
        private readonly string _connectionString;
        private readonly IEntityTypeMapping[] _mappings;

        public EntityDbContext(string connectionString, IEntityTypeMapping[] mappings)
        {
            Check.NotNull(connectionString, "connection string not specified");
            Check.NotNull(mappings, nameof(mappings), "entity mappings not specified");

            _connectionString = connectionString;
            _mappings = mappings;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.UseSqlServer(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _mappings.ForEach(m => m.AddMappings(modelBuilder));
        }
    }
}
