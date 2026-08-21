# Limitação de Escala: Sensibilidade ao Tamanho da Janela (FFT)

O critério de detecção de cortes do **kHz Analyzer** foi empiricamente calibrado e validado sobre uma base específica: **FFT de 65536 amostras com agregação por Média**. 

Durante os experimentos para otimização de performance (mudança para janelas menores de 8192 amostras e experimentação com agregação *Max Hold*), descobrimos uma limitação matemática fundamental no desenho do critério atual:

**O critério de corte NÃO é invariante de escala em relação ao tamanho da janela (Bin Width).**

## O Problema

A detecção de presença de sinal de alta frequência repousa sobre a comparação entre:
1. Um **limiar relativo** dinâmico, derivado do pico global de energia do arquivo somado a uma constante absoluta do piso de ruído teórico (ex: -96 dB para 16-bits) mais uma margem fixa de +3 dB.
2. A **densidade espectral de potência (PSD)** por *bin* da Transformada de Fourier.

A energia total do piso de ruído (dither, analógico, etc) é constante no tempo, mas no domínio da frequência ela é **distribuída pelo número de *bins* do FFT**:
- Em um FFT de **65.536** pontos, o ruído é rateado em 32.768 *bins*.
- Em um FFT de **8.192** pontos, o ruído é rateado em apenas 4.096 *bins*.

Como há 8 vezes menos *bins* na janela menor, a energia de ruído contida em cada *bin* individual é concentrada e, portanto, **sobe `10 * log10(8) = 9,03 dB`**.

## O Efeito Prático

Como o limite de ruído tolerado no algoritmo (`PisoDeRuidoTeorico`) é uma **constante absoluta cravada por bit-depth** (que não leva o tamanho do FFT em conta), a redução do tamanho da janela (ou seja, aumento da largura de banda do *bin*) faz com que o ruído de fundo natural da faixa (ex: ruído de dither) passe a violar o limiar de detecção.

Isso cria **Falsos Negativos** perigosos: o ruído elevado engana o motor, que pensa haver "conteúdo harmônico da música" acima de 20 kHz em arquivos que, na verdade, têm cortes abruptos (ex: MP3 16 kHz com *upscale* e dither). O uso de agregações como *Max Hold* (em vez de Média) eleva ainda mais o teto do ruído devido à variância, agravando profundamente a falha.

## Limitação Adicional: A Instabilidade do Denominador Dinâmico na Amostragem

A exploração inicial tentou usar amostragem aleatória (ex: 80 janelas) para acelerar a análise. Descobrimos que, embora rápida, a amostragem isolada **não converge de forma confiável** em faixas de fronteira com alta heterogeneidade (onde a energia aguda só aparece em trechos muito curtos).

A causa matemática não está no cálculo da alta frequência em si, mas no **limiar relativo**:
O critério aceita a presença de altas frequências se a PSD ultrapassar `maxPsd + PisoDeRuidoTeorico + 3.0dB`.
Onde `maxPsd` é o **pico de energia global** do que foi examinado.

Como a amostragem sorteia trechos diferentes, o pico global medido na amostra varia (o denominador flutua). Ao flutuar a referência de teto, a linha de corte "dança" para cima e para baixo. Se a amostra, por acaso, pegar as partes mais graves, a linha de corte desce, e o ruído agudo natural (dither) cruza o limiar gerando um **falso positivo**.

**Evidência Experimental (Gin e Juice):**
Ao testar a mesma faixa com diferentes números de janelas, não houve convergência para o valor exato (20.126 Hz da Análise Completa), mas sim forte oscilação induzida pelo sorteio:
- 80 janelas: 20.449,1 Hz
- 120 janelas: 22.049,3 Hz
- 160 janelas: 16.464,2 Hz (Gerou um **Falso Positivo**)
- 240 janelas: 22.049,3 Hz

## Conclusão e Diretriz de Produto

1. **Janela Intacta:** A janela de FFT de 65536 permanece intocada, pois a redução corrompe o SNR por bin de frequência.
2. **Triagem em Dois Estágios:** Devido à instabilidade do limiar relativo, o aplicativo não expõe livre escolha de amostragem. Em vez disso, ele usa uma arquitetura de triagem condicional:
   - **Estágio 1 (Rápido):** Amostra o arquivo. Se apontar Corte Claro (< 18 kHz) ou Banda Cheia (> 21.5 kHz), aceitamos o veredito direto com segurança estatística.
   - **Estágio 2 (Completo):** Se cair na zona limítrofe (18k - 21.5k), o motor automaticamente engatilha uma Análise Completa (Streaming), eliminando a variabilidade do denominador.
3. **A-Streaming:** A Análise Completa, agora feita por leitura em blocos (streaming), mantém identidade bit-a-bit com o algoritmo original carregando a faixa 100%, mas custando apenas poucos megabytes na memória, o que resolve os travamentos que antes destruíam o SO por excesso de Garbage Collection e paginação.
