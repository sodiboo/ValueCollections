using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ValueCollections.Benchmarks;

public class Program {
    public static void Main() {
        BenchmarkSwitcher.FromAssemblies(AppDomain.CurrentDomain.GetAssemblies()).Run();
        Console.ReadLine();
    }
}

[MemoryDiagnoser]
public class FixedSizeBenchmarks {
    [Benchmark]
    public int SmallListOfStruct() {
        List<int> list = [1, 2, 3, 4, 5];
        list.Add(6);
        list.Add(7);
        list.Add(8);
        return list.IndexOf(3);
    }
    [Benchmark]
    public int SmallValueListOfStruct() {
        using ValueList<int> list = [1, 2, 3, 4, 5];
        list.Add(6);
        list.Add(7);
        list.Add(8);
        return list.IndexOf(3);
    }
}

[MemoryDiagnoser]
public class ListBenchmarks {
    [Benchmark]
    public int SmallListOfStruct() {
        List<int> list = [1, 2, 3, 4, 5];
        return list.IndexOf(3);
    }
    [Benchmark]
    public int SmallValueListOfStruct() {
        using ValueList<int> list = [1, 2, 3, 4, 5];
        return list.IndexOf(3);
    }

    [Benchmark]
    public int SmallListOfClass() {
        List<string> list = ["1", "2", "3", "4", "5"];
        return list.IndexOf("3");
    }
    [Benchmark]
    public int SmallValueListOfClass() {
        using ValueList<string> list = ["1", "2", "3", "4", "5"];
        return list.IndexOf("3");
    }

    [Benchmark]
    public int LargeListOfStruct() {
        List<int> list = [];
        for (int i = 0; i < 10_000; i++) {
            list.Add(i);
        }
        return list.Count;
    }
    [Benchmark]
    public int LargeValueListOfStruct() {
        using ValueList<int> list = [];
        for (int i = 0; i < 10_000; i++) {
            list.Add(i);
        }
        return list.Count;
    }
}

[MemoryDiagnoser]
public class HashSetBenchmarks {
    [Benchmark]
    public bool SmallHashSetOfStruct() {
        HashSet<int> hashSet = [1, 2, 3, 4, 5];
        return hashSet.Contains(3);
    }
    [Benchmark]
    public bool SmallValueHashSetOfStruct() {
        using ValueHashSet<int> hashSet = [1, 2, 3, 4, 5];
        return hashSet.Contains(3);
    }

    [Benchmark]
    public bool SmallHashSetOfClass() {
        HashSet<string> hashSet = ["1", "2", "3", "4", "5"];
        return hashSet.Contains("3");
    }
    [Benchmark]
    public bool SmallValueHashSetOfClass() {
        using ValueHashSet<string> hashSet = ["1", "2", "3", "4", "5"];
        return hashSet.Contains("3");
    }

    [Benchmark]
    public int LargeHashSetOfStruct() {
        HashSet<int> hashSet = [];
        for (int i = 0; i < 10_000; i++) {
            hashSet.Add(i);
        }
        return hashSet.Count;
    }
    [Benchmark]
    public int LargeValueHashSetOfStruct() {
        using ValueHashSet<int> hashSet = [];
        for (int i = 0; i < 10_000; i++) {
            hashSet.Add(i);
        }
        return hashSet.Count;
    }
}

[MemoryDiagnoser]
public class DictionaryBenchmarks {
    [Benchmark]
    public bool SmallDictionaryOfStructs() {
        Dictionary<int, int> dictionary = new() { [1] = -1, [2] = -2, [3] = -3, [4] = -4, [5] = -5 };
        return dictionary.ContainsKey(3);
    }
    [Benchmark]
    public bool SmallValueDictionaryOfStructs() {
        using ValueDictionary<int, int> dictionary = new() { [1] = -1, [2] = -2, [3] = -3, [4] = -4, [5] = -5 };
        return dictionary.ContainsKey(3);
    }

    [Benchmark]
    public bool SmallDictionaryOfClasses() {
        Dictionary<string, string> dictionary = new() { ["1"] = "-1", ["2"] = "-2", ["3"] = "-3", ["4"] = "-4", ["5"] = "-5" };
        return dictionary.ContainsKey("3");
    }
    [Benchmark]
    public bool SmallValueDictionaryOfClasses() {
        using ValueDictionary<string, string> dictionary = new() { ["1"] = "-1", ["2"] = "-2", ["3"] = "-3", ["4"] = "-4", ["5"] = "-5" };
        return dictionary.ContainsKey("3");
    }

    [Benchmark]
    public int LargeDictionaryOfStructs() {
        Dictionary<int, int> dictionary = [];
        for (int i = 0; i < 10_000; i++) {
            dictionary.Add(i, -i);
        }
        return dictionary.Count;
    }
    [Benchmark]
    public int LargeValueDictionaryOfStructs() {
        using ValueDictionary<int, int> dictionary = [];
        for (int i = 0; i < 10_000; i++) {
            dictionary.Add(i, -i);
        }
        return dictionary.Count;
    }
}