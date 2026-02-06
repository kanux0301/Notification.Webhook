using Microsoft.Extensions.Logging;
using Moq;
using Notification.Webhook.Domain.Entities;
using Notification.Webhook.Domain.Enums;
using Notification.Webhook.Domain.ValueObjects;
using Notification.Webhook.Infrastructure.Providers;

namespace Notification.Webhook.Infrastructure.Tests;

public class ConsoleWebhookSenderTests
{
    private readonly Mock<ILogger<ConsoleWebhookSender>> _mockLogger;
    private readonly ConsoleWebhookSender _sender;

    public ConsoleWebhookSenderTests()
    {
        _mockLogger = new Mock<ILogger<ConsoleWebhookSender>>();
        _sender = new ConsoleWebhookSender(_mockLogger.Object);
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSuccess()
    {
        // Arrange
        var notification = WebhookNotification.Create(
            id: Guid.NewGuid(),
            url: WebhookUrl.Create("https://api.example.com/webhook"),
            method: WebhookHttpMethod.POST,
            payload: WebhookPayload.Create("{\"event\":\"test\"}"),
            headers: null,
            secret: null,
            maxRetries: 3,
            timeoutSeconds: 30);

        // Act
        var result = await _sender.SendAsync(notification);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_WithHeaders_ShouldReturnSuccess()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer token123" },
            { "X-Custom-Header", "custom-value" }
        };

        var notification = WebhookNotification.Create(
            id: Guid.NewGuid(),
            url: WebhookUrl.Create("https://api.example.com/webhook"),
            method: WebhookHttpMethod.POST,
            payload: WebhookPayload.Create("{\"event\":\"test\"}"),
            headers: headers,
            secret: "my-secret",
            maxRetries: 3,
            timeoutSeconds: 30);

        // Act
        var result = await _sender.SendAsync(notification);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WithCancellation_ShouldNotThrow()
    {
        // Arrange
        var notification = WebhookNotification.Create(
            id: Guid.NewGuid(),
            url: WebhookUrl.Create("https://api.example.com/webhook"),
            method: WebhookHttpMethod.PUT,
            payload: WebhookPayload.Create("{\"data\":\"value\"}"));

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _sender.SendAsync(notification, cts.Token);

        // Assert
        Assert.True(result.Success);
    }
}
