using MapLean.Graph;
using Xunit;

namespace MapLean.Graph.Tests;

public sealed class ManualMapTests
{
    [Fact]
    public void Present_matches_the_frozen_values()
    {
        var source = FrozenGraph.Present();
        var dto = ManualMap.Map(source);

        Assert.Equal("o-1", dto.Id);
        Assert.Equal("Austin", dto.ShipTo.City);
        Assert.Equal("Suite 2", dto.ShipTo.Line2);
        Assert.NotNull(dto.BillTo);
        Assert.Equal("Dallas", dto.BillTo.City);
        Assert.Null(dto.BillTo.Line2);
        Assert.Null(dto.Note);
        Assert.Equal("unset", dto.Channel);
        Assert.Equal(8, dto.Lines.Count);
        Assert.Equal(8, dto.Lines.Capacity);
        for (var i = 0; i < 8; i++)
        {
            Assert.Equal("sku-" + i, dto.Lines[i].Sku);
            Assert.Equal(i + 1, dto.Lines[i].Qty);
        }

        Assert.Null(typeof(OrderDto).GetProperty("Audit"));
        source.Lines[0] = new LineItem("changed", 9);
        Assert.Equal("sku-0", dto.Lines[0].Sku);
        Assert.Equal(1, dto.Lines[0].Qty);
    }

    [Fact]
    public void Absent_keeps_null_bill_and_empty_lines()
    {
        var dto = ManualMap.Map(FrozenGraph.Absent());

        Assert.Equal("o-2", dto.Id);
        Assert.Equal("Austin", dto.ShipTo.City);
        Assert.Null(dto.ShipTo.Line2);
        Assert.Null(dto.BillTo);
        Assert.Equal("note", dto.Note);
        Assert.Equal("unset", dto.Channel);
        Assert.Empty(dto.Lines);
        Assert.Equal(0, dto.Lines.Capacity);
    }
}
