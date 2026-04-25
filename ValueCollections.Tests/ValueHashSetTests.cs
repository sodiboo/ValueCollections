namespace ValueCollections.Tests;

public class ValueHashSetTests {
    [Fact]
    public void BasicTest() {
        using ValueHashSet<double> doubles = [1, 2, 3];
        doubles.Add(4);
        doubles.AddRange([5, 6]);
        doubles.ToList().ShouldBe([1, 2, 3, 4, 5, 6], ignoreOrder: true);
    }
    [Fact]
    public void ConstructorsTest() {
        new ValueHashSet<string>().ToList().ShouldBe([], ignoreOrder: true);
        new ValueHashSet<string>(capacity: 4).Capacity.ShouldBeGreaterThanOrEqualTo(4);
        new ValueHashSet<char>("abc").ToList().ShouldBe(['a', 'b', 'c'], ignoreOrder: true);
    }
    [Fact]
    public void UniqueTest() {
        using ValueHashSet<string> strings = [];
        strings.Add("abacus");
        strings.Add("banana");
        strings.Add("abacus");
        strings.Count.ShouldBe(2);
    }
    [Fact]
    public void AddTest() {
        using ValueHashSet<int> hashSet = new(capacity: 64);
        for (int i = 0; i < 100; i++) {
            hashSet.Add(i);
        }
        hashSet.Count.ShouldBe(100);
        hashSet.Capacity.ShouldBeGreaterThanOrEqualTo(hashSet.Count);
    }
    [Fact]
    public void RemoveTest() {
        using ValueHashSet<int> hashSet = [2, 4, 6];
        hashSet.TrimExcess();
        hashSet.Remove(2);
        hashSet.Count.ShouldBe(2);
        hashSet.ToHashSet().ShouldBe([4, 6]);
    }
    [Fact]
    public void WhereTest() {
        List<int> list = [1, 2, 3];
        list.ToValueHashSet().Where(num => num % 2 == 0).ToList().ShouldBe([2]);
    }
}