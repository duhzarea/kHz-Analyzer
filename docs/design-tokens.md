# Tradução de Design Tokens: Web -> WinUI 3

Esta tabela traduz os tokens fundamentais extraídos da referência Web (HTML/Tailwind/CSS) para propriedades nativas disponíveis no ecossistema XAML do WinUI 3.

**Legenda de Viabilidade:**
- **[DIRETO]** — Tem equivalente nativo 1:1 no XAML/WinUI.
- **[ADAPTADO]** — A intenção do design será mantida, mas a tecnologia difere.
- **[INVIÁVEL]** — O motor de renderização do WinUI não suporta. Alternativas propostas visando a harmonia da interface.

---

### Tipografia
| Web Token (CSS/Tailwind) | WinUI 3 (XAML) | Viabilidade | Notas |
| :--- | :--- | :--- | :--- |
| `font-family: 'Inter'` | `FontFamily="ms-appx:///Assets/Fonts/Inter.ttf#Inter"` | **[DIRETO]** | A fonte precisará ser empacotada no projeto em pasta de assets local. |
| `text-6xl` (96px, 72px, etc) | `FontSize="96"` | **[DIRETO]** | Unidade nativa em pixels efetivos (epx). |
| `font-medium` (500), `semibold` (600) | `FontWeight="Medium"` ou `FontWeight="SemiBold"` | **[DIRETO]** | |
| `tracking-tight` (-0.025em) | `CharacterSpacing="-25"` | **[ADAPTADO]** | Em XAML, o espaçamento é expresso em milésimos de um *em* (1/1000). Logo, -0.025 vira -25. |
| `text-transparent bg-clip-text bg-gradient-to-r` | `TextBlock.Foreground` + `LinearGradientBrush` | **[ADAPTADO]** | O efeito de "texto vazado com gradiente" é trivial no WinUI, mas construído diferente do CSS. Aplica-se um `LinearGradientBrush` direto no preenchimento do texto. |

### Cores e Superfícies
| Web Token (CSS/Tailwind) | WinUI 3 (XAML) | Viabilidade | Notas |
| :--- | :--- | :--- | :--- |
| `bg-[#050505]` | `<SolidColorBrush Color="#050505"/>` | **[DIRETO]** | O sistema WinUI tem excelente controle de Background no nível da Page. |
| `bg-white/5` | `<SolidColorBrush Color="#0DFFFFFF"/>` | **[DIRETO]** | Transparências podem ser declaradas via código Hex ARGB. 5% = ~0D. |
| `backdrop-blur-md bg-white/10` | `MicaBackdrop` / `DesktopAcrylicBackdrop` | **[ADAPTADO]** | O WinUI não suporta CSS `backdrop-filter: blur`. O efeito "glass" é nativo via Mica ou AcrylicBrush. Usaremos o **MicaBackdrop** na raiz da Window, e Paineis transparentes por cima, ou `AcrylicBrush` customizado (Luminosity, TintOpacity). |
| `bg-gradient-to-b from-orange-400 to-orange-600` | `<LinearGradientBrush StartPoint="0,0" EndPoint="0,1">` | **[DIRETO]** | Cores puras convertidas via `<GradientStop>`. |

### Componentes e Geometria
| Web Token (CSS/Tailwind) | WinUI 3 (XAML) | Viabilidade | Notas |
| :--- | :--- | :--- | :--- |
| `rounded-lg` (8px), `rounded-full` (9999px) | `CornerRadius="8"` ou `CornerRadius="9999"` | **[DIRETO]** | XAML aceita CornerRadius diretamente em Borders e Controls. |
| `shadow-2xl` / Sombra Projetada Hover | `ThemeShadow` ou `DropShadow` via Composition API | **[ADAPTADO]** | O controle visual de sombra em XAML requer o uso do painel `Shadow` ou injeção visual avançada. WinUI não possui a facilidade de espalhamento como `box-shadow`. Alternativa: usaremos `ThemeShadow` do sistema nativo para manter performance. |
| `mix-blend-mode: screen` | N/A | **[INVIÁVEL]** | WinUI não possui suporte a Blend Modes na UI Tree. Será descartado, ou simulado renderizando uma imagem já com a iluminação misturada. |

### Animações (Motion)
| Web Token (CSS/Tailwind) | WinUI 3 (XAML) | Viabilidade | Notas |
| :--- | :--- | :--- | :--- |
| `fadeSlideIn` (Opacidade e TranslateY) | `Storyboard` (DoubleAnimation) ou `ImplicitAnimations` | **[ADAPTADO]** | Movimentos na entrada serão construídos via XAML Storyboard ou da Microsoft.UI.Composition (mais moderno). |
| Easing `cubic-bezier(0.2, 0.8, 0.2, 1)` | `SplineDoubleKeyFrame` com curva equivalente | **[ADAPTADO]** | Curvas Bezier manuais são possíveis através das KeyFrames em Storyboards. |
| Hover states (escalar `1.05`, mudar cor) | `VisualStateManager` + `PointerEntered` / `PointerExited` | **[DIRETO]** | WinUI gerencia estados de interação perfeitamente por VMs. |
| Borda pulsante (`spin_3s_linear_infinite` em `conic-gradient`) | N/A | **[INVIÁVEL]** | WinUI 3 não oferece suporte nativo a gradientes cônicos renderizados em UI nem a animação de rotação contínua nesse formato de forma performática via brush. Alternativa: Substituir a aura rotatória por animação de pulsação suave em `LinearGradient` na borda. |
| `filter: blur(8px)` animando pra zero | `Gaussian Blur Effect` da Composition API | **[ADAPTADO]** | Possível apenas via Win2D ou Composition. Dada a complexidade vs. ganho visual em apps desktop, recomendo manter a translação + fade (sem o desfoque) para não sobrecarregar a Thread de renderização da interface C#. |

### Ícones
| Web Token (CSS/Tailwind) | WinUI 3 (XAML) | Viabilidade | Notas |
| :--- | :--- | :--- | :--- |
| `lucide_latest.js` (SVG) | `FontIcon` ou `PathIcon` | **[ADAPTADO]** | Em XAML não se usa injeção dinâmica de SVG via JS. Exportaremos os Paths SVG do Lucide para `PathIcon` ou utilizaremos as fontes `Segoe Fluent Icons` do próprio Windows para manter consistência absoluta com o OS. |
