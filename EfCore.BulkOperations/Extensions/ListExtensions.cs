namespace EfCore.BulkOperations.Extensions;

internal static class ListExtensions
{
    internal static List<List<T>> ChunkSplit<T>(this List<T> list, int chunkSize)
    {
        var count = list.Count;
        var chunks = new List<List<T>>();
        for (var i = 0; i < count; i += chunkSize)
        {
            var chunk = list.GetRange(i, Math.Min(chunkSize, count - i));
            chunks.Add(chunk);
        }
        return chunks;
    }

    internal static void ForEachWithIndex<T>(this IEnumerable<T> enumerable, Action<T, int> handler)
    {
        var idx = 0;
        foreach (var item in enumerable)
            handler(item, idx++);
    }
}