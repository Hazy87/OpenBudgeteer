﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OpenBudgeteer.Core.Common.Database
{
    public class SqliteDatabaseContextFactory : IDesignTimeDbContextFactory<SqliteDatabaseContext>
    {
        public SqliteDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            optionsBuilder.UseSqlite("Data Source=openbudgeteer.db");

            return new SqliteDatabaseContext(optionsBuilder.Options);
        }

        public SqliteDatabaseContext CreateDbContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite(
                    connectionString,
                    b => b.MigrationsAssembly("OpenBudgeteer.Core"));
            return new SqliteDatabaseContext(optionsBuilder.Options);
        }
    }
}
