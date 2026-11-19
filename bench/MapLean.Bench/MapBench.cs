using AutoMapper;
using BenchmarkDotNet.Attributes;
using MapLean.Graph;
using Microsoft.Extensions.Logging.Abstractions;

namespace MapLean.Bench;

[MemoryDiagnoser]
public class MapBench
{
    private readonly Order _order = FrozenGraph.Present();
    private readonly IMapper _mapper;

    public MapBench()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Address, AddressDto>();
            cfg.CreateMap<LineItem, LineItemDto>();
            cfg.CreateMap<Order, OrderDto>().ForMember(dto => dto.Channel, opt => opt.Ignore());
        }, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    [GlobalSetup]
    public void Check()
    {
        var manual = Manual();
        var generated = MapLean();
        var auto = AutoMapper();
        if (!Same(manual, generated) || !Same(manual, auto))
            throw new InvalidOperationException("mapped output differs");
    }

    [Benchmark(Baseline = true)]
    public OrderDto Manual() => ManualMap.Map(_order);

    [Benchmark]
    public OrderDto MapLean() => OrderMaps.Apply(_order);

    [Benchmark]
    public OrderDto AutoMapper() => _mapper.Map<OrderDto>(_order);

    private static bool Same(OrderDto left, OrderDto right)
    {
        if (left.Id != right.Id || left.Note != right.Note || left.Channel != right.Channel)
            return false;
        if (left.ShipTo != right.ShipTo || left.BillTo != right.BillTo)
            return false;
        if (left.Lines.Count != right.Lines.Count)
            return false;
        for (var i = 0; i < left.Lines.Count; i++)
        {
            if (left.Lines[i] != right.Lines[i])
                return false;
        }

        return true;
    }
}
