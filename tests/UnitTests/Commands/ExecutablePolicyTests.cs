using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Infrastructure.Commands;

namespace LeccorWindowsHealthCenter.UnitTests.Commands;

public class ExecutablePolicyTests
{
    private static readonly string System32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
    private readonly ExecutablePolicy _policy = new();

    private static CommandRequest Request(string path, params string[] args) => new() { ExecutablePath = path, Arguments = args };

    [Fact]
    public void Accepts_trusted_system_tool()
    {
        _policy.Validate(Request(Path.Combine(System32, "hostname.exe")));
    }

    [Theory]
    [InlineData("hostname.exe")]
    [InlineData(@"System32\hostname.exe")]
    [InlineData("")]
    public void Rejects_relative_paths(string path) =>
        Assert.Throws<CommandRejectedException>(() => _policy.Validate(Request(path)));

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("powershell.exe")]
    [InlineData("wscript.exe")]
    public void Rejects_shell_interpreters(string exe)
    {
        var ex = Assert.Throws<CommandRejectedException>(() => _policy.Validate(Request(Path.Combine(System32, exe))));
        Assert.Contains("Interpretadores", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_path_traversal()
    {
        var traversal = System32 + @"\..\System32\hostname.exe";

        Assert.Throws<CommandRejectedException>(() => _policy.Validate(Request(traversal)));
    }

    [Fact]
    public void Rejects_executables_outside_trusted_directories()
    {
        var temp = Path.Combine(Path.GetTempPath(), "malicioso.exe");

        var ex = Assert.Throws<CommandRejectedException>(() => _policy.Validate(Request(temp)));
        Assert.Contains("fora dos diretórios confiáveis", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_non_exe_files()
    {
        Assert.Throws<CommandRejectedException>(() => _policy.Validate(Request(Path.Combine(System32, "drivers", "etc", "hosts"))));
    }

    [Theory]
    [InlineData("valor\r\nnova-linha")]
    [InlineData("nulo\0")]
    public void Rejects_control_characters_in_arguments(string argument) =>
        Assert.Throws<CommandRejectedException>(() => _policy.Validate(Request(Path.Combine(System32, "hostname.exe"), argument)));

    [Fact]
    public void Shell_metacharacters_are_harmless_literal_arguments()
    {
        // Sem shell e com ArgumentList, "&" e "|" chegam ao processo como texto literal.
        _policy.Validate(Request(Path.Combine(System32, "hostname.exe"), "C:\\ & del /q C:\\*", "a | b"));
    }

    [Fact]
    public void Additional_trusted_executable_is_allowed_when_it_exists()
    {
        var self = Environment.ProcessPath!;
        var policy = new ExecutablePolicy([self]);

        if (string.Equals(Path.GetExtension(self), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            policy.Validate(Request(self));
        }
    }

    [Fact]
    public void Rejects_non_positive_timeout()
    {
        var request = Request(Path.Combine(System32, "hostname.exe")) with { Timeout = TimeSpan.Zero };

        Assert.Throws<CommandRejectedException>(() => _policy.Validate(request));
    }

    [Fact]
    public void Formats_command_line_with_quotes_for_display_only()
    {
        var line = CommandExecutor.FormatCommandLine(Request(@"C:\Windows\System32\dism.exe", "/Online", "/Cleanup-Image", "valor com espaço"));

        Assert.Equal(@"C:\Windows\System32\dism.exe /Online /Cleanup-Image ""valor com espaço""", line);
    }
}
