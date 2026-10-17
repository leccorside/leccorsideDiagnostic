using LeccorWindowsHealthCenter.Shared.Security;

namespace LeccorWindowsHealthCenter.UnitTests.Shared;

public class SensitiveDataRedactorTests
{
    [Theory]
    [InlineData("password=Sup3rS3cret!", "Sup3rS3cret!")]
    [InlineData("senha: minhaSenha123", "minhaSenha123")]
    [InlineData("{\"apiKey\":\"abc123def456\"}", "abc123def456")]
    [InlineData("API_KEY=xyz-987", "xyz-987")]
    [InlineData("tool --token s3cr3tvalue", "s3cr3tvalue")]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig", "eyJhbGciOiJIUzI1NiJ9")]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "dXNlcjpwYXNz")]
    [InlineData("curl -H Bearer abc.def.ghi", "abc.def.ghi")]
    [InlineData("Cookie=session=abcdef", "abcdef")]
    [InlineData("uso ghp_1234567890abcdefghijABCDEFGHIJ123456 no git", "ghp_1234567890abcdefghijABCDEFGHIJ123456")]
    [InlineData("AKIAABCDEFGHIJKLMNOP", "AKIAABCDEFGHIJKLMNOP")]
    public void Removes_secret_values(string input, string secret)
    {
        var redacted = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains(SensitiveDataRedactor.Mask, redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Disk / Event 153 ocorreu 47 vezes")]
    [InlineData(@"C:\Windows\System32\dism.exe /Online /Cleanup-Image /ScanHealth")]
    [InlineData("")]
    public void Keeps_ordinary_text_intact(string input) =>
        Assert.Equal(input, SensitiveDataRedactor.Redact(input));

    [Theory]
    [InlineData("Password", true)]
    [InlineData("ApiKey", true)]
    [InlineData("connectionString", true)]
    [InlineData("ScannerId", false)]
    [InlineData("DurationMs", false)]
    [InlineData(null, false)]
    public void Detects_sensitive_property_names(string? key, bool expected) =>
        Assert.Equal(expected, SensitiveDataRedactor.IsSensitiveKey(key));
}
