using System.Net.Http.Json;
using NotinoDemo.Models;

namespace NotinoDemo.Background;

public sealed class TrafficSimulator : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TrafficSimulator> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;

    private static readonly string[] ProductPaths =
    [
        "/api/products",
        "/api/products/1",
        "/api/products/category/Parfemy",
        "/api/products/999",
        "/api/products/0",
        "/api/products/category/Parfemy?page=0",
        "/api/products/7/discount?percent=10"
    ];

    public TrafficSimulator(
        IHttpClientFactory httpClientFactory,
        ILogger<TrafficSimulator> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(nameof(TrafficSimulator));
                var path = ProductPaths[Random.Shared.Next(ProductPaths.Length)];
                if (Random.Shared.Next(0, 3) == 0)
                {
                    var invalidCode = Random.Shared.Next(0, 2) == 0;
                    var request = invalidCode
                        ? new CheckoutRequest(1, [new CheckoutItem(1, 1)], "INVALIDCODE")
                        : new CheckoutRequest(3, [new CheckoutItem(4, 1)]);

                    using var response = await client.PostAsJsonAsync("/api/orders/checkout", request, stoppingToken);
                    _logger.LogInformation("TrafficSimulator POST /api/orders/checkout => {StatusCode}", (int)response.StatusCode);
                }
                else
                {
                    using var response = await client.GetAsync(path, stoppingToken);
                    _logger.LogInformation("TrafficSimulator GET {Path} => {StatusCode}", path, (int)response.StatusCode);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "TrafficSimulator request failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);
        }
    }
}
