# Especificação — Espectrograma e Análise Musical (Key / BPM)

> **Status:** especificação aprovada, execução NÃO iniciada.
> Estas funcionalidades entram como **Fase 5A** e **Fase 5B**, após a Fase 5
> (telas restantes) e antes da Fase 6 (QA). Não devem ser iniciadas enquanto a
> Fase 3 tiver pendências abertas.

## Contexto e referência

O produto de referência para o espectrograma é o **Spek** (spek.cc): leitura de um
arquivo por vez, espectrograma tempo × frequência com escala de amplitude em dB.
O kHz Analyzer deve entregar isso **como um recurso entre outros**, mantendo a
análise em lote como fluxo principal.

A referência para a análise musical é o comportamento de ferramentas de DJ que
detectam tonalidade e andamento ao carregar uma faixa.

---

## PRINCÍPIO ARQUITETURAL OBRIGATÓRIO

O motor de verificação de 19 kHz (`SpectrumAnalyzer`) está validado e é o núcleo
do produto. As novas análises são **módulos independentes e adicionais**:

```
kHzAnalyzer.Core/
├── SpectrumAnalyzer.cs      ← EXISTENTE, validado, NÃO ALTERAR
├── Fft.cs                   ← existente; pode ganhar métodos novos,
│                               sem alterar Welch nem Transform
├── WavAiffReader.cs          ← existente; pode ganhar leitura em blocos
├── Spectrogram/              ← NOVO (Fase 5A)
└── MusicAnalysis/            ← NOVO (Fase 5B)
```

Regras:
- Nenhuma alteração de comportamento em `SpectrumAnalyzer.Analisar`.
- O teste de regressão de fixtures deve continuar passando idêntico depois de
  cada etapa destas fases.
- As novas análises são **opcionais** e sob demanda: a varredura em lote de
  20 kHz não deve ficar mais lenta por causa delas.

---

# FASE 5A — ESPECTROGRAMA

## 5A.1 Motor de STFT

Novo componente, separado do periodograma existente.

**Parâmetros:**
- Janela: 2048 amostras, Hann (mesma ordem de grandeza do Spek, que reporta
  `W:2048`)
- Hop (avanço): 512 amostras (75% de sobreposição) como padrão
- Eixo de frequência: **linear**, de 0 a Nyquist (não logarítmico — é o que o
  Spek faz e é o que permite ler o corte de banda visualmente)
- Amplitude: magnitude em dB, piso configurável, padrão −120 dB, teto 0 dB
- Canais: mixados para mono, consistente com o motor de 20 kHz

**Restrição de desempenho — leia antes de implementar:**
Uma faixa de 5 minutos a 48 kHz tem ~14,4 milhões de amostras. Com hop de 512,
isso resulta em ~28 mil colunas de FFT. A tela tem ~1.800 pixels de largura.

Calcular 28 mil colunas e depois descartar 94% é desperdício, mas simplesmente
aumentar o hop até obter 1.800 colunas **perde transientes** (o espectrograma
fica "liso" e deixa de mostrar as batidas).

Implemente assim: calcule com hop pequeno e **agregue por pixel usando o máximo**
(não a média) das colunas que caem naquele pixel. Máximo preserva transientes;
média os apaga. Torne a estratégia de agregação explícita no código.

**Memória:**
O `WavAiffReader` atual carrega o arquivo inteiro em `float[]`. Para WAV de
24 bits estéreo com 5 minutos isso passa de 100 MB só de amostras. Para o
espectrograma, adicione um caminho de **leitura em blocos** (streaming), sem
alterar o método existente usado pelo motor de 20 kHz. Meça o pico de memória
com uma faixa longa (10+ minutos) e reporte.

## 5A.2 Renderização

- Produzir um buffer BGRA em thread de trabalho; criar o `WriteableBitmap`
  na thread de UI apenas no momento de exibir.
- Progresso visível durante o cálculo (faixas longas levam segundos).
- Cancelável: fechar o painel deve abortar o cálculo em andamento.
- **Cache**: guardar o espectrograma calculado, com chave derivada de caminho +
  tamanho + data de modificação. Reabrir a mesma faixa deve ser instantâneo.
  Definir limite de tamanho do cache e política de descarte.

**Paleta de cores:**
Não copiar a paleta do Spek. Projetar uma escala própria, derivada dos tokens do
design system, respeitando duas exigências técnicas:
- ser **perceptualmente monotônica** (amplitude maior sempre parece mais
  "intensa"; escalas tipo arco-íris falham nisso e criam bandas falsas);
- funcionar para daltônicos — a leitura não deve depender de distinguir vermelho
  de verde.
Oferecer uma variante em escala de cinza como alternativa.

## 5A.3 Eixos e leitura

- Eixo vertical: frequência em kHz, com marcações legíveis
- Eixo horizontal: tempo em `mm:ss`
- Legenda de amplitude em dB
- **Linha de referência em 20 kHz**, destacada: é a razão de existir do app, e
  permite ver de imediato se a faixa tem conteúdo acima dela
- Ao passar o cursor: mostrar tempo, frequência e amplitude naquele ponto
- Cabeçalho com os dados reais do arquivo (formato, taxa, bits, canais,
  duração), no espírito da linha de informação do Spek

## 5A.4 Onde vive na interface

**a) Painel de detalhe (fluxo em lote):**
Ao selecionar uma linha na tabela de resultados, o painel de detalhe (Fase 5)
ganha uma aba/seção com o espectrograma daquela faixa, calculado sob demanda —
nunca durante a varredura em lote.

**b) Inspeção de arquivo único (fluxo Spek):**
Arrastar **um arquivo** (não uma pasta) para a janela deve abrir a visão de
inspeção individual e calcular o espectrograma direto.
Isto exige estender o drag-and-drop já previsto:
- soltar **pasta** → análise em lote (comportamento já especificado)
- soltar **arquivo de áudio** → inspeção individual
- soltar **vários arquivos** → definir e documentar o comportamento
- soltar tipo não suportado → mensagem clara, sem exceção

---

# FASE 5B — DETECÇÃO DE KEY E BPM

## 5B.1 Expectativa de precisão — leia antes de prometer

Registre estas expectativas na documentação e na interface:

| Análise | Precisão realista em música eletrônica |
|---|---|
| BPM | alta (>90%) em 4/4 com pulso estável |
| Tonalidade | moderada (~70–80%); ambígua em faixas com pouco conteúdo harmônico |

Detecção de tonalidade é problema aberto. Não prometa paridade com produtos
comerciais consolidados. **Sempre exibir confiança** junto do resultado, e
nunca apresentar a detecção como verdade absoluta.

## 5B.2 BPM

Abordagem recomendada:
1. Envelope de energia de início de evento (*onset*) via fluxo espectral
2. Autocorrelação ou banco de filtros pente sobre o envelope
3. Busca do pico na faixa de 60–200 BPM
4. **Tratar erro de oitava explicitamente**: 128 e 64 BPM produzem picos
   parecidos. Documentar a heurística de desambiguação escolhida
5. Reportar BPM e confiança

## 5B.3 Tonalidade

1. **Estimar afinação primeiro.** Faixas eletrônicas nem sempre estão em
   A4 = 440 Hz (pitch shift, samples desafinados). Estimar o desvio antes de
   mapear para classes de altura, senão o erro se propaga.
2. Extrair cromagrama (12 classes de altura), com pesos por oitava
3. Correlacionar com perfis de tonalidade maior e menor
4. Reportar: tonalidade mais provável, segunda opção, e confiança
5. Analisar preferencialmente trechos com conteúdo harmônico (evitar intro
   só de bateria)

## 5B.4 Comparação com o nome do arquivo — o recurso de maior valor

Os nomes de arquivo do usuário já trazem Camelot e BPM
(`11A - 128 - Artista - Título`), e o `TrackNameParser` já os extrai.

Implemente **comparação e sinalização de divergência**:
- key detectada × key do nome do arquivo
- BPM detectado × BPM do nome do arquivo
- marcar visualmente quando divergirem
- **nunca sobrescrever** o dado do nome do arquivo automaticamente; o usuário
  decide

Isto tem dois valores: é útil para o DJ (encontra faixas mal marcadas na
biblioteca) e serve como **conjunto de validação real** para medir a precisão da
própria detecção. Use a biblioteca do usuário para medir a taxa de concordância
e registre o número em `docs/`.

## 5B.5 Notação — verificar antes de implementar

O sistema de notação **Camelot** é associado a um produto comercial e pode estar
protegido como marca. Antes de gerar códigos Camelot como funcionalidade do
produto:
- verificar a situação de marca/propriedade dessa notação;
- adotar como padrão a **notação musical convencional** (ex.: "A menor",
  "F# maior"), que é de domínio comum;
- exibir Camelot vindo do nome do arquivo é apenas mostrar dado do usuário —
  isso é diferente de o app gerar e comercializar a notação.
Registrar a conclusão em `docs/design-decisions.md`. Não presumir permissão.

## 5B.6 Custo de processamento

Estas análises são caras. Regras:
- **Não** executar durante a varredura de 20 kHz por padrão
- Opção nas Configurações: analisar key/BPM junto do lote (desligado por padrão)
- No fluxo individual e no painel de detalhe: sob demanda
- Reaproveitar leitura de áudio e FFT quando possível, para não decodificar o
  arquivo três vezes
- Persistir resultados no cache, junto com os do espectrograma

---

## Impacto nas fases existentes

- **Fase 5** (telas): o painel de detalhe passa a hospedar espectrograma e dados
  musicais; a tela de Configurações ganha opções de espectrograma (janela, piso
  em dB, paleta) e de análise musical
- **Fase 6** (QA): estender a cobertura — espectrograma de faixas longas
  (10+ min), mono e estéreo, 44.1 e 48 kHz, WAV e AIFF; medição de pico de
  memória; cancelamento durante cálculo; concordância de key/BPM contra a
  biblioteca real
- **Fase 7** (MSIX): reavaliar tamanho do pacote e uso de memória; o
  espectrograma é a funcionalidade com maior chance de causar falha por memória
  em máquina modesta
- **Relatório DOCX**: decidir se o espectrograma entra como imagem no relatório.
  Se sim, isso muda o gerador (exige adicionar parte de imagem e relacionamento
  ao pacote OPC — hoje o gerador só escreve texto e tabela). Tratar como item
  próprio, não como detalhe.

## Fixtures adicionais de regressão

Acrescentar ao conjunto de teste:
- sinal com varredura de frequência conhecida (*sweep*) → o espectrograma deve
  mostrar uma diagonal; permite validar o mapeamento de eixos
- tom puro em frequência conhecida → deve aparecer como linha horizontal na
  posição correta
- clique periódico em intervalo conhecido → BPM esperado exato
- acorde sintetizado em tonalidade conhecida → tonalidade esperada
