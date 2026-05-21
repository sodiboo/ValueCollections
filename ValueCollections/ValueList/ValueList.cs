using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ValueCollections;

/// <summary>
/// A version of <see cref="List{T}"/> which minimizes as many heap allocations as possible.
/// </summary>
/// <remarks>
/// You should dispose it after use to ensure the rented buffer is returned to the array pool.
/// </remarks>
public readonly partial struct ValueList<T> : IDisposable, IList<T>, IReadOnlyList<T> {
    private readonly BackingBuffer<InnerValueList> BackingBuffer = new();

    /// <summary>
    /// Constructs a value list with a default capacity of 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList() { }

    /// <summary>
    /// Whether these two ValueLists share the same backing buffer. That is, whether updates to `a` are reflected in `b`, and whether disposing `b` makes `a` unusable (and vice versa)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IdentityEquals(ValueList<T> a, ValueList<T> b) => BackingBuffer<InnerValueList>.IdentityEquals(a.BackingBuffer, b.BackingBuffer);

    /// <summary>
    /// Disposes the instance and returns the rented buffer to the array pool if needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => BackingBuffer.Dispose();

    private ref InnerValueList Inner {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref BackingBuffer.Data;
    }
    private struct InnerValueList : IDisposable {

        private T[]? RentedBuffer;
        public int PopulatedElements { get; set; }

        public readonly Span<T> Buffer => RentedBuffer is null ? [] : RentedBuffer.AsSpan();

        public int Capacity {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => RentedBuffer?.Length ?? 0;
            set {
                if (value == Capacity) return;

                var PrevBuffer = Interlocked.Exchange(ref RentedBuffer, value == 0 ? null : ArrayPool<T>.Shared.Rent(value));

                if (PrevBuffer is not null && RentedBuffer is not null) {
                    var src = PrevBuffer.AsSpan();
                    var dst = RentedBuffer.AsSpan();
                    var len = int.Min(int.Min(src.Length, dst.Length), PopulatedElements);
                    src[..len].CopyTo(dst[..len]);
                }

                if (PrevBuffer is not null) {
                    ArrayPool<T>.Shared.Return(PrevBuffer);
                }
            }
        }

        public void Dispose() {
            Buffer.Clear();
            Capacity = 0;
            this = default;
        }
    }

    private Span<T> Buffer => Inner.Buffer;

    /// <summary>
    /// Returns the current number of elements in the list.
    /// </summary>
    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Inner.PopulatedElements;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set => Inner.PopulatedElements = value;
    }

    /// <summary>
    /// Returns the current maximum capacity before the span must be resized.
    /// </summary>
    public int Capacity {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Inner.Capacity;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set => Inner.Capacity = value;
    }

    /// <summary>
    /// Constructs a value list with the given capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList(int capacity) {
        EnsureCapacity(capacity);
    }
    /// <summary>
    /// Constructs a value list with the given elements.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList(scoped ReadOnlySpan<T> initialElements) {
        AddRange(initialElements);
    }
    /// <summary>
    /// Constructs a value list with the given elements.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-1)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList(ReadOnlyMemory<T> initialElements) {
        AddRange(initialElements);
    }
    /// <summary>
    /// Constructs a value list with the given elements.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-4)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList(IEnumerable<T> initialElements) {
        AddRange(initialElements);
    }
    /// <summary>
    /// Constructs a value list with the given elements.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-2)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList(ValueList<T> initialElements) {
        AddRange(initialElements.Span);
    }
    /// <summary>
    /// Constructs a value list with the given elements.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-3)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList(ValueHashSet<T> initialElements) {
        AddRange(initialElements.AsSpan());
    }

    /// <inheritdoc/>
    readonly bool ICollection<T>.IsReadOnly {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }

    private Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Buffer[..Count];
    }

    /// <summary>
    /// Gets a span over the elements in the list.
    /// </summary>
    /// <remarks>
    /// Do not change the capacity of the list while the span is in use, because the span will continue pointing to the old buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => Span;

    /// <summary>
    /// Returns the element at the given index.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException"/>
    public readonly ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Span[index];
    }

    /// <summary>
    /// Returns the elements at the given range.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> Slice(int start, int end) => Span[start..end];

    /// <inheritdoc/>
    T IList<T>.this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => this[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this[index] = value;
    }

    /// <inheritdoc/>
    readonly T IReadOnlyList<T>.this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this[index];
    }

    /// <summary>
    /// Adds an element to the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T value) {
        EnsureCapacity(Count + 1);
        Buffer[Count] = value;
        Count++;
    }

    /// <summary>
    /// Adds multiple elements to the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(scoped ReadOnlySpan<T> values) {
        EnsureCapacity(Count + values.Length);
        values.CopyTo(Buffer[Count..]);
        Count += values.Length;
    }

    /// <summary>
    /// Adds multiple elements to the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-1)]
#endif
    public void AddRange(ReadOnlyMemory<T> values) {
        AddRange(values.Span);
    }

    /// <summary>
    /// Adds multiple elements to the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-2)]
#endif
    public void AddRange(IEnumerable<T> values) {
        if (values.TryGetNonEnumeratedCount(out int count)) {
            EnsureCapacity(Count + count);
            foreach (T value in values) {
                this[Count++] = value;
            }
        }
        else {
            foreach (T value in values) {
                Add(value);
            }
        }
    }

    /// <summary>
    /// Ensure's the list's capacity is at least <paramref name="newCapacity"/>, renting a larger buffer if not.<br/>
    /// This is useful when adding a predetermined number of items to the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int newCapacity) => Capacity = int.Max(Capacity, 1 << (int.Log2(newCapacity) + 1));

    /// <summary>
    /// Ensures the list's capacity is equal to its count, renting a smaller buffer if not.<br/>
    /// This is useful for reducing memory overhead when it is known that no more elements will be added to the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess() => Capacity = Count;

    /// <summary>
    /// Returns the index of <paramref name="value"/> or -1 if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T value) {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int index = 0; index < Count; index++) {
            if (comparer.Equals(Buffer[index], value)) {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns the index of the first value that matches the predicate, of -1 if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindIndex(Predicate<T> match) {
        for (int index = 0; index < Count; index++) {
            if (match(Buffer[index])) {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns the index of <paramref name="value"/> or -1 if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int LastIndexOf(T value) {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int index = Count - 1; index >= 0; index--) {
            if (comparer.Equals(Buffer[index], value)) {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns the index of the last value that matches the predicate, of -1 if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindLastIndex(Predicate<T> match) {
        for (int index = Count - 1; index >= 0; index--) {
            if (match(Buffer[index])) {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns whether <paramref name="value"/> is found in the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Contains(T value) {
        return IndexOf(value) >= 0;
    }

    /// <summary>
    /// Inserts <paramref name="value"/> at <paramref name="index"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, T value) {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Count);

        EnsureCapacity(Count + 1);
        Buffer[index..Count].CopyTo(Buffer[(index + 1)..]);
        Buffer[index] = value;
        Count++;
    }

    /// <summary>
    /// Removes an element at <paramref name="index"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAt(int index) {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        Buffer[(index + 1)..].CopyTo(Buffer[index..]);

        Count--;
        Buffer[Count] = default!;
    }

    /// <summary>
    /// Finds and removes the first instance of <paramref name="value"/> from the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T value) {
        int index = IndexOf(value);
        if (index < 0) {
            return false;
        }
        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes every element matching <paramref name="predicate"/> from the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RemoveWhere(Func<T, bool> predicate) {
        int counter = 0;
        int index = 0;
        while (index < Count) {
            if (predicate(Buffer[index])) {
                RemoveAt(index);
                counter++;
            }
            else {
                index++;
            }
        }
        return counter;
    }

    /// <summary>
    /// Removes every element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() {
        Span.Clear();
        Count = 0;
    }

    /// <summary>
    /// Sorts the elements using the default comparer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sort() {
        Span.Sort();
    }

    /// <summary>
    /// Sorts the elements using <paramref name="comparer"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sort<TComparer>(TComparer comparer) where TComparer : IComparer<T> {
        Span.Sort(comparer);
    }

    /// <summary>
    /// Copies every element to <paramref name="destination"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> destination) {
        Span.CopyTo(destination);
    }

    /// <summary>
    /// Copies every element to <paramref name="destination"/> at <paramref name="destinationIndex"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] destination, int destinationIndex) {
        CopyTo(destination.AsSpan(destinationIndex));
    }

    /// <summary>
    /// Returns an enumerator that iterates over the elements of the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Enumerator GetEnumerator() => new(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Enumerates the elements of a <see cref="ValueList{T}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<T> {
        private readonly ValueList<T> List;
        private int Index;

        /// <summary>
        /// Constructs a new enumerator over the elements of <paramref name="list"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ValueList<T> list) {
            List = list;
            Index = -1;
        }

        /// <summary>
        /// Returns the element at the current position of the list.
        /// </summary>
        public readonly T Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => List[Index];
        }

        /// <inheritdoc/>
        readonly object? IEnumerator.Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Current;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IDisposable.Dispose() {
        }

        /// <summary>
        /// Advances the enumerator to the next element of the list.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the enumerator successfully advanced to the next element; <see langword="false"/> if the enumerator reached the end of the list.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            Index++;
            return Index < List.Count;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the list.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() {
            Index = -1;
        }
    }
}