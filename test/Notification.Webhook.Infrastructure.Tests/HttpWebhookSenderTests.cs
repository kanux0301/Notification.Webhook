using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Notification.Webhook.Domain.Entities;
using Notification.Webhook.Domain.Enums;
using Notification.Webhook.Domain.ValueObjects;
using Notification.Webhook.Infrastructure.Configuration;
using Notification.Webhook.Infrastructure.Providers;

namespace Notification.Webhook.Infrastructure.Tests;

public class HttpWebhookSenderTests
{
    private readonly Mock<ILogger<HttpWebhookSender>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly IOptions<WebhookOptions> _options;

    public HttpWebhookSenderTests()
    {
        _mockLogger = new Mock<ILogger<HttpWebhookSender>>();
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object);
        _options = Options.Create(new WebhookOptions
        {
            DefaultTimeoutSeconds = 30,
            DefaultMaxRetries = 3,
            RetryDelayMilliseconds = 100
        });
    }

    [Fact]
    public async Task SendAsync_WithSuccessResponse_ShouldReturnSuccess()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.OK, "{\"status\":\"received\"}");

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        var result = await sender.SendAsync(notification);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("{\"status\":\"received\"}", result.ResponseBody);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task SendAsync_WithServerError_ShouldReturnFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification(maxRetries: 0);

        // Act
        var result = await sender.SendAsync(notification);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithCreatedResponse_ShouldReturnSuccess()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.Created, "{\"id\":123}");

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        var result = await sender.SendAsync(notification);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithNoContentResponse_ShouldReturnSuccess()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.NoContent, "");

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        var result = await sender.SendAsync(notification);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(204, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithBadRequest_ShouldReturnFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.BadRequest, "{\"error\":\"invalid payload\"}");

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification(maxRetries: 0);

        // Act
        var result = await sender.SendAsync(notification);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithCustomHeaders_ShouldIncludeHeaders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK")
            });

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer token123" },
            { "X-Custom-Header", "custom-value" }
        };
        var notification = CreateTestNotification(headers: headers);

        // Act
        await sender.SendAsync(notification);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Authorization"));
        Assert.True(capturedRequest.Headers.Contains("X-Custom-Header"));
        Assert.Equal("Bearer token123", capturedRequest.Headers.GetValues("Authorization").First());
    }

    [Fact]
    public async Task SendAsync_WithSecret_ShouldIncludeSignature()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK")
            });

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification(secret: "my-webhook-secret");

        // Act
        await sender.SendAsync(notification);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-Webhook-Signature"));
        var signature = capturedRequest.Headers.GetValues("X-Webhook-Signature").First();
        Assert.StartsWith("sha256=", signature);
    }

    [Fact]
    public async Task SendAsync_ShouldIncludeNotificationId()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK")
            });

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        await sender.SendAsync(notification);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-Notification-Id"));
    }

    [Theory]
    [InlineData(WebhookHttpMethod.POST)]
    [InlineData(WebhookHttpMethod.PUT)]
    [InlineData(WebhookHttpMethod.PATCH)]
    public async Task SendAsync_WithDifferentMethods_ShouldUseCorrectMethod(WebhookHttpMethod method)
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK")
            });

        var sender = new HttpWebhookSender(_httpClient, _options, _mockLogger.Object);
        var notification = CreateTestNotification(method: method);

        // Act
        await sender.SendAsync(notification);

        // Assert
        Assert.NotNull(capturedRequest);
        var expectedMethod = method switch
        {
            WebhookHttpMethod.POST => HttpMethod.Post,
            WebhookHttpMethod.PUT => HttpMethod.Put,
            WebhookHttpMethod.PATCH => HttpMethod.Patch,
            _ => HttpMethod.Post
        };
        Assert.Equal(expectedMethod, capturedRequest.Method);
    }

    private void SetupMockResponse(HttpStatusCode statusCode, string content)
    {
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }

    private static WebhookNotification CreateTestNotification(
        WebhookHttpMethod method = WebhookHttpMethod.POST,
        Dictionary<string, string>? headers = null,
        string? secret = null,
        int maxRetries = 3)
    {
        return WebhookNotification.Create(
            id: Guid.NewGuid(),
            url: WebhookUrl.Create("https://api.example.com/webhook"),
            method: method,
            payload: WebhookPayload.Create("{\"event\":\"test\"}"),
            headers: headers,
            secret: secret,
            maxRetries: maxRetries,
            timeoutSeconds: 30);
    }
}
