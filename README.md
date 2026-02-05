# Notification.Webhook

A .NET 10 microservice for delivering webhook notifications via HTTP callbacks.

## Functionality

### Worker Service
The worker service consumes messages from a message queue (RabbitMQ or Azure Service Bus) and delivers webhook notifications to configured endpoints.

**Features:**
- HTTP/HTTPS webhook delivery with configurable methods (POST, PUT, PATCH)
- Custom headers support for authentication and metadata
- HMAC-SHA256 signature generation for payload verification
- Configurable retry logic with exponential backoff
- Request timeout handling
- Support for multiple content types (JSON, XML, form data)

### Message Format
The service expects messages with the following structure:
- `NotificationId` - Unique identifier for tracking
- `WebhookUrl` - Target endpoint URL (HTTPS recommended)
- `HttpMethod` - HTTP method (POST, PUT, PATCH)
- `Payload` - Request body content
- `ContentType` - Content-Type header value
- `Headers` - Optional custom headers dictionary
- `Secret` - Optional secret for HMAC signature generation
- `MaxRetries` - Maximum retry attempts on failure
- `TimeoutSeconds` - Request timeout in seconds

### Security
When a secret is provided, the service generates an HMAC-SHA256 signature of the payload and includes it in the `X-Webhook-Signature` header. Recipients can verify payload integrity by computing the same signature.

## Project Structure

```
Notification.Webhook/
├── src/
│   ├── Notification.Webhook.Domain/        # Entities, value objects, domain services
│   ├── Notification.Webhook.Application/   # Commands, messaging interfaces
│   ├── Notification.Webhook.Infrastructure/# HTTP client, message consumers, providers
│   └── Notification.Webhook.Worker/        # Background worker service
├── test/
│   └── Notification.Webhook.Worker.Tests/  # Unit tests
└── deployment/
    ├── Dockerfile
    ├── docker-compose.yml
    └── build.yaml
```

## Configuration

### appsettings.json
```json
{
  "Webhook": {
    "Provider": "Http",
    "DefaultTimeoutSeconds": 30,
    "DefaultMaxRetries": 3
  },
  "Messaging": {
    "Provider": "RabbitMQ",
    "RabbitMQ": {
      "HostName": "localhost",
      "Port": 5672,
      "UserName": "guest",
      "Password": "guest",
      "QueueName": "webhook-notifications"
    }
  }
}
```

### Providers
- `Http` - Production provider for actual HTTP delivery
- `Console` - Development provider that logs to console

## Running the Service

### Local Development
```bash
cd src/Notification.Webhook.Worker
dotnet run
```

### Docker
```bash
cd deployment
docker-compose up -d
```

## Testing
```bash
dotnet test
```
