using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Notification.Webhook.Application.Common.Messaging;
using RabbitMQ.Client;

Console.WriteLine("=== Webhook Test Sender ===");
Console.WriteLine("This tool publishes a webhook message to RabbitMQ.");
Console.WriteLine("Make sure the Worker service is running to process it.\n");

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Read RabbitMQ settings
var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
var userName = configuration["RabbitMQ:UserName"] ?? "guest";
var password = configuration["RabbitMQ:Password"] ?? "guest";
var queueName = configuration["RabbitMQ:QueueName"] ?? "notifications.webhook";

Console.WriteLine($"RabbitMQ: {hostName}:{port} | Queue: {queueName}\n");

// Get webhook URL from user
Console.Write("Enter webhook URL (e.g., https://webhook.site/your-id): ");
var webhookUrl = Console.ReadLine()?.Trim();

if (string.IsNullOrEmpty(webhookUrl))
{
    Console.WriteLine("Webhook URL is required!");
    return;
}

// Get HTTP method
Console.Write("Enter HTTP method (POST/PUT/PATCH) [POST]: ");
var methodInput = Console.ReadLine()?.Trim().ToUpperInvariant();
var httpMethod = methodInput switch
{
    "PUT" => "PUT",
    "PATCH" => "PATCH",
    _ => "POST"
};

// Get payload
Console.Write("Enter custom payload JSON (or press Enter for default): ");
var payloadInput = Console.ReadLine()?.Trim();
var notificationId = Guid.NewGuid();
var payload = string.IsNullOrEmpty(payloadInput)
    ? JsonSerializer.Serialize(new
    {
        @event = "test.webhook",
        timestamp = DateTime.UtcNow.ToString("o"),
        data = new
        {
            message = "Hello from Notification.Webhook TestSender!",
            environment = "Development",
            notificationId = notificationId.ToString()
        }
    }, new JsonSerializerOptions { WriteIndented = true })
    : payloadInput;

// Get secret (optional)
Console.Write("Enter webhook secret for HMAC signature (or press Enter to skip): ");
var secret = Console.ReadLine()?.Trim();

// Create the message
var message = new SendWebhookMessage(
    NotificationId: notificationId,
    WebhookUrl: webhookUrl,
    HttpMethod: httpMethod,
    Payload: payload,
    ContentType: "application/json",
    Headers: new Dictionary<string, string>
    {
        { "X-Custom-Header", "TestSender" },
        { "X-Test-Timestamp", DateTime.UtcNow.ToString("o") }
    },
    Secret: string.IsNullOrEmpty(secret) ? null : secret,
    MaxRetries: 3,
    TimeoutSeconds: 30);

var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });

Console.WriteLine($"\nNotification ID: {notificationId}");
Console.WriteLine($"Method: {httpMethod}");
Console.WriteLine($"Payload:\n{payload}\n");

// Publish to RabbitMQ
try
{
    var factory = new ConnectionFactory
    {
        HostName = hostName,
        Port = port,
        UserName = userName,
        Password = password
    };

    using var connection = await factory.CreateConnectionAsync();
    using var channel = await connection.CreateChannelAsync();

    await channel.QueueDeclareAsync(
        queue: queueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null);

    var body = Encoding.UTF8.GetBytes(messageJson);

    var properties = new BasicProperties
    {
        Persistent = true,
        ContentType = "application/json"
    };

    await channel.BasicPublishAsync(
        exchange: string.Empty,
        routingKey: queueName,
        mandatory: true,
        basicProperties: properties,
        body: body);

    Console.WriteLine($"Message published to RabbitMQ queue '{queueName}' successfully!");
    Console.WriteLine("The Worker service will pick it up and deliver the webhook.");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to publish message to RabbitMQ: {ex.Message}");
    Console.WriteLine("\nMake sure RabbitMQ is running. You can start it with:");
    Console.WriteLine("  cd deployment && docker-compose up -d rabbitmq");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
