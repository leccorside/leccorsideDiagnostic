namespace LeccorWindowsHealthCenter.Application.SystemInfo;

public enum SignatureStatus
{
    /// <summary>Não verificado (arquivo inacessível ou verificação falhou por outro motivo).</summary>
    Unknown,

    /// <summary>Assinatura Authenticode no próprio arquivo, válida.</summary>
    Valid,

    /// <summary>Sem assinatura no arquivo, mas presente em um catálogo do Windows (comum nos executáveis do sistema).</summary>
    SignedByCatalog,

    NoSignature,

    /// <summary>Assinatura presente, mas inválida (arquivo alterado, certificado não confiável ou revogado).</summary>
    Invalid,
}

/// <summary>Quem fez o executável: fabricante declarado (versão do arquivo) e quem assinou. Null = não informado.</summary>
public sealed record FileIdentity
{
    public required string Path { get; init; }

    public bool Exists { get; init; }

    /// <summary>CompanyName do recurso de versão — declarado pelo próprio arquivo, não comprovado.</summary>
    public string? Publisher { get; init; }

    public string? Description { get; init; }

    public string? Version { get; init; }

    public SignatureStatus Signature { get; init; }

    /// <summary>Nome comum (CN) do certificado que assinou, quando a assinatura está no arquivo.</summary>
    public string? Signer { get; init; }

    /// <summary>"Assinado por Microsoft Corporation", "Assinado pelo catálogo do Windows", "Sem assinatura"…</summary>
    public string SignatureText => Signature switch
    {
        SignatureStatus.Valid => Signer is null ? "Assinado" : $"Assinado por {Signer}",
        SignatureStatus.SignedByCatalog => "Assinado pelo catálogo do Windows",
        SignatureStatus.NoSignature => "Sem assinatura",
        SignatureStatus.Invalid => "Assinatura inválida",
        _ => "não verificado",
    };
}

public interface IFileIdentityProvider
{
    /// <summary>
    /// Fabricante e assinatura do arquivo. Verificar assinatura leva milissegundos por arquivo: o resultado fica em cache
    /// por caminho + data de modificação. Sem consulta de revogação na rede (o app funciona 100% local).
    /// </summary>
    Task<FileIdentity> GetAsync(string path, CancellationToken cancellationToken);
}
