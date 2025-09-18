using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ItemInterpreter.Logic
{
    public interface INotificationDispatcher
    {
        Task DispatchAsync(string message, CancellationToken cancellationToken = default);
    }

    public class WebhookNotificationDispatcher : INotificationDispatcher, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _webhookUrl;
        private bool _disposed;

        public WebhookNotificationDispatcher(string webhookUrl)
        {
            _webhookUrl = webhookUrl;
            _httpClient = new HttpClient();
        }

        public async Task DispatchAsync(string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_webhookUrl) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var payload = new { content = message };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
