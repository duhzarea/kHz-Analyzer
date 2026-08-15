# kHz Analyzer — Prompt de Execução para o Antigravity

> **Como usar:** execute **uma fase por vez**. Cole a fase, aguarde o relatório,
> valide, e só então avance. Não cole o documento inteiro de uma vez.

---

## CONTEXTO DO PROJETO

**Nome do software:** kHz Analyzer
**Raiz do projeto:** `G:\#kHz Analyzer`
**Versão de referência (a mais atual e funcional):** `G:\#kHz Analyzer\KhzAnalyzer funcionando`

**O que o app faz:** varre em lote arquivos `.wav`, `.aiff` e `.aif` de uma pasta
(recursivamente) e verifica, por análise espectral (FFT), se cada faixa tem
energia real detectável até 20 kHz. Não basta olhar a taxa de amostragem: um
arquivo 44.1 kHz pode estar cortado em 16 kHz por um filtro no master. O
critério compara o espectro com o piso de ruído de quantização do próprio
arquivo (derivado da profundidade de bits, com margem de 3 dB), na banda de
18 kHz a 20 kHz+.

**Saídas atuais:** relatório `.docx` formatado e `.csv` bruto, gravados dentro da
própria pasta analisada. O nome da faixa no relatório segue padrão de gravadora
(estilo Beatport): `Artista - Título (Remix)`, com Key (Camelot) e BPM extraídos
do nome do arquivo quando presentes.

**Referência de produto:** Spek (https://www.spek.cc/) — analisador de espectro
local, leve, instalável, sem dependência de nuvem. O kHz Analyzer deve ter esse
mesmo caráter: **100% local, sem telemetria, sem login, sem envio de áudio para
servidor algum.**

**Objetivo final:** app instalável no Windows (`.exe` / `.msix`), publicável na
Microsoft Store, com identidade visual própria e de alto padrão.

---

## REGRAS PERMANENTES (valem em todas as fases)

1. **Nunca delete arquivos.** Se algo precisar sair do caminho, mova para
   `G:\#kHz Analyzer\_quarentena\<data>\` preservando a estrutura de pastas.
   Relate o que foi movido e por quê.
2. **Nunca altere o critério técnico de análise** (banda de 18–20 kHz, piso de
   ruído por bit depth, margem de 3 dB, mixagem de canais para mono) sem
   justificativa técnica explícita e aprovação. Esse critério já foi validado
   contra um resultado de referência conhecido (ver Fase 6).
3. **Antes de modificar qualquer arquivo, leia-o.** Não reescreva por suposição.
4. Preserve o que funciona. Refatore incrementalmente e valide após cada etapa.
5. Ao final de cada fase, produza um relatório: arquivos criados, alterados e
   movidos; riscos; limitações; o que ficou pendente; próximo passo sugerido.
6. **Não implemente sistema de licença, pagamento, trava premium ou telemetria.**
7. Não exponha segredos, tokens ou credenciais em nenhum arquivo.
8. Mantenha `README.md` na raiz do projeto atualizado ao final de cada fase.
9. A interface deve permanecer responsiva durante análises longas (a análise roda
   fora da thread de UI).

---

## FASE 0 — AUDITORIA E DECISÃO DE STACK

**Não modifique nada nesta fase. Apenas leia e relate.**

Existe uma ambiguidade crítica a resolver antes de qualquer código: a pasta
`KhzAnalyzer funcionando` pode conter uma implementação em **Python (tkinter)**
ou em **C# / WinUI 3** — ambas são executadas por `.bat` e PowerShell, então a
forma de execução não distingue as duas.

Faça:

1. Liste recursivamente `G:\#kHz Analyzer`, ignorando `bin`, `obj`,
   `__pycache__`, `.git` e `node_modules`.
2. Identifique **todas** as versões/cópias do app presentes na raiz, e para cada
   uma determine: linguagem, framework de UI, ponto de entrada, se gera `.docx`,
   se tem seletor de pasta, e se aparenta estar funcional.
3. Determine qual stack a versão de referência usa. Se houver mais de uma
   implementação, aponte qual é a mais completa e por quê.
4. Mapeie: motor de análise (FFT/DSP), leitura de arquivos, geração de relatório,
   acoplamento entre UI e lógica, dependências externas, ausência de cache,
   oportunidades de paralelização, tratamento de erros.
5. Avalie o caminho até MSIX **para a stack encontrada**:
   - Se **WinUI 3 / .NET**: MSIX é nativo. Reporte se `Package.appxmanifest`
     existe e o que precisa ser ajustado.
   - Se **Python**: MSIX exige empacotar o app (ex.: PyInstaller) e embutir
     runtime. Reporte o custo e os riscos disso, incluindo tamanho do pacote e
     comportamento do seletor de pasta dentro do sandbox MSIX.
   - Se **ambas existirem**: recomende explicitamente qual deve ser a base do
     produto final, com justificativa.

**Entregue um relatório e PARE.** Aguarde decisão antes da Fase 1.

**Critérios de aceitação:** todas as versões inventariadas · stack de referência
identificada sem ambiguidade · caminho para MSIX avaliado · nada modificado.

---

## FASE 1 — CONSOLIDAÇÃO E ORGANIZAÇÃO

Com a stack definida na Fase 0:

1. Estabeleça a estrutura canônica em `G:\#kHz Analyzer`, separando claramente:
   - camada de apresentação (UI)
   - camada de aplicação (orquestração)
   - motor de análise / DSP (deve ser executável sem a UI)
   - módulo de relatórios (deve ser independente da tela)
2. Mova versões obsoletas e duplicadas para `_quarentena\<data>\`. **Não delete.**
   Antes de mover qualquer coisa, confirme que a versão de referência continua
   executando; se mover algo quebrar a execução, reverta imediatamente.
3. Crie `.gitignore` cobrindo: `bin/`, `obj/`, `__pycache__/`, `.venv/`,
   `_quarentena/`, `*.csv`, `*.docx`, `.env`, `*.pfx`, `*.snk`.
   **Relatórios gerados (`.csv`/`.docx`) contêm nomes e caminhos da biblioteca
   pessoal do usuário e não devem ser versionados.**
4. Crie/atualize `README.md` na raiz: o que é o app, critério técnico de análise,
   como rodar em desenvolvimento, estrutura de pastas, estado atual.
5. Ao final, execute o app e confirme que continua funcionando.

**Critérios de aceitação:** estrutura clara · nada deletado · app ainda roda ·
`README.md` e `.gitignore` criados.

---

## FASE 2 — EXTRAÇÃO DO DESIGN SYSTEM DE REFERÊNCIA

**Fontes a estudar (leia os arquivos locais):**

- `G:\SITE\ASIMOV\design systems\lumina-video\design-system.html` — **fonte
  primária**
- `G:\SITE\ASIMOV\design systems\lumina-video\index.html` — fonte secundária
  (animações, composição de seções, elementos de destaque)

Leia o CSS (inline e externo) dessas páginas e **extraia os tokens reais**, não
aproximações de memória:

1. **Tipografia:** famílias (primária, pilha de fallback, monoespaçada se
   houver), estratégia de carregamento, e a escala completa — `h1`→`h6`,
   `body-lg`/`body`/`body-sm`, `caption`/`label`/`helper`. Para cada papel:
   família, peso numérico, tamanho, altura de linha, espaçamento entre letras,
   transformação de caixa.
2. **Tratamento tipográfico característico:** a referência usa **lettering
   transparente / vazado, predominantemente branco**. Documente exatamente como
   isso é construído no CSS (preenchimento com gradiente, contorno, máscara de
   opacidade, mistura de camadas — o que for).
3. **Cores:** cada cor em HEX, RGB e HSL; variantes de opacidade; participação em
   gradientes; papel semântico (primária / secundária / acento / neutra /
   feedback / decorativa); regras de uso por contexto (texto, fundo, borda,
   hover, active, disabled); intenção de contraste (AA / AAA quando inferível).
4. **Componentes:** para cada um presente na referência — botões, inputs, cards,
   navbar, seções, modais, badges, tooltips — documente estados (default, hover,
   active, focus, disabled), raio de canto, sombra/elevação, espaçamento interno,
   borda, e a animação associada (propriedade, duração, curva de easing, delay).
5. **Animações e movimento:** catalogue fundos animados, transições de
   surgimento (entrada de elementos), efeitos de glow, micro-interações de
   botão. Registre duração, easing e o que dispara cada uma.
6. **Ícones:** estilo (contorno vs. preenchido), espessura de traço, grade de
   tamanhos.

**Entregue:** um documento `docs\design-system-referencia.md` com todos os tokens
extraídos, e um `docs\design-tokens.md` com a tabela de tradução para a stack do
app (ver Fase 3). **Não escreva código de UI nesta fase.**

**Critérios de aceitação:** tokens extraídos dos arquivos reais (não inventados)
· tratamento do lettering transparente documentado tecnicamente · catálogo de
animações com duração e easing.

---

## FASE 3 — TRADUÇÃO PARA A STACK NATIVA (LEIA COM ATENÇÃO)

**Restrição técnica fundamental:** a referência é HTML/CSS. Se o app é WinUI 3, a
UI é XAML — que **não** suporta CSS. Não existe `backdrop-filter`,
`@keyframes`, `mix-blend-mode`, `filter: blur()` ou `text-stroke`. Tentar
copiar CSS para XAML vai falhar.

O trabalho aqui é **tradução deliberada**, preservando a intenção estética com os
recursos nativos:

| Intenção do CSS | Equivalente nativo (WinUI 3) |
|---|---|
| `backdrop-filter: blur()` | `MicaBackdrop` / `DesktopAcrylicBackdrop`, ou `Composition` blur brush |
| `@keyframes`, `transition` | `Storyboard`, `ThemeTransition`, `Composition` animations, `ImplicitAnimations` |
| Texto com preenchimento gradiente | `TextBlock.Foreground` com `LinearGradientBrush` |
| Texto vazado / contornado | `OpacityMask` sobre camada, ou geometria de texto (`Path`) |
| `box-shadow` / glow | `DropShadow` via Composition, ou `Border` com `ThemeShadow` |
| `:hover`, `:active`, `:disabled` | `VisualStateManager` / estados do `ControlTemplate` |
| Variáveis CSS (`--token`) | `ResourceDictionary` com `ThemeResource` |
| Fundos animados | `Canvas` + Composition, ou `AnimatedVisualPlayer` (Lottie) |

Se a stack for Python/tkinter, seja explícito: **tkinter não é capaz de
reproduzir esse nível visual.** Nesse caso, reporte isso e recomende a migração
para WinUI 3 antes de investir em design, em vez de produzir uma aproximação
pobre.

Produza:

1. `Styles\Tokens.xaml` — cores, opacidades, gradientes, raios, espaçamentos,
   elevações, durações e easings, todos como recursos nomeados.
2. `Styles\Typography.xaml` — a escala tipográfica completa como estilos
   nomeados de `TextBlock`.
3. `Styles\Controls.xaml` — estilos e templates de botões, inputs, cards, badges,
   tooltips, com estados visuais e animações.
4. `Styles\Animations.xaml` — transições e storyboards reutilizáveis.

**Regra:** zero cor, tamanho de fonte, raio ou duração escrito diretamente numa
página. Tudo referencia token. Isso é o que permite tema claro/escuro e mudança
de identidade sem caçar valores espalhados.

**Critérios de aceitação:** tokens centralizados · nenhum valor mágico nas
páginas · tema claro e escuro funcionando · app compila e roda.

---

## FASE 4 — HOME PAGE (VITRINE)

A tela inicial é a peça de maior impacto. Ela deve funcionar como demonstração
das capacidades do app, não como formulário.

Construa:

1. **Hero** com o lettering transparente/vazado branco característico da
   referência, aplicado ao nome **kHz Analyzer**, sobre fundo animado elegante e
   discreto (movimento lento, sem distrair, sem custo alto de GPU).
2. **Fluxo primário em destaque:** selecionar pasta → analisar → ver resultado.
   O usuário deve entender o que fazer em menos de três segundos, sem ler
   instruções.
3. **Dashboard ao vivo:** total de faixas, aprovadas, abaixo de 20 kHz, erros —
   com animação de contagem e transição de entrada escalonada (stagger).
4. **Progresso rico:** arquivo atual, contador, percentual, tempo decorrido,
   ETA com média móvel, velocidade média. A interface nunca deve parecer travada.
5. **Micro-interações:** glow e elevação em hover nos botões, feedback de
   pressionado, estados de foco visíveis para acessibilidade por teclado.
6. **Estado vazio** desenhado com intenção (antes da primeira análise), não uma
   tela em branco.

Aplique as animações de fundo, de surgimento e de componente **de forma
consistente em todas as telas**, não só na home.

**Restrições:** não altere o motor de análise nesta fase · animação nunca deve
bloquear a thread de UI · o app precisa continuar utilizável com animações
reduzidas (respeitando a preferência de "reduzir movimento" do Windows).

**Critérios de aceitação:** hero implementado com o lettering característico ·
fluxo primário óbvio · dashboard animado · sem regressão funcional · sem queda
de responsividade durante análise.

---

## FASE 5 — TELAS RESTANTES

Aplicando os mesmos tokens e animações:

1. **Resultados:** tabela com ordenação por coluna (ordenar por veredito deve
   jogar os problemas para o topo), filtros rápidos (Todas / Aprovadas /
   Problemas / Erros), busca por nome. Precisa manter performance com milhares de
   linhas — use virtualização, não recarregue a lista inteira a cada interação.
2. **Detalhes da faixa:** ao selecionar, exibir nome, formato, taxa de
   amostragem, profundidade de bits, duração, frequência máxima detectada,
   veredito e **o motivo do veredito**. O painel também passará a hospedar (a
   partir da Fase 5A/5B) o **espectrograma** calculado sob demanda e os
   **dados musicais (Key/BPM)** detectados. Não invente métricas: se um dado não
   existe, diga que não está disponível.
3. **Relatórios:** visualizar antes de exportar, gerar `.docx`, exportar `.csv`.
   Módulo independente da tela. Arquitetura preparada para PDF no futuro.
   A inclusão do espectrograma como imagem nos relatórios será avaliada e tratada
   como item próprio (requer pacote OPC no gerador DOCX nativo).
4. **Configurações:** tema (claro / escuro / sistema), número de workers,
   local dos relatórios, intensidade de animação. Incluirá **opções de
   espectrograma** (janela, piso em dB, paleta) e opções para rodar **análise de
   Key/BPM junto ao lote** (desligado por padrão). Persistir e validar.
5. **Sobre:** ícone, nome, versão semântica (MAJOR.MINOR.PATCH), número de build,
   licenças de terceiros.

**Critérios de aceitação:** ordenação, filtros e busca funcionais · performance
com milhares de linhas · detalhes sem dados inventados · configurações
persistentes.

---

## FASE 5A — ESPECTROGRAMA (Nova)
**Especificação completa e obrigatória:** `docs/spec-espectrograma-e-analise-musical.md` (parâmetros de STFT, estratégia de agregação, cache, paleta, expectativas de precisão, restrições de memória e fixtures adicionais).

Adicionar motor de STFT com sobreposição, agregando resultados via max-pooling para visualização (estilo Spek). Processado sob demanda via thread de trabalho com renderização em background (BGRA para WriteableBitmap). Incluir interface gráfica para visualização individual (drag and drop) e dentro do detalhe de faixa. Fazer leitura de arquivos em bloco (streaming) para gerenciar o pico de memória.

## FASE 5B — ANÁLISE MUSICAL: KEY E BPM (Nova)
**Especificação completa e obrigatória:** `docs/spec-espectrograma-e-analise-musical.md` (parâmetros de STFT, estratégia de agregação, cache, paleta, expectativas de precisão, restrições de memória e fixtures adicionais).

Adicionar módulo DSP de detecção de tempo (BPM) via fluxo espectral e tonalidade (Key) via cromagrama. Processado de maneira independente da varredura principal de lote. A interface deve sinalizar divergências comparando com a Key e o BPM já extraídos via `TrackNameParser` a partir do nome do arquivo.

---

## FASE 6 — VALIDAÇÃO FUNCIONAL (NÃO PULE)

**Resultado de referência conhecido.** 
- O teste AUTORITATIVO de regressão é o conjunto de fixtures sintéticas em `tests/fixtures/`, com os valores esperados documentados em `tests/fixtures/expected.md`. É esse que precisa passar 100%.
- A pasta `G:\NOT SHARE` é apenas OBSERVAÇÃO, não referência: ela muda com o tempo, porque é a biblioteca viva do usuário.
- Registre em `docs/regression-baseline.md` o resultado observado mais recente, com data e contagem, e deixe explícito que serve como referência histórica.
- **Nota Histórica:** A baseline anterior (Total: 113 · OK: 112 · Abaixo: 1 para "Jackie, Toni B - INFECTED.wav" a ≈16.630 Hz) foi descontinuada porque a pasta real mudou e esse arquivo não existe mais. Essa baseline antiga não deve ser reintroduzida para evitar falsas quebras.

Rode o app redesenhado na pasta real apenas como observação. **Se os testes de fixtures não baterem exatamente, o motor foi alterado indevidamente — investigue e corrija antes de prosseguir.** Este é o teste de regressão mais importante do projeto.

Teste também:

- pastas diferentes, em discos diferentes, incluindo subpastas aninhadas
- pasta sem nenhum arquivo de áudio
- arquivo corrompido e formato não suportado (erro deve ser isolado, não derrubar
  a análise inteira)
- caminhos com acentos, espaços, `#` e nomes longos
- biblioteca grande (comportamento de memória e responsividade)
- cancelamento no meio da análise (incluindo cálculo de espectrograma)
- **[Item aberto de QA]** cancelar durante a fase de enumeração de arquivos (como uma enumeração local rápida leva ~23ms, deve ser validado testando o acesso a uma pasta grande na nuvem que exija latência, criando janela manual de clique).
- tema claro, escuro e alternância em tempo de execução
- **Testes estendidos das Fases 5A/5B:** espectrograma de faixas longas (10+ min), mono e estéreo, 44.1 e 48 kHz, WAV e AIFF; medição de pico de memória para leitura em blocos; concordância de Key/BPM contra uma biblioteca real.

**Critérios de aceitação:** números idênticos à referência · erros isolados por
arquivo · sem travamento de UI · relatório de QA produzido.

---

## FASE 7 — EMPACOTAMENTO: EXE E MSIX

**Objetivo:** instalador Windows funcional, no modelo do Spek — app local,
instalável, sem dependência de nuvem.

1. **Build de release** e geração do executável.
2. **MSIX:**
   - `Package.appxmanifest` com identidade, nome de exibição, publisher,
     descrição, e os ícones em todas as escalas exigidas.
   - Declarar a capacidade de acesso amplo ao sistema de arquivos
     (`broadFileSystemAccess`), **necessária porque o app lê pastas arbitrárias
     em qualquer disco** — e documentar essa justificativa, porque ela será
     avaliada na certificação da Store.
   - **Testar o seletor de pasta dentro do pacote MSIX instalado**, não só em
     modo de desenvolvimento. O comportamento de acesso a arquivos difere entre
     os dois; isso é a causa mais comum de app que funciona no dev e falha
     instalado.
   - **Reavaliar tamanho e uso de RAM do pacote:** o espectrograma e a leitura 
     em blocos para cálculos em lote longos podem estourar a memória de máquinas
     modestas. Certificar que há mitigação e coletas de garbage collection adequadas.
3. **Assinatura — seja explícito com o usuário sobre isso:**
   - Para instalar localmente: gerar certificado autoassinado e instalá-lo como
     confiável na máquina. Sem isso o Windows recusa o MSIX.
   - Para a Microsoft Store: a assinatura é feita pela Microsoft no processo de
     submissão; o certificado local não é usado lá.
   - **Nunca versione o arquivo de certificado (`.pfx`) nem sua senha.**
4. **Ícone e identidade:** substituir os ícones padrão do template por ícones
   próprios do kHz Analyzer, em todos os tamanhos requeridos.
5. **Testar instalação em máquina limpa** (ou usuário limpo), sem SDK nem
   runtime de desenvolvimento instalados. Verificar se o Windows App Runtime é
   resolvido corretamente como dependência.
6. **Documentar o processo de release** em `docs\release.md`, com os comandos
   exatos, de forma reproduzível.

**Critérios de aceitação:** MSIX gerado · instalação testada · seletor de pasta
funcionando **dentro do pacote instalado** · máquina limpa validada · release
documentado · `README.md` atualizado.

---

## FASE 8 — PREPARAÇÃO PARA A MICROSOFT STORE

1. Verificar requisitos de certificação e rodar o Windows App Certification Kit.
2. Preparar os ativos da listagem: capturas de tela, descrição, palavras-chave,
   classificação de idade.
3. Redigir política de privacidade. Como o app é **totalmente local, sem coleta
   de dados, sem telemetria e sem envio de áudio para servidores**, a política
   deve declarar exatamente isso — é uma vantagem de posicionamento, não uma
   formalidade.
4. Documentar o fluxo de submissão e versionamento de releases futuros.

**Critérios de aceitação:** certificação passando · ativos preparados · política
de privacidade redigida · fluxo de submissão documentado.

---

## O QUE NÃO FAZER (EM NENHUMA FASE)

- Deletar arquivos (mova para `_quarentena\`)
- Alterar o critério de análise espectral sem justificativa aprovada
- Implementar licença, pagamento, trava premium ou telemetria
- Enviar áudio, caminhos ou metadados do usuário para qualquer servidor
- Versionar relatórios gerados, certificados ou segredos
- Copiar CSS literalmente para XAML
- Considerar uma fase concluída só porque o app abre
