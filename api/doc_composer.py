"""
Document composition engine for Google Docs.
Translates a structured JSON content schema into Google Docs batchUpdate requests
with full rich formatting, branding presets, and table styling.
"""

import re
from dataclasses import dataclass


# ---------------------------------------------------------------------------
# Inline text run
# ---------------------------------------------------------------------------

@dataclass
class Run:
    text: str
    bold: bool = False
    italic: bool = False
    url: str | None = None


# ---------------------------------------------------------------------------
# Branding presets (document text styling)
# ---------------------------------------------------------------------------

PRESETS = {
    "snowcap": {
        "font": "Lexend",
        "h1": {"size": 24, "color": {"red": 0.290, "green": 0.435, "blue": 0.647}},   # #4A6FA5
        "h2": {"size": 18, "color": {"red": 0.239, "green": 0.353, "blue": 0.502}},   # #3D5A80
        "h3": {"size": 14, "color": {"red": 0.239, "green": 0.353, "blue": 0.502}},   # #3D5A80
        "body": {"size": 11, "color": {"red": 0.176, "green": 0.216, "blue": 0.282}},  # #2D3748
    },
}

# ---------------------------------------------------------------------------
# Table styling (universal, independent of branding)
# ---------------------------------------------------------------------------

TABLE_STYLE = {
    "font": "Calibri",
    "size": 12,
    "header_bg": {"red": 0.259, "green": 0.522, "blue": 0.957},   # #4285F4
    "header_fg": {"red": 1.0, "green": 1.0, "blue": 1.0},         # white
    "alt_row_bg": {"red": 0.910, "green": 0.941, "blue": 0.996},  # #E8F0FE
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _rgb(color_dict):
    """Wrap an {red, green, blue} dict into the full Docs API color structure."""
    return {"color": {"rgbColor": color_dict}}


def _pt(magnitude):
    """Build a dimension in points."""
    return {"magnitude": magnitude, "unit": "PT"}


_INLINE_RE = re.compile(
    r'\[(?P<link_text>[^\]]+)\]\((?P<link_url>https?://[^)]+)\)'
    r'|(?P<stars>\*{1,3})(?P<star_text>.*?)(?P=stars)'
    r'|(?P<bare_url>https?://\S+)'
)


def _parse_inline(text: str) -> list[Run]:
    """Parse **bold**, *italic*, ***both***, [text](url), and bare URLs into runs."""
    runs = []
    last_end = 0
    for match in _INLINE_RE.finditer(text):
        if match.start() > last_end:
            runs.append(Run(text=text[last_end:match.start()]))
        if match.group("link_text"):
            runs.append(Run(
                text=match.group("link_text"),
                url=match.group("link_url"),
            ))
        elif match.group("stars"):
            stars = len(match.group("stars"))
            runs.append(Run(
                text=match.group("star_text"),
                bold=stars >= 2,
                italic=stars % 2 == 1,
            ))
        elif match.group("bare_url"):
            url = match.group("bare_url")
            runs.append(Run(text=url, url=url))
        last_end = match.end()
    if last_end < len(text):
        runs.append(Run(text=text[last_end:]))
    return runs if runs else [Run(text=text)]


def _get_runs(block: dict) -> list[Run]:
    """Extract runs from a content block (explicit runs or parsed text)."""
    if "runs" in block:
        return [
            Run(text=r["text"], bold=r.get("bold", False), italic=r.get("italic", False),
                url=r.get("url"))
            for r in block["runs"]
        ]
    return _parse_inline(block.get("text", ""))


def _style_text(start_index: int, runs: list[Run],
                font: str = None, size: float = None, color: dict = None) -> list[dict]:
    """
    Generate UpdateTextStyle requests.
    One request for branding (full range), then per-run requests for bold/italic/links.
    """
    total_len = sum(len(r.text) for r in runs)
    if total_len == 0:
        return []

    requests = []
    end_index = start_index + total_len

    # Branding pass: font, size, color across the full text range
    if font or size or color:
        style = {}
        fields = []
        if font:
            style["weightedFontFamily"] = {"fontFamily": font, "weight": 400}
            fields.append("weightedFontFamily")
        if size:
            style["fontSize"] = _pt(size)
            fields.append("fontSize")
        if color:
            style["foregroundColor"] = _rgb(color)
            fields.append("foregroundColor")
        requests.append({
            "updateTextStyle": {
                "range": {"startIndex": start_index, "endIndex": end_index},
                "textStyle": style,
                "fields": ",".join(fields),
            }
        })

    # Inline formatting pass: bold/italic per run
    pos = start_index
    for run in runs:
        if not run.text:
            continue
        run_end = pos + len(run.text)
        if run.bold or run.italic:
            style = {}
            fields = []
            if run.bold:
                style["bold"] = True
                fields.append("bold")
            if run.italic:
                style["italic"] = True
                fields.append("italic")
            requests.append({
                "updateTextStyle": {
                    "range": {"startIndex": pos, "endIndex": run_end},
                    "textStyle": style,
                    "fields": ",".join(fields),
                }
            })
        pos = run_end

    # Link pass: apply hyperlinks
    pos = start_index
    for run in runs:
        if not run.text:
            continue
        run_end = pos + len(run.text)
        if run.url:
            requests.append({
                "updateTextStyle": {
                    "range": {"startIndex": pos, "endIndex": run_end},
                    "textStyle": {
                        "link": {"url": run.url}
                    },
                    "fields": "link",
                }
            })
        pos = run_end

    return requests


# ---------------------------------------------------------------------------
# Block composers — each returns (requests, new_index)
# ---------------------------------------------------------------------------

def _compose_heading(index: int, block: dict, brand: dict | None):
    level = block.get("level", 1)
    runs = _get_runs(block)
    content_text = "".join(r.text for r in runs)
    full_text = content_text + "\n"

    requests = []

    # Insert text
    requests.append({
        "insertText": {
            "location": {"index": index},
            "text": full_text,
        }
    })

    # Paragraph style: heading level
    named_style = f"HEADING_{min(level, 6)}"
    requests.append({
        "updateParagraphStyle": {
            "range": {"startIndex": index, "endIndex": index + len(full_text)},
            "paragraphStyle": {"namedStyleType": named_style},
            "fields": "namedStyleType",
        }
    })

    # Text style: branding + inline formatting
    if brand:
        style_key = f"h{min(level, 3)}"
        bs = brand[style_key]
        requests.extend(_style_text(index, runs, brand["font"], bs["size"], bs["color"]))
    else:
        requests.extend(_style_text(index, runs))

    return requests, index + len(full_text)


def _compose_paragraph(index: int, block: dict, brand: dict | None):
    runs = _get_runs(block)
    content_text = "".join(r.text for r in runs)
    full_text = content_text + "\n"

    requests = [{
        "insertText": {
            "location": {"index": index},
            "text": full_text,
        }
    }]

    if brand:
        bs = brand["body"]
        requests.extend(_style_text(index, runs, brand["font"], bs["size"], bs["color"]))
    else:
        requests.extend(_style_text(index, runs))

    return requests, index + len(full_text)


def _compose_list(index: int, block: dict, brand: dict | None):
    style = block.get("style", "bullet")
    items = block.get("items", [])
    if not items:
        return [], index

    # Parse inline formatting per item
    all_item_runs = [_parse_inline(item) for item in items]

    # Build full text
    parts = ["".join(r.text for r in item_runs) for item_runs in all_item_runs]
    full_text = "\n".join(parts) + "\n"

    requests = []

    # Insert text
    requests.append({
        "insertText": {
            "location": {"index": index},
            "text": full_text,
        }
    })

    # Apply bullet or numbered list
    preset = (
        "BULLET_DISC_CIRCLE_SQUARE" if style == "bullet"
        else "NUMBERED_DECIMAL_ALPHA_ROMAN"
    )
    requests.append({
        "createParagraphBullets": {
            "range": {"startIndex": index, "endIndex": index + len(full_text)},
            "bulletPreset": preset,
        }
    })

    # Text styling per item
    pos = index
    for item_runs in all_item_runs:
        item_len = sum(len(r.text) for r in item_runs)
        if brand:
            bs = brand["body"]
            requests.extend(_style_text(pos, item_runs, brand["font"], bs["size"], bs["color"]))
        else:
            requests.extend(_style_text(pos, item_runs))
        pos += item_len + 1  # +1 for \n

    return requests, index + len(full_text)


def _compose_table(index: int, block: dict, brand: dict | None):
    headers = block["headers"]
    rows = block.get("rows", [])
    num_cols = len(headers)
    num_rows = len(rows) + 1  # +1 for header row

    requests = []

    # --- 1. Insert empty table ---
    requests.append({
        "insertTable": {
            "rows": num_rows,
            "columns": num_cols,
            "location": {"index": index},
        }
    })

    # --- 2. Build cell content in reading order ---
    all_cells = []  # (row, col, text)
    for c, header_text in enumerate(headers):
        all_cells.append((0, c, header_text))
    for r, row_data in enumerate(rows):
        for c, cell_text in enumerate(row_data):
            all_cells.append((r + 1, c, str(cell_text)))

    # --- 3. Cell position formula (empty table) ---
    def cell_pos(r, c):
        return index + 4 + r * (2 * num_cols + 1) + c * 2

    # --- 4. Insert cell content back-to-front ---
    for r, c, text in reversed(all_cells):
        requests.append({
            "insertText": {
                "location": {"index": cell_pos(r, c)},
                "text": text,
            }
        })

    # --- 5. Calculate final positions after all insertions ---
    cumulative = 0
    cell_ranges = []  # (row, col, text, final_start, final_end)
    for r, c, text in all_cells:
        final_start = cell_pos(r, c) + cumulative
        final_end = final_start + len(text)
        cell_ranges.append((r, c, text, final_start, final_end))
        cumulative += len(text)

    # --- 6. Style all table text: Calibri 12pt ---
    if cell_ranges:
        requests.append({
            "updateTextStyle": {
                "range": {
                    "startIndex": cell_ranges[0][3],
                    "endIndex": cell_ranges[-1][4],
                },
                "textStyle": {
                    "weightedFontFamily": {"fontFamily": TABLE_STYLE["font"], "weight": 400},
                    "fontSize": _pt(TABLE_STYLE["size"]),
                },
                "fields": "weightedFontFamily,fontSize",
            }
        })

    # --- 7. Style header row text: bold, white ---
    header_cells = [cr for cr in cell_ranges if cr[0] == 0]
    if header_cells:
        requests.append({
            "updateTextStyle": {
                "range": {
                    "startIndex": header_cells[0][3],
                    "endIndex": header_cells[-1][4],
                },
                "textStyle": {
                    "bold": True,
                    "foregroundColor": _rgb(TABLE_STYLE["header_fg"]),
                },
                "fields": "bold,foregroundColor",
            }
        })

    # --- 8. Center-align header cells ---
    for _r, _c, _t, start, end in header_cells:
        requests.append({
            "updateParagraphStyle": {
                "range": {"startIndex": start, "endIndex": end},
                "paragraphStyle": {"alignment": "CENTER"},
                "fields": "alignment",
            }
        })

    # --- 9. Header row background ---
    # InsertTable creates a preceding paragraph; the actual table element
    # starts at index + 1, so tableStartLocation must use that offset.
    table_start = index + 1
    requests.append({
        "updateTableCellStyle": {
            "tableRange": {
                "tableCellLocation": {
                    "tableStartLocation": {"index": table_start},
                    "rowIndex": 0,
                    "columnIndex": 0,
                },
                "rowSpan": 1,
                "columnSpan": num_cols,
            },
            "tableCellStyle": {
                "backgroundColor": _rgb(TABLE_STYLE["header_bg"]),
            },
            "fields": "backgroundColor",
        }
    })

    # --- 10. Alternating data row backgrounds ---
    for r in range(2, num_rows, 2):
        requests.append({
            "updateTableCellStyle": {
                "tableRange": {
                    "tableCellLocation": {
                        "tableStartLocation": {"index": table_start},
                        "rowIndex": r,
                        "columnIndex": 0,
                    },
                    "rowSpan": 1,
                    "columnSpan": num_cols,
                },
                "tableCellStyle": {
                    "backgroundColor": _rgb(TABLE_STYLE["alt_row_bg"]),
                },
                "fields": "backgroundColor",
            }
        })

    # --- 11. Next index ---
    # Total footprint: 1 (preceding para) + table_struct_size
    table_struct_size = 5 + (num_rows - 1) * (2 * num_cols + 1) + (num_cols - 1) * 2
    total_cell_content = sum(len(text) for _, _, text in all_cells)
    new_index = index + 1 + table_struct_size + total_cell_content

    return requests, new_index


def _compose_page_break(index: int, block: dict, brand: dict | None):
    requests = [{
        "insertPageBreak": {
            "location": {"index": index},
        }
    }]
    # Page break occupies 2 index positions (break char + newline)
    return requests, index + 2


def _compose_image(index: int, block: dict, brand: dict | None):
    uri = block["uri"]
    width_pt = block.get("width_pt")

    request = {
        "insertInlineImage": {
            "uri": uri,
            "location": {"index": index},
        }
    }
    if width_pt:
        request["insertInlineImage"]["objectSize"] = {
            "width": _pt(width_pt),
        }

    requests = [request]

    # Newline after image to terminate the paragraph
    requests.append({
        "insertText": {
            "location": {"index": index + 1},
            "text": "\n",
        }
    })

    return requests, index + 2


# ---------------------------------------------------------------------------
# Handler dispatch
# ---------------------------------------------------------------------------

_HANDLERS = {
    "heading": _compose_heading,
    "paragraph": _compose_paragraph,
    "list": _compose_list,
    "table": _compose_table,
    "page_break": _compose_page_break,
    "image": _compose_image,
}


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def compose(content_blocks: list[dict], branding: str = "none",
            start_index: int = 1) -> list[dict]:
    """
    Translate content blocks into Google Docs batchUpdate requests.

    Args:
        content_blocks: List of content block dicts.
        branding: Preset name ("snowcap") or "none" for defaults.
        start_index: Document index to begin at (1 for new docs).

    Returns:
        List of batchUpdate request dicts.
    """
    brand = PRESETS.get(branding) if branding and branding != "none" else None
    requests = []
    idx = start_index

    for block in content_blocks:
        block_type = block.get("type")
        handler = _HANDLERS.get(block_type)
        if not handler:
            raise ValueError(f"Unknown content block type: {block_type!r}")
        block_requests, idx = handler(idx, block, brand)
        requests.extend(block_requests)

    return requests
