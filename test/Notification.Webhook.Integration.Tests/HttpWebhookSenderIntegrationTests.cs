using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Notification.Webhook.Domain.Entities;
using Notification.Webhook.Domain.Enums;
using Notification.Webhook.Domain.ValueObjects;
using Notification.Webhook.Infrastructure.Configuration;
using Notification.Webhook.Infrastructure.Providers;
using Notification.Webhook.Integration.Tests.Fixtures;

namespace Notification.Webhook.Integration.Tests;

public class HttpWebhookSenderIntegrationTests : IDisposable
{
    private readonly TestWebhookServer _server;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<HttpWebhookSender>> _mockLogger;
    private readonly IOptions<WebhookOptions> _options;
    private readonly HttpWebhookSender _sender;

    public HttpWebhookSenderIntegrationTests()
    {
        _server = new TestWebhookServer();
        _httpClient = new HttpClient();
        _mockLogger = new Mock<ILogger<HttpWebhookSender>>();
        _options = Options.Create(new WebhookOptions
        {
            DefaultTimeoutSeconds = 30,
            DefaultMaxRetries = 3,
            RetryDelayMilliseconds = 100
        });
        _sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
    }

    [Fact]
    public async Task SendAsync_ToRealServer_ShouldDeliverWebhook()
    {
        var notification = CreateNotification(_server.BaseUrl + "webhook");

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Equal("POST", received.Method);
        Assert.Contains("event", received.Body);
    }

    [Fact]
    public async Task SendAsync_WithCustomHeaders_ShouldIncludeHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer test-token-123" },
            { "X-Custom-Header", "custom-value" }
        };
        var notification = CreateNotification(_server.BaseUrl + "webhook", headers: headers);

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Equal("Bearer test-token-123", received.Headers["Authorization"]);
        Assert.Equal("custom-value", received.Headers["X-Custom-Header"]);
    }

    [Fact]
    public async Task SendAsync_WithSecret_ShouldIncludeValidSignature()
    {
        var secret = "my-webhook-secret";
        var payload = "{\"event\":\"order.created\",\"data\":{\"id\":123}}";
        var notification = CreateNotification(_server.BaseUrl + "webhook", payload: payload, secret: secret);

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.True(received.Headers.ContainsKey("X-Webhook-Signature"));

        var signature = received.Headers["X-Webhook-Signature"];
        Assert.StartsWith("sha256=", signature);

        // Verify signature is correct
        var expectedSignature = ComputeHmacSignature(payload, secret);
        Assert.Equal($"sha256={expectedSignature}", signature);
    }

    [Fact]
    public async Task SendAsync_WithNotificationId_ShouldIncludeIdHeader()
    {
        var notificationId = Guid.NewGuid();
        var notification = CreateNotification(_server.BaseUrl + "webhook", id: notificationId);

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.True(received.Headers.ContainsKey("X-Notification-Id"));
        Assert.Equal(notificationId.ToString(), received.Headers["X-Notification-Id"]);
    }

    [Theory]
    [InlineData(WebhookHttpMethod.POST, "POST")]
    [InlineData(WebhookHttpMethod.PUT, "PUT")]
    [InlineData(WebhookHttpMethod.PATCH, "PATCH")]
    public async Task SendAsync_WithDifferentMethods_ShouldUseCorrectMethod(WebhookHttpMethod method, string expectedMethod)
    {
        _server.ClearReceivedWebhooks();
        var notification = CreateNotification(_server.BaseUrl + "webhook", method: method);

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Equal(expectedMethod, received.Method);
    }

    [Fact]
    public async Task SendAsync_WhenServerReturnsError_ShouldReturnFailure()
    {
        _server.ResponseHandler = _ => (HttpStatusCode.InternalServerError, "{\"error\":\"Server Error\"}");

        var notification = CreateNotification(_server.BaseUrl + "webhook", maxRetries: 0);

        var result = await _sender.SendAsync(notification);

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WhenServerReturnsBadRequest_ShouldReturnFailure()
    {
        _server.ResponseHandler = _ => (HttpStatusCode.BadRequest, "{\"error\":\"Invalid payload\"}");

        var notification = CreateNotification(_server.BaseUrl + "webhook", maxRetries: 0);

        var result = await _sender.SendAsync(notification);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithJsonPayload_ShouldSetCorrectContentType()
    {
        var notification = CreateNotification(
            _server.BaseUrl + "webhook",
            payload: "{\"event\":\"test\"}",
            contentType: "application/json");

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Contains("application/json", received.ContentType);
    }

    [Fact]
    public async Task SendAsync_WithXmlPayload_ShouldSetCorrectContentType()
    {
        var notification = CreateNotification(
            _server.BaseUrl + "webhook",
            payload: "<event><type>test</type></event>",
            contentType: "application/xml");

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Contains("application/xml", received.ContentType);
    }

    [Fact]
    public async Task SendAsync_ShouldMeasureDuration()
    {
        var notification = CreateNotification(_server.BaseUrl + "webhook");

        var result = await _sender.SendAsync(notification);

        Assert.True(result.Success);
        Assert.NotNull(result.Duration);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    private static WebhookNotification CreateNotification(
        string url,
        Guid? id = null,
        WebhookHttpMethod method = WebhookHttpMethod.POST,
        string payload = "{\"event\":\"test\"}",
        string contentType = "application/json",
        Dictionary<string, string>? headers = null,
        string? secret = null,
        int maxRetries = 0)
    {
        return WebhookNotification.Create(
            id: id ?? Guid.NewGuid(),
            url: WebhookUrl.Create(url),
            method: method,
            payload: WebhookPayload.Create(payload, contentType),
            headers: headers,
            secret: secret,
            maxRetries: maxRetries,
            timeoutSeconds: 30);
    }

    private static string ComputeHmacSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _server.Dispose();
    }
}
