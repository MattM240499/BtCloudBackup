using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace BtCloudDownload.BtCloud;

public class BtCloudAuthorizationClient : IDisposable
{
    private readonly BtCloudTokenStore _btCloudTokenStore;
    private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);
    private readonly ILogger _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _refreshTokenResiliencePipeline;
    private string? _token;
    private CancellationTokenSource? _tokenCancellationSource;


    public BtCloudAuthorizationClient(BtCloudTokenStore btCloudTokenStore, ILogger<BtCloudAuthorizationClient> logger) 
    {
        _btCloudTokenStore = btCloudTokenStore;
        _logger = logger;

        _refreshTokenResiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>()
            {
                MaxRetryAttempts = 10,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(r => !r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.MovedPermanently),
                OnRetry = o => OnRefreshTokenRetry(o),
            })
            .Build();
    }

    private ValueTask OnRefreshTokenRetry(OnRetryArguments<HttpResponseMessage> o)
    {
        if (o.Outcome.Exception is not null)
        {
            _logger.LogError(o.Outcome.Exception, "Failed to download zip");
        }
        else 
        {
            _logger.LogError("Failed to download zip with StatusCode = {StatusCode}", o.Outcome.Result!.StatusCode);
        }
        return ValueTask.CompletedTask;
    }

    public async Task<string> GetToken() 
    {
        await _tokenSemaphore.WaitAsync();
        try
        {
            if (_token is null) 
            {
                _token = _btCloudTokenStore.GetToken() ?? throw new Exception("No starting token found in store");
                await RefreshTokenNoLock();
            }
            if (_tokenCancellationSource?.IsCancellationRequested == true) 
            {
                await RefreshTokenNoLock();
            }
            return _token!;
        }
        finally 
        {
            _tokenSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _tokenCancellationSource?.Dispose();
    }

        private void SetToken(string token) 
    {
        _token = token;
        _btCloudTokenStore.SaveToken(_token);
        _tokenCancellationSource?.Dispose();
        _tokenCancellationSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        _tokenCancellationSource.Token.Register(async () => await RefreshToken());
    }

    private async Task RefreshToken()
    {
        await _tokenSemaphore.WaitAsync();
        try
        {
            await RefreshTokenNoLock();

        }
        catch (Exception e) 
        {
           _logger.LogError(e, $"Failed to refresh token");
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    private async Task RefreshTokenNoLock()
    {
        _logger.LogInformation("Generating new token...");
        var newToken = await GetNewToken();
        SetToken(newToken);
        _logger.LogInformation("New token generated!");
    }

    private async Task<string> GetNewToken() 
    {
        using var httpClient = CreateTokenHttpClient();

        var response = await _refreshTokenResiliencePipeline.ExecuteAsync(async token => await httpClient.GetAsync("https://btcloud.bt.com/web/app/accessToken", token));

        if (response.StatusCode == HttpStatusCode.MovedPermanently)
        {
            _logger.LogError("Token no longer valid. Clearing stored token");
            _btCloudTokenStore.SaveToken("");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private HttpClient CreateTokenHttpClient()
    {
        HttpClientHandler httpClientHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; },
            AutomaticDecompression = DecompressionMethods.All
        };
        var httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.Add("Authorization", $"NWB token= \"{_token}\"; Path=/; Domain=btcloud.bt.com; HttpOnly; Secure; Max-Age=1800");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Cookie", $"sei=1799; NWB={_token}; defaultLocale=en-GB; currency=GBP; ll=1729434517250; ci=1729434523255");
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
}
