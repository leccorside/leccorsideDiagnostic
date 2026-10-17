# Product

<!-- impeccable:product-schema 1 -->

## Platform

windows

## Users

**Primário: desenvolvedor / power user** (confirmado pelo usuário em 17/10/2026). Usa o próprio computador Windows
para desenvolvimento — Docker, WSL, Hyper-V, Node.js, Java, .NET, Git, VS Code, Visual Studio, IntelliJ, Android
Studio (seção 31 do PROMPT.txt). Quer saber, com evidência técnica, por que a máquina está lenta, instável ou sem
espaço, e corrigir sem arriscar o ambiente de trabalho (volumes do Docker, distribuições WSL, serviços).

Situação de uso: abre o app quando percebe lentidão, falhas, crashes ou disco cheio; roda uma varredura (rápida em
1–3 min ou completa), lê os achados, decide o que corrigir e confere o antes/depois. Valoriza detalhes técnicos
(Event IDs, caminhos de registro, comandos, exit codes) — disponíveis no **modo avançado**.

Restrição herdada do PROMPT (Definition of Done, seção 71): todo resultado precisa continuar **compreensível para
um usuário comum**. O modo padrão explica em linguagem clara; o técnico fica atrás do modo avançado, sem esconder a evidência.

## Product Purpose

Auditoria completa de um PC Windows 10/11 64-bit — hardware, Windows, armazenamento, processos, drivers, rede,
performance, aplicativos e ambiente de desenvolvimento — com correção segura:
**VARREDURA → DIAGNÓSTICO → CLASSIFICAÇÃO → CORREÇÃO → VALIDAÇÃO → RELATÓRIO.**

Centraliza o que hoje exige Gerenciador de Tarefas, Resource Monitor, Event Viewer, Storage Sense, Sysinternals,
SFC/DISM/CHKDSK, análise SMART, gerenciador de inicialização e diagnóstico de rede.

Sucesso = o usuário instala, clica em "Executar varredura completa" e recebe poucos problemas confirmados, cada um
respondendo: o que aconteceu, como sabemos, qual o impacto, como corrigir, qual o risco da correção e se dá para desfazer
(seções 72–73).

## Positioning

Diagnóstico **baseado em evidência**, não um "PC Cleaner". O que o diferencia de limpadores e otimizadores:

- todo achado aponta para a origem (contador, evento, registro, comando) e tem nível de confiança — nunca
  "encontramos 154 problemas";
- scanner e correção são separados: encontrar não significa corrigir; toda correção tem risco (SAFE→CRITICAL),
  aprovação proporcional, backup antes, validação depois e rollback quando aplicável;
- melhoria só é afirmada com medição antes/depois;
- sem dados não há nota: o score fica "sem dados" em vez de inventar um número;
- recusa explícita de "otimizações" sem evidência (RAM booster, registry cleaner, desativar Defender/Update/SysMain...).

## Operating Context

- App desktop instalado (e futuramente portable) em Windows 10/11 x64; arm64 depois.
- Roda **sem administrador**; pede UAC apenas para operações específicas (processo elevado separado, planejado).
- Funciona **100% localmente**: nada é enviado para servidores externos sem consentimento explícito.
- Dados locais em `%ProgramData%\LeccorWindowsHealthCenter` (logs, backups, histórico SQLite, relatórios).
- Relatórios exportáveis (JSON hoje; HTML, CSV e PDF depois) — usados para registrar/compartilhar o diagnóstico.
- Uso típico em máquina de desenvolvimento com Docker Desktop, WSL e várias toolchains instaladas.

## Capabilities and Constraints

- **Stack (existente):** C# / .NET 10 / WPF / MVVM (CommunityToolkit.Mvvm), Clean Architecture, SQLite, Serilog.
  Nada de Electron no app principal (seção 3: RAM, tamanho e impacto da própria ferramenta).
- **Plataforma `windows`:** app desktop nativo WPF. As referências do impeccable para web (CSS, navegador,
  `impeccable detect`) e para iOS/Android não se aplicam; seguir as convenções do Windows 11 (Fluent) e verificar
  visualmente nos temas claro e escuro.
- **Performance da própria ferramenta:** ociosa < 1% de CPU, preferencialmente < 200 MB de RAM; varreduras com
  concorrência limitada (seção 58).
- **Riscos:** SAFE · LOW · MEDIUM · HIGH · CRITICAL; CRITICAL nunca é executado automaticamente.
- **Terminologia da interface:** varredura (rápida, completa, personalizada), achado/problema, recomendação,
  evidência, confiança (confirmado / provável / possível), correção, risco, desfazer, modo avançado, Dry Run (simulação).
- **Distribuição:** uso interno / clientes próprios (confirmado). Não é produto público nem comercial por ora —
  não presumir loja, preço, licenciamento público ou marketing.
- **Idioma:** pt-BR agora; inglês depois (confirmado). Textos de interface devem ficar preparados para localização.
- **Em aberto:** quem são exatamente os "clientes próprios" e se eles serão operadores técnicos ou usuários finais;
  modelo de suporte e atualização para esses clientes.

## Brand Commitments

- Nome: **Leccor Windows Health Center** (provisório no PROMPT; "LECCOR" + "WINDOWS HEALTH CENTER" nas logos).
- Logos oficiais obrigatórias (seção 43): `logo1.jpg` (fundo claro) e `logo2.png` (fundo transparente, texto escuro),
  na raiz e em `src/LeccorWindowsHealthCenter.App/Assets/`. Símbolo: raposa geométrica em gradiente laranja→amarelo.
- A interface deve transmitir **diagnóstico técnico + transparência + segurança** e evitar a aparência de
  "PC Cleaner duvidoso" (seção 43).
- Voz: direta e factual; afirma só o que mediu; nomeia limitações ("não disponível") em vez de estimar.

## Evidence on Hand

- `PROMPT.txt`: especificação completa do produto (73 seções) — fonte da verdade dos requisitos.
- Logos: `logo1.jpg`, `logo2.png`; derivados `Assets/logo-mark.png` e `Assets/app.ico`.
- Ainda **não existem**: depoimentos, clientes nomeados, métricas de uso, benchmarks públicos ou capturas de
  diagnósticos reais. Não inventar nenhum deles.

## Product Principles

1. **Medir antes de afirmar.** Sem medição não há conclusão, nota ou melhoria declarada.
2. **Explicar cada achado.** O que aconteceu, como sabemos, impacto, correção, risco e se é reversível — sempre.
3. **Segurança acima da conveniência.** Nada muda sem aprovação proporcional ao risco, backup prévio e caminho de volta.
4. **A ferramenta não pode ser a causa da lentidão** que está medindo.
5. **Local e privado por padrão.** Nenhum dado sai da máquina sem consentimento explícito.

## Accessibility & Inclusion

- Contraste de texto ≥ 4,5:1 (texto grande e componentes ≥ 3:1) nos temas claro e escuro — medido, não estimado.
- Navegação completa por teclado com foco visível; ícones de status com nome acessível para leitores de tela.
- Temas claro, escuro e "seguir o Windows". Sem exigência formal de norma (ex.: WCAG AA certificado) definida até agora.
