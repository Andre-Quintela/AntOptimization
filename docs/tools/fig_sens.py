"""Gera fig_beta_rho.png: mapa de calor do gap medio do ACO por beta x rho.

Le o runs.csv exportado pelo modulo de geracao de dados. Uso:
    python fig_sens.py [caminho/para/runs.csv]
Padrao: ../../datasets/vitoria-es/runs.csv
"""
import csv
import os
import statistics
import sys
from collections import defaultdict

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.dirname(os.path.abspath(__file__))
RUNS = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    OUT, "..", "..", "datasets", "vitoria-es", "runs.csv")
F = "C:/Windows/Fonts/arial.ttf"
FB = "C:/Windows/Fonts/arialbd.ttf"
S = 3  # escala para alta resolucao


def font(sz, bold=False):
    return ImageFont.truetype(FB if bold else F, sz * S)


def read_cells(path):
    cells = defaultdict(list)
    with open(path, encoding="utf-8", newline="") as fh:
        for row in csv.DictReader(fh):
            cells[(float(row["beta"]), float(row["evaporation_rate"]))].append(
                float(row["gap_percent"]))
    return {k: statistics.mean(v) for k, v in cells.items()}, \
           {k: len(v) for k, v in cells.items()}


def ramp(t):
    """Escala sequencial clara -> escura (legivel tambem em impressao P&B)."""
    t = max(0.0, min(1.0, t))
    stops = [(0.0, (239, 246, 255)), (0.5, (147, 197, 253)), (1.0, (23, 55, 110))]
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            f = (t - t0) / (t1 - t0)
            return tuple(round(a + (b - a) * f) for a, b in zip(c0, c1))
    return stops[-1][1]


def main():
    if not os.path.exists(RUNS):
        # O conjunto de dados nao e versionado; sem ele mantem-se a figura ja gerada.
        print("aviso: runs.csv nao encontrado em", os.path.abspath(RUNS),
              "- fig_beta_rho.png nao foi regenerada")
        return
    means, counts = read_cells(RUNS)
    betas = sorted({k[0] for k in means})
    rhos = sorted({k[1] for k in means})
    lo, hi = min(means.values()), max(means.values())

    cw, ch = 118, 62          # celula
    left, top = 86, 34        # rotulos dos eixos
    bar_h = 24
    w = left + cw * len(rhos) + 20
    h = top + ch * len(betas) + bar_h + 84

    img = Image.new("RGB", (w * S, h * S), "white")
    d = ImageDraw.Draw(img)

    def text(x, y, s, sz=12, bold=False, color="#111827", anchor="la"):
        d.text((x * S, y * S), s, font=font(sz, bold), fill=color, anchor=anchor)

    def num(v, casas=2):
        return f"{v:.{casas}f}".replace(".", ",")

    for j, rho in enumerate(rhos):
        text(left + cw * j + cw / 2, top - 24, "\u03c1 = " + num(rho, 1), 13, True, anchor="ma")
    text(left / 2 + 10, top - 24, "\u03b2", 13, True, anchor="ma")

    for i, beta in enumerate(betas):
        y = top + ch * i
        text(left - 14, y + ch / 2, f"{beta:g}", 12, True, anchor="rm")
        for j, rho in enumerate(rhos):
            v = means[(beta, rho)]
            x = left + cw * j
            t = (v - lo) / (hi - lo) if hi > lo else 0.0
            d.rectangle([x * S, y * S, (x + cw) * S, (y + ch) * S],
                        fill=ramp(t), outline="#ffffff", width=2 * S)
            text(x + cw / 2, y + ch / 2, num(v), 14, True,
                 "#f8fafc" if t > 0.55 else "#0f172a", anchor="mm")

    # barra de escala
    by = top + ch * len(betas) + 26
    bw = cw * len(rhos)
    for px in range(bw):
        c = ramp(px / max(1, bw - 1))
        d.rectangle([(left + px) * S, by * S, (left + px + 1) * S, (by + bar_h) * S], fill=c)
    d.rectangle([left * S, by * S, (left + bw) * S, (by + bar_h) * S],
                outline="#1f2937", width=2 * S)
    text(left, by + bar_h + 6, num(lo) + "% (menor gap)", 11)
    text(left + bw, by + bar_h + 6, num(hi) + "% (maior gap)", 11, anchor="ra")

    total = sum(counts.values())

    def milhar(v):
        return f"{v:,}".replace(",", ".")

    text(left, by + bar_h + 28,
         "M\u00e9dias sobre " + milhar(total) + " execu\u00e7\u00f5es (" +
         milhar(total // len(counts)) + " por c\u00e9lula), agregando \u03b1,",
         11, color="#4b5563")
    text(left, by + bar_h + 44,
         "o n\u00famero de formigas e o n\u00famero de itera\u00e7\u00f5es.",
         11, color="#4b5563")

    path = os.path.join(OUT, "fig_beta_rho.png")
    img.save(path)
    print("ok", path, img.size)


if __name__ == "__main__":
    main()
