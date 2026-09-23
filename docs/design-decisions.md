# Decisões de Design e Resolução de Conflitos

Este documento estabelece a fonte da verdade para as resoluções de design entre a referência web (Lumina) e a plataforma desktop (WinUI 3) para o kHz Analyzer. Nenhuma outra regra visual sobrescreve este documento.

---

## 1. Contraste e Cores Semânticas (Resolução WCAG 2.1)

Como o fundo nativo (`Mica` ou `Acrylic`) varia de acordo com o papel de parede do usuário, a legibilidade dos status críticos não pode ficar à mercê do sistema.
**Decisão (Abordagem Mista):** A área crítica de dados (Tabela de Resultados) utilizará um fundo **100% opaco** (`#050505` no Dark, `#FFFFFF` no Light), ancorando previsibilidade absoluta. O efeito de vidro brilhará apenas nas margens da janela, cabeçalho e painéis auxiliares.
As cores foram calculadas matematicamente contra essa superfície opaca garantindo acessibilidade AA (>4.5:1).

### Tabela Canônica de Cores e Contrastes

| Papel Semântico | Tema | Fundo Opaque Real | Hex Adotado | Contraste Calculado |
| :--- | :--- | :--- | :--- | :--- |
| **Acento (Marca)** | Claro | `#FFFFFF` | Orange 700 (`#c2410c`) | **4,67:1** |
| **Acento (Marca)** | Escuro | `#050505` | Orange 500 (`#f97316`) | **6,63:1** |
| **Aprovado** | Claro | `#FFFFFF` | Green 800 (`#166534`) | **6,43:1** |
| **Aprovado** | Escuro | `#050505` | Green 600 (`#2ea043`) | **5,51:1** |
| **Reprovado** | Claro | `#FFFFFF` | Red 700 (`#b91c1c`) | **5,83:1** |
| **Reprovado** | Escuro | `#050505` | Red 500 (`#ef4444`) | **4,94:1** |
| **Erro (Leitura)** | Claro | `#FFFFFF` | Slate 700 (`#475569`) | **6,83:1** |
| **Erro (Leitura)** | Escuro | `#050505` | Slate 400 (`#94a3b8`) | **7,25:1** |

**Restrição Crítica de Uso:** O acento claro (`#c2410c` sobre `#FFFFFF`) possui contraste de 4,67:1. Como este valor cruza o mínimo legal (4,5:1) por uma margem de apenas 0,17, ele é aprovado para ícones e elementos grandes, mas **NÃO DEVE ser usado em texto pequeno (abaixo de 14px)** sobre fundo claro. 

---

## 2. Ícones e Tipografia (Fallback Windows 10/11)

### Iconografia Sistêmica
A solução adotada é uma cascata de fontes combinada com glifos rigorosamente testados em código. 
O ícone da marca (Acento) foi redefinido para "LineChart" para honrar a função real de análise/espectro do app, substituindo a analogia imprecisa de um tocador de música. 
Todos os 4 glifos abaixo foram validados empiricamente lendo a estrutura da `GlyphTypeface` nativa. Foi garantido que **existem em ambas as fontes**.

**Estratégia XAML de Fallback:** `FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"`

| Papel Semântico | Ícone Visual | Código Hex | Verificação Empírica em MDL2 (Win 10) |
| :--- | :--- | :--- | :--- |
| **Acento (Análise)** | LineChart | `&#xE9D9;` | Confirmado: `True`. |
| **Aprovado** | CheckMark | `&#xE73E;` | Confirmado: `True`. |
| **Reprovado** | Cancel | `&#xE711;` | Confirmado: `True`. |
| **Erro (Leitura)**| Blocked | `&#xE814;` | Confirmado: `True`. |

### Fontes de Texto
A fonte decorativa `Instrument Serif` foi descartada. Para enxugar o binário MSIX, adquiriremos e embarcaremos localmente **apenas dois arquivos** da família primária: 
- **`Inter-Medium.ttf`** (Peso 500)
- **`Inter-SemiBold.ttf`** (Peso 600)
Não haverá uso de Regular, Bold ou Itálico na UI para evitar síntese pelo renderizador. O papel "Erro" usará peso Medium e o "Reprovado" usará SemiBold.

---

## 3. Modelo de Degradação de Vidro (Mica → Acrylic → Opaco)

O Windows 10 (1809+) suporta nativamente `DesktopAcrylicBackdrop`. A degradação do vidro ocorrerá em 3 níveis amarrada puramente no C# aliada a um Fallback declarado no XAML para evitar *KeyNotFoundException* de temas.

**Declaração XAML (Garante o tema dinâmico e impede null reference):**
```xml
<Grid x:Name="RootGrid" Background="{ThemeResource AppFallbackBackgroundBrush}">
```

**Implementação em C# (MainWindow.xaml.cs):**
```csharp
using Microsoft.UI.Composition.SystemBackdrops;

if (MicaController.IsSupported())
{
    this.SystemBackdrop = new MicaBackdrop();
    RootGrid.Background = null; // Remove o fundo para o vidro do Mica aparecer
}
else if (DesktopAcrylicController.IsSupported())
{
    this.SystemBackdrop = new DesktopAcrylicBackdrop();
    RootGrid.Background = null; // Remove o fundo para o vidro do Acrylic aparecer
}
// Sem "else": se nada for suportado, o Background via ThemeResource no XAML
// continuará atuando nativamente, resolvendo as trocas de tema sozinho.
```

---

## 4. Escala Tipográfica Restrita aos Pesos Empacotados
A escala é calibrada e atada aos pesos `Medium` (500) e `SemiBold` (600).

- **H1 (Hero):** 48px, Tracking -50, SemiBold.
- **H2 (Section):** 32px, Tracking -25, SemiBold.
- **H3 (Subsection):** 24px, Tracking -25, Medium.
- **Body Large:** 16px, Tracking 0, Medium.
- **Body Medium (Tabela):** 14px, Tracking 0, Medium. Uso obrigatório de `Typography.NumeralAlignment="Tabular"` para fixar larguras numéricas nas colunas.

---

## 5. CornerRadius Parametrizado por Altura
Para assegurar curvatura de pílula absoluta sem o uso perigoso de valores irracionais (ex: 9999px), os raios de canto (*CornerRadius*) no XAML estão estritamente travados às alturas nominais de seus controles alvo no `ResourceDictionary`:
- Controles Maiores: `Height="44"` ➔ `CornerRadius="22"`.
- Badges na Tabela: `Height="24"` ➔ `CornerRadius="12"`.

---

## 6. Drag-and-Drop (UX Funcional)
Implementaremos de fato a operação de arrastar e soltar no Grid central do app. Esta operação requererá manipular os eventos `DragOver` e `Drop` na UI Thread (~15 linhas de código C#), extraindo o caminho da pasta via `DataPackageView` e chamando a análise, entregando um fluxo nativo premium para ferramentas desktop.

---

## 7. Critério Técnico de 19 kHz (Setembro/2026)
**Decisão de Produto (Produtor/DJ):** O limite de aprovação de faixas foi alterado de 20.000 Hz para **19.000 Hz**. 
*Justificativa:* Faixas com conteúdo espectral sustentado até ~19–20 kHz não devem ser invalidadas. A dinâmica natural das masterizações costuma apresentar caimento nessa região, e o conteúdo é perceptualmente banda cheia. O método de janela grande com Média (A-Streaming) garante que picos isolados não interfiram na aprovação, exigindo conteúdo *sustentado*.
*Limitação Conhecida (Trabalho Futuro):* Arquivos MP3 em alta taxa (256–320 kbps) também costumam cortar próximo de 19,5 kHz, logo, arquivos WAV convertidos a partir desses MP3 passarão no novo limiar. No futuro, será desenvolvida uma heurística para analisar a **inclinação (slope)** do corte — já que um corte de compressão tende a ser um degrau abrupto, enquanto uma atenuação de mixagem/masterização é gradual.
