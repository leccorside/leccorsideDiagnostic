---
target: tela de Armazenamento do Leccor (2ª rodada)
total_score: 28
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:D:\\0000000-PROJETOS\\leccorsideDiagnostic\\src\\LeccorWindowsHealthCenter.App\\Views\\StorageView.xaml"
target_fingerprint: "sha256:7f7db0b00650df982efdd8162ff92ca0c3042e8374654011b1ab34d1f9c76e71"
target_path: "D:\\0000000-PROJETOS\\leccorsideDiagnostic\\src\\LeccorWindowsHealthCenter.App\\Views\\StorageView.xaml"
timestamp: 2026-10-19T05-47-28Z
slug: wshealthcenter-app-views-storageview-xaml-a5352bc6
---
# Crítica — tela Armazenamento (StorageView.xaml), 2ª rodada

Método: duas avaliações isoladas (A: revisão de design · B: detector). Detector inconclusivo: não analisa XAML (verificado com controles).

| # | Heurística | Nota | Problema principal |
|---|---|---|---|
| 1 | Visibilidade do status | 2 | Análise longa só com texto de 12 px, sem barra determinada; copiar caminho sem retorno |
| 2 | Mundo real | 3 | "4 TB em C:\" num volume de 3,6 TB; "C:" x "C:\" |
| 3 | Controle e liberdade | 3 | Linha folha sai do app para o Explorer sem aviso |
| 4 | Consistência | 2 | Linha de pasta com duas ações; unidades mudam entre linhas iguais; 0 GB âmbar com texto "falham" |
| 5 | Prevenção de erros | 3 | Dica do Docker manda docker system prune sem dizer o custo |
| 6 | Reconhecer x lembrar | 3 | "Ordenar por" só vale para pastas e filtro só para arquivos, sem dizer |
| 7 | Flexibilidade | 3 | Falta copiar comandos, ordenar arquivos, listas longas sem virtualização |
| 8 | Estética | 3 | Página enorme depois da análise |
| 9 | Recuperação | 3 | Alerta de disco com falha não leva a nada |
| 10 | Ajuda | 3 | Soma acima do usado explicada só na 4ª linha |
| **Total** | | **28/40** | Bom |

## Problemas prioritários
- [P1] Título da análise afirma tamanho maior que o usado (links físicos): mostrar o usado no título e a soma na legenda; a longo prazo deduplicar por File ID.
- [P1] Alertas críticos fora do Tab/leitor de tela (Border sem peer); sem HeadingLevel nos títulos de seção.
- [P2] Sem progresso determinado na análise; status em Caption; cópia sem confirmação.
- [P2] Linha de pasta com ação dupla; caso dominante ainda filtrado por MinFolderBytes; até 500 linhas sem virtualização.
- [P2] Dica do Docker com comando destrutivo sem custo; comandos não copiáveis.
- [P3] Unidades por linha, severidade x texto em volume a 0 B, "C:" x "C:\", duas colunas de arquivos em 980 px.
