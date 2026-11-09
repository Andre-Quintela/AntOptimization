"""Gera fig_convergencia.png: convergencia media do ACO por tamanho de instancia.

Le o convergence.csv exportado pelo AntOptimization.Benchmark. Para cada execucao, a curva e
normalizada como excesso percentual sobre a melhor distancia que aquela mesma execucao atingiu ao
final, o que permite comparar tamanhos de instancia na mesma escala. Uso:

    python fig_conv.py [caminho/para/convergence.csv]

Padrao: ../../datasets/comparacao/convergence.csv
"""
import csv
import os
import statistics
import sys
from collections import defaultdict

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.dirname(os.path.abspath(__file__))
CSV_PATH = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    OUT, "..", "..", "datasets", "comparacao", "convergence.csv")
F = "C:/Windows/Fonts/arial.ttf"
FB = "C:/Windows/Fonts/arialbd.ttf"
S = 3  # escala para alta resolucao

# Tracejados distintos para que a figura continue legivel impressa em preto e branco.
STYLES = [
    ("#1d4ed8", None),
    ("#b45309", (18, 10)),
    ("#15803d", (6, 8)),
    ("#7e22ce", (22, 8, 6, 8)),
]


def font(sz, bold=False):
    return ImageFont.truetype(FB if bold else F, sz * S)


def read_curves(path):
    """{(point_count, instance, repetition): [distancia por iteracao]}"""
    curves = defaultdict(list)
    with open(path, encoding="utf-8", newline="") as fh:
        for row in csv.DictReader(fh):
            key = (int(row["point_count"]), int(row["instance_index"]), int(row["repetition"]))
            curves[key].append((int(row["iteration"]), float(row["best_distance_m"])))
    return {k: [d for _, d in sorted(v)] for k, v in curves.items()}


def mean_excess(curves):
    """{point_count: [excesso medio (%) sobre o valor final, por iteracao]}"""
    by_size = defaultdict(list)
    for (point_count, _, _), series in curves.items():
        final = series[-1]
        if final > 0:
            by_size[point_count].append([(v - final) / final * 100.0 for v in series])

    out = {}
    for point_count, series_list in by_size.items():
        length = min(len(s) for s in series_list)
        out[point_count] = [statistics.mean(s[i] for s in series_list) for i in range(length)]
    return out


def dashed_line(d, points, color, pattern, width):
    """Desenha uma polilinha tracejada (o Pillow nao tem tracejado nativo)."""
    if pattern is None:
        d.line(points, fill=color, width=width, joint="curve")
        return
    pat, pi, remaining, drawing = list(pattern), 0, pattern[0], True
    for (x1, y1), (x2, y2) in zip(points, points[1:]):
        seg = ((x2 - x1) ** 2 + (y2 - y1) ** 2) ** 0.5
        pos = 0.0
        while pos < seg:
            step = min(remaining, seg - pos)
            t0, t1 = pos / seg, (pos + step) / seg
            if drawing:
                d.line([x1 + (x2 - x1) * t0, y1 + (y2 - y1) * t0,
                        x1 + (x2 - x1) * t1, y1 + (y2 - y1) * t1], fill=color, width=width)
            pos += step
            remaining -= step
            if remaining <= 1e-9:
                pi = (pi + 1) % len(pat)
                remaining = pat[pi]
                drawing = not drawing


def main():
    if not os.path.exists(CSV_PATH):
        print("aviso: convergence.csv nao encontrado em", os.path.abspath(CSV_PATH),
              "- fig_convergencia.png nao foi regenerada")
        return

    series = mean_excess(read_curves(CSV_PATH))
    sizes = sorted(series)
    iterations = max(len(v) for v in series.values())
    ymax = max(max(v) for v in series.values())
    ymax = max(1.0, ymax * 1.08)

    left, right, top, bottom = 56, 24, 46, 62
    pw, ph = 560, 250                      # area do grafico
    w, h = left + pw + right, top + ph + bottom

    img = Image.new("RGB", (w * S, h * S), "white")
    d = ImageDraw.Draw(img)

    def text(x, y, s, sz=11, bold=False, color="#111827", anchor="la"):
        d.text((x * S, y * S), s, font=font(sz, bold), fill=color, anchor=anchor)

    def px(it):
        return left + (it - 1) / max(1, iterations - 1) * pw

    def py(value):
        return top + ph - value / ymax * ph

    # grade e eixo y
    steps = 5
    for k in range(steps + 1):
        value = ymax * k / steps
        y = py(value)
        d.line([left * S, y * S, (left + pw) * S, y * S],
               fill="#e5e7eb" if k else "#1f2937", width=(1 if k else 2) * S)
        text(left - 8, y, f"{value:.1f}".replace(".", ","), 10, anchor="rm", color="#374151")
    text(left - 48, top - 26, "Excesso sobre a melhor distância final da própria execução (%)",
         11, True, anchor="la")

    # eixo x
    d.line([left * S, (top + ph) * S, (left + pw) * S, (top + ph) * S], fill="#1f2937", width=2 * S)
    for it in [1] + list(range(20, iterations + 1, 20)):
        x = px(it)
        d.line([x * S, (top + ph) * S, x * S, (top + ph + 5) * S], fill="#1f2937", width=2 * S)
        text(x, top + ph + 9, str(it), 10, anchor="ma", color="#374151")
    text(left + pw / 2, top + ph + 28, "Iteração", 11, True, anchor="ma")

    # curvas
    for i, point_count in enumerate(sizes):
        color, pattern = STYLES[i % len(STYLES)]
        values = series[point_count]
        points = [(px(k + 1) * S, py(v) * S) for k, v in enumerate(values)]
        dashed_line(d, points, color, pattern, 2 * S)

    # legenda (as curvas convergem para o mesmo ponto, entao rotular as pontas nao funciona)
    lx, ly = left + pw - 128, top + 10
    d.rectangle([lx * S, ly * S, (lx + 122) * S, (ly + 18 + 17 * len(sizes)) * S],
                fill="#ffffff", outline="#d1d5db", width=1 * S)
    text(lx + 10, ly + 8, "Pontos por instância", 10, True, color="#374151")
    for i, point_count in enumerate(sizes):
        color, pattern = STYLES[i % len(STYLES)]
        y = ly + 30 + 17 * i
        dashed_line(d, [((lx + 10) * S, y * S), ((lx + 44) * S, y * S)], color, pattern, 2 * S)
        text(lx + 52, y, f"n = {point_count}", 11, False, color="#111827", anchor="lm")

    text(left, top + ph + 46,
         "Média das execuções de cada tamanho; parâmetros padrão (α = 1, β = 2, m = 20, ρ = 0,5).",
         10, color="#4b5563")

    path = os.path.join(OUT, "fig_convergencia.png")
    img.save(path)
    print("ok", path, img.size)


if __name__ == "__main__":
    main()
