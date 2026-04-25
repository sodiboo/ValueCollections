namespace ValueCollections.Tests;

public class ValueDictionaryTests {
    [Fact]
    public void BasicTest() {
        using ValueDictionary<float, double> doubles = new() {
            [1] = -1,
            [2] = -2,
            [3] = -3,
        };
        doubles.Add(4, -4);
        doubles.AddRange([new KeyValuePair<float, double>(5, -5), new KeyValuePair<float, double>(6, -6)]);
        doubles.ToList().ShouldBe([new(1, -1), new(2, -2), new(3, -3), new(4, -4), new(5, -5), new(6, -6)], ignoreOrder: true);
    }
    [Fact]
    public void ConstructorsTest() {
        new ValueDictionary<string, int>().ToList().ShouldBe([], ignoreOrder: true);
        new ValueDictionary<string, int>(capacity: 4).Capacity.ShouldBeGreaterThanOrEqualTo(4);
        new ValueDictionary<char, long>([new('a', 5L), new('X', 3L)]).ToList().ShouldBe([new('a', 5L), new('X', 3L)], ignoreOrder: true);
    }
    [Fact]
    public void UniqueTest() {
        using ValueDictionary<string, float> strings = [];
        strings.TryAdd("abacus", 3f);
        strings.TryAdd("banana", 123.5f);
        strings.TryAdd("abacus", -7f);
        strings.Count.ShouldBe(2);
    }
    [Fact]
    public void AddTest() {
        using ValueDictionary<int, bool> dictionary = new(capacity: 64);
        for (int i = 0; i < 100; i++) {
            dictionary.Add(i, i % 2 == 0);
        }
        dictionary.Count.ShouldBe(100);
        dictionary.Capacity.ShouldBeGreaterThanOrEqualTo(dictionary.Count);
    }
    [Fact]
    public void RemoveTest() {
        using ValueDictionary<int, bool> dictionary = new() {
            [2] = true,
            [4] = false,
            [6] = true,
        };
        dictionary.TrimExcess();
        dictionary.Remove(2);
        dictionary.Count.ShouldBe(2);
        dictionary.ToDictionary().ShouldBe(new() {
            [4] = false,
            [6] = true,
        });
    }
    [Fact]
    public void WhereTest() {
        List<KeyValuePair<int, char>> list = [new(1, 'a'), new(2, 'b'), new(3, 'c')];
        list.ToValueDictionary().Where(entry => entry.Key % 2 == 0).ToList().ShouldBe([new(2, 'b')]);
    }
}