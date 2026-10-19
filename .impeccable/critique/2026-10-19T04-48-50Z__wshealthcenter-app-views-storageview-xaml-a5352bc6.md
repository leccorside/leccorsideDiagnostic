---
target: tela de Armazenamento do Leccor
total_score: 26
max_score: 40
na_heuristics: 
p0_count: 1
p1_count: 3
target_identity: "file:D:\\0000000-PROJETOS\\leccorsideDiagnostic\\src\\LeccorWindowsHealthCenter.App\\Views\\StorageView.xaml"
target_fingerprint: "sha256:188b9a9f75c2edecb0bc01fd88c4f4eecd19611bccfec3887aa6326d2a155912"
target_path: "D:\\0000000-PROJETOS\\leccorsideDiagnostic\\src\\LeccorWindowsHealthCenter.App\\Views\\StorageView.xaml"
timestamp: 2026-10-19T04-48-50Z
slug: wshealthcenter-app-views-storageview-xaml-a5352bc6
---
# Crítica — tela Armazenamento (StorageView.xaml)

Método: duas avaliações isoladas (A: revisão de design · B: detector). Detector inconclusivo: não analisa XAML.

| # | Heurística | Nota | Problema principal |
|---|---|---|---|
| 1 | Visibilidade do status | 3 | Progresso da análise só em legenda de 12 px; sem barra nem anúncio ao leitor de tela |
| 2 | Mundo real | 3 | Jargão no modo padrão: "WMI", "links físicos", "MBR · 1 partição" |
| 3 | Controle e liberdade | 3 | Nova análise apaga a anterior na hora; sem voltar por teclado |
| 4 | Consistência | 3 | Último item da trilha é botão que não faz nada; sem "Abrir no Explorer" |
| 5 | Prevenção de erros | 3 | .vhdx/.iso listados como arquivos comuns, sem aviso de "não apague direto" |
| 6 | Reconhecer x lembrar | 2 | Disco com falha prevista no rodapé; vínculo volume ↔ disco exige memória |
| 7 | Flexibilidade | 1 | Sem ordenação, filtro, abrir/copiar caminho, ver mais que 20, exportar |
| 8 | Estética | 3 | Legenda do volume com 5–6 fatos; 24 botões iguais |
| 9 | Recuperação de erros | 2 | Erro com ex.Message cru; "sem permissão" sem ação; aviso vazio no fim |
| 10 | Ajuda | 3 | Notas inline ótimas; falta dizer o limite de "pouco espaço" |
| **Total** | | **26/40** | Aceitável |

## Problemas prioritários
- [P0] Disco com falha prevista escondido no rodapé (Expander fechado, legenda de 12 px). Fix: cartão de aviso no topo com evidência e ação, Expander crítico aberto, "disco com falha prevista" na legenda dos volumes afetados.
- [P1] Análise não fala a língua do desenvolvedor: .vhdx do Docker/WSL, blobs de modelos e caches de toolchain aparecem crus; Temporários e caches sem npm/NuGet/Gradle/pip/Docker. Fix: classificador de arquivos conhecidos + locais de dev.
- [P1] Navegação sem saída: "N pastas menores" com quase todo o conteúdo não abre; pasta só com arquivos sem destino; listas de arquivos mostram o volume inteiro sem dizer. Fix: expandir pastas menores quando dominantes, filtrar/rotular listas pelo nó atual.
- [P1] Teclado e leitor de tela: foco perdido após Analisar e ao entrar em pasta; progresso sem região viva; caminho só em tooltip; linhas anunciam só o nome; aviso x crítico só por cor. Fix: foco programático, LiveSetting, nomes acessíveis completos, texto diferente por nível.
- [P2] Ordem da página e estado vazio: análise longe com muitos volumes; aviso de leitura vazia no fim; "Análise de espaço" sem volume. Fix: aviso vazio no topo, esconder análise sem volumes, compactar volumes > 6.

## Persona
- Alex: sem ordenar/filtrar/abrir no Explorer/copiar caminho/exportar; teto de 20 arquivos; só raiz do volume; análise longa perdida ao trocar de volume.
- Sam: foco perdido, progresso não anunciado, caminhos só em tooltip, Expander sem nome, linhas desabilitadas fora do Tab.
- Dev Docker/WSL: docker_data.vhdx 213 GB cru, "sem alteração há anos" sem dizer o que é seguro, sem ponte para Limpeza/Desenvolvimento.
