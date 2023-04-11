using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
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

        public DbSet<Transaction> Transaction { get; set; } = default!;

        public DbSet<SignResult> SignResult { get; set; } = default!;
    }
}
