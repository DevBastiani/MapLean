using MapLean;

namespace MapLean.Graph;

public sealed record Address(string City, string? Line2);

public sealed record LineItem(string Sku, int Qty);

public sealed record Order(
    string Id,
    Address ShipTo,
    Address? BillTo,
    List<LineItem> Lines,
    string? Note,
    string Audit);

public sealed record AddressDto(string City, string? Line2);

public sealed record LineItemDto(string Sku, int Qty);

public sealed record OrderDto(
    string Id,
    AddressDto ShipTo,
    AddressDto? BillTo,
    List<LineItemDto> Lines,
    string? Note)
{
    [Leave]
    public string Channel { get; init; } = "unset";
}

public static class FrozenGraph
{
    public static Order Present()
    {
        var lines = new List<LineItem>(8);
        for (var i = 0; i < 8; i++)
            lines.Add(new LineItem("sku-" + i, i + 1));

        return new Order(
            "o-1",
            new Address("Austin", "Suite 2"),
            new Address("Dallas", null),
            lines,
            null,
            "audit-secret");
    }

    public static Order Absent()
    {
        return new Order(
            "o-2",
            new Address("Austin", null),
            null,
            new List<LineItem>(),
            "note",
            "audit-secret");
    }
}
