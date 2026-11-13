# Changelog

Todas as mudanças relevantes do kHz Analyzer. Datas no fuso de São Paulo.
Branch de trabalho: `spec-v2`.

## [0.12.0] — 2026-11-12 · Proposta B

Redesign completo da interface (padrão "Proposta B"), player integrado, atalhos de teclado e a nova tela inicial.

### Novidades
- **Tela inicial (hero) e hub de módulos.** O app abre na apresentação oficial: título "kHz Analyzer", espectro ao vivo que alterna entre *Lossless* e *MP3 → WAV* (corte em 16.229 Hz sombreado em vermelho e selo "⚠ Below 20 kHz, check"), três módulos centralizados (Analyzer, Download & Trim, Snippets) e o rodapé com **Settings**, **About** e a marca **v0.12.0 · Crafted by DUHZZ** (versão lida do próprio build, discreta, acende ao passar o mouse e abre o GitHub). A tag "Design System" foi removida.
- **Vermelho Apple Music (#FA243C)** em todos os destaques, botões primários e estados ativos, inclusive nos controles nativos do Windows (rampa de acento do sistema).
- **Now Playing:** reprodução de WAV e AIFF pelo próprio decodificador PCM do app (o Windows não decodifica AIFF), forma de onda da faixa, timeline com bolinha em forma de gota que acompanha a música e aceita clique e arraste, preenchimento vermelho do tempo decorrido, volume e repetir.
- **Menu do botão direito na tabela:** Play, Track information, Show full analysis (espectro médio real da faixa com a marca de 20 kHz), Show in Explorer, Remove from queue.
- **Tooltip das faixas:** nome completo, formato, taxa, bits, frequência máxima, veredito e caminho.
- **Barra lateral recolhível** com o botão de painel no topo e **Ctrl+B**.
- **21 atalhos de teclado** e a seção **Settings › Keyboard Shortcuts** que lista todos.
- **Troca de tema estilo macOS:** o novo tema se espalha em círculo a partir do ponto clicado.
- **Settings › Animations:** On (padrão), Follow Windows ou Off.
- Item **Home** na barra lateral e a tecla **Home** para voltar à tela inicial.
- **Theme, About e Settings** na mesma régua dos outros itens da barra lateral, 20 px acima da borda da janela.
- **Relevo de vidro** nos três módulos da tela inicial e no botão de tema: borda chanfrada com a luz principal no canto superior esquerdo, linha estrutural no meio e retorno quente do vermelho da marca embaixo à direita.
- Licença **GPL-3.0**.
- Fontes da marca: Instrument Serif (títulos), Inter (texto) e Geist Mono (dados), com as licenças OFL incluídas.

### Correções
- **Coluna BPM mostrava o número da faixa.** Arquivos como `01 - Artista - Título.wav` exibiam "01" como BPM. Agora só valores entre 60 e 220 contam como BPM; o número da faixa é descartado.
- **Teste de referência de 19,4 kHz ainda esperava aprovação** pela regra antiga de 19 kHz. Agora espera ⚠. Verificação completa: 10 de 10.
- **Tela inicial transparente:** o Analyzer aparecia por trás, e um campo de pasta escondido podia receber o que era digitado. A tela inicial agora tem fundo próprio e o shell fica fora do foco até um módulo ser escolhido.
- **Atalhos ignorados logo após abrir o app:** nada recebia o foco do teclado. O foco agora vai para o primeiro módulo (sem anel de foco visível) e volta para a barra lateral quando se perde.
- **Animações nunca rodavam** com "Efeitos de animação" desligado no Windows. Agora são controladas por Settings › Animations.
- **Troca de tema mostrava partes faltando** durante a transição: as animações de entrada não gravavam o valor final, e a foto do tema antigo saía sem esses elementos.
- **Tema escuro marrom/azulado:** o acrílico deixava o papel de parede atravessar. Uma camada escura neutra foi adicionada.
- **Enter na tabela não tocava a faixa:** o item da lista consumia a tecla.
- **Bolinha da timeline iluminada de cima**, destoando do resto da tela, que é iluminado do canto superior esquerdo. Agora é uma conta de vidro na mesma luz, com retorno quente do trilho vermelho e sombra de contato.
- **Botões do player grandes demais**, vazando para fora do card. Agora têm 36 e 50 px e ficam dentro do player.
- **Pop-ups e tooltips translúcidos demais**, com a tabela vazando por trás. Agora têm fundo quase sólido, borda e sombra.
- **Hover quase invisível nos botões** (diferença média medida de 3,6 em 255). Os novos botões têm brilho, elevação e reflexo.
- **Fechamento ao abrir a tela inicial:** o seletor Lossless/MP3 disparava antes de os controles existirem. Encontrado e corrigido durante o desenvolvimento.
- **Teclado ABNT2:** Ctrl+/ usa a tecla própria do "/"; os números do teclado numérico também acionam Ctrl+1…3.

### Problemas conhecidos
- **Ctrl+1** pode não chegar ao app se outro programa do PC registrar esse atalho global. O botão Analyzer e o F5 continuam funcionando.
- **Tolerância de MP3 (19 kHz):** a regra existe, mas o motor ainda não lê arquivos MP3; precisa da decodificação via FFmpeg.
- **Primeira análise sem cache** levou cerca de 15 min na versão Debug, contra cerca de 4,5 min no teste do motor. Falta medir a versão Release instalada.

## [0.11.0] — 2026-11-11 · Regra dos 20 kHz

### Mudanças
- **Limite de aprovação passou de 19 kHz para 20 kHz** em todo o app e nos relatórios. Toda faixa abaixo de 20 kHz recebe ⚠. Faixas de MP3 entre 19 e 20 kHz são marcadas como "tolerated", mas continuam com ⚠. Os relatórios mostram sempre o valor absoluto em Hz.
- Novo ícone do app e pastas salvas dos Snippets.
- **Snippets:** gerador de prévias MP3 em lote.
- **Download & Trim:** baixa áudio ou vídeo de um link, inteiro ou cortado por tempo, com FFmpeg e yt-dlp.
- Novo shell, build sem pacote MSIX e **instalador que deixa escolher a pasta e o disco** (Inno Setup), além da versão portátil em .zip.

### Efeito na biblioteca de referência (G:\NOT SHARE, 465 faixas)
- 458 aprovadas, 6 com ⚠ (2 entre 19 e 20 kHz e 4 abaixo de 19 kHz), 1 ilegível.

## [0.10.0] — 2026-11-10

- Motor de análise em streaming verificado contra o método original em memória: 43 de 43 arquivos idênticos e mesmo resultado com 1 ou 4 processos. Decisões da spec v2.1 registradas em `docs/spec-v2.1-decisions.md`.
