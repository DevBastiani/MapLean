using MapLean.Graph;
using Xunit;

namespace MapLean.Tests;

public sealed class FrozenEqualityTests
{
    [Fact]
    public void Generated_matches_manual_on_the_frozen_graph()
    {
        AssertSame(FrozenGraph.Present());
        AssertSame(FrozenGraph.Absent());
    }

    private static void AssertSame(Order source)
    {
        var manual = ManualMap.Map(source);
        var generated = OrderMaps.Apply(source);
        Assert.Equal(manual.Id, generated.Id);
        Assert.Equal(manual.ShipTo, generated.ShipTo);
        Assert.Equal(manual.BillTo, generated.BillTo);
        Assert.Equal(manual.Note, generated.Note);
        Assert.Equal(manual.Channel, generated.Channel);
        Assert.Equal("unset", generated.Channel);
        Assert.Equal(manual.Lines.Count, generated.Lines.Count);
        Assert.Equal(manual.Lines.Capacity, generated.Lines.Capacity);
        for (var i = 0; i < manual.Lines.Count; i++)
            Assert.Equal(manual.Lines[i], generated.Lines[i]);
        source.Lines.Add(new LineItem("later", 3));
        Assert.Equal(manual.Lines.Count, generated.Lines.Count);
    }
}
