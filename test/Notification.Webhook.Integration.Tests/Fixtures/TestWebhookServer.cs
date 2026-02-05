using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Notification.Webhook.Integration.Tests.Fixtures;

public class TestWebhookServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly Task _listenerTask;
    private readonly ConcurrentQueue<ReceivedWebhook> _receivedWebhooks;

    public string BaseUrl { get; }
    public int Port { get; }

    public TestWebhookServer()
    {
        Port = GetAvailablePort();
        BaseUrl = $"http://localhost:{Port}/";

        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseUrl);
        _receivedWebhooks = new ConcurrentQueue<ReceivedWebhook>();
        _cts = new CancellationTokenSource();

        _listener.Start();
        _listenerTask = Task.Run(ListenAsync);
    }

    public IReadOnlyList<ReceivedWebhook> ReceivedWebhooks => _receivedWebhooks.ToList();

    public Func<HttpListenerRequest, (HttpStatusCode StatusCode, string Body)>? ResponseHandler { get; set; }

    private async Task ListenAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                await HandleRequestAsync(context);
            }
            catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        string body;
        using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        var headers = new Dictionary<string, string>();
        foreach (string? key in request.Headers.AllKeys)
        {
            if (key != null)
            {
                headers[key] = request.Headers[key] ?? string.Empty;
            }
        }

        var webhook = new ReceivedWebhook(
            Method: request.HttpMethod,
            Path: request.Url?.PathAndQuery ?? "/",
            Body: body,
            ContentType: request.ContentType ?? string.Empty,
            Headers: headers,
            ReceivedAt: DateTime.UtcNow);

        _receivedWebhooks.Enqueue(webhook);

        var (statusCode, responseBody) = ResponseHandler?.Invoke(request)
            ?? (HttpStatusCode.OK, "{\"status\":\"received\"}");

        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";

        var buffer = Encoding.UTF8.GetBytes(responseBody);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void ClearReceivedWebhooks()
    {
        while (_receivedWebhooks.TryDequeue(out _)) { }
    }

    public async Task<ReceivedWebhook?> WaitForWebhookAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_receivedWebhooks.TryPeek(out var webhook))
            {
                return webhook;
            }
            await Task.Delay(10);
        }
        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        _cts.Dispose();
    }
}

public record ReceivedWebhook(
    string Method,
    string Path,
    string Body,
    string ContentType,
    Dictionary<string, string> Headers,
    DateTime ReceivedAt);

file class TcpListener : System.Net.Sockets.TcpListener
{
    public TcpListener(IPAddress localaddr, int port) : base(localaddr, port) { }
}
