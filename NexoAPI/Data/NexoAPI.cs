﻿using System;
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

        public DbSet<Account> Address { get; set; } = default!;
    }
}
