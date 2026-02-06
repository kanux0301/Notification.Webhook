using Notification.Webhook.Domain.ValueObjects;

namespace Notification.Webhook.Domain.Tests;

public class WebhookUrlTests
{
    [Theory]
    [InlineData("https://api.example.com/webhook")]
    [InlineData("http://localhost:8080/callback")]
    [InlineData("https://hooks.slack.com/services/xxx")]
    public void Create_WithValidUrl_ShouldSucceed(string url)
    {
        // Act
        var webhookUrl = WebhookUrl.Create(url);

        // Assert
        Assert.Equal(url, webhookUrl.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyUrl_ShouldThrow(string? url)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => WebhookUrl.Create(url!));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://files.example.com")]
    [InlineData("ws://socket.example.com")]
    public void Create_WithInvalidUrl_ShouldThrow(string url)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => WebhookUrl.Create(url));
    }
}
