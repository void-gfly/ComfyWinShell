using WpfDesktop.Models;

namespace WpfDesktop.Services;

/// <summary>
/// 根据 torch.cuda 顺序重排 GPU 快照。
/// </summary>
public static class CudaDeviceOrderHelper
{
    /// <summary>
    /// 按照 torch.cuda 的设备名称顺序重排 GPU 列表。
    /// </summary>
    /// <param name="gpus">硬件监控采集到的 GPU 列表。</param>
    /// <param name="cudaDevices">torch.cuda 顺序的设备名称列表。</param>
    /// <returns>按 torch.cuda 顺序排列后的 GPU 列表。</returns>
    public static List<GpuInfoSnapshot> OrderGpusByCudaDevices(
        IReadOnlyList<GpuInfoSnapshot> gpus,
        IReadOnlyList<CudaDeviceInfo> cudaDevices)
    {
        if (gpus.Count <= 1 || cudaDevices.Count == 0)
        {
            return gpus.ToList();
        }

        var indexedGpus = gpus
            .Select((gpu, index) => new IndexedGpu(index, gpu))
            .ToArray();

        var buckets = new Dictionary<string, Queue<IndexedGpu>>(StringComparer.OrdinalIgnoreCase);
        foreach (var indexedGpu in indexedGpus)
        {
            var key = NormalizeName(indexedGpu.Gpu.Name);
            if (!buckets.TryGetValue(key, out var queue))
            {
                queue = new Queue<IndexedGpu>();
                buckets[key] = queue;
            }

            queue.Enqueue(indexedGpu);
        }

        var ordered = new List<GpuInfoSnapshot>(gpus.Count);
        var consumed = new bool[indexedGpus.Length];

        foreach (var cudaDevice in cudaDevices)
        {
            var key = NormalizeName(cudaDevice.Name);
            if (!buckets.TryGetValue(key, out var queue) || queue.Count == 0)
            {
                continue;
            }

            var indexedGpu = queue.Dequeue();
            if (consumed[indexedGpu.Index])
            {
                continue;
            }

            consumed[indexedGpu.Index] = true;
            ordered.Add(indexedGpu.Gpu);
        }

        for (var i = 0; i < indexedGpus.Length; i++)
        {
            if (!consumed[i])
            {
                ordered.Add(indexedGpus[i].Gpu);
            }
        }

        return ordered;
    }

    private static string NormalizeName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
    }

    private sealed record IndexedGpu(int Index, GpuInfoSnapshot Gpu);
}
