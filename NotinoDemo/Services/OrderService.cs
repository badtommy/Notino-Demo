using NotinoDemo.Data;
using NotinoDemo.Models;

namespace NotinoDemo.Services;

public sealed class OrderService
{
    private readonly SqlBootstrapper _bootstrapper;
    private readonly ProductService _productService;

    public OrderService(SqlBootstrapper bootstrapper, ProductService productService)
    {
        _bootstrapper = bootstrapper;
        _productService = productService;
    }

    public async Task<OrderResult> ProcessCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Checkout request must contain at least one item.");
        }

        var customers = await _bootstrapper.GetCustomersAsync(cancellationToken);
        var customer = customers.First(c => c.Id == request.CustomerId);

        decimal subtotal = 0;
        foreach (var item in request.Items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId, cancellationToken);
            subtotal += product.Price * item.Quantity;
        }

        var discountPercent = 0m;
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            var discountCodes = await _bootstrapper.GetDiscountCodesAsync(cancellationToken);
            discountPercent = discountCodes[request.DiscountCode];
        }

        var normalizedCustomerName = customer.Name!.ToUpperInvariant();
        var total = Math.Round(subtotal - (subtotal * (discountPercent / 100m)), 2);

        var provisionalOrder = new OrderResult(0, normalizedCustomerName, subtotal, discountPercent, total, "Processed");
        var orderId = await _bootstrapper.InsertOrderAsync(provisionalOrder, request.CustomerId, cancellationToken);

        return provisionalOrder with { OrderId = orderId };
    }
}
