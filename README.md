# kHz Analyzer

O **kHz Analyzer** é uma ferramenta de análise técnica de áudio desenhada para música eletrônica. Ela varre em lote arquivos de áudio (`.wav`, `.aiff`, `.aif`) de uma pasta (de forma recursiva) e verifica, por meio de análise espectral nativa (FFT), se cada faixa possui energia real detectável até a banda de 20 kHz.

## Critério Técnico de Análise

O aplicativo não se fia apenas nos metadados de taxa de amostragem (ex: 44.1 kHz), pois um arquivo pode ter sofrido um *hard cut* aos 16 kHz no processo de masterização. O critério de avaliação utiliza:
*   **Banda de Análise:** Verifica a energia espectral entre 18 kHz e 20+ kHz.
*   **Piso de Ruído:** O limiar de corte é calculado a partir do piso de ruído de quantização do próprio arquivo (derivado da profundidade de bits: 16, 24 ou 32 bits).
*   **Margem:** Utiliza-se uma margem de segurança de 3 dB acima do piso de ruído.
*   **Processamento:** Os canais (quando estéreo ou superior) são mixados para mono e a Densidade Espectral de Potência (PSD) é calculada usando uma implementação otimizada sem dependências externas.

## Estrutura de Pastas

*   **`KhzAnalyzer`:** Camada de apresentação e orquestração. Contém o projeto principal em C# / WinUI 3 (Windows App SDK), responsável pela interface gráfica, interações do usuário e paralelismo na execução das análises.
*   **`kHzAnalyzer.Core`:** Motor de análise matemática e DSP (Digital Signal Processing). Totalmente nativo em C#, sem dependências externas. Contém leitores próprios para cabeçalhos WAV/AIFF e a implementação da FFT.
*   **`kHzAnalyzer.ConsoleTest`:** Projeto auxiliar para testes CLI (Command Line Interface), utilizado para a validação matemática do motor e testes unitários/funcionais diretos contra bibliotecas de referência.
*   **`docs`:** Documentação do projeto, documentação do design system e instruções de release.
*   **`_quarentena`:** Diretório que guarda implementações obsoletas (ex: interfaces antigas em Python/Eel) e arquivos descartados. Ignorado pelo Git.

## Estado Atual

*   O motor de análise matemática (`kHzAnalyzer.Core`) está **validado, robusto e finalizado**, dispensando dependências de terceiros (como NAudio ou MathNet).
*   A interface (`KhzAnalyzer`) está estabelecida em **WinUI 3**, integrando-se perfeitamente com o motor de análise via C#. 
*   **Geração de Relatórios:** Implementação nativa (`.docx` e `.csv`) totalmente concluída dentro de `kHzAnalyzer.Core` (sem dependências como OpenXml), rodando de forma paralela via Task.
*   **Em desenvolvimento:** Redesign visual premium (Design System) da interface WinUI 3 (Fases 2 e 3).

## Como Rodar em Desenvolvimento

Para executar o aplicativo a partir do código-fonte:

1. Certifique-se de ter o .NET SDK (versão 10.0 ou apropriada instalada) e o Windows App SDK.
2. Navegue até a pasta da interface gráfica:
   ```cmd
   cd "KhzAnalyzer"
   ```
   ```cmd
   "1 - Rodar kHz Analyzer.bat"
   ```
   *Alternativa:* Abra o repositório em uma IDE compatível (Visual Studio) e defina o projeto `KhzAnalyzer` como inicialização.

## Próximas Fases (Roadmap)

Conforme as novas especificações, o desenvolvimento seguirá com:
*   **Fase 5A:** Espectrograma de áudio (tempo × frequência, estilo Spek) com processamento sob demanda, preservação de transientes e escala de cor perceptualmente monotônica.
*   **Fase 5B:** Detecção de Key (tonalidade) e BPM com extração nativa via DSP (fluxo espectral, autocorrelação e cromagrama) e comparação com metadados do nome do arquivo.
*(Ambas posicionadas após a Fase 5 e antes da Fase 6 de Validação Funcional).*
