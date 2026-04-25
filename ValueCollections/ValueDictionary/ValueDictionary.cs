using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ValueCollections;

/// <summary>
/// A version of <see cref="Dictionary{TKey, TValue}"/> which minimizes as many heap allocations as possible.
/// </summary>
/// <remarks>
/// You should dispose it after use to ensure the rented buffer is returned to the array pool.
/// </remarks>
public readonly partial struct ValueDictionary<TKey, TValue> : IDisposable, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> {
    private readonly BackingBuffer<InnerValueDictionary> BackingBuffer = new();

    /// <summary>
    /// Constructs a value dictionary with a default capacity of 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary() { }

    /// <summary>
    /// Disposes the instance and returns the rented buffers to the array pool if needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => BackingBuffer.Dispose();
    // {
    //     if (RentedBuffer is not null) {
    //         ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(RentedBuffer);
    //     }
    //     if (RentedHashCodes is not null) {
    //         ArrayPool<int>.Shared.Return(RentedHashCodes);
    //     }
    //     this = default;
    // }

    private ref InnerValueDictionary Inner {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref BackingBuffer.Data;
    }

    private struct InnerValueDictionary : IDisposable {

        public IEqualityComparer<TKey> Comparer { readonly get => field ?? EqualityComparer<TKey>.Default; set; }
        private KeyValuePair<TKey, TValue>[]? RentedBuffer;
        public int PopulatedElements { get; set; }
        private int[]? RentedHashCodes;

        public readonly Span<KeyValuePair<TKey, TValue>> Buffer => RentedBuffer is null ? [] : RentedBuffer.AsSpan();
        public readonly Span<int> HashCodes => RentedHashCodes is null ? [] : RentedHashCodes.AsSpan();

        public int Capacity {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => RentedBuffer?.Length ?? 0;
            set {
                if (value == Capacity) {
                    return;
                }

                {
                    var PrevBuffer = Interlocked.Exchange(ref RentedBuffer, value == 0 ? null : ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(value));

                    if (PrevBuffer is not null && RentedBuffer is not null) {
                        var src = PrevBuffer.AsSpan();
                        var dst = RentedBuffer.AsSpan();
                        var len = int.Min(int.Min(src.Length, dst.Length), PopulatedElements);
                        src[..len].CopyTo(dst[..len]);
                    }

                    if (PrevBuffer is not null) {
                        ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(PrevBuffer);
                    }
                }

                {
                    var PrevHashCodes = Interlocked.Exchange(ref RentedHashCodes, value == 0 ? null : ArrayPool<int>.Shared.Rent(value));

                    if (PrevHashCodes is not null && RentedHashCodes is not null) {
                        var src = PrevHashCodes.AsSpan();
                        var dst = RentedHashCodes.AsSpan();
                        var len = int.Min(int.Min(src.Length, dst.Length), PopulatedElements);
                        src[..len].CopyTo(dst[..len]);
                    }

                    if (PrevHashCodes is not null) {
                        ArrayPool<int>.Shared.Return(PrevHashCodes);
                    }
                }

            }

        }

        public void Dispose() {
            Buffer.Clear();
            Capacity = 0;
            this = default;
        }
    }

    private Span<KeyValuePair<TKey, TValue>> Buffer => Inner.Buffer;
    private Span<int> HashCodesBuffer => Inner.HashCodes;

    /// <summary>
    /// Returns the current number of entries in the dictionary.
    /// </summary>
    public readonly int Count {
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
    /// A comparer used to differentiate the keys of the hash set.
    /// </summary>
    public IEqualityComparer<TKey> Comparer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Inner.Comparer;
    }

    /// <inheritdoc/>
    readonly bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }

    private Span<KeyValuePair<TKey, TValue>> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Buffer[..Count];
    }

    /// <summary>
    /// Gets a span over the entries in the dictionary.
    /// </summary>
    /// <remarks>
    /// Do not change the capacity of the dictionary while the span is in use, because the span will continue pointing to the old buffer.<br/>
    /// The span is read-only to ensure the entries are synchronized with the hash codes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<KeyValuePair<TKey, TValue>> AsSpan() => Buffer[..Count];

    /// <summary>
    /// Constructs a value dictionary with the given capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary(int capacity) {
        EnsureCapacity(capacity);
    }
    /// <summary>
    /// Constructs a value dictionary with the given entries.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> initialEntries) {
        AddRange(initialEntries);
    }
    /// <summary>
    /// Constructs a value dictionary with the given entries.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-1)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary(ReadOnlyMemory<KeyValuePair<TKey, TValue>> initialEntries) {
        AddRange(initialEntries);
    }
    /// <summary>
    /// Constructs a value dictionary with the given entries.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-5)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary(IEnumerable<KeyValuePair<TKey, TValue>> initialEntries) {
        AddRange(initialEntries);
    }
    /// <summary>
    /// Constructs a value dictionary with the given entries.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-2)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary(ValueDictionary<TKey, TValue> initialEntries) {
        AddRange(initialEntries.AsSpan());
    }
    /// <summary>
    /// Constructs a value dictionary with the given entries.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-3)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary(ValueList<KeyValuePair<TKey, TValue>> initialEntries) {
        AddRange(initialEntries.AsSpan());
    }
    /// <summary>
    /// Constructs a value dictionary with the given entries.
    /// </summary>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-4)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueDictionary(ValueHashSet<KeyValuePair<TKey, TValue>> initialEntries) {
        AddRange(initialEntries.AsSpan());
    }

    /// <summary>
    /// Returns the value at the given key.
    /// </summary>
    /// <exception cref="KeyNotFoundException"/>
    public TValue this[TKey key] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get {
            if (TryGetValue(key, out TValue? value)) {
                return value;
            }
            else {
                throw new KeyNotFoundException("The key was not found");
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            if (TryFindIndex(key, out int index)) {
                Buffer[index] = new KeyValuePair<TKey, TValue>(key, value);
            }
            else {
                Insert(index, new KeyValuePair<TKey, TValue>(key, value));
            }
        }
    }

    /// <inheritdoc/>
    TValue IDictionary<TKey, TValue>.this[TKey key] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => this[key];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this[key] = value;
    }

    /// <inheritdoc/>
    readonly TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this[key];
    }

    /// <summary>
    /// Adds an entry to the dictionary, throwing if the key is already present.
    /// </summary>
    /// <exception cref="ArgumentException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(KeyValuePair<TKey, TValue> entry) {
        if (!TryAdd(entry.Key, entry.Value)) {
            throw new ArgumentException("Tried to add duplicate key");
        }
    }

    /// <summary>
    /// Adds an entry to the dictionary, throwing if the key is already present.
    /// </summary>
    /// <exception cref="ArgumentException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value) {
        Add(new KeyValuePair<TKey, TValue>(key, value));
    }

    /// <summary>
    /// Adds an entry to the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(KeyValuePair<TKey, TValue> entry) {
        if (TryFindIndex(entry.Key, out int index)) {
            return false;
        }
        Insert(index, new KeyValuePair<TKey, TValue>(entry.Key, entry.Value));
        return true;
    }

    /// <summary>
    /// Adds an entry to the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TKey key, TValue value) {
        return TryAdd(new KeyValuePair<TKey, TValue>(key, value));
    }

    /// <summary>
    /// Adds multiple entries to the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> entries) {
        EnsureCapacity(Count + entries.Length);
        foreach (KeyValuePair<TKey, TValue> entry in entries) {
            Add(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Adds multiple entries to the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-1)]
#endif
    public void AddRange(ReadOnlyMemory<KeyValuePair<TKey, TValue>> entries) {
        AddRange(entries.Span);
    }

    /// <summary>
    /// Adds multiple entries to the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(-2)]
#endif
    public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> entries) {
        if (entries.TryGetNonEnumeratedCount(out int count)) {
            EnsureCapacity(Count + count);
            foreach (KeyValuePair<TKey, TValue> entry in entries) {
                Add(entry.Key, entry.Value);
            }
        }
        else {
            foreach (KeyValuePair<TKey, TValue> entry in entries) {
                Add(entry.Key, entry.Value);
            }
        }
    }

    /// <summary>
    /// Ensure's the dictionary's capacity is at least <paramref name="newCapacity"/>, renting a larger buffer if not.<br/>
    /// This is useful when adding a predetermined number of entries to the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int newCapacity) => Capacity = int.Max(Capacity, 1 << (int.Log2(newCapacity) + 1));

    /// <summary>
    /// Ensures the dictionary's capacity is equal to its count, renting a smaller buffer if not.<br/>
    /// This is useful for reducing memory overhead when it is known that no more entries will be added to the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess() => Capacity = Count;

    /// <summary>
    /// Returns the value at <paramref name="key"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value) {
        if (TryFindIndex(key, out int index)) {
            value = Buffer[index].Value!;
            return true;
        }
        else {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Returns whether <paramref name="entry"/> is found in the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Contains(KeyValuePair<TKey, TValue> entry) {
        return TryGetValue(entry.Key, out TValue? value) && EqualityComparer<TValue>.Default.Equals(value, entry.Value);
    }

    /// <summary>
    /// Returns whether <paramref name="key"/> is found in the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsKey(TKey key) {
        return TryFindIndex(key, out _);
    }

    /// <summary>
    /// Returns whether <paramref name="value"/> is found in the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsValue(TValue value) {
        EqualityComparer<TValue> comparer = EqualityComparer<TValue>.Default;
        for (int index = 0; index < Count; index++) {
            if (comparer.Equals(Buffer[index].Value, value)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes <paramref name="key"/> from the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key, [NotNullWhen(true)] out TValue? value) {
        if (TryFindIndex(key, out int index)) {
            value = Buffer[index].Value!;
            RemoveAt(index);
            return true;
        }
        else {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Removes <paramref name="key"/> from the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key) {
        if (TryFindIndex(key, out int index)) {
            RemoveAt(index);
            return true;
        }
        else {
            return false;
        }
    }

    /// <summary>
    /// Removes <paramref name="entry"/> from the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(KeyValuePair<TKey, TValue> entry) {
        if (TryFindIndex(entry.Key, out int index)) {
            EqualityComparer<TValue> comparer = EqualityComparer<TValue>.Default;
            if (comparer.Equals(Buffer[index].Value, entry.Value)) {
                RemoveAt(index);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes every element matching <paramref name="predicate"/> from the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RemoveWhere(Func<KeyValuePair<TKey, TValue>, bool> predicate) {
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
    /// Removes every entry.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() {
        Span.Clear();
        Count = 0;
    }

    /// <summary>
    /// Copies every entry to <paramref name="destination"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> destination) {
        Span.CopyTo(destination);
    }

    /// <summary>
    /// Copies every entry to <paramref name="destination"/> at <paramref name="destinationIndex"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(KeyValuePair<TKey, TValue>[] destination, int destinationIndex) {
        CopyTo(destination.AsSpan(destinationIndex));
    }

    /// <summary>
    /// Returns an enumerator that iterates over the elements of the dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
        return ((IEnumerable<KeyValuePair<TKey, TValue>>)this.ToArray()).GetEnumerator();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly IEnumerator IEnumerable.GetEnumerator() {
        return this.ToArray().GetEnumerator();
    }

    /// <summary>
    /// Returns a hash set of keys in the dictionary.
    /// </summary>
    public readonly ValueList<TKey> Keys {
        get => Select(entry => entry.Key);
    }

    /// <inheritdoc/>
    readonly ICollection<TKey> IDictionary<TKey, TValue>.Keys {
        get => Keys.ToList();
    }

    /// <inheritdoc/>
    readonly IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys {
        get => Keys.ToList();
    }

    /// <summary>
    /// Returns a hash set of keys in the dictionary.
    /// </summary>
    public readonly ValueList<TValue> Values {
        get => Select(entry => entry.Value);
    }

    /// <inheritdoc/>
    readonly ICollection<TValue> IDictionary<TKey, TValue>.Values {
        get => Values.ToList();
    }

    /// <inheritdoc/>
    readonly IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values {
        get => Values.ToList();
    }

    /// <summary>
    /// Calculates a hash code for <paramref name="key"/> using <see cref="Comparer"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly int GetHashCode(TKey? key) {
        return key is null ? 0 : Comparer.GetHashCode(key);
    }

    /// <summary>
    /// Returns the index of <paramref name="key"/> or -1 if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly bool TryFindIndex(TKey key, out int index) {
        int hashCode = GetHashCode(key);

        int startIndex = HashCodesBuffer[..Count].BinarySearch(hashCode);
        if (startIndex < 0) {
            startIndex = ~startIndex;
        }
        while (startIndex > 0 && HashCodesBuffer[startIndex - 1] == hashCode) {
            startIndex--;
        }

        for (index = startIndex; index < Count; index++) {
            int existingHashCode = HashCodesBuffer[index];
            if (existingHashCode != hashCode) {
                break;
            }
            KeyValuePair<TKey, TValue> existingEntry = Buffer[index];
            if (Comparer.Equals(existingEntry.Key, key)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Inserts <paramref name="entry"/> at <paramref name="index"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Insert(int index, KeyValuePair<TKey, TValue> entry) {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Count);

        int hashCode = GetHashCode(entry.Key);

        EnsureCapacity(Count + 1);
        Buffer[index..Count].CopyTo(Buffer[(index + 1)..]);
        HashCodesBuffer[index..Count].CopyTo(HashCodesBuffer[(index + 1)..]);
        Buffer[index] = entry;
        HashCodesBuffer[index] = hashCode;
        Count++;
    }

    /// <summary>
    /// Removes an entry at <paramref name="index"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveAt(int index) {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        Buffer[(index + 1)..].CopyTo(Buffer[index..]);
        HashCodesBuffer[(index + 1)..].CopyTo(HashCodesBuffer[index..]);

        Count--;
        Buffer[Count] = default!;
        HashCodesBuffer[Count] = default!;
    }

    /// <summary>
    /// Enumerates the entries of a <see cref="ValueList{T}"/>.
    /// </summary>
    public ref struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>> {
        private readonly ValueDictionary<TKey, TValue> Dictionary;
        private int Index;

        /// <summary>
        /// Constructs a new enumerator over the entries of <paramref name="dictionary"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ValueDictionary<TKey, TValue> dictionary) {
            Dictionary = dictionary;
            Index = -1;
        }

        /// <summary>
        /// Returns the element at the current position of the dictionary.
        /// </summary>
        public readonly KeyValuePair<TKey, TValue> Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Dictionary.Buffer[Index];
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
        /// Advances the enumerator to the next element of the dictionary.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the enumerator successfully advanced to the next element; <see langword="false"/> if the enumerator reached the end of the dictionary.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            Index++;
            return Index < Dictionary.Count;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the dictionary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() {
            Index = -1;
        }
    }
}
