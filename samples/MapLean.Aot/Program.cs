using MapLean.Graph;

var order = FrozenGraph.Present();
var manual = ManualMap.Map(order);
var generated = OrderMaps.Apply(order);
if (manual.Id != generated.Id || manual.Note != generated.Note || manual.Channel != generated.Channel)
    return 1;
if (manual.ShipTo != generated.ShipTo || manual.BillTo != generated.BillTo)
    return 1;
if (manual.Lines.Count != generated.Lines.Count)
    return 1;
for (var i = 0; i < manual.Lines.Count; i++)
{
    if (manual.Lines[i] != generated.Lines[i])
        return 1;
}

Console.WriteLine("ok");
return 0;
