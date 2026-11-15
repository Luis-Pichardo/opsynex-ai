#!/usr/bin/env python3
"""Genera docs/Documentacion_Opsynex_AI.docx a partir de los Markdown de docs/documentacion/.

Uso:
    python3 scripts/generar_documentacion.py

Requisitos: pandoc (>= 2.x) y python-docx (pip install python-docx).

Flujo:
1. Une los archivos NN_*.md de docs/documentacion/ en orden numérico.
2. Crea una plantilla de estilos (reference.docx) con la identidad visual del proyecto.
3. Convierte con pandoc (tabla de contenido incluida).
4. Post-procesa con python-docx: bordes y encabezados de tablas, pie de página con número.
"""

from __future__ import annotations

import datetime as dt
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

try:
    from docx import Document
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    from docx.oxml import OxmlElement
    from docx.oxml.ns import qn
    from docx.shared import Pt, RGBColor
except ImportError:  # pragma: no cover
    sys.exit("Falta python-docx: pip install python-docx")

ROOT = Path(__file__).resolve().parent.parent
SRC_DIR = ROOT / "docs" / "documentacion"
OUTPUT = ROOT / "docs" / "Documentacion_Opsynex_AI.docx"

FONT = "Calibri"
PRIMARY = RGBColor(0x1E, 0x2A, 0x5A)  # azul corporativo
ACCENT = RGBColor(0x3B, 0x4F, 0xB8)
HEADER_FILL = "1E2A5A"
BORDER_COLOR = "B8C0D9"


def read_metadata() -> dict[str, str]:
    """Lee metadata.yaml (formato simple clave: "valor")."""
    meta: dict[str, str] = {}
    path = SRC_DIR / "metadata.yaml"
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            m = re.match(r'^\s*(\w+)\s*:\s*"?(.*?)"?\s*$', line)
            if m:
                meta[m.group(1)] = m.group(2)
    return meta


def collect_markdown() -> str:
    files = sorted(p for p in SRC_DIR.glob("*.md") if re.match(r"^\d+_", p.name))
    if not files:
        sys.exit(f"No hay archivos NN_*.md en {SRC_DIR}")
    parts = [p.read_text(encoding="utf-8").strip() for p in files]
    return "\n\n".join(parts) + "\n"


def build_reference(tmp: Path) -> Path:
    """Crea reference.docx partiendo de la plantilla por defecto de pandoc."""
    ref = tmp / "reference.docx"
    with ref.open("wb") as fh:
        subprocess.run(
            ["pandoc", "--print-default-data-file", "reference.docx"],
            check=True,
            stdout=fh,
        )
    return ref


def find_style(doc, name: str):
    """Busca un estilo por su nombre exacto en el XML (python-docx traduce
    "Heading 1" a "heading 1" y no encuentra los estilos que crea pandoc)."""
    for st in doc.styles:
        if st.element.name_val == name or st.style_id == name.replace(" ", ""):
            return st
    return None


def clear_theme(style) -> None:
    """Quita fuentes y colores de tema para que se respeten los valores explícitos."""
    rpr = style.element.rPr
    if rpr is None:
        return
    for tag in ("w:rFonts", "w:color"):
        el = rpr.find(qn(tag))
        if el is not None:
            for attr in list(el.attrib):
                if "Theme" in attr or "theme" in attr:
                    del el.attrib[attr]


def apply_styles(doc) -> None:
    """Aplica la identidad visual a los estilos del documento final."""

    def style_font(name: str, size: float, color=None, bold=None, before=None, after=None):
        st = find_style(doc, name)
        if st is None:
            return
        clear_theme(st)
        st.font.name = FONT
        st.font.size = Pt(size)
        if color is not None:
            st.font.color.rgb = color
        if bold is not None:
            st.font.bold = bold
        pf = st.paragraph_format
        if before is not None:
            pf.space_before = Pt(before)
        if after is not None:
            pf.space_after = Pt(after)

    style_font("Normal", 11, before=0, after=6)
    style_font("Body Text", 11, before=0, after=6)
    style_font("First Paragraph", 11, before=0, after=6)
    style_font("Compact", 11, before=0, after=2)
    style_font("Title", 30, PRIMARY, True, 120, 6)
    style_font("Subtitle", 15, ACCENT, False, 0, 18)
    style_font("Author", 11, before=0, after=0)
    style_font("Date", 11, before=0, after=24)
    style_font("Heading 1", 20, PRIMARY, True, 24, 10)
    style_font("Heading 2", 15, PRIMARY, True, 18, 8)
    style_font("Heading 3", 12.5, ACCENT, True, 12, 6)
    style_font("Heading 4", 11.5, ACCENT, True, 10, 4)
    style_font("Block Text", 10.5, RGBColor(0x55, 0x5E, 0x7A), False, 4, 4)
    style_font("Source Code", 9.5, before=0, after=0)
    for name in ("Heading 1", "Heading 2", "Heading 3", "Heading 4"):
        st = find_style(doc, name)
        if st is not None:
            st.paragraph_format.keep_with_next = True
    # Portada, índice y cada "Parte" (Heading 1) comienzan en página nueva.
    for name in ("Heading 1", "TOC Heading"):
        st = find_style(doc, name)
        if st is not None:
            st.paragraph_format.page_break_before = True


def set_cell_shading(cell, fill: str) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), fill)
    tc_pr.append(shd)


def set_table_borders(table) -> None:
    tbl_pr = table._tbl.tblPr
    borders = OxmlElement("w:tblBorders")
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        el = OxmlElement(f"w:{edge}")
        el.set(qn("w:val"), "single")
        el.set(qn("w:sz"), "4")
        el.set(qn("w:space"), "0")
        el.set(qn("w:color"), BORDER_COLOR)
        borders.append(el)
    for old in tbl_pr.findall(qn("w:tblBorders")):
        tbl_pr.remove(old)
    tbl_pr.append(borders)


def add_page_number_footer(doc) -> None:
    for section in doc.sections:
        p = section.footer.paragraphs[0] if section.footer.paragraphs else section.footer.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run = p.add_run("Opsynex AI · Página ")
        run.font.size = Pt(9)
        for kind, text in (("begin", None), (None, "PAGE"), ("end", None)):
            r = p.add_run()
            r.font.size = Pt(9)
            if kind:
                fld = OxmlElement("w:fldChar")
                fld.set(qn("w:fldCharType"), kind)
                r._r.append(fld)
            else:
                instr = OxmlElement("w:instrText")
                instr.set(qn("xml:space"), "preserve")
                instr.text = text
                r._r.append(instr)


def postprocess(path: Path) -> None:
    doc = Document(str(path))
    apply_styles(doc)
    for table in doc.tables:
        set_table_borders(table)
        if table.rows:
            for cell in table.rows[0].cells:
                set_cell_shading(cell, HEADER_FILL)
                for par in cell.paragraphs:
                    for run in par.runs:
                        run.font.bold = True
                        run.font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)
    add_page_number_footer(doc)
    # Pide a Word actualizar la tabla de contenido al abrir el documento.
    settings = doc.settings.element
    if settings.find(qn("w:updateFields")) is None:
        upd = OxmlElement("w:updateFields")
        upd.set(qn("w:val"), "true")
        settings.append(upd)
    doc.save(str(path))


def main() -> None:
    if not shutil.which("pandoc"):
        sys.exit("pandoc no está instalado (apt-get install pandoc / brew install pandoc).")
    meta = read_metadata()
    today = dt.date.today().strftime("%d/%m/%Y")
    with tempfile.TemporaryDirectory() as tmpdir:
        tmp = Path(tmpdir)
        md_file = tmp / "documento.md"
        md_file.write_text(collect_markdown(), encoding="utf-8")
        ref = build_reference(tmp)
        cmd = [
            "pandoc",
            str(md_file),
            "-f", "markdown+pipe_tables+grid_tables",
            "-t", "docx",
            "--reference-doc", str(ref),
            "--toc",
            "--toc-depth=2",
            "--resource-path", str(SRC_DIR),
            "-M", f"title={meta.get('title', 'Opsynex AI')}",
            "-M", f"subtitle={meta.get('subtitle', '')}",
            "-M", f"author={meta.get('client', '')}",
            "-M", f"date=Versión {meta.get('version', '0.0.0')} · {today}",
            "-M", "toc-title=Contenido",
            "-o", str(OUTPUT),
        ]
        subprocess.run(cmd, check=True)
    postprocess(OUTPUT)
    print(f"Documento generado: {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
