using System.Net;
using System.Net.Http.Json;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.Tenant;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed partial class AdvancedRetailApiTests
{
    [Fact]
    public async Task V2Context_ReturnsCanonicalEnvelopeAndCorrelationHeader()
    {
        await using var factory = new AdvancedRetailFactory();
        using var owner = await AuthClientAsync(factory, "owner");

        using var response = await owner.GetAsync("/api/v2/context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<TenantContextDto>>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Data.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(body.Meta.CorrelationId));
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal(body.Meta.CorrelationId, Assert.Single(values!));
        Assert.True(body.Meta.AsOf <= DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public async Task V2Context_UnauthenticatedErrorIsCanonicalAndDoesNotLeakStack()
    {
        await using var factory = new AdvancedRetailFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v2/context");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("AUTHENTICATION_REQUIRED", body!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));
        Assert.Empty(body.Details);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stack", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CapabilityAndValidationErrorsUseSameCanonicalContract()
    {
        await using var factory = new AdvancedRetailFactory();
        using var owner = await AuthClientAsync(factory, "owner");

        using var forbidden = await owner.GetAsync("/api/retail/catalog");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var forbiddenBody = await forbidden.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(forbiddenBody);
        Assert.Equal("CAPABILITY_NOT_ENABLED", forbiddenBody!.Code);
        Assert.False(string.IsNullOrWhiteSpace(forbiddenBody.CorrelationId));

        using var invalid = await owner.PostAsJsonAsync("/api/products", new { });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidBody = await invalid.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(invalidBody);
        Assert.Equal("VALIDATION_FAILED", invalidBody!.Code);
        Assert.False(string.IsNullOrWhiteSpace(invalidBody.CorrelationId));
        Assert.NotEmpty(invalidBody.Details);
    }
}
