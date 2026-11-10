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

## Evidência Experimental (F-80 vs A-Streaming)

O método F-80 (80 janelas, média) foi testado contra o A-Streaming sobre a biblioteca completa. O erro não tem limite conhecido e ocorre nos dois sentidos: na série de *Gin e Juice*, a amostragem com 160 janelas deu 16.464 Hz contra 20.126 Hz do método completo (subestimação de 3,6 kHz); com 40 janelas, *Crazy* deu 21.662 Hz contra 16.229 Hz (superestimação de 5,4 kHz).

Terminologia usada no projeto: **falso negativo** = a amostragem APROVA o que o método completo REPROVA (o erro perigoso, deixa passar um master cortado); **falso positivo** = a amostragem reprova o que o método completo aprova.

O F-80 pode aprovar faixas que na verdade não atingem os 19 kHz exigidos, ou seja, produz **falsos negativos**. Um exemplo concreto dessa falha foi registrado:
- **Exemplo de Falha do F-80:** Ocorreu um caso extremo em que uma faixa reprovada no A-Streaming obteve `maxF_for_BelowA = 21829.3 Hz` no F-80.
Isso prova que o F-80 não é estatisticamente seguro para aprovar faixas em nenhuma hipótese.

## Conclusão e Diretriz de Produto

1. **Janela Intacta:** A janela de FFT de 65536 permanece intocada, pois a redução corrompe o SNR por bin de frequência.
2. **Abandono da Triagem:** A ideia de usar o F-80 como "Estágio 1" para reprovação prévia foi desativada no caminho de produção. Testes de performance revelaram que rodar o F-80 e depois o A-Streaming para as exceções é, na média, **mais lento** do que simplesmente rodar o A-Streaming em tudo desde o início, devido à altíssima eficiência alcançada pela nova implementação.
3. **A-Streaming Definitivo:** O A-Streaming agora é o motor único e definitivo. Com otimizações agressivas de reuso de buffer de memória para o FFT (evitando alocações a cada janela e pressão no Garbage Collector), a leitura em streaming por blocos atinge performance insuperável mantendo exatidão matemática irretocável em relação ao método original de carregamento completo, consumindo pouquíssima RAM.
