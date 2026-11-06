using System.IO;
using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.DevTools;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.DevTools;

[SupportedOSPlatform("windows")]
public sealed class DevToolsProvider : IDevToolsProvider
{
    private const string LxssKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Lxss";
    private readonly ILogger<DevToolsProvider> _logger;

    public DevToolsProvider(ILogger<DevToolsProvider> logger)
    {
        _logger = logger;
    }

    public Task<DevEnvironmentSummary> GetDevEnvironmentAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var tools = CollectDevTools(cancellationToken);
            var wslDistros = CollectWslDistributions(cancellationToken);
            var docker = CollectDockerStatus(wslDistros, cancellationToken);
            var pathAnalysis = AnalyzePathEnvironment();
            var hasHyperV = CheckHyperVStatus();

            return new DevEnvironmentSummary
            {
                Tools = tools,
                WslDistributions = wslDistros,
                Docker = docker,
                PathAnalysis = pathAnalysis,
                HasHyperV = hasHyperV,
                CapturedAt = DateTimeOffset.UtcNow
            };
        }, cancellationToken);
    }

    private static List<DevToolItem> CollectDevTools(CancellationToken cancellationToken)
    {
        var tools = new List<DevToolItem>();

        // 1. Git
        tools.Add(DetectTool("git", "Git", "VCS", "git.exe",
            [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Git\bin\git.exe")]));

        // 2. Node.js & Package Managers
        tools.Add(DetectTool("node", "Node.js", "Runtime", "node.exe",
            [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"nodejs\node.exe")]));
        tools.Add(DetectTool("npm", "npm", "Package Manager", "npm.cmd", []));
        tools.Add(DetectTool("yarn", "Yarn", "Package Manager", "yarn.cmd", []));
        tools.Add(DetectTool("pnpm", "pnpm", "Package Manager", "pnpm.cmd", []));

        // 3. .NET SDKs
        tools.Add(DetectDotNet());

        // 4. Java / JDK
        tools.Add(DetectJava());
        tools.Add(DetectTool("maven", "Maven", "Build Tool", "mvn.cmd", []));
        tools.Add(DetectTool("gradle", "Gradle", "Build Tool", "gradle.bat", []));

        // 5. Python
        tools.Add(DetectTool("python", "Python", "Runtime", "python.exe", []));

        // 6. IDEs
        tools.Add(DetectVsCode());
        tools.Add(DetectVisualStudio());
        tools.Add(DetectJetBrainsIdes());
        tools.Add(DetectAndroidStudio());

        return tools;
    }

    private static DevToolItem DetectTool(string id, string name, string category, string executable, string[] standardPaths)
    {
        var foundPath = FindInPath(executable);
        if (foundPath == null)
        {
            foreach (var p in standardPaths)
            {
                if (File.Exists(p))
                {
                    foundPath = p;
                    break;
                }
            }
        }

        if (foundPath != null)
        {
            string? version = null;
            try
            {
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(foundPath);
                version = !string.IsNullOrWhiteSpace(vi.ProductVersion) ? vi.ProductVersion : vi.FileVersion;
            }
            catch
            {
                // Ignora falha de versão
            }

            return new DevToolItem
            {
                Id = id,
                Name = name,
                Category = category,
                IsInstalled = true,
                Path = foundPath,
                Version = version ?? "Instalado",
                StatusDetail = "Detectado no sistema"
            };
        }

        return new DevToolItem
        {
            Id = id,
            Name = name,
            Category = category,
            IsInstalled = false,
            StatusDetail = "Não encontrado no PATH ou locais padrão"
        };
    }

    private static DevToolItem DetectDotNet()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var sdkDir = Path.Combine(programFiles, @"dotnet\sdk");

        if (Directory.Exists(sdkDir))
        {
            try
            {
                var sdks = Directory.GetDirectories(sdkDir)
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrEmpty(n) && char.IsDigit(n[0]))
                    .OrderByDescending(n => n)
                    .ToList();

                if (sdks.Count > 0)
                {
                    return new DevToolItem
                    {
                        Id = "dotnet",
                        Name = ".NET SDK",
                        Category = "Runtime",
                        IsInstalled = true,
                        Path = Path.Combine(programFiles, @"dotnet\dotnet.exe"),
                        Version = sdks[0],
                        StatusDetail = $"{sdks.Count} SDK(s) instalado(s): {string.Join(", ", sdks.Take(3))}"
                    };
                }
            }
            catch
            {
                // Falha de leitura de pasta
            }
        }

        return new DevToolItem
        {
            Id = "dotnet",
            Name = ".NET SDK",
            Category = "Runtime",
            IsInstalled = false,
            StatusDetail = "Nenhum .NET SDK encontrado"
        };
    }

    private static DevToolItem DetectJava()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        var javaPath = FindInPath("java.exe");

        if (string.IsNullOrEmpty(javaPath) && !string.IsNullOrEmpty(javaHome))
        {
            var candidate = Path.Combine(javaHome, @"bin\java.exe");
            if (File.Exists(candidate))
            {
                javaPath = candidate;
            }
        }

        if (javaPath != null)
        {
            string? version = null;
            try
            {
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(javaPath);
                version = vi.ProductVersion ?? vi.FileVersion;
            }
            catch
            {
            }

            return new DevToolItem
            {
                Id = "java",
                Name = "Java (JDK/JRE)",
                Category = "Runtime",
                IsInstalled = true,
                Path = javaPath,
                Version = version ?? "Instalado",
                StatusDetail = !string.IsNullOrEmpty(javaHome) ? $"JAVA_HOME: {javaHome}" : "Detectado no PATH"
            };
        }

        return new DevToolItem
        {
            Id = "java",
            Name = "Java (JDK/JRE)",
            Category = "Runtime",
            IsInstalled = false,
            StatusDetail = "Não encontrado"
        };
    }

    private static DevToolItem DetectVsCode()
    {
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string[] candidates =
        [
            Path.Combine(localApp, @"Programs\Microsoft VS Code\Code.exe"),
            Path.Combine(progFiles, @"Microsoft VS Code\Code.exe")
        ];

        foreach (var p in candidates)
        {
            if (File.Exists(p))
            {
                string? ver = null;
                try
                {
                    ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(p).ProductVersion;
                }
                catch
                {
                }

                return new DevToolItem
                {
                    Id = "vscode",
                    Name = "Visual Studio Code",
                    Category = "IDE",
                    IsInstalled = true,
                    Path = p,
                    Version = ver ?? "Instalado",
                    StatusDetail = "Editor de código ativo"
                };
            }
        }

        return new DevToolItem
        {
            Id = "vscode",
            Name = "Visual Studio Code",
            Category = "IDE",
            IsInstalled = false,
            StatusDetail = "Não instalado"
        };
    }

    private static DevToolItem DetectVisualStudio()
    {
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var vswhere = Path.Combine(progFilesX86, @"Microsoft Visual Studio\Installer\vswhere.exe");

        if (File.Exists(vswhere))
        {
            return new DevToolItem
            {
                Id = "visualstudio",
                Name = "Visual Studio",
                Category = "IDE",
                IsInstalled = true,
                Path = vswhere,
                Version = "Instalado",
                StatusDetail = "Instalação do Visual Studio detectada via VS Installer"
            };
        }

        return new DevToolItem
        {
            Id = "visualstudio",
            Name = "Visual Studio",
            Category = "IDE",
            IsInstalled = false,
            StatusDetail = "Não instalado"
        };
    }

    private static DevToolItem DetectJetBrainsIdes()
    {
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var toolboxApps = Path.Combine(localApp, @"JetBrains\Toolbox\apps");

        if (Directory.Exists(toolboxApps))
        {
            try
            {
                var ides = Directory.GetDirectories(toolboxApps).Select(Path.GetFileName).ToList();
                if (ides.Count > 0)
                {
                    return new DevToolItem
                    {
                        Id = "jetbrains",
                        Name = "JetBrains IDEs",
                        Category = "IDE",
                        IsInstalled = true,
                        Path = toolboxApps,
                        Version = $"{ides.Count} produto(s)",
                        StatusDetail = string.Join(", ", ides)
                    };
                }
            }
            catch
            {
            }
        }

        return new DevToolItem
        {
            Id = "jetbrains",
            Name = "JetBrains IDEs",
            Category = "IDE",
            IsInstalled = false,
            StatusDetail = "Nenhum produto JetBrains detectado via Toolbox"
        };
    }

    private static DevToolItem DetectAndroidStudio()
    {
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var studioExe = Path.Combine(progFiles, @"Android\Android Studio\bin\studio64.exe");

        if (File.Exists(studioExe))
        {
            return new DevToolItem
            {
                Id = "androidstudio",
                Name = "Android Studio",
                Category = "IDE",
                IsInstalled = true,
                Path = studioExe,
                Version = "Instalado",
                StatusDetail = "Ambiente de desenvolvimento Android"
            };
        }

        return new DevToolItem
        {
            Id = "androidstudio",
            Name = "Android Studio",
            Category = "IDE",
            IsInstalled = false,
            StatusDetail = "Não instalado"
        };
    }

    private static List<WslDistributionInfo> CollectWslDistributions(CancellationToken cancellationToken)
    {
        var distros = new List<WslDistributionInfo>();

        try
        {
            using var lxssKey = Registry.CurrentUser.OpenSubKey(LxssKeyPath);
            if (lxssKey == null)
            {
                return distros;
            }

            var defaultGuid = lxssKey.GetValue("DefaultDistribution") as string;

            foreach (var subName in lxssKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var subKey = lxssKey.OpenSubKey(subName);
                if (subKey == null)
                {
                    continue;
                }

                var distroName = subKey.GetValue("DistributionName") as string;
                if (string.IsNullOrWhiteSpace(distroName))
                {
                    continue;
                }

                var version = subKey.GetValue("Version") as int? ?? 2;
                var basePath = subKey.GetValue("BasePath") as string;
                string? vhdxPath = null;
                long? vhdxSize = null;

                if (!string.IsNullOrWhiteSpace(basePath) && Directory.Exists(basePath))
                {
                    var vhdxCandidate = Path.Combine(basePath, "ext4.vhdx");
                    if (File.Exists(vhdxCandidate))
                    {
                        vhdxPath = vhdxCandidate;
                        try
                        {
                            vhdxSize = new FileInfo(vhdxCandidate).Length;
                        }
                        catch
                        {
                        }
                    }
                }

                distros.Add(new WslDistributionInfo
                {
                    Name = distroName,
                    WslVersion = version,
                    State = "Configurada",
                    IsDefault = string.Equals(subName, defaultGuid, StringComparison.OrdinalIgnoreCase),
                    BasePath = basePath,
                    VhdxPath = vhdxPath,
                    VhdxSizeBytes = vhdxSize
                });
            }
        }
        catch (Exception)
        {
            // Erro ao ler registro do Lxss
        }

        return distros;
    }

    private static DockerStatusInfo CollectDockerStatus(IReadOnlyList<WslDistributionInfo> wslDistros, CancellationToken cancellationToken)
    {
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var dockerExe = Path.Combine(progFiles, @"Docker\Docker\Docker Desktop.exe");
        var isInstalled = File.Exists(dockerExe) || FindInPath("docker.exe") != null;

        var isRunning = false;
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Docker Desktop");
            isRunning = processes.Length > 0;
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
        catch
        {
        }

        // Se Docker estiver usando WSL2, procura o tamanho do disco docker-desktop-data
        long? totalUsage = null;
        var dataDistro = wslDistros.FirstOrDefault(d => d.Name.Contains("docker-desktop-data", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("docker-desktop", StringComparison.OrdinalIgnoreCase));
        if (dataDistro?.VhdxSizeBytes.HasValue == true)
        {
            totalUsage = dataDistro.VhdxSizeBytes.Value;
        }

        return new DockerStatusInfo
        {
            IsInstalled = isInstalled,
            IsRunning = isRunning,
            Version = isInstalled ? "Docker Desktop" : null,
            Backend = dataDistro != null ? "WSL2" : "Hyper-V / Nativo",
            TotalDiskUsageBytes = totalUsage,
            RunningContainers = 0,
            TotalContainers = 0,
            TotalImages = 0,
            TotalVolumes = 0
        };
    }

    private static PathEnvironmentAnalysis AnalyzePathEnvironment()
    {
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        var sysPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty;

        var userEntries = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sysEntries = sysPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var invalid = new List<string>();

        foreach (var entry in userEntries.Concat(sysEntries).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(entry))
            {
                invalid.Add(entry);
            }
        }

        var totalLen = userPath.Length + sysPath.Length;

        return new PathEnvironmentAnalysis
        {
            UserEntries = userEntries,
            SystemEntries = sysEntries,
            InvalidEntries = invalid,
            TotalLength = totalLen
        };
    }

    private static bool CheckHyperVStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\vmms");
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindInPath(string executable)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var folder in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(folder, executable);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
