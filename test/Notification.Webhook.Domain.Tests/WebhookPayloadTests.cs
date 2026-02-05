using Notification.Webhook.Domain.ValueObjects;

namespace Notification.Webhook.Domain.Tests;

public class WebhookPayloadTests
{
    [Fact]
    public void Create_WithValidPayload_ShouldSucceed()
    {
        var content = "{\"event\":\"order.created\"}";
        var contentType = "application/json";

        var payload = WebhookPayload.Create(content, contentType);

        Assert.Equal(content, payload.Content);
        Assert.Equal(contentType, payload.ContentType);
    }

    [Fact]
    public void Create_WithDefaultContentType_ShouldUseJson()
    {
        var content = "{\"test\":true}";

        var payload = WebhookPayload.Create(content);

        Assert.Equal("application/json", payload.ContentType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyContent_ShouldThrow(string? content)
    {
        Assert.Throws<ArgumentException>(() => WebhookPayload.Create(content!));
    }
}
