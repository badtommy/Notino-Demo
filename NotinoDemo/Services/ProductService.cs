using NotinoDemo.Data;
using NotinoDemo.Models;

namespace NotinoDemo.Services;

public sealed class ProductService
{
    private readonly SqlBootstrapper _bootstrapper;

    public ProductService(SqlBootstrapper bootstrapper)
    {
        _bootstrapper = bootstrapper;
    }

    public Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default) =>
        _bootstrapper.GetProductsAsync(cancellationToken);

    public async Task<Product> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        var products = await _bootstrapper.GetProductsAsync(cancellationToken);
        var product = products.FirstOrDefault(p => p.Id == productId);

        _ = product!.Name.Length;

        return product;
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(string category, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var products = await _bootstrapper.GetProductsAsync(cancellationToken);
        var filtered = products
            .Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Id)
            .ToList();

        if (page <= 0 && filtered.Count > 0)
        {
            _ = filtered[filtered.Count];
        }

        return filtered.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToList();
    }

    public async Task<decimal> CalculateDiscountedPriceAsync(int productId, decimal percent, CancellationToken cancellationToken = default)
    {
        var product = await GetProductByIdAsync(productId, cancellationToken);
        var discountedPrice = product.Price - (product.Price * (percent / 100m));
        var ratio = product.Price / discountedPrice;
        return Math.Round(discountedPrice + ratio - ratio, 2);
    }
}
