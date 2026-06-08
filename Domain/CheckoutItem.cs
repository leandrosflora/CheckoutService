namespace CheckoutService.Domain;

public sealed class CheckoutItem
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    public decimal Total => Quantity * UnitPrice;

    private CheckoutItem()
    {
    }

    public CheckoutItem(Guid skuId, int quantity, decimal unitPrice)
    {
        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("Invalid SKU", nameof(skuId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Unit price cannot be negative", nameof(unitPrice));
        }

        Id = Guid.NewGuid();
        SkuId = skuId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
