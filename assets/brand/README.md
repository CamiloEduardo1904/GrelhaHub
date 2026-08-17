# GrelhaHub — Kit de Marca v2

Identidade visual oficial do GrelhaHub — sistema de gestão para restaurantes e deliveries.

## O Símbolo — Monograma GH

As letras G e H (iniciais de GrelhaHub) construídas inteiramente com listras de grelha.

**A ligadura:** G e H compartilham a mesma barra central (em âmbar) — ela é, ao mesmo
tempo, a barra do meio do G, o travessão do H e uma listra da grelha.

> Leitura da marca: "a barra da grelha é o que conecta Grelha ao Hub."

Todas as pernas do H e as laterais do G são pilhas de listras — o monograma inteiro
é feito de grelha, sem elementos decorativos.

## Paleta de Cores

| Cor            | Hex       | Uso                                        |
|----------------|-----------|--------------------------------------------|
| Laranja brasa  | #FF6B2C   | Cor primária, barras, "Hub" no wordmark    |
| Âmbar chama    | #FFB020   | Barra da ligadura (fundo escuro)           |
| Creme          | #FFE3D3   | Barra da ligadura (fundo laranja)          |
| Carvão         | #14100C   | Fundos dark                                |
| Texto escuro   | #1D1812   | Texto/barras em fundo claro                |
| Cinza quente   | #9C8E7E   | Tagline, texto secundário                  |
| Borda clara    | #E5E0D8   | Bordas em fundo branco                     |

Regra: a barra da ligadura SEMPRE tem cor de destaque diferente das demais barras.

## Tipografia

- Wordmark: sans-serif bold (700), letter-spacing -1px
- "Grelha" em neutro (branco/carvão) + "Hub" sempre em laranja #FF6B2C
- Tagline "DO PEDIDO À BRASA": caps, letter-spacing 5px, cinza quente

## Estrutura das Pastas

```
grelhahub-v2/
├── logo-principal/      Monograma + wordmark + tagline (empilhado, 2:1)
│   ├── svg/             dark | light | transparente
│   └── png/             1360x680 (2x) e 680x340 (1x)
├── logo-horizontal/     Versão compacta para navbar/header (360x90)
│   ├── svg/             dark | light | transparente
│   └── png/             720x180 (2x) e 360x90 (1x)
├── icone-app/           Ícone quadrado (cantos arredondados iOS/Android)
│   ├── svg/             laranja | dark | light | monograma transparente
│   └── png/             1024, 512, 256, 192, 128
├── favicon/             favicon.ico (16+32+48) | PNGs | apple-touch-icon 180
└── wordmark/            Apenas o texto GrelhaHub
    ├── svg/             escuro | branco
    └── png/             680x200 (2x) e 340x100 (1x)
```

## Guia Rápido de Uso

| Contexto                          | Arquivo                                          |
|-----------------------------------|--------------------------------------------------|
| Capa de apresentação / slide      | logo-principal/png/...-dark-1360x680.png         |
| Documento impresso (fundo branco) | logo-principal/png/...-light-1360x680.png        |
| Navbar do sistema (dark mode)     | logo-horizontal/svg/...-horizontal-dark.svg      |
| Navbar do sistema (light mode)    | logo-horizontal/svg/...-horizontal-light.svg     |
| Ícone do app mobile / PWA         | icone-app/png/grelhahub-icone-laranja-1024.png   |
| PWA manifest (192/512)            | icone-app/png/...-laranja-192.png / -512.png     |
| Favicon do site                   | favicon/favicon.ico                              |
| Assinatura de e-mail              | logo-horizontal/png/...-transparente-360x90.png  |

## Formatos

- **SVG**: vetor, escala infinita — preferir sempre (web, sistema)
- **PNG 2x**: telas retina, apresentações, projetor
- **PNG 1x**: uso geral
- **ICO**: favicon multi-resolução (16/32/48 embutidos)
