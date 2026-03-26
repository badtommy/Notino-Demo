namespace NotinoDemo.Models;

public sealed record Product(
    int Id,
    string Name,
    string Category,
    decimal Price,
    int Stock,
    bool IsActive = true);

public sealed record Customer(
    int Id,
    string? Name,
    string Email,
    bool IsGuest = false);

public sealed record CheckoutItem(int ProductId, int Quantity);

public sealed record CheckoutRequest(
    int CustomerId,
    List<CheckoutItem> Items,
    string? DiscountCode = null);

public sealed record OrderResult(
    int OrderId,
    string CustomerName,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal Total,
    string Status);
