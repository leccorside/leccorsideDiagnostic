---
name: Leccor Windows Health Center
description: Diagnóstico técnico do Windows lido como um laudo — superfícies calmas, evidência em primeiro plano e a Brasa Leccor apenas onde há ação ou atenção.
colors:
  brasa-leccor: "#C2520A"
  brasa-leccor-hover: "#A9440A"
  brasa-leccor-pressed: "#8F3A08"
  on-brasa: "#FFFFFF"
  brasa-leccor-dark: "#F58A2A"
  brasa-leccor-hover-dark: "#FF9B40"
  brasa-leccor-pressed-dark: "#D9761F"
  on-brasa-dark: "#1B1B1B"
  brand-gradient-start: "#FF4B00"
  brand-gradient-mid: "#FF9A00"
  brand-gradient-end: "#F5D61E"
  chart-cpu: "#E8650F"
  chart-ram: "#0F6CBD"
  chart-ram-dark: "#4CA0E0"
  surface-app: "#F3F3F3"
  surface-sidebar: "#EEEEEE"
  surface-card: "#FFFFFF"
  surface-card-alt: "#F9F9F9"
  border-card: "#E5E5E5"
  text-primary: "#1B1B1B"
  text-secondary: "#5F5F5F"
  text-tertiary: "#8A8A8A"
  nav-hover: "#E3E3E3"
  nav-selected: "#E0E0E0"
  track: "#E6E6E6"
  button-secondary: "#FBFBFB"
  button-secondary-hover: "#F0F0F0"
  button-secondary-pressed: "#E3E3E3"
  surface-app-dark: "#1F1F1F"
  surface-sidebar-dark: "#191919"
  surface-card-dark: "#2B2B2B"
  surface-card-alt-dark: "#262626"
  border-card-dark: "#383838"
  text-primary-dark: "#F5F5F5"
  text-secondary-dark: "#C2C2C2"
  text-tertiary-dark: "#8F8F8F"
  nav-hover-dark: "#2D2D2D"
  nav-selected-dark: "#333333"
  track-dark: "#3A3A3A"
  button-secondary-dark: "#333333"
  button-secondary-hover-dark: "#3C3C3C"
  button-secondary-pressed-dark: "#2A2A2A"
  severity-critical: "#C42B1C"
  severity-high: "#B3470C"
  severity-medium: "#9D5D00"
  severity-low: "#005FB8"
  severity-info: "#616161"
  status-ok: "#0F7B0F"
  severity-critical-dark: "#FF99A4"
  severity-high-dark: "#FFA266"
  severity-medium-dark: "#FCE100"
  severity-low-dark: "#60CDFF"
  severity-info-dark: "#ABABAB"
  status-ok-dark: "#6CCB5F"
typography:
  display:
    fontFamily: "Segoe UI Variable Display, Segoe UI, sans-serif"
    fontSize: "40px"
    fontWeight: 600
  headline:
    fontFamily: "Segoe UI Variable Display, Segoe UI, sans-serif"
    fontSize: "28px"
    fontWeight: 600
  verdict:
    fontFamily: "Segoe UI Variable Display, Segoe UI, sans-serif"
    fontSize: "26px"
    fontWeight: 700
  metric:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "22px"
    fontWeight: 600
  title:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "16px"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
  button:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 600
  label:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 400
  label-strong:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 600
  technical:
    fontFamily: "Consolas, monospace"
    fontSize: "12px"
    fontWeight: 400
rounded:
  hairline: "3px"
  sm: "4px"
  md: "6px"
  lg: "8px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  card: "20px"
  section: "24px"
  page: "32px"
components:
  button-primary:
    backgroundColor: "{colors.brasa-leccor}"
    textColor: "{colors.on-brasa}"
    typography: "{typography.button}"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  button-primary-hover:
    backgroundColor: "{colors.brasa-leccor-hover}"
    textColor: "{colors.on-brasa}"
  button-primary-pressed:
    backgroundColor: "{colors.brasa-leccor-pressed}"
    textColor: "{colors.on-brasa}"
  button-secondary:
    backgroundColor: "{colors.button-secondary}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body}"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  button-secondary-hover:
    backgroundColor: "{colors.button-secondary-hover}"
  button-secondary-pressed:
    backgroundColor: "{colors.button-secondary-pressed}"
  card:
    backgroundColor: "{colors.surface-card}"
    rounded: "{rounded.lg}"
    padding: "{spacing.card}"
  card-inset:
    backgroundColor: "{colors.surface-card-alt}"
    rounded: "{rounded.md}"
    padding: "14px"
  nav-item:
    textColor: "{colors.text-primary}"
    typography: "{typography.body}"
    rounded: "{rounded.md}"
    padding: "6px 12px"
  nav-item-hover:
    backgroundColor: "{colors.nav-hover}"
  nav-item-selected:
    backgroundColor: "{colors.nav-selected}"
  checkbox-checked:
    backgroundColor: "{colors.brasa-leccor}"
    textColor: "{colors.on-brasa}"
    rounded: "{rounded.sm}"
    size: "18px"
  progress-bar:
    backgroundColor: "{colors.track}"
    rounded: "{rounded.hairline}"
    height: "6px"
  milestone-badge:
    backgroundColor: "{colors.brasa-leccor}"
    textColor: "{colors.on-brasa}"
    typography: "{typography.label-strong}"
    rounded: "{rounded.sm}"
    padding: "2px 8px"
---

# Design System: Leccor Windows Health Center

## Overview

**Creative North Star: "O Laudo Técnico"**

A interface é um laudo de diagnóstico. Como um bom laudo, ela é calma, ordenada e sóbria: o que se destaca é o
resultado medido, não a moldura. As superfícies são neutras e planas, à maneira do Windows 11, para que número,
veredito e evidência tenham toda a atenção. A cor existe para significar — severidade, estado, ação — e nunca para
enfeitar. A Brasa Leccor, o laranja queimado da raposa, é a assinatura: aparece no botão que age, na pílula de seleção,
no foco e nas leituras ao vivo, e em quase nenhum outro lugar.

A densidade é de ferramenta de trabalho para um desenvolvedor: cartões com respiro, tipografia do sistema, hierarquia
clara entre título, veredito, métrica e legenda. O detalhe técnico (Event IDs, caminhos, comandos) existe, mas fica
recolhido até o modo avançado, em fonte de medição. Os componentes são táteis e confiantes: respondem visivelmente a
hover, foco e seleção, sem sombras em repouso.

Rejeitado de forma explícita (PRODUCT.md): a estética de "PC Cleaner duvidoso" — alarmes vermelhos por todo lado,
contadores inflados, gradientes chamativos, botões de "corrigir tudo".

**Key Characteristics:**

- Neutros planos do Windows 11 em dois temas completos (claro e escuro), com paridade de papéis entre eles.
- Um único acento de marca (Brasa Leccor), com contraste medido em cada tema.
- Cores semânticas de severidade com contraste ≥ 4,5:1 como texto, iguais em significado nos dois temas.
- Hierarquia por tom e borda de 1 px; sombra apenas em elementos flutuantes.
- Leituras numéricas grandes; legendas pequenas e secundárias; técnico em Consolas só no modo avançado.

## Colors

Paleta neutra e fria do Windows 11 com um único acento quente de marca e uma família semântica de severidade.

### Primary

- **Brasa Leccor**: laranja queimado da raposa. Fundo do botão principal, pílula do item de menu selecionado, caixa de
  seleção marcada, chevron em foco, leitura "em execução". No tema claro é escurecida em relação à logo para que o texto
  branco sobre ela passe de 4,5:1; no escuro, clareia para manter contraste sobre o cartão grafite.
- **Brasa Leccor Profunda** (hover): a mesma brasa um passo mais densa, só para hover/pressão do botão principal.
- **Gradiente da Raposa** (Faísca Vermelha → Laranja Raposa → Ouro da Raposa): o gradiente da logo. Existe como recurso
  de marca de uso pontual (ver regra abaixo).

### Secondary

- **Traço CPU** e **Traço RAM**: as duas séries de leitura ao vivo do dashboard. A CPU usa o laranja original da logo
  (elemento gráfico, contraste de 3:1 suficiente); a RAM usa um azul sóbrio para nunca ser confundida com a CPU.

### Tertiary — família semântica

- **Crítico** (vermelho), **Alto** (laranja ferrugem), **Médio** (âmbar escuro no claro / amarelo no escuro),
  **Baixo** (azul), **Recomendação** (cinza) e **Saudável** (verde). Cada valor foi escolhido para passar de 4,5:1 como
  texto sobre o cartão e sobre o fundo do seu tema.

### Neutral

- **Névoa de Laudo** (surface-app): fundo da janela; o cartão branco "flutua" sobre ele só por tom.
- **Lateral** (surface-sidebar): a barra de navegação, um tom abaixo do fundo, separada por borda de 1 px.
- **Folha do Laudo** (surface-card): cartões; **Folha Recuada** (surface-card-alt): blocos dentro de cartões.
- **Fio do Cartão** (border-card): a borda de 1 px que define cartões, botões secundários e divisões.
- **Tinta Principal / Secundária / Terciária** (text-*): título e corpo; legendas e rótulos; ícones de estado neutro.
- **Trilho** (track): fundo de barras de progresso e do anel do score.

### Named Rules

**The Brasa Escassa Rule.** A Brasa Leccor marca apenas ação ou atenção: o botão principal da tela, a seleção atual,
o foco e leituras em andamento. Se uma tela tem dois botões em Brasa, um deles está errado.

**The Significado Fixo Rule.** Uma cor semântica significa a mesma coisa em toda a interface e nos dois temas. Vermelho
é crítico; cinza é recomendação. Nunca usar uma cor de severidade como decoração ou para outro sentido.

**The Contraste Medido Rule.** Toda cor usada como texto tem contraste calculado (≥ 4,5:1; ≥ 3:1 para gráficos e
componentes) no fundo real em cada tema antes de entrar no sistema. "Parece legível" não é medição.

**The Faísca Única Rule.** O Gradiente da Raposa pode aparecer em no máximo um elemento de marca por tela (por exemplo,
o anel do score em estado excelente ou a tela de boas-vindas) e nunca em texto, botões ou fundos de área.

## Typography

**Display Font:** Segoe UI Variable Display (com fallback Segoe UI)
**Body Font:** Segoe UI Variable Text (com fallback Segoe UI)
**Label/Mono Font:** Consolas, apenas para referências técnicas no modo avançado

**Character:** a fonte do próprio Windows, usada com disciplina de laudo: pesos semibold para estrutura, bold apenas para
o veredito, regular para leitura. Familiar para quem usa o sistema todo dia; a marca não vem da tipografia.

### Hierarchy

- **Display** (600, 40px): o número do score dentro do anel. Um por tela.
- **Headline** (600, 28px): título da página ("Saúde do computador", "Varredura").
- **Verdict** (700, 26px): o veredito do score ("BOM", "ATENÇÃO", "SEM DADOS"), na cor semântica do rating.
- **Metric** (600, 22px): leituras ao vivo nos cartões de uso (CPU, RAM, disco).
- **Title** (600, 16px): títulos de seção e de cartão; títulos de achados.
- **Body** (400, 14px): texto corrido, itens de menu, botões secundários, explicações de achados.
- **Button** (600, 14px): rótulo do botão principal.
- **Label** (400, 12px): legendas, subtítulos de cartão, confiança, contagens.
- **Label Strong** (600, 12px): rótulos de campo dos achados ("O que aconteceu?", "Como sabemos disso?").
- **Technical** (Consolas 12px): origem técnica de evidências; visível apenas no modo avançado.

### Named Rules

**The Uma Leitura Grande Rule.** Cada cartão tem no máximo um número em Display ou Metric; o resto é Body ou Label.

**The Técnico Recolhido Rule.** Consolas aparece só para dado técnico real (Event ID, caminho, comando) e só no modo
avançado — nunca como "estética técnica".

## Layout

- **Estrutura:** sidebar fixa de 248px à esquerda (marca no topo, menu principal, Configurações no rodapé) e área de
  conteúdo com rolagem vertical.
- **Página:** margens de 32px nas laterais (28px no topo), largura máxima de 1200px. Título, subtítulo e então blocos
  separados por 16px.
- **Cartões:** padding interno de 20px; gutter de 8–16px entre cartões lado a lado; grupos relacionados em grades de
  colunas iguais (4 leituras de uso, 3 modos de varredura).
- **Ritmo:** 4 · 8 · 16 · 20 · 24 · 32. Mais espaço acima de um título de seção que abaixo dele.
- **Responsividade (janela):** a janela mínima é 980×620. Rótulos de botões quebram linha em vez de serem cortados;
  ações de cartão estreito vão para uma linha própria; textos nunca têm altura fixa.

## Elevation & Depth

Plano em repouso. A profundidade vem de camadas de tom (fundo → cartão → bloco recuado) e de bordas de 1 px; cartões,
páginas e controles não têm sombra. Apenas elementos que flutuam sobre o conteúdo — menus, tooltips, diálogos e
pop-ups — ganham uma sombra suave, para se separar do que está atrás.

### Shadow Vocabulary

- **Flutuante** (provisório; ver sidecar): sombra difusa com deslocamento vertical, mais forte no tema escuro. Só para
  menus, tooltips e diálogos.

### Named Rules

**The Plano em Repouso Rule.** Nada que fica no fluxo da página tem sombra. Se um cartão precisa de destaque, ele ganha
tom ou borda, não sombra.

**The Sombra Só Flutua Rule.** Sombra significa "isto está acima do conteúdo e vai sumir". Usá-la em outro lugar
mente sobre a hierarquia.

## Shapes

Cantos suavemente arredondados em quatro degraus, do maior contêiner ao menor detalhe: cartões em 8px; botões, itens
de menu e cabeçalhos expansíveis em 6px; caixas de seleção e selos em 4px; trilhos de progresso e polegar da barra de
rolagem em 3px. Círculos aparecem só onde o conteúdo é circular por natureza: anel do score, pontos de severidade,
botões de opção. A pílula de seleção do menu (3×16px) é a única forma vertical de marca.

## Components

### Buttons

Táteis e confiantes: o botão diz o que faz e reage visivelmente.

- **Shape:** cantos de 6px.
- **Primary:** fundo Brasa Leccor, texto claro (escuro no tema escuro), semibold, padding 8×16px. Um por tela.
- **Hover / Pressed / Focus:** hover escurece para Brasa Leccor Profunda e pressionado escurece mais um passo
  (Brasa Pressionada); foco de teclado desenha borda na cor do texto. Desabilitado: 45% de opacidade.
- **Secondary:** fundo neutro quase do tom do cartão, borda de 1px, texto principal; hover um tom abaixo,
  pressionado mais um tom.
- **Rótulos:** verbo + objeto ("Executar varredura completa"); quebram linha centralizados quando falta espaço, nunca são cortados.

### Cards / Containers

- **Corner Style:** 8px.
- **Background:** Folha do Laudo; blocos internos em Folha Recuada com cantos de 6px.
- **Shadow Strategy:** nenhuma (Plano em Repouso).
- **Border:** 1px no Fio do Cartão. Exceção: um aviso de estado pode trocar a borda pela cor semântica correspondente, em 1px.
- **Internal Padding:** 20px.

### Inputs / Fields

- **CheckBox:** caixa de 18px com cantos de 4px, borda neutra; marcada vira Brasa Leccor com check em ícone Fluent.
  Hover reforça a borda; foco engrossa a borda para 2px.
- **RadioButton:** círculo de 18px; marcado vira anel Brasa com ponto central.
- **Texto somente leitura:** seleção na Brasa translúcida, nunca no azul padrão do sistema.

### Navigation

- **Sidebar:** itens com ícone Segoe Fluent (28px de largura) e rótulo Body; padding 6×12px, cantos de 6px.
- **Estados:** hover em Nav Hover; selecionado em Nav Selected com a **pílula Brasa** de 3×16px à esquerda; foco com
  borda secundária de 1px.
- **Rodapé:** Configurações separada do menu principal, com seleção independente.

### Score Ring (signature)

Anel de 140px (110px no resultado da varredura) com trilho neutro e arco na cor do rating. Sem dados, mostra só o
trilho e um travessão — nunca 100. O número em Display fica no centro, com "/ 100" em Label abaixo. Abaixo do veredito,
uma linha em Label diz **quais áreas o score avaliou** ("Avaliou 2 de 6 áreas: Hardware e Performance. Ainda não
medidas: …") — um 100 sobre parte das áreas não pode parecer "computador perfeito".

### Info Card

Cartão de inventário/detalhe: **um único título com a informação real** (o nome do processador, "32 GB instalados"),
legenda opcional abaixo e linhas rótulo → valor (rótulo em Label secundário, valor em Body). A categoria ("Processador",
"Placa de vídeo") é o título da seção, fora do cartão. Valor ausente é "não disponível" em tinta secundária.
Em cartões estreitos (três colunas), as linhas são **empilhadas** (rótulo acima do valor) para valores longos não
quebrarem em várias linhas.

### Process Bar Row

Linha de "maiores consumidores": nome, PID em Label secundário, valor em Body semibold à direita e barra de 4px na cor
da série (Traço CPU ou Traço RAM) — nunca na Brasa.

### Row Button

Linha inteira clicável dentro de um cartão (ex.: pasta na análise de espaço). Sem fundo em repouso; hover em Nav Hover,
pressionado em Nav Selected, foco com borda secundária de 1px — os mesmos tons do menu. Cantos de 6px e margem
negativa de 8px para o hover alinhar com o conteúdo do cartão. Linha não navegável fica desabilitada, sem chevron.

### Volume Row

Rótulo do volume em Body semibold, legenda em Label (sistema de arquivos · disco · tipo), "x livres de y" em Body
com números tabulares à direita, ação secundária "Analisar" e barra de 6px com a fração **usada** em Traço RAM.
Âmbar (Medium) ou vermelho (Critical) — barra, valor em semibold e "pouco espaço livre" na legenda — só quando a regra
de pouco espaço acusaria o mesmo volume (mesma função `LowFreeSpaceRule.Assess`; The Significado Fixo Rule).

### Diagnostic Alert (laudo primeiro)

Telas de inventário abrem pelo diagnóstico mais grave. Cartão por achado, acima de todo o resto, com borda de 1px na cor
semântica (Critical ou Medium), ícone Fluent de 20px na mesma cor e com nome acessível do nível ("Crítico"/"Atenção"),
título em Title, o que aconteceu em Body, a evidência em Label e a ação em Body semibold. Ação disponível (ex.:
"Analisar C:") é botão secundário à direita. Mesmo critério das regras da varredura. Sem achados, uma linha calma com
ícone de check em Status OK: "Nenhum disco sinaliza problema…". Leitura vazia também vai para o topo.

### Accessible Region

Bloco não acionável que precisa ser lido por quem navega no teclado (ex.: cartão de alerta): `AccessibleRegion` entra
no Tab, aparece ao leitor de tela como grupo com o texto inteiro e mostra o anel `CardFocusRing` (2px, Texto
Secundário, cantos de 10px). Títulos de página, seção e cartão de alerta declaram `HeadingLevel` (1, 2, 3).

### Row Reveal Button

Ao lado de cada linha de pasta, um Row Button compacto só com o ícone Fluent de abrir fora (13px, tinta secundária),
nome acessível "Mostrar X no Explorer". A linha em si tem uma ação só (entrar na pasta); a seta aparece apenas onde há
para onde entrar.

### Floating (menus e tooltips)

Menu de contexto e tooltip usam a sombra Flutuante (`FloatingShadow`: desfoque 16, deslocamento 4 para baixo, 22%),
fundo Folha do Laudo, borda Fio do Cartão, cantos de 8px (menu) ou 6px (tooltip). Itens do menu com ícone Fluent de
14px, hover em Nav Hover, atalho em Label secundário à direita.

### Filter Box

Campo de texto com cantos de 6px, borda neutra de 1px e foco com borda Brasa de 2px; o texto de dica vem do `Tag` e
some quando há texto. Seleção na Brasa translúcida.

### Disk Summary Row

Com mais de 4 discos, cartões completos viram uma lista compacta dentro de um único cartão: Expander por disco com
nome em Body semibold e resumo em Label (tipo · tamanho · estado SMART, na cor do estado); abrir mostra as linhas do
cartão completo.

### Folder Row

Ícone Fluent (pasta ou documento) em tinta secundária, nome que corta no fim (dica com o caminho), "% · N arquivos" em Label, tamanho em
Body semibold tabular, chevron só quando dá para entrar, barra de 4px em Traço RAM proporcional à pasta atual. Linhas
de resumo ("Arquivos nesta pasta", "N pastas menores") usam a mesma forma sem navegação. A trilha acima usa Row Buttons
compactos separados por chevrons, sem chevron após o último nível. Nas listas de arquivos, o nome quebra linha
(nunca corta: o fim diferencia arquivos parecidos) e a pasta é encurtada no meio ("C:\Users\…\blobs").

### Table Row (lista ao vivo)

Para listas longas atualizadas no lugar (Processos): `ListBox` virtualizado com o estilo `TableRow`. Linha sem borda,
hover em Nav Hover, seleção com fundo Nav Selected e pílula Brasa de 3px à esquerda (igual à navegação), foco de
teclado com borda de 2px. Colunas de largura fixa compartilhadas com o cabeçalho; números alinhados à direita e
tabulares, com separador de milhar ("1.284.337"); nome corta no fim com reticências (a célula precisa de largura
limitada: Grid, nunca StackPanel horizontal) e a dica traz nome + caminho. Cabeçalho ordenável: Row Buttons compactos
em Label semibold, com a seta Fluent (chevron 9px, tinta secundária) só na coluna ordenada e nunca no modo árvore;
clicar de novo inverte. Árvore: 12px por nível, até 8 níveis. Em larguras estreitas, a coluna menos essencial sai (o
valor continua no painel de detalhes) em vez de cortar todas. Lista vazia ou pesquisa sem resultado diz por quê no
lugar da lista. O painel de detalhes abaixo não passa de 45% da tela e rola por dentro.

Fabricante com assinatura inválida em Critical: "assinatura inválida · Fabricante" (a marca primeiro, para o corte
nunca levá-la, e o fabricante declarado continua visível, porque é a evidência).

### Inline Confirmation

Ação destrutiva (Finalizar processo) confirma no lugar, sem diálogo modal: o botão vira uma caixa com borda neutra de
1px, a frase com o custo ("Trabalho não salvo nesse programa será perdido.") e Primary "Encerrar" + Secondary
"Cancelar". O foco vai para "Encerrar"; Esc/Cancelar volta. Sem cor semântica: não há achado, só uma decisão.

### Named Rules

**The Sem Sobretítulo Rule.** Nenhum rótulo pequeno acima de um título. A categoria vira título de seção; o cartão
leva só a informação real.

### Severity Dot & Verdict

Ponto de 10px na cor semântica antes de cada achado e de cada contagem; o texto da severidade repete a mesma cor.
O veredito do score usa Verdict na cor do rating.

### Live Reading Card

Cartão com rótulo em Body secundário à esquerda e leitura em Metric à direita, seguido de um gráfico de linha de
48–64px (Traço CPU ou Traço RAM, preenchimento translúcido, linha de base no Trilho) ou de uma barra de progresso de 6px.
O eixo X do gráfico cobre o período disponível e a legenda diz qual é ("Últimos 10 s", "Últimos 12 min"). Valor
indisponível é escrito "não disponível", nunca estimado.

### Scrollbar

Sem setas e com trilho transparente; polegar de 4px que engrossa para 7px no hover, cantos de 3px, na cor do tema.

## Do's and Don'ts

### Do:

- **Do** usar a Brasa Leccor em no máximo um botão principal por tela, mais seleção e foco (The Brasa Escassa Rule).
- **Do** medir o contraste de toda cor nova como texto nos dois temas (≥ 4,5:1; ≥ 3:1 para gráficos) antes de usá-la.
- **Do** aplicar cor semântica por estilo com `DynamicResource`, para que mude junto com o tema.
- **Do** dar a todo controle estados visíveis de hover, foco de teclado, pressionado e desabilitado (componentes táteis).
- **Do** escrever "não disponível" quando uma métrica não tem fonte confiável, e mostrar "SEM DADOS" no lugar do score.
- **Do** usar ícones Segoe Fluent com nome acessível para todo ícone de estado.
- **Do** verificar toda tela nos temas claro e escuro, com a janela também em ~1100px de largura.

### Don't:

- **Don't** colocar sombra em cartões, páginas ou botões — sombra só em menus, tooltips e diálogos.
- **Don't** usar o Gradiente da Raposa em texto, botões ou fundos de área; no máximo um elemento de marca por tela.
- **Don't** usar vermelho/laranja para chamar atenção sem um achado de severidade correspondente — nada de alarmes de "PC Cleaner".
- **Don't** usar caracteres Unicode (✓ ✗ →) ou emoji como ícone; usar a família Segoe Fluent.
- **Don't** fixar altura de texto nem cortar rótulos de botão; deixar quebrar linha.
- **Don't** usar Consolas como estética fora do dado técnico do modo avançado.
- **Don't** deixar controles com o visual padrão do WPF (barra de rolagem, caixas de seleção, expander) — todo controle usa a paleta do tema.
