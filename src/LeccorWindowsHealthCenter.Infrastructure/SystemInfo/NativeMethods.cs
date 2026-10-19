using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

/// <summary>
/// IOCTL_STORAGE_QUERY_PROPERTY e lixeira. O disco é aberto com acesso 0 (sem leitura/escrita): consultar propriedades
/// não exige administrador e não toca nos dados do disco.
/// </summary>
internal static class NativeMethods
{
    public const uint IoctlStorageQueryProperty = 0x002D1400;
    public const uint IoctlVolumeGetVolumeDiskExtents = 0x00560000;
    public const uint FileShareReadWrite = 0x00000003;
    public const uint OpenExisting = 3;

    // STORAGE_PROPERTY_ID
    public const int StorageDeviceSeekPenaltyProperty = 7;
    public const int StorageDeviceTrimProperty = 8;
    public const int StorageDeviceTemperatureProperty = 52;

    [StructLayout(LayoutKind.Sequential)]
    public struct ShQueryRbInfo
    {
        public int Size;
        public long SizeBytes;
        public long NumItems;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    public static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, byte[] inBuffer, int inSize, byte[] outBuffer, int outSize, out int bytesReturned, IntPtr overlapped);

    /// <summary>Caminho → PIDL do shell (liberar com <see cref="CoTaskMemFree"/>). Retorna HRESULT.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    /// <summary>Com cidl = 0 e o PIDL completo do item: abre a pasta-mãe com o item selecionado. Não executa nada.</summary>
    [DllImport("shell32.dll")]
    public static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr apidl, uint flags);

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(IntPtr pointer);

    /// <summary>Tamanho total da lixeira de todas as unidades (rootPath null). Retorna HRESULT.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHQueryRecycleBinW")]
    public static extern int ShQueryRecycleBin(string? rootPath, ref ShQueryRbInfo info);
}

/// <summary>Propriedades de um disco físico lidas por IOCTL. Null = o dispositivo/driver não informa.</summary>
internal static class StorageDeviceQuery
{
    public static bool? IncursSeekPenalty(int disk) => QueryFlag(disk, NativeMethods.StorageDeviceSeekPenaltyProperty);

    public static bool? TrimEnabled(int disk) => QueryFlag(disk, NativeMethods.StorageDeviceTrimProperty);

    /// <summary>STORAGE_TEMPERATURE_DATA_DESCRIPTOR: limites em 8/10 e a primeira leitura (TemperatureInfo[0].Temperature) em 26.</summary>
    public static (double? Current, double? Warning, double? Critical) Temperature(int disk)
    {
        var data = Query(disk, NativeMethods.StorageDeviceTemperatureProperty, 512);
        if (data is null || data.Length < 32 || BitConverter.ToUInt16(data, 12) == 0)
        {
            return (null, null, null);
        }

        return (Plausible(BitConverter.ToInt16(data, 26)), Plausible(BitConverter.ToInt16(data, 10)), Plausible(BitConverter.ToInt16(data, 8)));
    }

    /// <summary>
    /// Disco físico onde o volume começa (VOLUME_DISK_EXTENTS: contagem em 0, DiskNumber do primeiro trecho em 8).
    /// Não depende do WMI de armazenamento, que pode estourar o timeout quando outra consulta está em andamento.
    /// </summary>
    public static int? VolumeDiskNumber(char driveLetter)
    {
        using var handle = NativeMethods.CreateFile($@"\\.\{driveLetter}:", 0, NativeMethods.FileShareReadWrite, IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        var output = new byte[256];
        return NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlVolumeGetVolumeDiskExtents, [], 0, output, output.Length, out var returned, IntPtr.Zero)
               && returned >= 12 && BitConverter.ToUInt32(output, 0) > 0
            ? (int)BitConverter.ToUInt32(output, 8)
            : null;
    }

    /// <summary>Faixa física de um disco funcionando; fora dela é valor-sentinela do firmware (0, -273, 0x7FFF).</summary>
    internal static double? Plausible(short celsius) => celsius is > 0 and < 150 ? celsius : null;

    // DEVICE_SEEK_PENALTY_DESCRIPTOR / DEVICE_TRIM_DESCRIPTOR: Version, Size, BOOLEAN no offset 8.
    private static bool? QueryFlag(int disk, int property)
    {
        var data = Query(disk, property, 12);
        return data is { Length: >= 9 } ? data[8] != 0 : null;
    }

    private static byte[]? Query(int disk, int property, int outputSize)
    {
        using var handle = NativeMethods.CreateFile($@"\\.\PhysicalDrive{disk}", 0, NativeMethods.FileShareReadWrite, IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        // STORAGE_PROPERTY_QUERY: PropertyId, QueryType (0 = PropertyStandardQuery), AdditionalParameters[1].
        var input = new byte[12];
        BitConverter.GetBytes(property).CopyTo(input, 0);
        var output = new byte[outputSize];
        return NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlStorageQueryProperty, input, input.Length, output, output.Length, out var returned, IntPtr.Zero)
            ? output.AsSpan(0, returned).ToArray()
            : null;
    }
}
