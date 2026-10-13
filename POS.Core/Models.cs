namespace POS.Core;

public enum StockMode { EXACT, WEIGHT, MANUAL }
public enum SaleUnit { UNIT, KG, PACK }
public enum PaymentMethod { CASH, TRANSFER, CARD, OTHER }
public enum ReplenishmentStatus { SUFICIENTE, BAJO, REPONER }
public sealed record Product(long Id, string? Code, string Name, string Category, SaleUnit Unit, StockMode Mode, long PriceMinor, long? CostMinor, bool Active);
public sealed record InventoryRow(long ProductId, string Name, StockMode Mode, long? QuantityBase, long? MinimumBase, ReplenishmentStatus Status);
public sealed record CartLine(long ProductId, long QuantityBase);
public sealed record SaleSummary(long Id, string CreatedAt, string Status, string PaymentMethod, long TotalMinor);
public sealed record SaleItem(long ProductId, string Name, SaleUnit Unit, long QuantityBase, long UnitPriceMinor, long TotalMinor);
public sealed record Movement(long Id, long ProductId, long? SaleId, string Type, long DeltaBase, string CreatedAt, string? Note);
public sealed record PricePreview(long ProductId, string Name, long OldPriceMinor, long NewPriceMinor);

public static class Pricing
{
    public static long Money(decimal pesos) => checked((long)decimal.Round(pesos * 100m, 0, MidpointRounding.AwayFromZero));
    public static long Quantity(decimal quantity, SaleUnit unit)
    {
        if (quantity <= 0) throw new ArgumentException("La cantidad debe ser mayor que cero.");
        var scaled = unit == SaleUnit.KG ? quantity * 1000m : quantity;
        if (scaled != decimal.Truncate(scaled)) throw new ArgumentException(unit == SaleUnit.KG ? "La precisión mínima es un gramo." : "La cantidad debe ser entera.");
        return checked((long)scaled);
    }
    public static long Total(long price, long quantity, SaleUnit unit)
    {
        if (price < 0 || quantity <= 0) throw new ArgumentException("Precio o cantidad inválidos.");
        return checked((long)decimal.Round((decimal)price * quantity / (unit == SaleUnit.KG ? 1000m : 1m), 0, MidpointRounding.AwayFromZero));
    }
    public static void Validate(Product p)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || p.PriceMinor < 0 || p.CostMinor < 0) throw new ArgumentException("Revisá nombre, precio y costo.");
        if (!Enum.IsDefined(p.Unit) || !Enum.IsDefined(p.Mode)) throw new ArgumentException("Unidad o stock inválidos.");
        if (p.Mode == StockMode.WEIGHT && p.Unit != SaleUnit.KG || p.Mode == StockMode.EXACT && p.Unit == SaleUnit.KG) throw new ArgumentException("El control de stock no corresponde a la unidad de venta.");
    }
}

public interface IPosService
{
    IReadOnlyList<Product> Products(string search = "", bool includeInactive = false);
    long SaveProduct(Product product);
    void SetActive(long id, bool active);
    IReadOnlyList<InventoryRow> Inventory();
    void AdjustStock(long productId, long value, bool absolute, string note);
    void SetManualStatus(long productId, ReplenishmentStatus status);
    void SetMinimumStock(long productId, long? minimumBase);
    IReadOnlyList<Movement> Movements(long productId);
    long CompleteSale(IReadOnlyList<CartLine> lines, PaymentMethod payment);
    IReadOnlyList<SaleSummary> Sales(DateTime from, DateTime to);
    IReadOnlyList<SaleItem> SaleItems(long saleId);
    void CancelSale(long saleId, string reason);
    IReadOnlyList<PricePreview> PreviewPrices(string category, decimal amount, bool percentage, long roundToMinor);
    void ApplyPrices(IReadOnlyList<PricePreview> preview);
    void Backup(string destination);
    void Restore(string source);
}
