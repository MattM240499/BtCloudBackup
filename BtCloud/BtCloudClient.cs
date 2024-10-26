// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using BtCloudDownload;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace BtCloudDownload.BtCloud;

public class BtCloudClient
{
    private readonly BtCloudAuthorizationClient _authClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _getDocumentsResiliencePipeline;
    private readonly ResiliencePipeline<HttpResponseMessage> _getZipResiliencePipeline;
    private readonly ILogger _logger;

    public BtCloudClient(BtCloudAuthorizationClient authClient, ILogger<BtCloudClient> logger) 
    {
        _authClient = authClient;
        _logger = logger;
        _getDocumentsResiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>()
            {
                MaxRetryAttempts = 10,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(r => !r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.Unauthorized),
                OnRetry = OnGetDocumentsRetry
            })
            .Build();

        _getZipResiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>()
            {
                MaxRetryAttempts = 10,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(r => !r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.Unauthorized),
                OnRetry = OnGetZipRetry
            })
            .Build();
    }

    public async Task<DocumentResponse> GetDocuments(int start, int count, string? cursor) 
    {
        var token = await _authClient.GetToken();
        using var httpClient = CreateDocumentHttpClient(token);

        var cursorParam = cursor == null ? "" : $"&cursor={cursor}";
        var response = await _getDocumentsResiliencePipeline.ExecuteAsync(async token => await httpClient.GetAsync(
            $"https://btcloud.bt.com/dv/api/user/39fe8288233147d58982dd950e92177d/browse/document?sort=creationdate&order=desc&start={start}&count={count}&repository=LAPTOP-5R3Q9HCO&repository=TABLET&repository=SyncDrive&repository=DESKTOP-9LOMI1J{cursorParam}", token));
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        var data = JsonSerializer.Deserialize<DocumentResponse>(json, jsonOptions);
        if (data is null) throw new Exception("Data deserialized to null");
        return data;
    }

    public async Task<DocumentResponse> GetPhotos(int start, int count, string? cursor) 
    {
        var token = await _authClient.GetToken();
        using var httpClient = CreateDocumentHttpClient(token);

        var cursorParam = cursor == null ? "" : $"&cursor={cursor}";
        var response = await _getDocumentsResiliencePipeline.ExecuteAsync(async token => await httpClient.GetAsync(
            $"https://btcloud.bt.com/dv/api/user/39fe8288233147d58982dd950e92177d/browse/imagevideo?sort=creationdate&order=desc&start={start}&count={count}&repository=LAPTOP-5R3Q9HCO&repository=TABLET&repository=SyncDrive&repository=DESKTOP-9LOMI1J{cursorParam}"));

        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        var data = JsonSerializer.Deserialize<DocumentResponse>(json, jsonOptions);
        if (data is null) throw new Exception("Data deserialized to null");
        return data;
    }

    public async Task<DocumentResponse> GetAudio(int start, int count, string? cursor) 
    {
        var token = await _authClient.GetToken();
        using var httpClient = CreateAudioHttpClient(token);

        var cursorParam = cursor == null ? "" : $"&cursor={cursor}";
        var response = await _getDocumentsResiliencePipeline.ExecuteAsync(async token => await httpClient.GetAsync(
            $"https://btcloud.bt.com/dv/api/user/39fe8288233147d58982dd950e92177d/browse/audio?sort=creationdate&order=desc&start={start}&count={count}&repository=LAPTOP-5R3Q9HCO&repository=TABLET&repository=SyncDrive&repository=DESKTOP-9LOMI1J{cursorParam}", token));

        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        var data = JsonSerializer.Deserialize<DocumentResponse>(json, jsonOptions);
        if (data is null) throw new Exception("Data deserialized to null");
        return data;
    }

    public async Task<Stream> GetZip(string[] files) 
    {
        var token = await _authClient.GetToken();
        var zipPayloadValues = files
            .Select(f => $"repositoryPath={HttpUtility.UrlEncode(f)}")
            .Append($"NWB={token}")
            .Append($"name=Zip{DateTime.UtcNow.Ticks}");

        var zipPayload = string.Join("&", zipPayloadValues);

        using var httpClient = CreateZipHttpClient(token);

        var content = new StringContent(zipPayload, new MediaTypeHeaderValue("application/x-www-form-urlencoded"));

        var request = new HttpRequestMessage(HttpMethod.Post, "https://btcloud.bt.com/dv/api/user/39fe8288233147d58982dd950e92177d/operations/zip");
        request.Content = content;
        var response = await _getZipResiliencePipeline.ExecuteAsync(async token => await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token));

        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadAsStreamAsync();

        return data;
    }

    private HttpClient CreateDocumentHttpClient(string token)
    {
        HttpClientHandler httpClientHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; },
            AutomaticDecompression = DecompressionMethods.All
        };

        var httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.newbay.dv-1.20+json"));
        httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"NWB token=\"{token}\"; authVersion=\"1.0\"");
        httpClient.DefaultRequestHeaders.Add("connection", "keep-alive");
        httpClient.DefaultRequestHeaders.Add("Cookie", $"sei=1799; NWB={token}; defaultLocale=en-GB; currency=GBP; ll=1729434517250; ci=1729434523255");
        httpClient.DefaultRequestHeaders.Add("Host", "btcloud.bt.com");
        httpClient.DefaultRequestHeaders.Add("Referrer", "https://btcloud.bt.com/web/app/39fe8288233147d58982dd950e92177d/vault");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Google Chrome\";v=\"129\", \"Not=A?Brand\";v=\"8\", \"Chromium\";v=\"129\"");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("X-Client-Identifier", "WhiteLabelWebApp");
        httpClient.DefaultRequestHeaders.Add("X-Client-Platform", "WEB");
        return httpClient;
    }

    private HttpClient CreateAudioHttpClient(string token)
    {
        HttpClientHandler httpClientHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; },
            AutomaticDecompression = DecompressionMethods.All
        };

        var httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.newbay.dv-1.20+json"));
        httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"NWB token=\"{token}\"; authVersion=\"1.0\"");
        httpClient.DefaultRequestHeaders.Add("connection", "keep-alive");
        httpClient.DefaultRequestHeaders.Add("Cookie", $"sei=1799; NWB={token}; defaultLocale=en-GB; currency=GBP; ll=1729434517250; ci=1729434523255");
        httpClient.DefaultRequestHeaders.Add("Host", "btcloud.bt.com");
        httpClient.DefaultRequestHeaders.Add("Referrer", "https://btcloud.bt.com/web/app/39fe8288233147d58982dd950e92177d/vault");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Google Chrome\";v=\"129\", \"Not=A?Brand\";v=\"8\", \"Chromium\";v=\"129\"");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("X-Client-Identifier", "WhiteLabelWebApp");
        httpClient.DefaultRequestHeaders.Add("X-Client-Platform", "WEB");
        return httpClient;
    }

    
    private HttpClient CreateZipHttpClient(string token)
    {
        HttpClientHandler httpClientHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; },
            AutomaticDecompression = DecompressionMethods.All,
        };

        var httpClient = new HttpClient(httpClientHandler);
        httpClient.Timeout = TimeSpan.FromMinutes(30);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.newbay.dv-1.20+json"));
        httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Cache-Control", $"max-age=0");
        httpClient.DefaultRequestHeaders.Add("connection", "keep-alive");
        httpClient.DefaultRequestHeaders.Add("Cookie", $"NWB={token}; defaultLocale=en-GB; currency=GBP; ll=1729434517250; ci=1729434523255");
        httpClient.DefaultRequestHeaders.Add("Host", "btcloud.bt.com");
        httpClient.DefaultRequestHeaders.Add("Origin", "https://btcloud.bt.com");
        httpClient.DefaultRequestHeaders.Add("Referrer", "https://btcloud.bt.com/web/app/39fe8288233147d58982dd950e92177d/vault");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Google Chrome\";v=\"129\", \"Not=A?Brand\";v=\"8\", \"Chromium\";v=\"129\"");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
        return httpClient;
    }

    private ValueTask OnGetDocumentsRetry(OnRetryArguments<HttpResponseMessage> o)
    {
        if (o.Outcome.Exception is not null)
        {
            _logger.LogError(o.Outcome.Exception, "Failed to download documents");
        }
        else 
        {
            _logger.LogError("Failed to download documents with StatusCode = {StatusCode}", o.Outcome.Result!.StatusCode);
        }
        return ValueTask.CompletedTask;
    }

    private ValueTask OnGetZipRetry(OnRetryArguments<HttpResponseMessage> o)
    {
        if (o.Outcome.Exception is not null)
        {
            _logger.LogError(o.Outcome.Exception, "Failed to get zip");
        }
        else 
        {
            _logger.LogError("Failed to get zip with StatusCode = {StatusCode}", o.Outcome.Result!.StatusCode);
        }
        return ValueTask.CompletedTask;
    }
}