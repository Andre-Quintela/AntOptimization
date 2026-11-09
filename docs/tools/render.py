"""Renderiza um PDF em PNGs (uma imagem por página) para conferência visual.

Uso: python render.py <arquivo.pdf> <pasta_saida>
Requer: pip install pymupdf
"""
import os
import sys

import pymupdf

pdf, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
doc = pymupdf.open(pdf)
print("paginas:", doc.page_count)
for i, page in enumerate(doc):
    page.get_pixmap(dpi=70).save(os.path.join(outdir, f"p{i+1:02d}.png"))
    if "\nREFERÊNCIAS" in page.get_text():
        print("referencias na pagina", i + 1)
