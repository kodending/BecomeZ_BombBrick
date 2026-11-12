from pathlib import Path
import re

from pypdf import PdfReader
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Image, PageBreak, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle


root = Path(__file__).resolve().parents[1]
source = root / "Docs" / "MAP_MONSTER_DISTRIBUTION_DRAFT_KO.md"
output = root / "output" / "pdf" / "map-monster-distribution-draft-ko.pdf"
output.parent.mkdir(parents=True, exist_ok=True)

pdfmetrics.registerFont(TTFont("Malgun", r"C:\Windows\Fonts\malgun.ttf"))
pdfmetrics.registerFont(TTFont("Malgun-Bold", r"C:\Windows\Fonts\malgunbd.ttf"))

maps = []
current = None
for line in source.read_text(encoding="utf-8").splitlines():
    heading = re.fullmatch(r"## \d+\. (.+)", line)
    if heading:
        current = []
        maps.append((heading.group(1), current))
        continue
    row = re.fullmatch(
        r"\| (.+?) \| (.+?) \| (.+?) \| !\[[^]]*]\((MonsterThumbnails/[^)]+\.png)\) \|",
        line,
    )
    if row and current is not None:
        current.append((row.group(1), row.group(2), row.group(3), source.parent / row.group(4)))

assert len(maps) == 5 and all(len(rows) == 16 for _, rows in maps)

styles = getSampleStyleSheet()
title_style = ParagraphStyle(
    "MapTitle", parent=styles["Heading1"], fontName="Malgun-Bold", fontSize=16,
    leading=19, textColor=colors.HexColor("#17212B"), spaceAfter=0,
)
cell_style = ParagraphStyle(
    "Cell", parent=styles["BodyText"], fontName="Malgun", fontSize=8.7,
    leading=10.5, textColor=colors.HexColor("#17212B"),
)
cell_bold_style = ParagraphStyle("CellBold", parent=cell_style, fontName="Malgun-Bold")
header_style = ParagraphStyle("Header", parent=cell_bold_style, textColor=colors.white)
note_style = ParagraphStyle(
    "Note", parent=cell_style, fontSize=7.5, textColor=colors.HexColor("#65727E"),
)

page_width, _ = landscape(A4)
doc = SimpleDocTemplate(
    str(output), pagesize=landscape(A4), leftMargin=12 * mm, rightMargin=12 * mm,
    topMargin=9 * mm, bottomMargin=9 * mm,
    title="맵별 몬스터 분배안 - 이미지 포함 검토본",
    author="BecomeZ_BombBrick",
)


def footer(canvas, document):
    canvas.saveState()
    canvas.setFont("Malgun", 7.5)
    canvas.setFillColor(colors.HexColor("#7A8793"))
    canvas.drawString(12 * mm, 5 * mm, "검토본 - DB 및 실제 Wave 미반영")
    canvas.drawRightString(page_width - 12 * mm, 5 * mm, str(document.page))
    canvas.restoreState()


story = []
for map_index, (map_name, rows) in enumerate(maps, start=1):
    if map_index > 1:
        story.append(PageBreak())
    story.append(Paragraph(f"{map_index}. {map_name}", title_style))
    story.append(Paragraph("16개체 · 실제 전투 프리팹 Idle 첫 프레임", note_style))
    story.append(Spacer(1, 3 * mm))
    data = [[
        Paragraph("개체", header_style),
        Paragraph("계열", header_style),
        Paragraph("배정 이유", header_style),
        Paragraph("몬스터 이미지", header_style),
    ]]
    for monster, family, reason, image_path in rows:
        assert image_path.is_file(), image_path
        data.append([
            Paragraph(monster, cell_bold_style),
            Paragraph(family, cell_style),
            Paragraph(reason, cell_style),
            Image(str(image_path), width=9 * mm, height=9 * mm),
        ])

    table = Table(
        data,
        colWidths=[34 * mm, 31 * mm, 171 * mm, 32 * mm],
        rowHeights=[8 * mm] + [9.4 * mm] * 16,
        repeatRows=1,
    )
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#24313C")),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
        ("FONTNAME", (0, 0), (-1, 0), "Malgun-Bold"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("ALIGN", (3, 0), (3, -1), "CENTER"),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 1),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 1),
        ("LINEBELOW", (0, 0), (-1, -1), 0.35, colors.HexColor("#D7DEE4")),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F5F7F9")]),
    ]))
    story.append(table)

doc.build(story, onFirstPage=footer, onLaterPages=footer)
reader = PdfReader(str(output))
image_count = sum(
    sum(1 for item in (page["/Resources"].get("/XObject") or {}).values()
        if item.get_object().get("/Subtype") == "/Image")
    for page in reader.pages
)
assert len(reader.pages) == 5 and image_count == 80
