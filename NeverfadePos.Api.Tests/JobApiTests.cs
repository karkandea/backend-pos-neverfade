using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Job;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed partial class AdvancedRetailApiTests
{
    [Fact]
    public async Task V2JobGet_AllowsOwnerAndCreatingActor_WithCanonicalEnvelope()
    {
        await using var factory = new AdvancedRetailFactory();
        var job = await SeedJobAsync(factory, "kasir");

        using var owner = await AuthClientAsync(factory, "owner");
        using var ownerResponse = await owner.GetAsync($"/api/v2/jobs/{job.Id}");
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        var ownerBody = await ownerResponse.Content
            .ReadFromJsonAsync<ApiResponseEnvelope<JobDto>>();
        Assert.NotNull(ownerBody);
        Assert.Equal(job.Id, ownerBody!.Data.Id);
        Assert.Equal("qa_export", ownerBody.Data.Kind);
        Assert.Equal("queued", ownerBody.Data.State);
        Assert.Equal("source-job-correlation", ownerBody.Data.CorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(ownerBody.Meta.CorrelationId));
        Assert.True(ownerResponse.Headers.TryGetValues(
            "X-Correlation-Id",
            out var ownerCorrelation));
        Assert.Equal(
            ownerBody.Meta.CorrelationId,
            Assert.Single(ownerCorrelation!));

        using var actor = await AuthClientAsync(factory, "kasir");
        using var actorResponse = await actor.GetAsync($"/api/v2/jobs/{job.Id}");
        Assert.Equal(HttpStatusCode.OK, actorResponse.StatusCode);
    }

    [Fact]
    public async Task V2JobGet_HidesJobFromDifferentNonOwnerActor()
    {
        await using var factory = new AdvancedRetailFactory();
        var job = await SeedJobAsync(factory, "kasir");
        using var admin = await AuthClientAsync(factory, "admin");

        using var response = await admin.GetAsync($"/api/v2/jobs/{job.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("JOB_NOT_FOUND", body!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));
        Assert.DoesNotContain(
            "Exception",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task V2JobGet_CrossTenantGuidIsHiddenWithoutStackLeak()
    {
        await using var factory = new AdvancedRetailFactory();
        Guid foreignJobId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var foreignTenant = new Tenant
            {
                NamaToko = "Foreign Tenant",
                Slug = $"foreign-{Guid.NewGuid():N}",
                Status = "active"
            };
            db.Tenants.Add(foreignTenant);
            await db.SaveChangesAsync();

            using var tenantScope = scope.ServiceProvider
                .GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(foreignTenant.Id, "job-cross-tenant-test");
            var foreignJob = new Job
            {
                TenantId = foreignTenant.Id,
                Kind = "foreign_export",
                State = "running",
                CorrelationId = "foreign-secret-correlation"
            };
            db.Jobs.Add(foreignJob);
            await db.SaveChangesAsync();
            foreignJobId = foreignJob.Id;
        }

        using var owner = await AuthClientAsync(factory, "owner");
        using var response = await owner.GetAsync(
            $"/api/v2/jobs/{foreignJobId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("JOB_NOT_FOUND", body!.Code);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("foreign-secret-correlation", raw);
        Assert.DoesNotContain("stack", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Job> SeedJobAsync(
        AdvancedRetailFactory factory,
        string actorUsername)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Tenants
            .SingleAsync(x => x.Slug == "warung-lumpia-beef");

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenant.Id, "job-api-test");

        var actorId = await db.Users
            .Where(x => x.Username == actorUsername)
            .Select(x => x.Id)
            .SingleAsync();
        var outletId = await db.Outlets
            .Where(x => x.Active)
            .OrderByDescending(x => x.IsDefault)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        var job = new Job
        {
            TenantId = tenant.Id,
            OutletId = outletId == Guid.Empty ? null : outletId,
            ActorUserId = actorId,
            Kind = "qa_export",
            State = "queued",
            CorrelationId = "source-job-correlation"
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }
}
