namespace LeccorWindowsHealthCenter.Elevated;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? pipeName = null;
        string? token = null;
        var parentPid = 0;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--pipe", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                pipeName = args[++i];
            }
            else if (string.Equals(args[i], "--token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                token = args[++i];
            }
            else if (string.Equals(args[i], "--parent-pid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var pid))
            {
                parentPid = pid;
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(pipeName) || string.IsNullOrWhiteSpace(token) || parentPid <= 0)
        {
            return 1;
        }

        var server = new ElevatedServer(pipeName, token, parentPid);
        await server.RunAsync().ConfigureAwait(false);
        return 0;
    }
}

