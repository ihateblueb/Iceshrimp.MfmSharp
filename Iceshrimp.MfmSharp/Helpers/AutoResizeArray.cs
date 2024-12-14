using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Iceshrimp.MfmSharp.Helpers;

/// <summary>
/// Modified List&lt;T&gt; that can go on the stack 
/// </summary>
[PublicAPI]
internal struct AutoResizeArray<T>(T[] existing)
{
	public AutoResizeArray() : this(EmptyArray) { }

	public static readonly AutoResizeArray<T> Default = new([]);

	private T[] _array = existing;
	private int _size  = existing.Length;
	public  int Count => _size;

	#pragma warning disable CA1825, IDE0300 // avoid the extra generic instantiation for Array.Empty<T>()
	// ReSharper disable once UseCollectionExpression
	private static readonly T[] EmptyArray = new T[0];
	#pragma warning restore CA1825, IDE0300

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public AutoResizeArray<T> Add(T item)
	{
		var array = _array;
		var size  = _size;
		if ((uint)size < (uint)array.Length)
		{
			_size       = size + 1;
			array[size] = item;
		}
		else
		{
			AddWithResize(item);
		}

		return this;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void AddWithResize(T item)
	{
		var size = _size;
		Grow(size + 1);
		_size        = size + 1;
		_array[size] = item;
	}

	public AutoResizeArray<T> AddRange(T[] items)
	{
		{
			var count = items.Length;
			if (count > 0)
			{
				if (_array.Length - _size < count)
					Grow(checked(_size + count));

				items.CopyTo(_array, _size);
				_size += count;
			}
		}

		return this;
	}

	public T[] AsArray(bool force = false) => !force && _size > 1000
		? ToArray() // Benchmarks say this is faster, why is beyond me
		: _array.Length == _size
			? _array
			: _array[.._size];

	public T[] ToArray() => _size == _array.Length ? _array.AsSpan().ToArray() : _array[.._size].AsSpan().ToArray();

	public void Trim() => Capacity = _size;

	public Span<T> AsSpan() => _array.AsSpan()[.._size];

	public T this[int index]
	{
		get => _array[index];
		set => _array[index] = value;
	}

	private const int DefaultCapacity = 4;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetNewCapacity(int capacity)
	{
		var newCapacity = _array.Length == 0 ? DefaultCapacity : 2 * _array.Length;

		if ((uint)newCapacity > Array.MaxLength)
			newCapacity = Array.MaxLength;
		if (newCapacity < capacity)
			newCapacity = capacity;

		return newCapacity;
	}

	private void Grow(int capacity) => Capacity = GetNewCapacity(capacity);

	public int Capacity
	{
		get => _array.Length;
		set
		{
			if (value == _array.Length)
				return;

			if (value > 0)
			{
				var newItems = new T[value];
				if (_size > 0) Array.Copy(_array, newItems, _size);

				_array = newItems;
			}
			else
			{
				_array = EmptyArray;
			}
		}
	}
}
