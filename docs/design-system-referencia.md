# Design System de Referência: Lumina

Este documento compila a extração rigorosa dos tokens, propriedades e padrões visuais encontrados nos arquivos de referência do Lumina Video (`design-system.html` e `index.html`).

## 1. TIPOGRAFIA

A hierarquia tipográfica da referência é construída com um sistema robusto e bem espaçado.

- **Família Primária:** `Inter` (sans-serif). 
- **Família Secundária/Decorativa:** `Instrument Serif` (serif).
- **Fallback Stack (Tailwind Sans):** Embora não declarado explicitamente no CSS inspecionado, utiliza o fallback padrão sem serifa do sistema se a fonte local falhar.
- **Estratégia de Carregamento:** Carregamento local via `@font-face` apontando para arquivos `.woff2` otimizados. Os pesos da Inter variam de 300 a 700. A Instrument Serif é carregada no peso 400.

### Escala Completa:
- **Heading 1 / Hero Display:** 96px (size) / 86px (line-height). Peso: 500 (Medium). Letter-spacing: `tracking-tighter` (-0.05em).
- **Heading 2 / Section Title:** 72px / 76px. Peso: 500. Letter-spacing: `tracking-tight` (-0.025em).
- **Heading 3 / Subsection:** 48px / 50px. Peso: 500. Letter-spacing: `tracking-tight`.
- **Heading 4 / Card Title:** 30px / 36px. Peso: 600 (Semibold). Letter-spacing: `tracking-tight`.
- **Body Large / Paragraph:** 18px / 28px. Peso: 500.
- **Body Medium:** 14px / 20px. Peso: 500.
- **Body Small:** 12px / 16px. Peso: 500.
- **Uppercase Label:** 12px / 16px. Peso: 500. Letter-spacing: `tracking-widest` (0.1em). Text-transform: `uppercase`.

## 2. O LETTERING TRANSPARENTE / VAZADO

O efeito visual que destaca palavras no texto heroico **não é** um contorno (`-webkit-text-stroke`) nem usa opacidade vazada com mistura com fundo. 
A precisão técnica mostra que o efeito é um **preenchimento com gradiente acoplado a um recorte no texto (background-clip: text)**.

**Como é construído no CSS (Tailwind):**
```html
<span class="text-transparent bg-clip-text bg-gradient-to-r from-white via-neutral-200 to-neutral-500">
  Stories.
</span>
```

**Resultado Visual:** 
- O texto em si torna-se transparente (`color: transparent`).
- O gradiente atua como fundo e é recortado pela forma geométrica da letra (`-webkit-background-clip: text`).
- O gradiente flui horizontalmente (`to right`) iniciando em branco puro (`#ffffff`), passando por um cinza claro (`neutral-200`) e finalizando em cinza mais escuro (`neutral-500`). Isso cria um volume cromático que parece fundir com o fundo preto sem a necessidade de blend modes complexos, dando um aspecto metálico e elegante.

*(Nota: a referência possui texto "esmaecido" usando `text-white/40`, ou seja, branco com 40% de opacidade para composição da frase no hero de subtítulo, mas o brilho principal reside na aplicação do clip).*

## 3. CORES

O design apresenta estética *Dark Mode* predominante, com contrastes dramáticos em laranja brilhante.

- **Page Background (Fundo):** `#050505` (Quase preto, RGB: 5, 5, 5). Semântica de fundo absoluto do app.
- **Card Surface:** `white/5` (Branco a 5% de opacidade). Semântica de elevação primária.
- **Glass Surface:** `white/10` + `backdrop-blur-md` (Branco a 10% de opacidade com desfoque de fundo). Semântica de painéis flutuantes (navbar).
- **Orange Accent:** `orange-500` / `#f97316` / `rgb(249, 115, 22)`. Usada para feedbacks positivos, acentos pontuais e ícones ativos.
- **Orange Gradient:** Linear de `orange-400` (`#fb923c`) para `orange-600` (`#ea580c`). Semântica de botões primários e marcação de destaque.
- **Text Primary:** `white` (`#ffffff`). Usada em cabeçalhos (AA/AAA em fundos escuros).
- **Text Secondary:** `white/60` ou `neutral-400` (`#a3a3a3`). Usada para corpos de texto para aliviar a carga cognitiva de alto contraste contínuo.
- **Borders:** `white/5` para divisórias sutis e `white/10` para bordas de interação (hover/cards).

## 4. COMPONENTES

- **Primary Button (Gradient):**
  - Raio da Borda: `9999px` (pílula completa). Padding: `10px 24px` (min-height 44px).
  - Background: Complexo (`radial-gradient` no centro-base + `linear-gradient` laranja).
  - Possui `::before` e `::after` absolutos sobrepostos, recuados 1px e 2px (inset), com gradientes transparentes para criar brilho e contorno tridimensional falso.
  - Hover: Translação de 2px no ícone interno.
- **Glass Button (Aura Circular):**
  - Raio da Borda: `9999px`.
  - Fundo real: `neutral-950/90` com `backdrop-blur-xl`.
  - Hover: Elemento geral escala em `scale-105` e gera sombra laranjada dramática: `shadow-[0_0_50px_10px_rgba(249,115,22,0.45)]`.
- **Card Standard:**
  - Background: `bg-white/5`.
  - Borda: `border border-white/10`. Raio: `rounded-lg` (8px). 
  - Espaçamento interno (padding): `p-8` (32px).
- **Step Item:**
  - Default: Fundo transparente, texto secundário.
  - Active: Fundo `white/5`, sombra `shadow-xl`, deslocamento `translate-x-[-10px]`, e o badge circular preenche em `orange-500`.

## 5. ANIMAÇÕES E MOVIMENTO

As animações focam em revelação e profundidade. Nenhuma animação é brusca; todas têm intenção.

- **Scroll Reveal (Surgimento):** 
  - Animação chave: `fadeSlideIn`. 
  - Duração: ~1s ou 1.2s dependendo do bloco. Easing: `cubic-bezier(0.2, 0.8, 0.2, 1)`.
  - Propriedades animadas: Opacidade (0 -> 1), Transform (Y 30px -> 0), Filter (`blur(8px)` -> 0). Stagger (delay em escadinha, como 0.1s, 0.2s, 0.35s).
- **Floating Points (Botão Primário):** Partículas que deslizam verticalmente do fundo para o topo sumindo: transform `translateY(-55px)` e `opacity: 0`, repetição infinita.
- **Rotating Border Glow (Glass Button):**
  - Usa rotação angular infinita `spin_3s_linear_infinite` em um `conic-gradient` escondido atrás da pílula. Com padding de 1px, ele vaza e aparece como uma aura pulsante pelas bordas.
- **Falling Beams:** Linhas brilhantes ao fundo. Movem no eixo Y com fade de entrada (50%) e saída. Linear 6 a 8 segundos.

## 6. ÍCONES

- **Biblioteca:** `Lucide` (Renderizada localmente por script `lucide_latest_...js`).
- **Estilo:** Desenho em contorno puro (Stroke), sem preenchimentos, exceto raras variações. 
- **Traço e Tamanho:** Utilizam `stroke-width="1.5"` ou `2` dependendo da visibilidade desejada. Os tamanhos padronizados orbitam os blocos de `w-3.5 h-3.5` (14px) até `w-6 h-6` (24px) para ações grandes.
