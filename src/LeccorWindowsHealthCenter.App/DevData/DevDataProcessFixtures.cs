#if DEBUG
using System.Text;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.App.DevData;

/// <summary>
/// Pior caso realista das telas Processos e Inicialização (break-ui). Nomes, caminhos, fabricantes e linhas de comando
/// seguem padrões reais do Windows (Squirrel/Teams, OneDrive por SID, NVIDIA DriverStore, instaladores NSIS em %TEMP%,
/// PowerShell com -EncodedCommand). Domínios só em example.com.
/// </summary>
public static class DevDataProcessFixtures
{
    private static readonly DateTimeOffset Boot = DateTimeOffset.UtcNow.AddDays(-23).AddHours(-7);
    private const string User = @"C:\Users\Aleksandra Wiśniewska-Kowalczyk";

    private const string Intune = @"C:\Program Files (x86)\Microsoft Intune Management Extension\Microsoft.Management.Services.IntuneWindowsAgent.exe";
    private const string Erp = User + @"\AppData\Local\Programs\northwind-industries-holdings-portal-do-cliente\resources\app.asar.unpacked\node_modules\@northwind-industries-holdings\native-bridge\bin\win32-x64\Integração Contábil — Escritório São João.exe";
    private const string Chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private const string GitSh = @"C:\Program Files\Git\usr\bin\sh.exe";
    private const string Razer = @"C:\Program Files (x86)\Razer\Synapse3\Service\Razer Synapse Service.exe";
    private const string Armoury = @"C:\Program Files\ASUS\ArmouryDevice\dll\ArmourySocketServer\ArmourySocketServer.exe";
    private const string Qq = @"C:\Program Files\Tencent\QQNT\QQ.exe";
    private const string Code = User + @"\AppData\Local\Programs\Microsoft VS Code Insiders\Code - Insiders.exe";
    private const string Node = @"C:\Program Files\nodejs\node.exe";
    private const string TempUpdater = User + @"\AppData\Local\Temp\nsx4F2A.tmp\northwind-updater.exe";
    private const string NvTmRep = @"C:\Windows\System32\DriverStore\FileRepository\nvltsi.inf_amd64_5b2f0e4c8d1a7e39\NvTmRep.exe";
    private const string Ollama = User + @"\AppData\Local\Programs\Ollama\ollama app.exe";

    private sealed record FakeProcess(
        int Pid, int Parent, string Name, string? Path, double CpuShare, long? Private, int Threads, int Handles,
        string? User = null, double IoPerSecond = 0, bool Protected = false);

    private static readonly FakeProcess[] WorstProcesses =
    [
        new(213456, 1180, "Microsoft.Management.Services.IntuneWindowsAgent", Intune, 0.42, 412_345_678, 64, 2_871, @"NT AUTHORITY\SYSTEM", 3_456_789),
        new(4, 0, "System", null, 0, null, 312, 284_337, Protected: true),
        new(5892, 1180, "MsMpEng", null, 1.0, null, 97, 2_118, Protected: true),
        new(48123, 99999, "Integração Contábil — Escritório São João", Erp, 0.18, 2_345_678_901, 88, 4_102, @"AzureAD\AleksandraWiśniewska-Kowalczyk", 1_284_000_000),
        new(7, 4321, "sh", GitSh, 0.03, 3_145_728, 3, 112, @"AzureAD\AleksandraWiśniewska-Kowalczyk"),
        new(31337, 1180, "Razer Synapse Service", Razer, 0.02, 1_073_741_824, 4_096, 1_284_337, @"NT AUTHORITY\SYSTEM", 512),
        new(61234, 1180, "vmmem", null, 0.11, 32L << 30, 34, 1_337, Protected: true),
        new(40001, 22222, "ArmourySocketServer", Armoury, 0.004, 26_214_400, 89, 1_049, @"NT AUTHORITY\SYSTEM"),
        new(40002, 22222, "QQ", Qq, 0.006, 612_000_000, 71, 2_340, @"AzureAD\AleksandraWiśniewska-Kowalczyk", 98_304),
        new(40003, 22222, "Code - Insiders", Code, 0.02, 987_654_321, 41, 1_802, @"AzureAD\AleksandraWiśniewska-Kowalczyk"),
        new(40004, 22222, "chrome", Chrome, 0.05, 1_932_735_283, 52, 3_071, @"AzureAD\AleksandraWiśniewska-Kowalczyk", 2_621_440),
        new(1180, 912, "services", null, 0.001, null, 9, 812, Protected: true),
        new(912, 0, "wininit", null, 0, null, 1, 170, Protected: true),
        new(22222, 9001, "explorer", @"C:\Windows\explorer.exe", 0.009, 512_000_000, 206, 6_783, @"AzureAD\AleksandraWiśniewska-Kowalczyk"),
        new(51515, 22222, "northwind-updater", TempUpdater, 0.001, 14_680_064, 6, 221, @"AzureAD\AleksandraWiśniewska-Kowalczyk"),
    ];

    /// <summary>Cadeia real de "npm run dev": powershell → npm → node → … (14 níveis, mais que o recuo máximo de 12).</summary>
    private static IEnumerable<FakeProcess> DeepTree()
    {
        var names = new[] { "powershell", "cmd", "node", "node", "cmd", "node", "esbuild", "node", "cmd", "node", "node", "cmd", "node", "turbopack-dev-server-worker" };
        for (var i = 0; i < names.Length; i++)
        {
            yield return new FakeProcess(70_000 + i, i == 0 ? 40003 : 70_000 + i - 1, names[i], Node, 0.0005 * i, 40_000_000 + (i * 7_000_000L), 11, 210,
                @"AzureAD\AleksandraWiśniewska-Kowalczyk");
        }
    }

    public static ProcessInventorySnapshot Processes(DevDataMode mode, int logicalProcessors, IReadOnlySet<int> ended)
    {
        var list = mode switch
        {
            DevDataMode.Empty => [],
            DevDataMode.One => [new FakeProcess(9124, 22222, "LeccorWindowsHealthCenter", @"C:\Program Files\Leccor\LeccorWindowsHealthCenter.exe", 0.004, 131_000_000, 27, 789, @"AzureAD\AleksandraWiśniewska-Kowalczyk")],
            // Máquina de desenvolvimento pesada: ~1.500 processos (dezenas de abas, extensões, builds, contêineres).
            DevDataMode.Many => Enumerable.Range(0, 1_500)
                .Select(i => new FakeProcess(100_000 + i, 22222, (i % 4) switch { 0 => "chrome", 1 => "msedgewebview2", 2 => "node", _ => "svchost" },
                    (i % 4) switch { 0 => Chrome, 2 => Node, _ => null }, 0.0002 * (i % 9), 20_000_000 + (i * 911_000L), 12 + (i % 30), 150 + (i % 900),
                    Protected: i % 4 is 1 or 3))
                .Concat(WorstProcesses).Concat(DeepTree()).ToArray(),
            _ => WorstProcesses.Concat(DeepTree()).ToArray(),
        };

        var now = DateTimeOffset.UtcNow;
        var elapsed = now - Boot;
        return new ProcessInventorySnapshot(now, list.Where(p => !ended.Contains(p.Pid)).Select(p => new ProcessDetails
        {
            Pid = p.Pid,
            ParentPid = p.Parent,
            Name = p.Name,
            ExecutablePath = p.Path,
            User = p.User,
            PrivateBytes = p.Private,
            WorkingSetBytes = p.Private / 2,
            ThreadCount = p.Threads,
            HandleCount = p.Handles,
            // Contadores crescem com o relógio: duas fotografias dão a taxa desejada.
            TotalProcessorTime = p.Protected ? null : TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds * logicalProcessors * p.CpuShare),
            IoBytes = p.Protected ? null : (long)(elapsed.TotalSeconds * p.IoPerSecond),
            StartTime = p.Protected ? null : Boot.AddMinutes(p.Pid % 997),
        }).ToList());
    }

    public static FileIdentity Identity(string path) => path switch
    {
        Intune => Signed(path, "Microsoft Corporation", "Microsoft Intune Management Extension"),
        Erp => new FileIdentity
        {
            Path = path,
            Exists = true,
            Publisher = "Northwind Industries Holdings Participações e Empreendimentos S.A.",
            Description = "Integração Contábil — sincronização de lançamentos, notas fiscais eletrônicas (NF-e/NFS-e) e conciliação bancária",
            Version = "24.11.0.3187",
            Signature = SignatureStatus.Invalid,
        },
        TempUpdater => new FileIdentity { Path = path, Exists = true, Publisher = "Northwind Industries Holdings", Description = "northwind-updater", Signature = SignatureStatus.Invalid },
        GitSh => new FileIdentity { Path = path, Exists = true, Signature = SignatureStatus.NoSignature },
        Razer => Signed(path, "Razer Inc.", "Razer Synapse Service"),
        Armoury => new FileIdentity { Path = path, Exists = true, Publisher = "TODO: <公司名称>", Description = "TODO: <文件说明>", Signature = SignatureStatus.Valid, Signer = "ASUSTeK COMPUTER INC." },
        Qq => Signed(path, "Tencent", "QQ", signer: "深圳市腾讯计算机系统有限公司"),
        Code => Signed(path, "Microsoft Corporation", "Visual Studio Code - Insiders"),
        Chrome => Signed(path, "Google LLC", "Google Chrome"),
        Node => Signed(path, "Node.js", "Node.js JavaScript Runtime", signer: "OpenJS Foundation"),
        NvTmRep => Signed(path, "NVIDIA Corporation", "NVIDIA Crash Report"),
        @"C:\Windows\explorer.exe" => new FileIdentity { Path = path, Exists = true, Publisher = "Microsoft Corporation", Description = "Windows Explorer", Signature = SignatureStatus.SignedByCatalog },
        Ollama => new FileIdentity { Path = path, Exists = true, Signature = SignatureStatus.NoSignature },
        _ when path.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase) =>
            new FileIdentity { Path = path, Exists = true, Publisher = "Microsoft Corporation", Signature = SignatureStatus.SignedByCatalog },
        _ => new FileIdentity { Path = path, Exists = false },
    };

    private static FileIdentity Signed(string path, string publisher, string description, string? signer = null) => new()
    {
        Path = path,
        Exists = true,
        Publisher = publisher,
        Description = description,
        Signature = SignatureStatus.Valid,
        Signer = signer ?? publisher,
    };

    // ---------------- Inicialização ----------------

    private const string RunKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyMachine = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Script real de "baixar e executar" que instaladores (e malware) gravam em RunOnce, codificado em UTF-16 como o PowerShell exige.</summary>
    private static string EncodedPowerShell()
    {
        const string script = "$ErrorActionPreference='SilentlyContinue';$d=Join-Path $env:LOCALAPPDATA 'Northwind\\Integracao';"
            + "New-Item -ItemType Directory -Force -Path $d|Out-Null;$u='https://updates.example.com/northwind-industries-holdings/integracao-contabil/stable/win-x64/latest.json';"
            + "$m=Invoke-RestMethod -Uri $u -UseBasicParsing;$f=Join-Path $d ('setup-'+$m.version+'.exe');"
            + "Invoke-WebRequest -Uri $m.url -OutFile $f -UseBasicParsing;if((Get-FileHash $f -Algorithm SHA256).Hash -eq $m.sha256){"
            + "Start-Process -FilePath $f -ArgumentList '/S','/channel=stable','/D='+$d -WindowStyle Hidden -Wait};"
            + "Remove-Item $f -Force;Register-ScheduledTask -TaskName 'Northwind Integracao Contabil Updater' -Force|Out-Null";
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }

    private static StartupEntry Entry(string name, StartupSourceKind source, string location, string? command, string? exe, bool? exists, bool? enabled,
        string? detail = null, bool windows = false, bool currentUser = true) => new()
    {
        Id = $"{source}|{location}|{name}",
        Name = name,
        Source = source,
        Location = location,
        CurrentUserOnly = currentUser,
        Command = command,
        ExecutablePath = exe,
        ExecutableExists = exists,
        Enabled = enabled,
        StateDetail = detail,
        IsWindowsComponent = windows,
    };

    private static List<StartupEntry> WorstEntries()
    {
        const string sid = "S-1-5-21-3623811015-3361044348-30300820-1013";
        return
        [
            Entry("GoogleChromeAutoLaunch_E9CC7D9444E20242CFEDF4753656D75C", StartupSourceKind.RegistryRun, RunKey,
                $"\"{Chrome}\" --no-startup-window /prefetch:5", Chrome, true, true),
            // Teams clássico desinstalado deixa este Run para trás (caso real e comum).
            Entry("com.squirrel.Teams.Teams", StartupSourceKind.RegistryRun, RunKey,
                $"{User}\\AppData\\Local\\Microsoft\\Teams\\Update.exe --processStart \"Teams.exe\" --process-start-args \"--system-initiated\"",
                $@"{User}\AppData\Local\Microsoft\Teams\Update.exe", false, true),
            Entry("Northwind Updater", StartupSourceKind.RegistryRun, RunKey,
                $"\"{TempUpdater}\" --silent --channel=stable --install-dir=\"{User}\\AppData\\Local\\Programs\\northwind-industries-holdings-portal-do-cliente\" --telemetry-endpoint=https://telemetry.example.com/v2/northwind-industries-holdings/installations/9f8e7d6c-5b4a-4c3d-8e2f-1a0b9c8d7e6f",
                TempUpdater, true, true),
            // RunOnce com PowerShell codificado: o executável é do Windows, então o provedor real o marca como componente do Windows.
            Entry("Northwind Pós-instalação", StartupSourceKind.RegistryRunOnce, @"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce",
                $@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand {EncodedPowerShell()}",
                @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", true, true, "roda uma vez no próximo logon", windows: true),
            Entry("Integração Contábil — Escritório São João (sincronização automática de lançamentos)", StartupSourceKind.StartupFolder,
                @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp", $"\"{Erp}\" --background", Erp, true, true, currentUser: false),
            Entry("Ollama", StartupSourceKind.StartupFolder, $@"{User}\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup",
                $"\"{Ollama}\"", Ollama, true, false, "desativado no Gerenciador de Tarefas"),
            Entry("QQ", StartupSourceKind.RegistryRun, RunKey, $"\"{Qq}\" /background", Qq, true, true),
            // Comando que não dá para interpretar (aspas não fechadas): caminho e existência desconhecidos.
            Entry("LegacyScanAgent", StartupSourceKind.RegistryRun, RunKeyMachine, "\"%ProgramFiles%\\Legacy Scan Agent\\agent", null, null, null, currentUser: false),
            Entry($"OneDrive Reporting Task-{sid}", StartupSourceKind.ScheduledTask, $@"\OneDrive Reporting Task-{sid}",
                $@"{User}\AppData\Local\Microsoft\OneDrive\OneDriveStandaloneUpdater.exe /reporting", $@"{User}\AppData\Local\Microsoft\OneDrive\OneDriveStandaloneUpdater.exe", true, true),
            Entry("NvTmRep_CrashReport1_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}", StartupSourceKind.ScheduledTask, @"\NvTmRep_CrashReport1_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}",
                $"{NvTmRep} --crash-report", NvTmRep, true, false, "tarefa desativada"),
            Entry("Adobe Acrobat Update Task", StartupSourceKind.ScheduledTask, @"\Adobe Acrobat Update Task",
                @"C:\Program Files (x86)\Common Files\Adobe\ARM\1.0\AdobeARM.exe", @"C:\Program Files (x86)\Common Files\Adobe\ARM\1.0\AdobeARM.exe", false, true),
            Entry("BatteryGauge-Logon", StartupSourceKind.ScheduledTask, @"\Lenovo\ImController\Plugins\LenovoBatteryGaugePackage\BatteryGauge-Logon",
                @"C:\ProgramData\Lenovo\ImController\Plugins\LenovoBatteryGaugePackage\x64\BGHelper.exe", @"C:\ProgramData\Lenovo\ImController\Plugins\LenovoBatteryGaugePackage\x64\BGHelper.exe", true, null),
            Entry("Proxy", StartupSourceKind.ScheduledTask, @"\Microsoft\Windows\Autochk\Proxy",
                "%windir%\\system32\\rundll32.exe /d acproxy.dll,PerformAutochkOperations", @"C:\Windows\System32\acproxy.dll", false, true, windows: true),
            Entry("Razer Synapse Service", StartupSourceKind.Service, "Razer Synapse Service", $"\"{Razer}\"", Razer, true, true, currentUser: false),
            Entry("Razer Game Manager Service", StartupSourceKind.Service, "Razer Game Manager Service",
                "\"C:\\Program Files (x86)\\Razer\\Razer Services\\GMS\\GameManagerService.exe\"", @"C:\Program Files (x86)\Razer\Razer Services\GMS\GameManagerService.exe", false, true, currentUser: false),
            Entry("Microsoft Intune Management Extension", StartupSourceKind.Service, "IntuneManagementExtension", $"\"{Intune}\"", Intune, true, true, currentUser: false),
            Entry("Windows Search", StartupSourceKind.Service, "WSearch", @"C:\Windows\system32\SearchIndexer.exe /Embedding", @"C:\Windows\system32\SearchIndexer.exe", true, true, windows: true, currentUser: false),
        ];
    }

    public static StartupInventory Startup(DevDataMode mode) => new()
    {
        CapturedAt = DateTimeOffset.UtcNow,
        Entries = mode switch
        {
            DevDataMode.Empty => [],
            DevDataMode.One => [Entry("GoogleChromeAutoLaunch_E9CC7D9444E20242CFEDF4753656D75C", StartupSourceKind.RegistryRun, RunKey, $"\"{Chrome}\" --no-startup-window", Chrome, true, true)],
            // Estação corporativa: ~400 serviços automáticos + ~200 tarefas de logon/boot.
            DevDataMode.Many => WorstEntries()
                .Concat(Enumerable.Range(0, 400).Select(i => Entry($"Serviço corporativo {i:000}", StartupSourceKind.Service, $"CorpSvc{i:000}",
                    $"\"C:\\Program Files\\Northwind\\Agents\\svc{i:000}.exe\"", $@"C:\Program Files\Northwind\Agents\svc{i:000}.exe", true, true, currentUser: false)))
                .Concat(Enumerable.Range(0, 200).Select(i => Entry($"Inventário {i:000}", StartupSourceKind.ScheduledTask, $@"\Northwind\Inventory\Task{i:000}",
                    $@"C:\Program Files\Northwind\Agents\inv.exe --task {i}", @"C:\Program Files\Northwind\Agents\inv.exe", true, i % 3 != 0, i % 3 == 0 ? "tarefa desativada" : null)))
                .ToList(),
            _ => WorstEntries(),
        },
        Warnings = mode == DevDataMode.Worst
            ? ["Tarefas agendadas: COMException 0x80070005 — Acesso negado. (tarefas de outros usuários não foram lidas)"]
            : [],
    };
}
#endif
