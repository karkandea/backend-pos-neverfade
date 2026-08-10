using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantA_CannotReadTenantB()
    {
        var databaseName = NewDatabaseName();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedProductsAsync(
            databaseName,
            tenantA,
            tenantB);

        await using var db = CreateDbContext(
            databaseName,
            CreateAuthenticatedContext(tenantA));

        var products = await db.Products
            .AsNoTracking()
            .ToListAsync();

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantB);

        Assert.Single(products);
        Assert.Equal(tenantA, products[0].TenantId);
        Assert.Null(product);
    }

    [Fact]
    public async Task MissingTenantContext_ReturnsNoTenantRows()
    {
        var databaseName = NewDatabaseName();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedProductsAsync(
            databaseName,
            tenantA,
            tenantB);

        await using var db = CreateDbContext(
            databaseName,
            CreateEmptyContext());

        Assert.Empty(await db.Products.ToListAsync());
    }

    [Fact]
    public async Task MalformedTenantClaim_ReturnsNoTenantRows()
    {
        var databaseName = NewDatabaseName();
        var tenantA = Guid.NewGuid();

        await SeedProductsAsync(
            databaseName,
            tenantA,
            Guid.NewGuid());

        var claims = new[]
        {
            new Claim("tenant_id", "not-a-guid")
        };

        await using var db = CreateDbContext(
            databaseName,
            CreateContext(claims));

        Assert.Empty(await db.Products.ToListAsync());
    }

    [Fact]
    public async Task MissingTenantContext_CannotWriteTenantRows()
    {
        await using var db = CreateDbContext(
            NewDatabaseName(),
            CreateEmptyContext());

        db.Products.Add(NewProduct(Guid.NewGuid(), "NO-CONTEXT"));

        var exception = await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains(
            "explicit tenant execution context",
            exception.Message);
    }

    [Fact]
    public async Task MismatchedTenantIdWrite_IsRejected()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var db = CreateDbContext(
            NewDatabaseName(),
            CreateAuthenticatedContext(tenantA));

        db.Products.Add(NewProduct(tenantB, "WRONG-TENANT"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TenantIdMutation_IsRejected()
    {
        var databaseName = NewDatabaseName();
        var tenantA = Guid.NewGuid();

        await SeedProductsAsync(
            databaseName,
            tenantA,
            Guid.NewGuid());

        await using var db = CreateDbContext(
            databaseName,
            CreateAuthenticatedContext(tenantA));

        var product = await db.Products.SingleAsync();
        product.TenantId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Equal(
            "TenantId cannot be changed.",
            exception.Message);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task TenantA_CannotUpdateOrDeleteTenantB(
        EntityState state)
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var db = CreateDbContext(
            NewDatabaseName(),
            CreateAuthenticatedContext(tenantA));

        var foreignProduct = NewProduct(
            tenantB,
            "FOREIGN");

        db.Attach(foreignProduct);
        db.Entry(foreignProduct).State = state;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TrustedSystemTenantA_CannotAccessTenantB()
    {
        var databaseName = NewDatabaseName();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedProductsAsync(
            databaseName,
            tenantA,
            tenantB);

        var context = CreateEmptyContext();

        using var scope = context.Begin(
            tenantA,
            "test-trusted-read");

        await using var db = CreateDbContext(
            databaseName,
            context);

        var products = await db.Products.ToListAsync();

        Assert.Single(products);
        Assert.Equal(tenantA, products[0].TenantId);

        db.Products.Add(NewProduct(
            tenantB,
            "TRUSTED-WRONG"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());
    }

    [Fact]
    public void NestedOrConflictingTrustedScope_IsRejected()
    {
        var context = CreateEmptyContext();

        using var scope = context.Begin(
            Guid.NewGuid(),
            "outer");

        Assert.Throws<InvalidOperationException>(() =>
            context.Begin(
                Guid.NewGuid(),
                "inner"));
    }

    [Fact]
    public void AuthenticatedTenant_CannotOpenTrustedScope()
    {
        var context = CreateAuthenticatedContext(
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            context.Begin(
                Guid.NewGuid(),
                "not-allowed"));
    }

    [Fact]
    public async Task CorrectTenantCrud_StillWorks()
    {
        var databaseName = NewDatabaseName();
        var tenantA = Guid.NewGuid();

        await using (var createDb = CreateDbContext(
            databaseName,
            CreateAuthenticatedContext(tenantA)))
        {
            var product = NewProduct(
                Guid.Empty,
                "CRUD");

            createDb.Products.Add(product);
            await createDb.SaveChangesAsync();

            Assert.Equal(tenantA, product.TenantId);
        }

        await using (var updateDb = CreateDbContext(
            databaseName,
            CreateAuthenticatedContext(tenantA)))
        {
            var product = await updateDb.Products.SingleAsync();
            product.Nama = "Updated";
            await updateDb.SaveChangesAsync();
        }

        await using (var deleteDb = CreateDbContext(
            databaseName,
            CreateAuthenticatedContext(tenantA)))
        {
            var product = await deleteDb.Products.SingleAsync();
            deleteDb.Products.Remove(product);
            await deleteDb.SaveChangesAsync();
        }

        await using var verifyDb = CreateDbContext(
            databaseName,
            CreateAuthenticatedContext(tenantA));

        Assert.Empty(await verifyDb.Products.ToListAsync());
    }

    private static async Task SeedProductsAsync(
        string databaseName,
        Guid tenantA,
        Guid tenantB)
    {
        var context = CreateEmptyContext();

        await using var db = CreateDbContext(
            databaseName,
            context);

        using (context.Begin(tenantA, "seed-tenant-a"))
        {
            db.Products.Add(NewProduct(tenantA, "TENANT-A"));
            await db.SaveChangesAsync();
        }

        using (context.Begin(tenantB, "seed-tenant-b"))
        {
            db.Products.Add(NewProduct(tenantB, "TENANT-B"));
            await db.SaveChangesAsync();
        }
    }

    private static Product NewProduct(
        Guid tenantId,
        string code)
    {
        return new Product
        {
            TenantId = tenantId,
            Kode = code,
            Nama = code,
            Kategori = "Test",
            HargaModal = 1,
            HargaJual = 2,
            Stok = 1,
            Satuan = "pcs"
        };
    }

    private static AppDbContext CreateDbContext(
        string databaseName,
        ITenantExecutionContext context)
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

        return new AppDbContext(options, context);
    }

    private static TenantExecutionContext
        CreateAuthenticatedContext(Guid tenantId)
    {
        return CreateContext(new[]
        {
            new Claim("tenant_id", tenantId.ToString())
        });
    }

    private static TenantExecutionContext CreateEmptyContext()
    {
        return CreateContext(Array.Empty<Claim>());
    }

    private static TenantExecutionContext CreateContext(
        IEnumerable<Claim> claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "Test"))
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        return new TenantExecutionContext(
            new CurrentUser(accessor));
    }

    private static string NewDatabaseName()
    {
        return $"tenant-isolation-{Guid.NewGuid():N}";
    }
}
