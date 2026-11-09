from PIL import Image, ImageDraw, ImageFont
import os

OUT = os.path.dirname(os.path.abspath(__file__))
F = "C:/Windows/Fonts/arial.ttf"
FB = "C:/Windows/Fonts/arialbd.ttf"
S = 3  # escala para alta resolução


def font(sz, bold=False):
    return ImageFont.truetype(FB if bold else F, sz * S)


def box(d, x, y, w, h, title, lines=(), fill="#ffffff", outline="#1f2937"):
    x, y, w, h = x * S, y * S, w * S, h * S
    d.rounded_rectangle([x, y, x + w, y + h], radius=8 * S, fill=fill, outline=outline, width=2 * S)
    ft = font(14, True)
    fl = font(12)
    total = 20 * S + len(lines) * 16 * S
    cy = y + (h - total) / 2
    tw = d.textlength(title, font=ft)
    d.text((x + (w - tw) / 2, cy), title, font=ft, fill="#111827")
    cy += 22 * S
    for ln in lines:
        lw = d.textlength(ln, font=fl)
        d.text((x + (w - lw) / 2, cy), ln, font=fl, fill="#374151")
        cy += 16 * S


def arrow(d, x1, y1, x2, y2, label=None):
    x1, y1, x2, y2 = x1 * S, y1 * S, x2 * S, y2 * S
    d.line([x1, y1, x2, y2], fill="#374151", width=2 * S)
    import math
    a = math.atan2(y2 - y1, x2 - x1)
    L = 10 * S
    p1 = (x2 - L * math.cos(a - 0.4), y2 - L * math.sin(a - 0.4))
    p2 = (x2 - L * math.cos(a + 0.4), y2 - L * math.sin(a + 0.4))
    d.polygon([(x2, y2), p1, p2], fill="#374151")
    if label:
        fl = font(11)
        d.text(((x1 + x2) / 2 + 6 * S, (y1 + y2) / 2 - 8 * S), label, font=fl, fill="#4b5563")


def crop(img, m=12):
    from PIL import ImageChops
    bg = Image.new("RGB", img.size, "white")
    x0, y0, x1, y1 = ImageChops.difference(img, bg).getbbox()
    m *= S
    return img.crop((max(0, x0 - m), max(0, y0 - m), min(img.width, x1 + m), min(img.height, y1 + m)))


def canvas(w, h):
    img = Image.new("RGB", (w * S, h * S), "white")
    return img, ImageDraw.Draw(img)


# Figura: arquitetura em camadas
img, d = canvas(760, 470)
box(d, 230, 15, 300, 62, "antoptimization.client", ["Angular 18 · Leaflet · Chart.js"], fill="#eff6ff")
box(d, 230, 120, 300, 62, "AntOptimization.Server", ["ASP.NET Core Web API · SSE · SPA host"], fill="#f0fdf4")
box(d, 5, 230, 240, 62, "AntOptimization.IoC", ["Injeção de dependências"], fill="#f9fafb")
box(d, 260, 230, 240, 62, "AntOptimization.Services", ["Orquestração dos casos de uso"], fill="#fefce8")
box(d, 515, 230, 240, 62, "AntOptimization.Infrastructure", ["OSRM · EF Core · SQLite"], fill="#fdf4ff")
box(d, 230, 360, 300, 80, "AntOptimization.Domain", ["Modelos, DTOs, interfaces", "Algoritmos: ACO, NN, 2-Opt, GA, exatos"], fill="#fff7ed")
arrow(d, 380, 77, 380, 118, "HTTP / JSON / SSE")
arrow(d, 330, 182, 125, 228)
arrow(d, 380, 182, 380, 228)
arrow(d, 430, 182, 635, 228)
arrow(d, 380, 292, 380, 358)
arrow(d, 635, 292, 500, 358)
arrow(d, 125, 292, 260, 358)
crop(img).save(os.path.join(OUT, "fig_arquitetura.png"), dpi=(300, 300))

# Figura: fluxo do ACO
img, d = canvas(760, 640)
steps = [
    ("Inicialização", ["τ(i,j) = τ0 = 1 para todas as arestas"]),
    ("Construção das rotas", ["cada formiga escolhe a próxima cidade", "por roleta: τ^α · η^β, com η = 1/d"]),
    ("Atualização do melhor global", ["menor distância de caminho aberto"]),
    ("Evaporação", ["τ ← (1 − ρ) · τ"]),
    ("Depósito", ["cada formiga k: τ ← τ + Q / L_k"]),
]
y = 15
for i, (t, ls) in enumerate(steps):
    box(d, 180, y, 400, 78, t, ls, fill="#eff6ff" if i % 2 == 0 else "#f9fafb")
    if i < len(steps) - 1:
        arrow(d, 380, y + 78, 380, y + 100)
    y += 100
box(d, 230, y + 10, 300, 62, "Fim", ["melhor rota e curva de convergência"], fill="#f0fdf4")
arrow(d, 380, y - 22, 380, y + 8, "critério de parada atingido")
# laço de iterações
d.line([580 * S, (y - 61) * S, 660 * S, (y - 61) * S, 660 * S, 154 * S], fill="#374151", width=2 * S)
arrow(d, 660, 154, 582, 154)
d.text((668 * S, 300 * S), "próxima", font=font(11), fill="#4b5563")
d.text((668 * S, 316 * S), "iteração", font=font(11), fill="#4b5563")
crop(img).save(os.path.join(OUT, "fig_aco.png"), dpi=(300, 300))

# Figura: pipeline do dataset
img, d = canvas(760, 560)
p = [
    ("1. Região e pontos", ["retângulo geográfico · N pontos com semente"]),
    ("2. Matriz de custos", ["OSRM ou Haversine (metros)"]),
    ("3. Extração de características", ["23 features da instância"]),
    ("4. Ótimo exato", ["força bruta (n ≤ 10) · Held-Karp (n ≤ 15)"]),
    ("5. Execuções do ACO", ["324 configurações × repetições"]),
    ("6. Persistência e exportação", ["SQLite · runs.csv · instances.csv · ZIP"]),
]
y = 12
for i, (t, ls) in enumerate(p):
    box(d, 180, y, 400, 70, t, ls, fill="#fff7ed" if i % 2 == 0 else "#f9fafb")
    if i < len(p) - 1:
        arrow(d, 380, y + 70, 380, y + 90)
    y += 91
crop(img).save(os.path.join(OUT, "fig_dataset.png"), dpi=(300, 300))
print("ok")
