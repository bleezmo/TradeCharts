using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace TradeCharts
{
    public partial class StocksContext : DbContext
    {
        public StocksContext()
        {
        }

        public StocksContext(DbContextOptions<StocksContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Trade> Trades { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Database=stocks;Username=dbuser;Password=abc123");
            }
        }
    }
}
