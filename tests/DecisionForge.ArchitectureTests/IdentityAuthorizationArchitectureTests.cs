using System.Reflection;
using DecisionForge.Api.Authorization;
using DecisionForge.Api.Identity;
using DecisionForge.Application;
using DecisionForge.Application.Platform;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DecisionForge.ArchitectureTests;

public sealed class IdentityAuthorizationArchitectureTests
{
    [Fact]
    public void DomainAndApplicationRemainIndependentOfIdentityAspNetAndEfCore()
    {
        string[] forbiddenPrefixes =
        [
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
        ];
        string[] references = typeof(ApplicationAssembly).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => forbiddenPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(references);
    }

    [Fact]
    public void IdentityPersistenceAndHttpContextAdaptersStayInOuterLayers()
    {
        Assert.True(typeof(DbContext).IsAssignableFrom(typeof(DecisionForgeIdentityDbContext)));
        Assert.Equal(
            "DecisionForge.Infrastructure.Identity",
            typeof(DecisionForgeIdentityDbContext).Namespace);
        Assert.Equal("DecisionForge.Api.Identity", typeof(HttpCurrentUserContext).Namespace);
        Assert.True(typeof(ICurrentUserContext).IsAssignableFrom(typeof(HttpCurrentUserContext)));
        Assert.True(typeof(DecisionForgeIdentityDbContext).IsSealed);
        Assert.True(typeof(HttpCurrentUserContext).IsSealed);
    }

    [Fact]
    public void ResourceHandlersAreClosedAndUseTypedRequirements()
    {
        Type[] handlers =
        [
            typeof(PurchaseRequestAuthorizationHandler),
            typeof(ApprovalStageAuthorizationHandler),
        ];

        Assert.All(handlers, handler =>
        {
            Assert.True(handler.IsSealed);
            Assert.True(typeof(IAuthorizationHandler).IsAssignableFrom(handler));
            Assert.Equal("DecisionForge.Api.Authorization", handler.Namespace);
        });
        Assert.True(typeof(IAuthorizationRequirement).IsAssignableFrom(
            typeof(PurchaseRequestAuthorizationRequirement)));
        Assert.True(typeof(IAuthorizationRequirement).IsAssignableFrom(
            typeof(ActOnApprovalStageRequirement)));
    }

    [Fact]
    public void RoleCatalogIsUniqueAndContainsEverySpecifiedPersona()
    {
        Assert.Equal(10, DecisionForgeIdentityRoles.All.Count);
        Assert.Equal(
            DecisionForgeIdentityRoles.All.Count,
            DecisionForgeIdentityRoles.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(DecisionForgeIdentityRoles.All, role =>
            Assert.Matches("^[A-Z][A-Za-z]+$", role));
        Assert.Contains(DecisionForgeIdentityRoles.Requester, DecisionForgeIdentityRoles.All);
        Assert.Contains(DecisionForgeIdentityRoles.Administrator, DecisionForgeIdentityRoles.All);
        Assert.Contains(DecisionForgeIdentityRoles.Auditor, DecisionForgeIdentityRoles.All);
    }

    [Fact]
    public void TrustedCurrentUserContractExposesNoRequestMutationSurface()
    {
        PropertyInfo property = Assert.Single(typeof(ICurrentUserContext).GetProperties());

        Assert.Equal(nameof(ICurrentUserContext.UserId), property.Name);
        Assert.Null(property.SetMethod);
        Assert.DoesNotContain(
            typeof(ICurrentUserContext).GetMethods(),
            method => !method.IsSpecialName);
    }
}
