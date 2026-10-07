using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Statik.Server.Models;

namespace Statik.Server.Data;

public class StatikDbContext(DbContextOptions<StatikDbContext> options) : IdentityDbContext<User>(options), IDataProtectionKeyContext
{
    public DbSet<UserPage> UserPages => Set<UserPage>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>().HasIndex(user => user.NormalizedEmail).IsUnique();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StatikDbContext).Assembly);
    }
}
