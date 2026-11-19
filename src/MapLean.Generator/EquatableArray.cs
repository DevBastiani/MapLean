using System.Collections.Immutable;

namespace MapLean.Generator;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items)
        => _items = items.IsDefault ? ImmutableArray<T>.Empty : items;

    public EquatableArray(IReadOnlyList<T> items)
    {
        if (items is null || items.Count == 0)
        {
            _items = ImmutableArray<T>.Empty;
            return;
        }

        var builder = ImmutableArray.CreateBuilder<T>(items.Count);
        for (var i = 0; i < items.Count; i++)
            builder.Add(items[i]);
        _items = builder.MoveToImmutable();
    }

    private ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public int Length => Items.Length;

    public T this[int index] => Items[index];

    public bool Equals(EquatableArray<T> other)
    {
        var left = Items;
        var right = other.Items;
        if (left.Length != right.Length)
            return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
        => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var items = Items;
        var hash = 17;
        for (var i = 0; i < items.Length; i++)
            hash = unchecked(hash * 31 + EqualityComparer<T>.Default.GetHashCode(items[i]));
        return hash;
    }
}
