namespace MapLean.Graph;

public static class ManualMap
{
    public static OrderDto Map(Order source)
    {
        var lines = source.Lines;
        var count = lines.Count;
        var mapped = new List<LineItemDto>(count);
        for (var i = 0; i < count; i++)
        {
            var line = lines[i];
            mapped.Add(new LineItemDto(line.Sku, line.Qty));
        }

        var bill = source.BillTo;
        return new OrderDto(
            source.Id,
            new AddressDto(source.ShipTo.City, source.ShipTo.Line2),
            bill is null ? null : new AddressDto(bill.City, bill.Line2),
            mapped,
            source.Note);
    }
}
