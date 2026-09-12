using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Statik.Server.Models;

namespace Statik.Server.Data;

public class StatikDbContext(DbContextOptions<StatikDbContext> options) : IdentityDbContext<User>(options)
{
    public DbSet<UserPage> UserPages => Set<UserPage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StatikDbContext).Assembly);
    }
}
