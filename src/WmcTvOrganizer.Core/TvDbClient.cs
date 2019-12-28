using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WmcTvOrganizer.Core
{
    public class TvDbClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _userKey;
        private readonly string _username;
        private readonly CancellationToken _cancellationToken;
        private readonly ILogger<TvDbClient> _logger;

        private string _token;
        
        public TvDbClient(IOptions<TvDbClientOptions> options, HttpClient httpClient, ILogger<TvDbClient> logger, CancellationTokenSource cancellationTokenSource)
        {
            _httpClient = httpClient;

            _apiKey = !string.IsNullOrEmpty(options.Value.ApiKey)
                ? options.Value.ApiKey
                : throw new ArgumentNullException(nameof(options.Value.ApiKey));

            _userKey = !string.IsNullOrEmpty(options.Value.UserKey)
                ? options.Value.ApiKey
                : throw new ArgumentNullException(nameof(options.Value.UserKey));

            _username = !string.IsNullOrEmpty(options.Value.Username)
                ? options.Value.Username
                : throw new ArgumentNullException(nameof(options.Value.Username));

            _httpClient.BaseAddress = new Uri(options.Value.BaseAddress);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            _logger = logger;
            _cancellationToken = cancellationTokenSource.Token;

        }

        public async Task<JsonDocument> SearchSeries(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"search/series?{WebUtility.UrlEncode(name)}");

            return await MakeRequest(request, true);
        }

        private async Task<string> GetToken()
        {
            StringContent payload;
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("apikey", _apiKey);
                    writer.WriteString("userkey", _userKey);
                    writer.WriteString("username", _username);
                    writer.WriteEndObject();
                }
                payload = new StringContent(Encoding.UTF8.GetString(stream.ToArray()), Encoding.UTF8, "application/json");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "login") {Content = payload};
            request.Headers.Add("Content-Type", "application/json");
            var doc = await MakeRequest(request, false);
            return doc.RootElement.GetProperty("token").GetString();

        }

        private async Task<JsonDocument> MakeRequest(HttpRequestMessage request, bool requiresToken)
        {
            if (requiresToken)
            {
                if (string.IsNullOrEmpty(_token))
                {
                    _token = await GetToken();
                }
                request.Headers.Add("Authorization", "Bearer " + _token);
            }
            
            try
            {
                var response = await _httpClient.SendAsync(request, _cancellationToken);
                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync();
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: _cancellationToken);
                return doc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error making request");
                throw;
            }
        }
    }

    public class TvDbClientOptions
    {
        public string ApiKey { get; set; }
        public string UserKey { get; set; }
        public string Username { get; set; }

        public string BaseAddress { get; set; } = "https://api.thetvdb.com";

    }
}
