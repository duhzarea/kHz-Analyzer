# Comparação de Motor: Completo vs Amostrado (Teste Rigoroso)

## Metodologia e Parâmetros
- **Hardware**: 12 lógicos
- **Amostra**: 21 arquivos (incluindo 11 limítrofes, 8 fixtures e casos conhecidos).
- **Semente Determinística**: O hash foi trocado para um DJB2 estável de string em C#, provando o determinismo inter-processos.

### Legenda de Divergências
- 🚨 **FALHA DE DETERMINISMO**: F não bateu com F2. Risco Crítico.
- ⚠️ **FALSO NEGATIVO**: F aprova o que A reprova. Risco grave de deixar master cortado passar.
- ⚠️ **FALSO POSITIVO**: F reprova o que A aprova. Incômodo, mas não perigoso.

| Arquivo | FreqMax A | Ver A | FreqMax F | Ver F | FreqMax F2 | Ver F2 | Divergência |
|---|---|---|---|---|---|---|---|
| aiff_20500hz_16b44k.aiff | 20517,8 | OK | 20517,8 | OK | 20517,8 | OK |  |
| aiff_48k_24bit.aiff | 23118,9 | OK | 23118,9 | OK | 23118,9 | OK |  |
| invalid_header.wav | 0,0 | ABAIXO | 0,0 | ABAIXO | 0,0 | ABAIXO |  |
| lowpass_8khz_16b44k.wav | 8017,8 | ABAIXO | 8017,8 | ABAIXO | 8017,8 | ABAIXO |  |
| nyquist_16khz_16b32k.wav | 16000,0 | ABAIXO | 16000,0 | ABAIXO | 16000,0 | ABAIXO |  |
| sine_21khz_24b44k.wav | 21112,6 | OK | 21112,6 | OK | 21112,6 | OK |  |
| stereo_21khz.wav | 21017,1 | OK | 21017,1 | OK | 21017,1 | OK |  |
| tiny_512samples.wav | 0,0 | ABAIXO | 0,0 | ABAIXO | 0,0 | ABAIXO |  |
| Gnarls Barkley - Crazy (GABSS AFTERNOON EDIT).wav | 16229,3 | ABAIXO | 16275,7 | ABAIXO | 16275,7 | ABAIXO |  |
| 5A - 130 - Fatsync, DOUG! - Fire!.wav | 19729,8 | ABAIXO | 19737,9 | ABAIXO | 19737,9 | ABAIXO |  |
| 11A - 128 - GREG Matrix Noise.aiff | 20936,3 | OK | 20868,4 | OK | 20868,4 | OK |  |
| 12A - 130 - Vintage Culture & Volkoder - Pornstar.aiff | 20069,6 | OK | 20073,7 | OK | 20073,7 | OK |  |
| 1A - 128 - GREG Eyes On Me.aiff | 21300,4 | OK | 21301,7 | OK | 21301,7 | OK |  |
| 2A - 127 - Adam K & Gabe - ID.wav | 20188,1 | OK | 20196,8 | OK | 20196,8 | OK |  |
| 4A - 130 - Gabe & Fezzo - Gin e Juice [UNR].wav | 20126,1 | OK | 20449,1 | OK | 20449,1 | OK |  |
| 8A - 130 - TOUGH ART - ROLLERCOASTER MASTER1.wav | 20117,4 | OK | 20117,4 | OK | 20117,4 | OK |  |
| Anyma x Volkoder - Another Dimension.wav | 20151,7 | OK | 20154,4 | OK | 20154,4 | OK |  |
| Blue Monday (Tough Art Edit).wav | 21402,7 | OK | 21406,7 | OK | 21406,7 | OK |  |
| Gabss, Balanka - Bottles (Original Mix).aiff | 21174,5 | OK | 21174,5 | OK | 21174,5 | OK |  |
| Laid Back - White Horse (Tough Art Edit) Master.wav | 21394,6 | OK | 21489,5 | OK | 21489,5 | OK |  |
| Vylow, Dekho - Handle That [Master V1].aiff | 21435,0 | OK | 21466,6 | OK | 21466,6 | OK |  |
