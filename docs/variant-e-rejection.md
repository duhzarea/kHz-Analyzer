# Rejeição da Variante E (Piso de Ruído Medido)

A **Variante E** tentou solucionar o problema do Fator de Crista estimando dinamicamente o piso de ruído do próprio arquivo, em vez de depender da profundidade de bits teórica.

## Metodologia Testada

O piso de ruído foi medido lendo a estatística de potência na banda final de alta frequência (entre `Nyquist - 1kHz` e `Nyquist`).

## Causa da Rejeição

A Variante E provou ser instável e enviesada, gerando quase que garantidamente subestimações da frequência máxima real:

1. **Subestimação Sistemática**: Faixas de 44.1 kHz, que tradicionalmente cravam 22.050 Hz no método Welch original, batiam em 21.716 Hz. As de 48 kHz batiam em 23.947 Hz em vez de 24.000 Hz.
2. **Contaminação pelo Sinal Real**: O maior problema arquitetural é que a banda medida (`Nyquist - 1kHz` a `Nyquist`) não continha _apenas_ ruído. Nas faixas legítimas sem corte, o sinal real do áudio se estendia por essa banda, elevando artificialmente o limiar de ruído.
3. **Falsos Positivos Induzidos**: Essa contaminação aumentava o limiar necessário para aprovar uma faixa. Se a energia chegasse até 19.972 Hz, mas o limiar medido estivesse muito alto por causa de harmônicos na faixa de 21-22 kHz, o método E a rejeitaria, classificando-a incorretamente como "ABAIXO" (como ocorreu com "R3ckzet - Breathe").

## Conclusão para o Futuro

A premissa de medir empiricamente o piso usando o mesmo estimador (Máximo vs Máximo) continua correta para anular o problema do fator de crista. No entanto, sua implementação exige que a medição seja feita em uma região onde **comprovadamente não há sinal**. Como os formatos de áudio sem perdas (lossless) guardam o sinal até o Nyquist real, não há uma "banda morta" universalmente segura e genérica dentro da faixa audível.

A implementação dessa lógica só seria possível se encontrarmos um critério inequívoco para determinar o piso (talvez procurando a banda de menor energia do espectro inteiro, em vez de focar perto do Nyquist). Até lá, a Variante E permanece descontinuada.
