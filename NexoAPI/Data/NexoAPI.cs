using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NexoAPI;
using NexoAPI.Models;

namespace NexoAPI.Data
{
    public class NexoAPIContext : DbContext
    {
        public NexoAPIContext(DbContextOptions<NexoAPIContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Account { get; set; } = default!;

        public DbSet<User> User { get; set; } = default!;

        public DbSet<Remark> Remark { get; set; } = default!;

        public DbSet<NexoAPI.Models.Transaction> Transaction { get; set; } = default!;
    }
}
