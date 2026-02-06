using Notification.Webhook.Domain.ValueObjects;

namespace Notification.Webhook.Domain.Tests;

public class WebhookPayloadTests
{
    [Fact]
    public void Create_WithValidPayload_ShouldSucceed()
    {
        // Arrange
        var content = "{\"event\":\"order.created\"}";
        var contentType = "application/json";

        // Act
        var payload = WebhookPayload.Create(content, contentType);

        // Assert
        Assert.Equal(content, payload.Content);
        Assert.Equal(contentType, payload.ContentType);
    }

    [Fact]
    public void Create_WithDefaultContentType_ShouldUseJson()
    {
        // Arrange
        var content = "{\"test\":true}";

        // Act
        var payload = WebhookPayload.Create(content);

        // Assert
        Assert.Equal("application/json", payload.ContentType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyContent_ShouldThrow(string? content)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => WebhookPayload.Create(content!));
    }
}
