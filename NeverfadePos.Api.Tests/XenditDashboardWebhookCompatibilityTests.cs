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
    [InlineData("payment.succeeded")]
    [InlineData("payment.failed")]
    public async Task LegacyDashboardSample_IsAcknowledged(
        string eventName)
    {
        var service = new ThrowingPaymentService(
            new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "XENDIT_WEBHOOK_INVALID",
                "strict v3 validator rejected legacy sample"));
        var controller = new XenditWebhookController(service);
        var webhook = CreateLegacySample(eventName);

        var result = await controller.Payment(
            "verified-by-service",
            webhook,
            CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, service.WebhookCalls);
    }

    [Fact]
    public async Task LegacyDashboardSample_WithInvalidToken_RemainsRejected()
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
                CreateLegacySample("payment.failed"),
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task InvalidNonDashboardPayload_RemainsRejected()
    {
        var expected = new PaymentApiException(
            StatusCodes.Status400BadRequest,
            "XENDIT_WEBHOOK_INVALID",
            "invalid webhook");
        var service = new ThrowingPaymentService(expected);
        var controller = new XenditWebhookController(service);
        var webhook = new XenditPaymentWebhookDto
        {
            Event = "payment.failed",
            Data = new XenditPaymentWebhookDataDto
            {
                LegacyId = "sample-id"
            }
        };

        var actual = await Assert.ThrowsAsync<PaymentApiException>(() =>
            controller.Payment(
                "verified-by-service",
                webhook,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void LegacyDashboardFields_AreDeserializedWithoutAffectingV3Fields()
    {
        const string json = """
            {
              "event": "payment.failed",
              "data": {
                "id": "dashboard-sample-payment",
                "amount": 10000
              }
            }
            """;

        var webhook = JsonSerializer.Deserialize<XenditPaymentWebhookDto>(json);

        Assert.NotNull(webhook);
        Assert.Equal("payment.failed", webhook.Event);
        Assert.Equal("dashboard-sample-payment", webhook.Data.LegacyId);
        Assert.Equal(10000m, webhook.Data.LegacyAmount);
        Assert.Equal(string.Empty, webhook.Data.PaymentId);
        Assert.Equal(string.Empty, webhook.Data.PaymentRequestId);
    }

    private static XenditPaymentWebhookDto CreateLegacySample(
        string eventName) => new()
    {
        Event = eventName,
        Data = new XenditPaymentWebhookDataDto
        {
            LegacyId = "dashboard-sample-payment",
            LegacyAmount = 10000m
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
