using System.Runtime.InteropServices;
using LeccorWindowsHealthCenter.Monitoring.Native;

namespace LeccorWindowsHealthCenter.Monitoring.Pdh;

/// <summary>
/// Consulta PDH com contadores adicionados pelo nome em inglês (<c>PdhAddEnglishCounter</c>): funciona em
/// Windows de qualquer idioma (os nomes "Processor", "Memory"... são traduzidos no Windows pt-BR).
/// Contador inexistente na máquina (ex.: GPU Engine em VM) é simplesmente ignorado.
/// </summary>
internal sealed class PdhQuery : IDisposable
{
    private readonly Dictionary<string, IntPtr> _counters = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr _query;

    public PdhQuery()
    {
        var status = NativeMethods.PdhOpenQuery(null, IntPtr.Zero, out _query);
        if (status != 0)
        {
            throw new InvalidOperationException($"PdhOpenQuery falhou (0x{status:X8}).");
        }
    }

    /// <summary>Adiciona o contador; devolve false quando ele não existe nesta máquina.</summary>
    public bool TryAdd(string path)
    {
        var status = NativeMethods.PdhAddEnglishCounter(_query, path, IntPtr.Zero, out var counter);
        if (status != 0)
        {
            return false;
        }

        _counters[path] = counter;
        return true;
    }

    public bool Has(string path) => _counters.ContainsKey(path);

    public bool Collect() => _query != IntPtr.Zero && NativeMethods.PdhCollectQueryData(_query) == 0;

    /// <summary>Valor formatado; null quando o contador não existe ou ainda não tem dado válido (ex.: taxa na 1ª coleta).</summary>
    public double? Value(string path)
    {
        if (!_counters.TryGetValue(path, out var counter))
        {
            return null;
        }

        var status = NativeMethods.PdhGetFormattedCounterValue(counter, NativeMethods.PdhFmtDouble | NativeMethods.PdhFmtNoCap100, out _, out var value);
        return status == 0 && value.CStatus is NativeMethods.PdhCstatusValidData or NativeMethods.PdhCstatusNewData
            ? value.DoubleValue
            : null;
    }

    /// <summary>Valores por instância de um contador com curinga (*).</summary>
    public IReadOnlyList<KeyValuePair<string, double>> Values(string path)
    {
        if (!_counters.TryGetValue(path, out var counter))
        {
            return [];
        }

        const uint format = NativeMethods.PdhFmtDouble | NativeMethods.PdhFmtNoCap100;
        uint size = 0;
        var status = NativeMethods.PdhGetFormattedCounterArray(counter, format, ref size, out _, IntPtr.Zero);
        if (status != NativeMethods.PdhMoreData || size == 0)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            status = NativeMethods.PdhGetFormattedCounterArray(counter, format, ref size, out var count, buffer);
            if (status != 0)
            {
                return [];
            }

            var itemSize = Marshal.SizeOf<NativeMethods.PdhFmtCounterValueItem>();
            var result = new List<KeyValuePair<string, double>>((int)count);
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<NativeMethods.PdhFmtCounterValueItem>(buffer + (i * itemSize));
                if (item.Value.CStatus is NativeMethods.PdhCstatusValidData or NativeMethods.PdhCstatusNewData)
                {
                    result.Add(new(Marshal.PtrToStringUni(item.Name) ?? string.Empty, item.Value.DoubleValue));
                }
            }

            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            _ = NativeMethods.PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }
}
