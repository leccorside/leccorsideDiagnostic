using System.Security.Principal;
using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.Infrastructure.Security;

public sealed class PrivilegeService : IPrivilegeService
{
    private readonly Lazy<(bool Elevated, string User)> _identity = new(() =>
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return (principal.IsInRole(WindowsBuiltInRole.Administrator), identity.Name);
    });

    public bool IsElevated => _identity.Value.Elevated;

    public string CurrentUser => _identity.Value.User;
}
