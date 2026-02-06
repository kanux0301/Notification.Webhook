using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Notification.Webhook.Application.Commands;
using Notification.Webhook.Application.Common.Messaging;
using Notification.Webhook.Infrastructure.Configuration;
using Notification.Webhook.Infrastructure.Providers;
using Notification.Webhook.Integration.Tests.Fixtures;

namespace Notification.Webhook.Integration.Tests;

public class WebhookPipelineIntegrationTests : IDisposable
{
    private readonly TestWebhookServer _server;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<HttpWebhookSender>> _mockSenderLogger;
    private readonly Mock<ILogger<SendWebhookCommand>> _mockCommandLogger;
    private readonly IOptions<WebhookOptions> _options;

    public WebhookPipelineIntegrationTests()
    {
        _server = new TestWebhookServer();
        _httpClient = new HttpClient();
        _mockSenderLogger = new Mock<ILogger<HttpWebhookSender>>();
        _mockCommandLogger = new Mock<ILogger<SendWebhookCommand>>();
        _options = Options.Create(new WebhookOptions
        {
            DefaultTimeoutSeconds = 30,
            DefaultMaxRetries = 3,
            RetryDelayMilliseconds = 100
        });
    }

    [Fact]
    public async Task FullPipeline_FromMessageToDelivery_ShouldSucceed()
    {
        // Arrange - Create the full pipeline
        var sender = new HttpWebhookSender(_httpClient, _options, _mockSenderLogger.Object);
        var command = new SendWebhookCommand(sender, _mockCommandLogger.Object);

        var message = new SendWebhookMessage(
            NotificationId: Guid.NewGuid(),
            WebhookUrl: _server.BaseUrl + "webhook/order",
            HttpMethod: "POST",
            Payload: "{\"event\":\"order.created\",\"order_id\":12345}",
            ContentType: "application/json",
            Headers: new Dictionary<string, string>
            {
                { "X-API-Key", "test-api-key" }
            },
            Secret: "webhook-secret",
            MaxRetries: 0,
            TimeoutSeconds: 30);

        // Act
        var result = await command.ExecuteAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Equal("POST", received.Method);
        Assert.Equal("/webhook/order", received.Path);
        Assert.Contains("order.created", received.Body);
        Assert.Contains("12345", received.Body);
        Assert.Equal("test-api-key", received.Headers["X-API-Key"]);
        Assert.True(received.Headers.ContainsKey("X-Webhook-Signature"));
        Assert.True(received.Headers.ContainsKey("X-Notification-Id"));
    }

    [Fact]
    public async Task FullPipeline_WithPutMethod_ShouldSucceed()
    {
        // Arrange
        var sender = new HttpWebhookSender(_httpClient, _options, _mockSenderLogger.Object);
        var command = new SendWebhookCommand(sender, _mockCommandLogger.Object);

        var message = new SendWebhookMessage(
            NotificationId: Guid.NewGuid(),
            WebhookUrl: _server.BaseUrl + "webhook/update",
            HttpMethod: "PUT",
            Payload: "{\"status\":\"updated\"}",
            ContentType: "application/json",
            Headers: null,
            Secret: null,
            MaxRetries: 0,
            TimeoutSeconds: 30);

        // Act
        var result = await command.ExecuteAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Equal("PUT", received.Method);
    }

    [Fact]
    public async Task FullPipeline_WhenServerFails_ShouldReturnError()
    {
        // Arrange
        _server.ResponseHandler = _ => (HttpStatusCode.ServiceUnavailable, "{\"error\":\"Service unavailable\"}");

        var sender = new HttpWebhookSender(_httpClient, _options, _mockSenderLogger.Object);
        var command = new SendWebhookCommand(sender, _mockCommandLogger.Object);

        var message = new SendWebhookMessage(
            NotificationId: Guid.NewGuid(),
            WebhookUrl: _server.BaseUrl + "webhook",
            HttpMethod: "POST",
            Payload: "{\"event\":\"test\"}",
            ContentType: "application/json",
            Headers: null,
            Secret: null,
            MaxRetries: 0,
            TimeoutSeconds: 30);

        // Act
        var result = await command.ExecuteAsync(message, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task FullPipeline_WithMultipleWebhooks_ShouldDeliverAll()
    {
        // Arrange
        var sender = new HttpWebhookSender(_httpClient, _options, _mockSenderLogger.Object);
        var command = new SendWebhookCommand(sender, _mockCommandLogger.Object);

        var messages = Enumerable.Range(1, 5).Select(i => new SendWebhookMessage(
            NotificationId: Guid.NewGuid(),
            WebhookUrl: _server.BaseUrl + $"webhook/event/{i}",
            HttpMethod: "POST",
            Payload: $"{{\"event\":\"test\",\"index\":{i}}}",
            ContentType: "application/json",
            Headers: null,
            Secret: null,
            MaxRetries: 0,
            TimeoutSeconds: 30)).ToList();

        // Act
        var tasks = messages.Select(m => command.ExecuteAsync(m, CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r.Success));

        // Wait for all to be received
        await Task.Delay(500);
        Assert.Equal(5, _server.ReceivedWebhooks.Count);
    }

    [Fact]
    public async Task FullPipeline_WithDifferentContentTypes_ShouldPreserveContentType()
    {
        // Arrange
        var sender = new HttpWebhookSender(_httpClient, _options, _mockSenderLogger.Object);
        var command = new SendWebhookCommand(sender, _mockCommandLogger.Object);

        // Test with form data content type
        var message = new SendWebhookMessage(
            NotificationId: Guid.NewGuid(),
            WebhookUrl: _server.BaseUrl + "webhook/form",
            HttpMethod: "POST",
            Payload: "field1=value1&field2=value2",
            ContentType: "application/x-www-form-urlencoded",
            Headers: null,
            Secret: null,
            MaxRetries: 0,
            TimeoutSeconds: 30);

        // Act
        var result = await command.ExecuteAsync(message, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        var received = await _server.WaitForWebhookAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Contains("application/x-www-form-urlencoded", received.ContentType);
        Assert.Equal("field1=value1&field2=value2", received.Body);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _server.Dispose();
    }
}
