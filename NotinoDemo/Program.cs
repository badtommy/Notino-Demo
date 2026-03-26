using NotinoDemo.Background;
using NotinoDemo.Data;
using NotinoDemo.Middleware;
using NotinoDemo.Models;
using NotinoDemo.Services;

var builder = WebApplication.CreateBuilder(args);

var selfBaseAddress = builder.Configuration["ASPNETCORE_URLS"]?
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .FirstOrDefault(url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    ?? "http://127.0.0.1:5000";

builder.AddSqlServerClient(connectionName: "sqldb");
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient(nameof(TrafficSimulator), client =>
{
    client.BaseAddress = new Uri(selfBaseAddress);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<SqlBootstrapper>();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddHostedService<TrafficSimulator>();

var app = builder.Build();

await app.Services.GetRequiredService<SqlBootstrapper>().InitializeAsync();

app.UseExceptionLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new
{
    name = "Notino Demo API",
    openApi = "/openapi/v1.json",
    logs = "logs/notino-errors.json"
}));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/products", async (ProductService service, CancellationToken cancellationToken) =>
{
    var products = await service.GetProductsAsync(cancellationToken);
    return Results.Ok(products);
});

app.MapGet("/api/products/{id:int}", async (int id, ProductService service, CancellationToken cancellationToken) =>
{
    var product = await service.GetProductByIdAsync(id, cancellationToken);
    return Results.Ok(product);
});

app.MapGet("/api/products/category/{category}", async (string category, int? page, int? pageSize, ProductService service, CancellationToken cancellationToken) =>
{
    var products = await service.GetProductsByCategoryAsync(category, page ?? 1, pageSize ?? 10, cancellationToken);
    return Results.Ok(products);
});

app.MapGet("/api/products/{id:int}/discount", async (int id, decimal percent, ProductService service, CancellationToken cancellationToken) =>
{
    var discountedPrice = await service.CalculateDiscountedPriceAsync(id, percent, cancellationToken);
    return Results.Ok(new { productId = id, percent, discountedPrice });
});

app.MapPost("/api/orders/checkout", async (CheckoutRequest request, OrderService service, CancellationToken cancellationToken) =>
{
    var result = await service.ProcessCheckoutAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.Run();
