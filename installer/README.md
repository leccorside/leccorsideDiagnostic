# Instalador

Ainda não implementado — previsto no **Passo 10** do [PASSOS.md](../PASSOS.md).

Requisitos (seção 62 do PROMPT):

- instalar, atualizar, reparar instalação e desinstalar;
- a desinstalação remove os arquivos do aplicativo, mas **não** remove relatórios e backups do usuário
  (`%ProgramData%\LeccorWindowsHealthCenter`) sem confirmação explícita;
- o executável principal é instalado sem elevação permanente (`asInvoker`); o instalador em si pede UAC.

Tecnologia candidata: WiX Toolset v5 (MSI) — decisão registrada no CONTEXTO.md quando o passo começar.
