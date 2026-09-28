using System.Net;
using Microsoft.Extensions.Options;
using NeverfadePos.Api.Services.WhatsApp;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class WahaDeliveryModeTests
{
    [Fact]
    public async Task Disabled_GetSessionReturnsNullWithoutNetworkCall()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:3000/")
        };
        var client = new WahaClient(
            http,
            Options.Create(new WahaOptions { Enabled = false }));

        var result = await client.GetSessionAsync("staging");

        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Disabled_SendTextFailsBeforeNetworkCall()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:3000/")
        };
        var client = new WahaClient(
            http,
            Options.Create(new WahaOptions { Enabled = false }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendTextAsync("staging", "628123456789", "receipt"));

        Assert.Contains("dinonaktifkan", error.Message);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Disabled_EnsureSessionFailsBeforeNetworkCall()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:3000/")
        };
        var client = new WahaClient(
            http,
            Options.Create(new WahaOptions { Enabled = false }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.EnsureSessionAsync("staging"));

        Assert.Equal(0, handler.Calls);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls += 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
