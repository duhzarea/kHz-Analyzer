# Comparação de Motor: Completo vs Amostrado (Teste Rigoroso)

## Metodologia e Parâmetros
- **Hardware**: 32 lógicos
- **Amostra**: 17 arquivos (incluindo 4 limítrofes, 8 fixtures e casos conhecidos).
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
| sustained_18500hz.wav | 18517,9 | ABAIXO | 18517,9 | ABAIXO | 18517,9 | ABAIXO |  |
| sustained_19400hz.wav | 19411,5 | OK | 19411,5 | OK | 19411,5 | OK |  |
| tiny_512samples.wav | 0,0 | ABAIXO | 0,0 | ABAIXO | 0,0 | ABAIXO |  |
| aiff_20500hz_16b44k.aiff | 20517,8 | OK | 20517,8 | OK | 20517,8 | OK |  |
| lowpass_8khz_16b44k.wav | 8017,8 | ABAIXO | 8017,8 | ABAIXO | 8017,8 | ABAIXO |  |
| nyquist_16khz_16b32k.wav | 16000,0 | ABAIXO | 16000,0 | ABAIXO | 16000,0 | ABAIXO |  |
| sine_21khz_24b44k.wav | 21112,6 | OK | 21112,6 | OK | 21112,6 | OK |  |
| stereo_21khz.wav | 21017,1 | OK | 21017,1 | OK | 21017,1 | OK |  |
| sustained_18500hz.wav | 18517,9 | ABAIXO | 18517,9 | ABAIXO | 18517,9 | ABAIXO |  |
| sustained_19400hz.wav | 19411,5 | OK | 19411,5 | OK | 19411,5 | OK |  |
