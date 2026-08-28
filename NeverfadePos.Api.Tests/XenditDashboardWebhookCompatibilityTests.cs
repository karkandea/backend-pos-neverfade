using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Controllers;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Services.Payment;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class XenditDashboardWebhookCompatibilityTests
{
    [Theory]
    [InlineData("payment.succeeded", "SUCCEEDED")]
    [InlineData("payment.failed", "FAILED")]
    public async Task DashboardSample_IsAcknowledged(
        string eventName,
        string status)
    {
        var service = new ThrowingPaymentService(
            new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "XENDIT_WEBHOOK_INVALID",
                "strict v3 validator rejected dashboard sample"));
        var controller = new XenditWebhookController(service);
        var webhook = CreateDashboardSample(eventName, status);

        var result = await controller.Payment(
            "verified-by-service",
            webhook,
            CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, service.WebhookCalls);
    }

    [Fact]
    public async Task DashboardV3PaymentStatusSample_RouteMissing_IsAcknowledged()
    {
        var service = new ThrowingPaymentService(
            new PaymentApiException(
                StatusCodes.Status404NotFound,
                "PAYMENT_ROUTE_NOT_FOUND",
                "dashboard fixture has no NeverFade route"));
        var controller = new XenditWebhookController(service);

        var result = await controller.Payment(
            "verified-by-service",
            CreateDashboardV3PaymentStatusSample(),
            CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, service.WebhookCalls);
    }

    [Fact]
    public async Task DashboardV3PaymentStatusSample_WithInvalidToken_RemainsRejected()
    {
        var expected = new PaymentApiException(
            StatusCodes.Status401Unauthorized,
            "XENDIT_WEBHOOK_UNAUTHORIZED",
            "invalid callback token");
        var service = new ThrowingPaymentService(expected);
        var controller = new XenditWebhookController(service);

        var actual = await Assert.ThrowsAsync<PaymentApiException>(() =>
            controller.Payment(
                "wrong-token",
                CreateDashboardV3PaymentStatusSample(),
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task UnknownV3Route_RemainsRejected()
    {
        var expected = new PaymentApiException(
            StatusCodes.Status404NotFound,
            "PAYMENT_ROUTE_NOT_FOUND",
            "unknown real payment route");
        var service = new ThrowingPaymentService(expected);
        var controller = new XenditWebhookController(service);
        var webhook = CreateDashboardV3PaymentStatusSample();
        webhook.Data.PaymentRequestId = "pr-real-unknown";

        var actual = await Assert.ThrowsAsync<PaymentApiException>(() =>
            controller.Payment(
                "verified-by-service",
                webhook,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DashboardSample_WithInvalidToken_RemainsRejected()
    {
        var expected = new PaymentApiException(
            StatusCodes.Status401Unauthorized,
            "XENDIT_WEBHOOK_UNAUTHORIZED",
            "invalid callback token");
        var service = new ThrowingPaymentService(expected);
        var controller = new XenditWebhookController(service);

        var actual = await Assert.ThrowsAsync<PaymentApiException>(() =>
            controller.Payment(
                "wrong-token",
                CreateDashboardSample("payment.failed", "FAILED"),
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task LegacyLikePayload_FromNonSampleBusiness_RemainsRejected()
    {
        var expected = new PaymentApiException(
            StatusCodes.Status400BadRequest,
            "XENDIT_WEBHOOK_INVALID",
            "invalid webhook");
        var service = new ThrowingPaymentService(expected);
        var controller = new XenditWebhookController(service);
        var webhook = CreateDashboardSample("payment.failed", "FAILED");
        webhook.BusinessId = "real_business_id";

        var actual = await Assert.ThrowsAsync<PaymentApiException>(() =>
            controller.Payment(
                "verified-by-service",
                webhook,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task MalformedDashboardSample_RemainsRejected()
    {
        var expected = new PaymentApiException(
            StatusCodes.Status400BadRequest,
            "XENDIT_WEBHOOK_INVALID",
            "invalid webhook");
        var service = new ThrowingPaymentService(expected);
        var controller = new XenditWebhookController(service);
        var webhook = CreateDashboardSample("payment.failed", "FAILED");
        webhook.Data.LegacyAmount = null;

        var actual = await Assert.ThrowsAsync<PaymentApiException>(() =>
            controller.Payment(
                "verified-by-service",
                webhook,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void FullDashboardSample_DeserializesExpectedCompatibilityFields()
    {
        const string json = """
            {
              "event": "payment.succeeded",
              "business_id": "sample_business_id",
              "created": "2022-02-16T06:01:09.997108276Z",
              "data": {
                "id": "pymt-2e9badf8-1473-4e8a-a1cf-d1e3214afc0f",
                "amount": 15000,
                "currency": "IDR",
                "payment_request_id": "pr-df560c7d-b059-4789-ad2f-3cee5d8230a8",
                "reference_id": "a5151a05-e84d-4cef-bb17-1ref3e7fb3a",
                "status": "SUCCEEDED"
              }
            }
            """;

        var webhook = JsonSerializer.Deserialize<XenditPaymentWebhookDto>(json);

        Assert.NotNull(webhook);
        Assert.Equal("payment.succeeded", webhook.Event);
        Assert.Equal("sample_business_id", webhook.BusinessId);
        Assert.Equal(
            "pymt-2e9badf8-1473-4e8a-a1cf-d1e3214afc0f",
            webhook.Data.LegacyId);
        Assert.Equal(15000m, webhook.Data.LegacyAmount);
        Assert.Equal(
            "pr-df560c7d-b059-4789-ad2f-3cee5d8230a8",
            webhook.Data.PaymentRequestId);
        Assert.Equal(
            "a5151a05-e84d-4cef-bb17-1ref3e7fb3a",
            webhook.Data.ReferenceId);
        Assert.Equal("SUCCEEDED", webhook.Data.Status);
        Assert.Equal("IDR", webhook.Data.Currency);
        Assert.Equal(string.Empty, webhook.Data.PaymentId);
    }

    private static XenditPaymentWebhookDto CreateDashboardSample(
        string eventName,
        string status) => new()
    {
        Event = eventName,
        BusinessId = "sample_business_id",
        Data = new XenditPaymentWebhookDataDto
        {
            LegacyId = "pymt-dashboard-sample",
            LegacyAmount = 15000m,
            PaymentRequestId = "pr-dashboard-sample",
            ReferenceId = "dashboard-reference",
            Status = status,
            Currency = "IDR"
        }
    };

    private static XenditPaymentWebhookDto CreateDashboardV3PaymentStatusSample() =>
        new()
        {
            Event = "payment.capture",
            BusinessId = "62440e322008e87fb29c1fd0",
            Data = new XenditPaymentWebhookDataDto
            {
                PaymentId = "py-97716cc2-2840-4ead-949b-db60e9aeb55e",
                PaymentRequestId = "pr-ced7965b-d588-49f1-ba41-d499277e5395",
                ReferenceId = "90392f42-d98a-49ef-a7f3-90392f42d98a",
                RequestAmount = 10000m,
                Status = "SUCCEEDED",
                ChannelCode = "CARDS",
                Currency = "IDR"
            }
        };

    private sealed class ThrowingPaymentService(Exception webhookException)
        : IPaymentService
    {
        public int WebhookCalls { get; private set; }

        public PaymentCapabilitiesDto GetCapabilities() =>
            throw new NotSupportedException();

        public Task<QrisPaymentDto> CreateQrisAsync(
            CreateTransactionDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentStatusDto> GetStatusAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentStatusDto?> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentStatusDto> CancelAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ProcessXenditWebhookAsync(
            string? callbackToken,
            XenditPaymentWebhookDto webhook,
            CancellationToken cancellationToken = default)
        {
            WebhookCalls++;
            return Task.FromException(webhookException);
        }
    }
}
