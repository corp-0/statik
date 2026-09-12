using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Statik.Server.Models;

namespace Statik.Server.Data.Configurations;

public class UserPageConfiguration : IEntityTypeConfiguration<UserPage>
{
    public void Configure(EntityTypeBuilder<UserPage> builder)
    {
        builder.HasKey(page => page.UserId);

        builder.HasOne(page => page.Owner)
            .WithOne()
            .HasForeignKey<UserPage>(page => page.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
