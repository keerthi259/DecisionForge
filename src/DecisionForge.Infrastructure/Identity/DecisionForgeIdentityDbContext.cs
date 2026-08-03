using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DecisionForge.Infrastructure.Identity;

public sealed class DecisionForgeIdentityDbContext(
    DbContextOptions<DecisionForgeIdentityDbContext> options)
    : IdentityDbContext<DecisionForgeUser, IdentityRole<Guid>, Guid>(options)
{
    public const string SchemaName = "identity";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(SchemaName);

        builder.Entity<DecisionForgeUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
