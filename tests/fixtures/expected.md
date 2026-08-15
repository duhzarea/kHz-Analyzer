# Expected Results

- `sine_21khz_24b44k.wav`: OK (44100Hz, 24-bit, Max Freq ~21000Hz)
- `lowpass_8khz_16b44k.wav`: ABAIXO (44100Hz, 16-bit, Max Freq ~8000Hz)
- `nyquist_16khz_16b32k.wav`: ABAIXO (32000Hz, 16-bit, Max Freq ~15500Hz)
- `invalid_header.wav`: ERRO (Invalid Format)
- `aiff_20500hz_16b44k.aiff`: OK (44100Hz, 16-bit, Max Freq ~20500Hz)
- `aiff_48k_24bit.aiff`: OK (48000Hz, 24-bit, Max Freq ~23000Hz)
- `stereo_21khz.wav`: OK (44100Hz, 16-bit, Channels 2, Max Freq ~21000Hz after mono mix)
- `tiny_512samples.wav`: ERRO (Not enough samples)