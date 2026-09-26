using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Outlet;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Payments;
using NeverfadePos.Api.Payments.Xendit;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class XenditPaymentFoundationTests
{
    [Fact]
    public async Task Capabilities_ReturnConfiguredPaymentMode()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);

        var capabilities = await client.GetFromJsonAsync<
            PaymentCapabilitiesDto>("/api/payments/capabilities");

        Assert.NotNull(capabilities);
        Assert.True(capabilities.QrisEnabled);
        Assert.Equal("live", capabilities.Mode);
        Assert.False(capabilities.IsSandbox);
    }

    [Fact]
    public async Task DisabledMode_BlocksQrisBeforeCreatingDraft()
    {
        await using var factory = new PaymentApiFactory(
            paymentMode: "Disabled");
        using var client = await CreateOwnerClientAsync(factory);
        var product = await GetProductAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/payments/qris",
            CreateQrisRequest(product));

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);
        Assert.Contains(
            "PAYMENT_QRIS_DISABLED",
            await response.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Empty(factory.Provider.Requests);
    }

    [Fact]
    public async Task AllowedSandboxTenant_CanUseExistingQrisFlow()
    {
        await using var factory = new PaymentApiFactory(
            useAllowedSandboxGate: true);
        using var client = await CreateOwnerClientAsync(factory);

        var capabilities = await client.GetFromJsonAsync<
            PaymentCapabilitiesDto>("/api/payments/capabilities");
        var payment = await CreatePaymentAsync(client);

        Assert.NotNull(capabilities);
        Assert.True(capabilities.QrisEnabled);
        Assert.Equal("sandbox", capabilities.Mode);
        Assert.True(capabilities.IsSandbox);
        Assert.Equal(PaymentConstants.StatusPending, payment.Status);
        Assert.Single(factory.Provider.Requests);
    }

    [Fact]
    public async Task CreateQris_UsesServerCalculatedAmount()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var product = await GetProductAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/payments/qris",
            CreateQrisRequest(product));

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var result = await response.Content
            .ReadFromJsonAsync<QrisPaymentDto>();
        Assert.NotNull(result);
        Assert.Equal(product.HargaJual, result.Amount);
        Assert.Equal("pending", result.Status);
        Assert.Equal("000201010212TEST-QRIS", result.QrString);
        Assert.Equal(product.HargaJual, factory.Provider.LastAmount);

        await using var manipulatedFactory = new PaymentApiFactory();
        using var manipulatedClient = await CreateOwnerClientAsync(
            manipulatedFactory);
        var manipulatedProduct = await GetProductAsync(manipulatedClient);
        var manipulated = CreateQrisRequest(
            manipulatedProduct,
            totalOverride: manipulatedProduct.HargaJual + 1m);
        var rejected = await manipulatedClient.PostAsJsonAsync(
            "/api/payments/qris",
            manipulated);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Empty(manipulatedFactory.Provider.Requests);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(101, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 101)]
    public async Task InvalidDiscountOrTax_IsRejectedBeforePaymentCreation(
        decimal discount,
        decimal tax)
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var product = await GetProductAsync(client);
        var request = CreateQrisRequest(product);
        request.Disc = discount;
        request.Tax = tax;

        var response = await client.PostAsJsonAsync(
            "/api/payments/qris",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Provider.Requests);
    }

    [Fact]
    public async Task InvalidWebhookToken_IsRejected()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/webhooks/xendit/payments");
        request.Headers.Add("x-callback-token", "wrong-token");
        request.Content = JsonContent.Create(
            CaptureWebhook(payment));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            "XENDIT_WEBHOOK_UNAUTHORIZED",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PaymentStatus_ReturnsPendingPayment()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);

        var response = await client.GetAsync($"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content
            .ReadFromJsonAsync<PaymentStatusDto>();
        Assert.NotNull(status);
        Assert.Equal(payment.Id, status.Id);
        Assert.Equal(payment.TransactionId, status.TransactionId);
        Assert.Equal(PaymentConstants.StatusPending, status.Status);
        Assert.Equal(payment.Amount, status.Amount);
        Assert.Equal(payment.ProviderPaymentRequestId,
            status.ProviderPaymentRequestId);
        Assert.Equal("000201010212TEST-QRIS", status.QrString);
        Assert.NotNull(status.ExpiresAt);
    }

    [Fact]
    public async Task PaymentAttention_IsReadOnlyOwnerScopedAndShowsUncertainReference()
    {
        await using var factory = new PaymentApiFactory();
        using var owner = await CreateOwnerClientAsync(factory);
        var empty = (await owner.GetFromJsonAsync<PaymentAttentionResponseDto>(
            "/api/payments/attention"))!;
        Assert.Equal(0, empty.Total);
        var original = await CreatePaymentAsync(owner);
        var response = await owner.GetAsync("/api/payments/attention");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var pending = (await response.Content.ReadFromJsonAsync<PaymentAttentionResponseDto>())!;
        Assert.Equal(1, pending.Total);
        var row = Assert.Single(pending.Items);
        Assert.Equal(original.Id, row.PaymentId);
        Assert.Equal(original.TransactionId, row.TransactionId);
        Assert.Equal(original.ProviderReferenceId, row.ProviderReferenceId);
        Assert.Equal(original.Amount, row.Amount);
        Assert.Equal("awaiting_provider_confirmation", row.Reason);
        Assert.Single(factory.Provider.Requests);
        using var cashier = await CreateTenantClientAsync(factory, "kasir", "kasir123");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync("/api/payments/attention")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(tenantId, "s2-owner-payment-triage");
            var rowToUpdate = await db.Payments.SingleAsync(x => x.Id == original.Id);
            rowToUpdate.Status = PaymentConstants.StatusCreating;
            rowToUpdate.ProviderPaymentRequestId = null;
            await db.SaveChangesAsync();
        }
        var unknown = (await owner.GetFromJsonAsync<PaymentAttentionResponseDto>(
            "/api/payments/attention"))!;
        Assert.Equal("provider_request_unknown", Assert.Single(unknown.Items).Reason);
        Assert.Single(factory.Provider.Requests);
    }

    [Fact]
    public async Task PaymentAttention_ForeignOutletDoesNotRevealOutstandingPayment()
    {
        await using var factory = new PaymentApiFactory();
        using var owner = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(owner);
        var another = await owner.PostAsJsonAsync("/api/outlets", new
        { code = "S2-PAY-OTHER", name = "Second outlet", address = "QA", phone = "", isDefault = false });
        Assert.Equal(HttpStatusCode.OK, another.StatusCode);
        var branch = (await another.Content.ReadFromJsonAsync<OutletDto>())!;
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        var other = (await owner.GetFromJsonAsync<PaymentAttentionResponseDto>(
            "/api/payments/attention"))!;
        Assert.Equal(branch.Id, other.OutletId);
        Assert.Equal(0, other.Total);
        Assert.Empty(other.Items);
        Assert.Single(factory.Provider.Requests);
        var oldPayment = await owner.GetAsync($"/api/payments/{payment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, oldPayment.StatusCode);
    }

    [Fact]
    public async Task CurrentPayment_RestoresPendingPaymentAndBlocksDuplicate()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);

        var current = await client.GetFromJsonAsync<PaymentStatusDto>(
            "/api/payments/current");
        var product = await GetProductAsync(client);
        var duplicate = await client.PostAsJsonAsync(
            "/api/payments/qris",
            CreateQrisRequest(product));

        Assert.NotNull(current);
        Assert.Equal(payment.Id, current.Id);
        Assert.Equal(PaymentConstants.StatusPending, current.Status);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(
            "PAYMENT_ALREADY_PENDING",
            await duplicate.Content.ReadAsStringAsync());
        Assert.Single(factory.Provider.Requests);
    }

    [Fact]
    public async Task DisplayExpiry_DoesNotPrematurelyCloseProviderPayment()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants
                .Where(x => x.Slug == "warung-lumpia-beef")
                .Select(x => x.Id)
                .SingleAsync();
            using var tenantScope = scope.ServiceProvider
                .GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(tenantId, "expire-payment-test");
            var entity = await db.Payments.SingleAsync(x => x.Id == payment.Id);
            entity.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        var status = await client.GetFromJsonAsync<PaymentStatusDto>(
            $"/api/payments/{payment.Id}");

        Assert.NotNull(status);
        Assert.Equal(PaymentConstants.StatusPending, status.Status);
        Assert.Null(status.FailureCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider
            .GetRequiredService<AppDbContext>();
        var verifyTenantId = await verifyDb.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();
        using var verifyTenantScope = verifyScope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(verifyTenantId, "verify-expired-payment");
        Assert.Equal(
            TransactionStatuses.PendingPayment,
            (await verifyDb.Transactions.SingleAsync(
                x => x.Id == payment.TransactionId)).Status);
    }

    [Fact]
    public async Task LateSuccessfulWebhook_AfterDisplayExpiry_FinalizesExactlyOnce()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.Where(x => x.Slug == "warung-lumpia-beef")
                .Select(x => x.Id).SingleAsync();
            using var tenantScope = scope.ServiceProvider
                .GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(tenantId, "late-webhook-test");
            (await db.Payments.SingleAsync(x => x.Id == payment.Id)).ExpiresAt =
                DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        using var webhookClient = factory.CreateClient();
        using var webhook = await SendWebhookAsync(webhookClient, CaptureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        using var duplicate = await SendWebhookAsync(webhookClient, CaptureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        var status = await client.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{payment.Id}");
        Assert.Equal(PaymentConstants.StatusPaid, status!.Status);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifyTenantId = await verifyDb.Tenants.Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id).SingleAsync();
        using var verifyTenantScope = verifyScope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(verifyTenantId, "verify-late-webhook");
        Assert.Single(await verifyDb.PaymentLedgerEntries
            .Where(x => x.PaymentId == payment.Id).ToListAsync());
    }

    [Fact]
    public async Task CancelPayment_CancelsProviderAndPreservesDraftForRecovery()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);

        var response = await client.PostAsync($"/api/payments/{payment.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<PaymentStatusDto>();
        Assert.Equal(PaymentConstants.StatusFailed, status!.Status);
        Assert.Equal("PAYMENT_REQUEST_CANCELED", status.FailureCode);
        Assert.Equal(payment.ProviderPaymentRequestId, factory.Provider.Cancelled.Single());

        var transaction = await client.GetFromJsonAsync<TransactionDto>(
            $"/api/transactions/{payment.TransactionId}");
        Assert.Equal(TransactionStatuses.Failed, transaction!.Status);
        Assert.Equal(PaymentConstants.StatusFailed, transaction.PaymentStatus);
        Assert.Equal("PAYMENT_REQUEST_CANCELED", transaction.PaymentFailureCode);
        Assert.NotEmpty(transaction.Items);

        var history = await client.GetFromJsonAsync<List<TransactionDto>>("/api/transactions");
        var listed = Assert.Single(history!, x => x.Id == payment.TransactionId);
        Assert.Equal(TransactionStatuses.Failed, listed.Status);
        Assert.Equal("PAYMENT_REQUEST_CANCELED", listed.PaymentFailureCode);

        var duplicate = await client.PostAsync($"/api/payments/{payment.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Single(factory.Provider.Cancelled);
    }

    [Fact]
    public async Task PaymentStatus_ReturnsPaidAfterValidWebhook()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);
        using var webhookClient = factory.CreateClient();

        var webhookResponse = await SendWebhookAsync(
            webhookClient,
            CaptureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var status = await client.GetFromJsonAsync<PaymentStatusDto>(
            $"/api/payments/{payment.Id}");

        Assert.NotNull(status);
        Assert.Equal(PaymentConstants.StatusPaid, status.Status);
    }

    [Fact]
    public async Task PaymentStatus_ReturnsFailedAfterFailureWebhook()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);
        using var webhookClient = factory.CreateClient();

        var webhookResponse = await SendWebhookAsync(
            webhookClient,
            FailureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var status = await client.GetFromJsonAsync<PaymentStatusDto>(
            $"/api/payments/{payment.Id}");

        Assert.NotNull(status);
        Assert.Equal(PaymentConstants.StatusFailed, status.Status);
    }

    [Fact]
    public async Task ProviderExpiry_IsStoredSafelyAndReturnedAsExpired()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(client);
        using var webhookClient = factory.CreateClient();
        using var response = await SendWebhookAsync(webhookClient, ExpiryWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await client.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{payment.Id}");
        Assert.Equal(PaymentConstants.StatusExpired, status!.Status);
        Assert.Equal("PAYMENT_REQUEST_EXPIRED", status.FailureCode);
    }

    [Fact]
    public async Task PaymentStatus_CrossTenantAccessReturnsNotFound()
    {
        await using var factory = new PaymentApiFactory();
        using var ownerClient = await CreateOwnerClientAsync(factory);
        var payment = await CreatePaymentAsync(ownerClient);
        var username = $"other-{Guid.NewGuid():N}";
        const string password = "other-owner-password";

        await SeedOtherTenantOwnerAsync(
            factory,
            username,
            password);
        using var otherTenantClient = await CreateTenantClientAsync(
            factory,
            username,
            password);

        var response = await otherTenantClient.GetAsync(
            $"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaymentStatus_UnknownPaymentReturnsNotFound()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);

        var response = await client.GetAsync(
            $"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SuccessfulWebhook_FinalizesExactlyOnce()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var productBefore = await GetProductAsync(client);
        var payment = await CreatePaymentAsync(client, productBefore);
        using var webhookClient = factory.CreateClient();

        var first = await SendWebhookAsync(
            webhookClient,
            CaptureWebhook(payment));
        var duplicate = await SendWebhookAsync(
            webhookClient,
            CaptureWebhook(payment));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>();
        var tenantId = await db.PaymentRoutes
            .Where(x => x.PaymentId == payment.Id)
            .Select(x => x.TenantId)
            .SingleAsync();

        using (tenantScope.Begin(tenantId, "verify-payment-success"))
        {
            var storedPayment = await db.Payments.SingleAsync();
            var transaction = await db.Transactions.SingleAsync(
                x => x.Id == payment.TransactionId);
            var product = await db.Products.SingleAsync(
                x => x.Id == productBefore.Id);

            Assert.Equal(PaymentConstants.StatusPaid, storedPayment.Status);
            Assert.Equal(TransactionStatuses.Paid, transaction.Status);
            Assert.Equal(transaction.Total, transaction.Dibayar);
            Assert.Equal(productBefore.Stok - 1, product.Stok);
            Assert.Single(await db.PaymentLedgerEntries.ToListAsync());
            Assert.Single(await db.PaymentWebhookEvents.ToListAsync());
            Assert.Single(await db.StockHistories
                .Where(x => x.Keterangan == $"Transaksi {transaction.NoTrx}")
                .ToListAsync());
        }
    }

    [Fact]
    public async Task FailedPayment_DoesNotFinalizeTransaction()
    {
        await using var factory = new PaymentApiFactory();
        using var client = await CreateOwnerClientAsync(factory);
        var productBefore = await GetProductAsync(client);
        var payment = await CreatePaymentAsync(client, productBefore);
        using var webhookClient = factory.CreateClient();

        var response = await SendWebhookAsync(
            webhookClient,
            FailureWebhook(payment));
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.PaymentRoutes
            .Where(x => x.PaymentId == payment.Id)
            .Select(x => x.TenantId)
            .SingleAsync();
        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "verify-payment-failure");

        Assert.Equal(
            PaymentConstants.StatusFailed,
            (await db.Payments.SingleAsync()).Status);
        Assert.Equal(
            TransactionStatuses.Failed,
            (await db.Transactions.SingleAsync(
                x => x.Id == payment.TransactionId)).Status);
        Assert.Equal(
            productBefore.Stok,
            (await db.Products.SingleAsync(
                x => x.Id == productBefore.Id)).Stok);
        Assert.Empty(await db.PaymentLedgerEntries.ToListAsync());
        Assert.Empty(await db.StockHistories
            .Where(x => x.Keterangan.StartsWith("Transaksi"))
            .ToListAsync());
    }

    [Fact]
    public async Task ProviderConfirmedExpiry_LeavesNoPendingAttempt_AndLateCaptureStillFinalizes()
    {
        await using var factory = new PaymentApiFactory();
        using var owner = await CreateOwnerClientAsync(factory);
        var before = await GetProductAsync(owner);
        var payment = await CreatePaymentAsync(owner, before);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.Where(x => x.Slug == "warung-lumpia-beef")
                .Select(x => x.Id).SingleAsync();
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(tenantId, "s2-provider-expiry");
            var entity = await db.Payments.SingleAsync(x => x.Id == payment.Id);
            entity.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        factory.Provider.ProviderStatus = "EXPIRED";
        var status = await owner.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{payment.Id}");
        Assert.Equal(PaymentConstants.StatusExpired, status!.Status);
        Assert.Equal("PAYMENT_REQUEST_EXPIRED", status.FailureCode);
        using var current = await owner.GetAsync("/api/payments/current");
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
        var failedSale = await owner.GetFromJsonAsync<TransactionDto>(
            $"/api/transactions/{payment.TransactionId}");
        Assert.Equal(TransactionStatuses.Failed, failedSale!.Status);
        using var webhook = factory.CreateClient();
        using var capture = await SendWebhookAsync(webhook, CaptureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);
        var paid = await owner.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{payment.Id}");
        Assert.Equal(PaymentConstants.StatusPaid, paid!.Status);
        var sale = await owner.GetFromJsonAsync<TransactionDto>(
            $"/api/transactions/{payment.TransactionId}");
        Assert.Equal(TransactionStatuses.Paid, sale!.Status);
        await using var verification = factory.Services.CreateAsyncScope();
        var verifyDb = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await verifyDb.PaymentRoutes.Where(x => x.PaymentId == payment.Id)
            .Select(x => x.TenantId).SingleAsync();
        using var tenantScope2 = verification.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenant, "s2-provider-expiry-final");
        Assert.Single(await verifyDb.PaymentLedgerEntries.Where(x => x.PaymentId == payment.Id).ToListAsync());
        Assert.Equal(before.Stok - 1,
            (await verifyDb.Products.SingleAsync(x => x.Id == before.Id)).Stok);
    }

    [Fact]
    public async Task DelayedFailureWebhook_AfterPaid_DoesNotDowngradeSaleOrLedger()
    {
        await using var factory = new PaymentApiFactory();
        using var owner = await CreateOwnerClientAsync(factory);
        var before = await GetProductAsync(owner);
        var payment = await CreatePaymentAsync(owner, before);
        using var webhookClient = factory.CreateClient();
        using var capture = await SendWebhookAsync(webhookClient, CaptureWebhook(payment));
        using var lateFailure = await SendWebhookAsync(webhookClient, FailureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);
        Assert.Equal(HttpStatusCode.OK, lateFailure.StatusCode);
        var status = await owner.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{payment.Id}");
        Assert.Equal(PaymentConstants.StatusPaid, status!.Status);
        var sale = await owner.GetFromJsonAsync<TransactionDto>($"/api/transactions/{payment.TransactionId}");
        Assert.Equal(TransactionStatuses.Paid, sale!.Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.PaymentRoutes.Where(x => x.PaymentId == payment.Id)
            .Select(x => x.TenantId).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-paid-failure-order-check");
        Assert.Single(await db.PaymentLedgerEntries.Where(x => x.PaymentId == payment.Id).ToListAsync());
        var events = await db.PaymentWebhookEvents.Where(x => x.PaymentId == payment.Id).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Contains(events, x => x.ProcessingStatus == "ignored_paid_terminal");
        var product = await db.Products.SingleAsync(x => x.Id == before.Id);
        Assert.Equal(before.Stok - 1, product.Stok);
        Assert.Single(await db.StockHistories.Where(x => x.Tipe == "transaksi").ToListAsync());
    }

    [Fact]
    public async Task SuccessAfterEarlierFailure_RecordsProviderPaidOnce()
    {
        await using var factory = new PaymentApiFactory();
        using var owner = await CreateOwnerClientAsync(factory);
        var before = await GetProductAsync(owner);
        var payment = await CreatePaymentAsync(owner, before);
        using var webhookClient = factory.CreateClient();
        using var failure = await SendWebhookAsync(webhookClient, FailureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, failure.StatusCode);
        using var capture = await SendWebhookAsync(webhookClient, CaptureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);
        using var duplicate = await SendWebhookAsync(webhookClient, CaptureWebhook(payment));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var status = await owner.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{payment.Id}");
        Assert.Equal(PaymentConstants.StatusPaid, status!.Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.PaymentRoutes.Where(x => x.PaymentId == payment.Id)
            .Select(x => x.TenantId).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-failure-paid-order-check");
        Assert.Single(await db.PaymentLedgerEntries.Where(x => x.PaymentId == payment.Id).ToListAsync());
        Assert.Equal(2, await db.PaymentWebhookEvents.CountAsync(x => x.PaymentId == payment.Id));
        Assert.Equal(before.Stok - 1, (await db.Products.SingleAsync(x => x.Id == before.Id)).Stok);
    }

    [Fact]
    public async Task AmbiguousCreateReply_KeepsOriginalAttemptAndRecoversFromVerifiedWebhook()
    {
        await using var factory = new PaymentApiFactory();
        factory.Provider.ThrowAfterAccept = true;
        using var owner = await CreateOwnerClientAsync(factory);
        var product = await GetProductAsync(owner);
        var attempt = await owner.PostAsJsonAsync("/api/payments/qris", CreateQrisRequest(product));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, attempt.StatusCode);
        Assert.Contains("PAYMENT_CREATION_UNCERTAIN", await attempt.Content.ReadAsStringAsync());
        var current = await owner.GetFromJsonAsync<PaymentStatusDto>("/api/payments/current");
        Assert.NotNull(current);
        Assert.Equal(PaymentConstants.StatusCreating, current.Status);
        Assert.Equal(string.Empty, current.ProviderPaymentRequestId);
        var secondCharge = await owner.PostAsJsonAsync("/api/payments/qris", CreateQrisRequest(product));
        Assert.Equal(HttpStatusCode.Conflict, secondCharge.StatusCode);
        Assert.Single(factory.Provider.Requests);

        using var webhook = factory.CreateClient();
        var requestId = "pr-" + current.ProviderReferenceId;
        using var invalidAmount = await SendWebhookAsync(webhook, new
        {
            @event = "payment.capture", business_id = "xendit-sandbox-business",
            created = DateTime.UtcNow,
            data = new
            {
                payment_id = $"py-{current.Id:N}", payment_request_id = requestId,
                reference_id = current.ProviderReferenceId,
                request_amount = current.Amount + 1m, status = "SUCCEEDED",
                channel_code = "QRIS", currency = "IDR"
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, invalidAmount.StatusCode);
        var stillCreating = await owner.GetFromJsonAsync<PaymentStatusDto>(
            $"/api/payments/{current.Id}");
        Assert.Equal(PaymentConstants.StatusCreating, stillCreating!.Status);
        var callback = new
        {
            @event = "payment.capture",
            business_id = "xendit-sandbox-business",
            created = DateTime.UtcNow,
            data = new
            {
                payment_id = $"py-{current.Id:N}", payment_request_id = requestId,
                reference_id = current.ProviderReferenceId,
                request_amount = current.Amount, status = "SUCCEEDED",
                channel_code = "QRIS", currency = "IDR"
            }
        };
        using var capture = await SendWebhookAsync(webhook, callback);
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);
        using var replay = await SendWebhookAsync(webhook, callback);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var paid = await owner.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{current.Id}");
        Assert.Equal(PaymentConstants.StatusPaid, paid!.Status);
        Assert.Equal(requestId, paid.ProviderPaymentRequestId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.GetAsync("/api/payments/current")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-lost-reply-webhook-recovery");
        Assert.Single(await db.PaymentRoutes.Where(x => x.PaymentId == current.Id).ToListAsync());
        Assert.Single(await db.PaymentLedgerEntries.Where(x => x.PaymentId == current.Id).ToListAsync());
        Assert.Single(await db.StockHistories.Where(x => x.Tipe == "transaksi").ToListAsync());
        Assert.Single(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task WebhookBeforeProviderCreateResponse_NeverRevertsPaidToPending()
    {
        await using var factory = new PaymentApiFactory();
        using var owner = await CreateOwnerClientAsync(factory);
        var product = await GetProductAsync(owner);
        factory.Provider.CallbackBeforeCreateResponse = async (referenceId, amount) =>
        {
            using var hook = factory.CreateClient();
            var paymentId = Guid.ParseExact(referenceId[3..], "N");
            using var callback = await SendWebhookAsync(hook, new
            {
                @event = "payment.capture", business_id = "xendit-sandbox-business",
                created = DateTime.UtcNow,
                data = new
                {
                    payment_id = $"py-{paymentId:N}", payment_request_id = "pr-" + referenceId,
                    reference_id = referenceId, request_amount = amount,
                    status = "SUCCEEDED", channel_code = "QRIS", currency = "IDR"
                }
            });
            Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        };
        var response = await owner.PostAsJsonAsync("/api/payments/qris", CreateQrisRequest(product));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<QrisPaymentDto>())!;
        Assert.Equal(PaymentConstants.StatusPaid, created.Status);
        var paid = await owner.GetFromJsonAsync<PaymentStatusDto>($"/api/payments/{created.Id}");
        Assert.Equal(PaymentConstants.StatusPaid, paid!.Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-webhook-before-response");
        Assert.Single(await db.PaymentRoutes.Where(x => x.PaymentId == created.Id).ToListAsync());
        Assert.Single(await db.PaymentLedgerEntries.Where(x => x.PaymentId == created.Id).ToListAsync());
        Assert.Single(await db.StockHistories.Where(x => x.Tipe == "transaksi").ToListAsync());
    }

    [Fact]
    public async Task PaymentAndLedgerQueries_RemainTenantIsolated()
    {
        var databaseName = $"payment-isolation-{Guid.NewGuid():N}";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedPaymentDataAsync(databaseName, tenantA, tenantB);

        await using var db = CreateTenantDb(databaseName, tenantA);
        Assert.Single(await db.Payments.ToListAsync());
        Assert.Single(await db.PaymentLedgerEntries.ToListAsync());
        Assert.All(await db.Payments.ToListAsync(), x =>
            Assert.Equal(tenantA, x.TenantId));
        Assert.All(await db.PaymentLedgerEntries.ToListAsync(), x =>
            Assert.Equal(tenantA, x.TenantId));
    }

    [Fact]
    public async Task ExistingCashTransaction_RemainsImmediateAndHasNoPayment()
    {
        await using var factory = new PaymentApiFactory(
            paymentMode: "Disabled");
        using var client = await CreateOwnerClientAsync(factory);
        var productBefore = await GetProductAsync(client);
        var request = CreateQrisRequest(productBefore);
        request.MetodePembayaran = "Tunai";
        request.Dibayar = productBefore.HargaJual;

        var response = await client.PostAsJsonAsync(
            "/api/transactions",
            request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var productAfter = await GetProductAsync(client, productBefore.Id);
        Assert.Equal(productBefore.Stok - 1, productAfter.Stok);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.PaymentRoutes.ToListAsync());
    }

    private static async Task<QrisPaymentDto> CreatePaymentAsync(
        HttpClient client,
        ProductDto? product = null)
    {
        product ??= await GetProductAsync(client);
        var response = await client.PostAsJsonAsync(
            "/api/payments/qris",
            CreateQrisRequest(product));
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        return (await response.Content
            .ReadFromJsonAsync<QrisPaymentDto>())!;
    }

    private static async Task<HttpResponseMessage> SendWebhookAsync(
        HttpClient client,
        object webhook)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/webhooks/xendit/payments");
        request.Headers.Add("x-callback-token", "xendit-test-callback-token");
        request.Content = JsonContent.Create(webhook);
        return await client.SendAsync(request);
    }

    private static object CaptureWebhook(QrisPaymentDto payment) => new
    {
        @event = "payment.capture",
        business_id = "xendit-sandbox-business",
        created = DateTime.UtcNow,
        data = new
        {
            payment_id = $"py-{payment.Id:N}",
            payment_request_id = payment.ProviderPaymentRequestId,
            reference_id = $"nf-{payment.Id:N}",
            request_amount = payment.Amount,
            status = "SUCCEEDED",
            channel_code = "QRIS",
            currency = "IDR"
        }
    };

    private static object FailureWebhook(QrisPaymentDto payment) => new
    {
        @event = "payment.failure",
        business_id = "xendit-sandbox-business",
        created = DateTime.UtcNow,
        data = new
        {
            payment_id = $"py-{payment.Id:N}",
            payment_request_id = payment.ProviderPaymentRequestId,
            reference_id = $"nf-{payment.Id:N}",
            request_amount = payment.Amount,
            status = "FAILED",
            channel_code = "QRIS",
            currency = "IDR",
            failure_code = "PAYMENT_FAILED"
        }
    };

    private static object ExpiryWebhook(QrisPaymentDto payment) => new
    {
        @event = "payment.failure",
        business_id = "xendit-sandbox-business",
        created = DateTime.UtcNow,
        data = new
        {
            payment_id = $"py-{payment.Id:N}",
            payment_request_id = payment.ProviderPaymentRequestId,
            reference_id = $"nf-{payment.Id:N}",
            request_amount = payment.Amount,
            status = "FAILED",
            channel_code = "QRIS",
            currency = "IDR",
            failure_code = "PAYMENT_REQUEST_EXPIRED"
        }
    };

    private static CreateTransactionDto CreateQrisRequest(
        ProductDto product,
        decimal? totalOverride = null)
    {
        var total = totalOverride ?? product.HargaJual;
        return new CreateTransactionDto
        {
            CustomerId = null,
            Items = new List<CreateTransactionItemDto>
            {
                new CreateTransactionItemDto
                {
                    Id = product.Id,
                    Nama = product.Nama,
                    HargaJual = product.HargaJual,
                    Qty = 1,
                    Subtotal = product.HargaJual
                }
            },
            Subtotal = product.HargaJual,
            Disc = 0m,
            Tax = 0m,
            DiscAmt = 0m,
            TaxAmt = 0m,
            Total = total,
            MetodePembayaran = "QRIS",
            Dibayar = 0m,
            Kembalian = 0m
        };
    }

    private static async Task<HttpClient> CreateOwnerClientAsync(
        PaymentApiFactory factory)
    {
        return await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
    }

    private static async Task<HttpClient> CreateTenantClientAsync(
        PaymentApiFactory factory,
        string username,
        string password)
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private static async Task SeedOtherTenantOwnerAsync(
        PaymentApiFactory factory,
        string username,
        string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>();
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            NamaToko = "Other Tenant",
            Slug = $"other-{tenantId:N}",
            Status = "active"
        });

        using (tenantScope.Begin(tenantId, "seed-other-payment-tenant"))
        {
            db.Users.Add(new User
            {
                TenantId = tenantId,
                Nama = "Other Owner",
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "owner",
                Active = true
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<ProductDto> GetProductAsync(
        HttpClient client,
        Guid? productId = null)
    {
        if (productId.HasValue)
        {
            return (await client.GetFromJsonAsync<ProductDto>(
                $"/api/products/{productId}"))!;
        }

        return (await client.GetFromJsonAsync<List<ProductDto>>(
            "/api/products"))!.First();
    }

    private static async Task SeedPaymentDataAsync(
        string databaseName,
        Guid tenantA,
        Guid tenantB)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var context = CreateContext(null);

        foreach (var tenantId in new[] { tenantA, tenantB })
        {
            using var scope = context.Begin(tenantId, "seed-payment-isolation");
            await using var db = new AppDbContext(options, context);
            var transaction = new NeverfadePos.Api.Entities.Transaction
            {
                TenantId = tenantId,
                NoTrx = $"TRX-{tenantId:N}",
                Status = TransactionStatuses.Paid
            };
            var payment = new Payment
            {
                TenantId = tenantId,
                TransactionId = transaction.Id,
                ProviderReferenceId = $"nf-{Guid.NewGuid():N}",
                ProviderPaymentRequestId = $"pr-{Guid.NewGuid():N}",
                ProviderPaymentId = $"py-{Guid.NewGuid():N}",
                Amount = 1000m,
                Status = PaymentConstants.StatusPaid
            };
            db.AddRange(
                transaction,
                payment,
                new PaymentLedgerEntry
                {
                    TenantId = tenantId,
                    PaymentId = payment.Id,
                    TransactionId = transaction.Id,
                    EntryType = PaymentConstants.LedgerPaymentCredit,
                    Amount = payment.Amount,
                    ProviderReference = payment.ProviderPaymentId
                });
            await db.SaveChangesAsync();
        }
    }

    private static AppDbContext CreateTenantDb(
        string databaseName,
        Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options, CreateContext(tenantId));
    }

    private static TenantExecutionContext CreateContext(Guid? tenantId)
    {
        var http = new DefaultHttpContext();
        if (tenantId.HasValue)
        {
            http.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[]
                    {
                        new System.Security.Claims.Claim(
                            "tenant_id",
                            tenantId.Value.ToString())
                    },
                    "test"));
        }

        return new TenantExecutionContext(
            new CurrentUser(new HttpContextAccessor
            {
                HttpContext = http
            }));
    }

    private sealed class PaymentApiFactory(
        string paymentMode = "Live",
        bool useAllowedSandboxGate = false)
        : WebApplicationFactory<Program>
    {
        private readonly string _databaseName =
            $"xendit-foundation-{Guid.NewGuid():N}";

        public FakeXenditProvider Provider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["Jwt:Key"] =
                    "tenant-payment-test-key-that-is-at-least-32-characters",
                ["Jwt:Issuer"] = "NeverfadePos.Payment.Test",
                ["Jwt:Audience"] = "NeverfadePos.Payment.Test.Client",
                ["PlatformJwt:Key"] =
                    "platform-payment-test-key-that-is-at-least-32-characters",
                ["PlatformJwt:Issuer"] = "NeverfadePos.Platform.Payment.Test",
                ["PlatformJwt:Audience"] =
                    "NeverfadePos.Platform.Payment.Test.Client",
                ["PlatformBootstrap:Enabled"] = "false",
                ["Payments:Mode"] = paymentMode,
                ["Payments:LiveEnabled"] = "true",
                ["Xendit:SecretApiKey"] = "xnd_production_test_key",
                ["Xendit:WebhookCallbackToken"] =
                    "xendit-test-callback-token"
            };

            foreach (var item in config)
            {
                builder.UseSetting(item.Key, item.Value);
            }

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(config));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<IXenditPaymentProvider>();

                if (useAllowedSandboxGate)
                {
                    services.RemoveAll<IPaymentModeGate>();
                    services.AddSingleton<IPaymentModeGate>(
                        new AllowedSandboxPaymentModeGate());
                }
                else if (string.Equals(
                    paymentMode,
                    "Live",
                    StringComparison.OrdinalIgnoreCase))
                {
                    services.RemoveAll<IPaymentModeGate>();
                    services.AddSingleton<IPaymentModeGate>(
                        new AllowedLivePaymentModeGate());
                }

                services.AddDbContext<AppDbContext>(options =>
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(
                            InMemoryEventId.TransactionIgnoredWarning)));
                services.AddSingleton<IXenditPaymentProvider>(Provider);
            });
        }
    }

    private sealed class AllowedLivePaymentModeGate
        : IPaymentModeGate
    {
        public PaymentCapabilitiesDto GetCapabilities(Guid tenantId) =>
            new()
            {
                QrisEnabled = true,
                Mode = "live",
                IsSandbox = false
            };

        public void EnsureQrisAllowed(Guid tenantId)
        {
        }
    }

    private sealed class AllowedSandboxPaymentModeGate
        : IPaymentModeGate
    {
        public PaymentCapabilitiesDto GetCapabilities(Guid tenantId) =>
            new()
            {
                QrisEnabled = true,
                Mode = "sandbox",
                IsSandbox = true
            };

        public void EnsureQrisAllowed(Guid tenantId)
        {
        }
    }

    private sealed class FakeXenditProvider : IXenditPaymentProvider
    {
        public List<(string ReferenceId, decimal Amount)> Requests { get; } =
            new();

        public decimal? LastAmount => Requests.LastOrDefault().Amount;
        public List<string> Cancelled { get; } = new();
        public string ProviderStatus { get; set; } = "UNKNOWN";
        public bool ThrowAfterAccept { get; set; }
        public Func<string, decimal, Task>? CallbackBeforeCreateResponse { get; set; }

        public Task<string> GetPaymentRequestStatusAsync(
            string paymentRequestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProviderStatus);


        public async Task<XenditPaymentRequestResult> CreateQrisAsync(
            string referenceId,
            decimal amount,
            string description,
            DateTime expiresAt,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((referenceId, amount));
            if (CallbackBeforeCreateResponse is not null)
                await CallbackBeforeCreateResponse(referenceId, amount);
            if (ThrowAfterAccept)
                throw new HttpRequestException("Provider accepted request but reply timed out.");
            return new XenditPaymentRequestResult(
                $"pr-{referenceId}",
                referenceId,
                amount,
                "REQUIRES_ACTION",
                "000201010212TEST-QRIS",
                expiresAt);
        }

        public Task CancelPaymentRequestAsync(
            string paymentRequestId,
            CancellationToken cancellationToken = default)
        {
            Cancelled.Add(paymentRequestId);
            return Task.CompletedTask;
        }
    }
}
