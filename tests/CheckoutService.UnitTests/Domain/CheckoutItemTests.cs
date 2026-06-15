using CheckoutService.Domain;

namespace CheckoutService.UnitTests.Domain;

public sealed class CheckoutItemTests
{
    [Fact]
    public void Constructor_WhenValuesAreValid_CalculatesTotal()
    {
        var skuId = Guid.NewGuid();

        var item = new CheckoutItem(skuId, 3, 19.90m);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(skuId, item.SkuId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(19.90m, item.UnitPrice);
        Assert.Equal(59.70m, item.Total);
    }

    [Fact]
    public void Constructor_WhenSkuIsEmpty_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CheckoutItem(Guid.Empty, 1, 10m));

        Assert.Equal("skuId", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenQuantityIsNotPositive_ThrowsArgumentException(int quantity)
    {
        var exception = Assert.Throws<ArgumentException>(() => new CheckoutItem(Guid.NewGuid(), quantity, 10m));

        Assert.Equal("quantity", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenUnitPriceIsNegative_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CheckoutItem(Guid.NewGuid(), 1, -0.01m));

        Assert.Equal("unitPrice", exception.ParamName);
    }
}
