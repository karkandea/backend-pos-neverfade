using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NeverfadePos.Api.Services.WhatsApp;

public sealed class WahaClient(
    HttpClient httpClient,
    IOptions<WahaOptions> options)
    : IWahaClient
{
    private readonly WahaOptions _options = options.Value;

    public async Task<WahaSessionInfo?> GetSessionAsync(
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"api/sessions/{Uri.EscapeDataString(sessionName)}");

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadSessionAsync(response, cancellationToken);
    }

    public async Task<WahaSessionInfo> EnsureSessionAsync(
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        var current = await GetSessionAsync(
            sessionName,
            cancellationToken);

        if (current is null)
        {
            using var create = CreateRequest(
                HttpMethod.Post,
                "api/sessions");
            create.Content = JsonContent.Create(new
            {
                name = sessionName,
                start = true
            });

            using var createResponse = await httpClient.SendAsync(
                create,
                cancellationToken);
            await EnsureSuccessAsync(createResponse, cancellationToken);

            return await ReadSessionAsync(
                createResponse,
                cancellationToken);
        }

        if (string.Equals(
                current.Status,
                "STOPPED",
                StringComparison.OrdinalIgnoreCase))
        {
            using var start = CreateRequest(
                HttpMethod.Post,
                $"api/sessions/{Uri.EscapeDataString(sessionName)}/start");
            start.Content = JsonContent.Create(new { });

            using var startResponse = await httpClient.SendAsync(
                start,
                cancellationToken);
            await EnsureSuccessAsync(startResponse, cancellationToken);

            return await GetSessionAsync(sessionName, cancellationToken)
                ?? current;
        }

        if (string.Equals(
                current.Status,
                "FAILED",
                StringComparison.OrdinalIgnoreCase))
        {
            using var restart = CreateRequest(
                HttpMethod.Post,
                $"api/sessions/{Uri.EscapeDataString(sessionName)}/restart");
            restart.Content = JsonContent.Create(new { });

            using var restartResponse = await httpClient.SendAsync(
                restart,
                cancellationToken);
            await EnsureSuccessAsync(restartResponse, cancellationToken);

            return await GetSessionAsync(sessionName, cancellationToken)
                ?? current;
        }

        return current;
    }

    public async Task<WahaQrCode> GetQrAsync(
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"api/{Uri.EscapeDataString(sessionName)}/auth/qr?format=image");
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<QrPayload>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "WAHA mengembalikan QR kosong.");

        if (string.IsNullOrWhiteSpace(payload.Data))
        {
            throw new InvalidOperationException(
                "WAHA mengembalikan QR kosong.");
        }

        return new WahaQrCode(
            string.IsNullOrWhiteSpace(payload.Mimetype)
                ? "image/png"
                : payload.Mimetype,
            payload.Data);
    }

    public async Task SendTextAsync(
        string sessionName,
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            "api/sendText");
        request.Content = JsonContent.Create(new
        {
            session = sessionName,
            chatId = $"{phoneNumber}@c.us",
            text,
            linkPreview = false
        });

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task LogoutAsync(
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"api/sessions/{Uri.EscapeDataString(sessionName)}/logout");
        request.Content = JsonContent.Create(new { });

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativeUrl)
    {
        if (httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException(
                "WAHA belum dikonfigurasi.");
        }

        var request = new HttpRequestMessage(method, relativeUrl);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(
                "X-Api-Key",
                _options.ApiKey);
        }

        return request;
    }

    private static async Task<WahaSessionInfo> ReadSessionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var root = document.RootElement;
        var name = root.TryGetProperty("name", out var nameValue)
            ? nameValue.GetString() ?? string.Empty
            : string.Empty;
        var status = root.TryGetProperty("status", out var statusValue)
            ? statusValue.GetString() ?? "UNKNOWN"
            : "UNKNOWN";

        string? phone = null;
        string? pushName = null;

        if (root.TryGetProperty("me", out var me) &&
            me.ValueKind == JsonValueKind.Object)
        {
            if (me.TryGetProperty("id", out var idValue))
            {
                phone = idValue.GetString()?.Split('@')[0];
            }

            if (me.TryGetProperty("pushName", out var pushNameValue))
            {
                pushName = pushNameValue.GetString();
            }
        }

        return new WahaSessionInfo(
            name,
            status,
            phone,
            pushName);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(
            cancellationToken);

        throw new InvalidOperationException(
            $"WAHA error {(int)response.StatusCode}: {detail}");
    }

    private sealed class QrPayload
    {
        public string Mimetype { get; set; } = "image/png";
        public string Data { get; set; } = string.Empty;
    }
}
