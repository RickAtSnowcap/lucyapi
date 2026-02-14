from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import HTMLResponse
from html import escape
import re
from pydantic import BaseModel
from typing import Optional
from datetime import datetime, timezone
from ..auth import verify_api_key
from ..database import get_pool

router = APIRouter()


class WikiCreate(BaseModel):
    title: str
    description: Optional[str] = None


class WikiUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None


class WikiSectionCreate(BaseModel):
    parent_id: int = 0
    title: str
    description: Optional[str] = None
    tags: Optional[list[str]] = None


class WikiSectionUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None
    tags: Optional[list[str]] = None


# ---------------------------------------------------------------------------
# Wiki CRUD
# ---------------------------------------------------------------------------

@router.get("/wikis")
async def get_wikis(caller: dict = Depends(verify_api_key)):
    """All wikis for the caller's user."""
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT wiki_id, title, description, updated_at FROM wikis WHERE user_id = $1 ORDER BY wiki_id",
        caller["user_id"]
    )
    return {"wikis": [dict(r) for r in rows]}


# ---------------------------------------------------------------------------
# Tag query endpoints (must be defined BEFORE /wikis/{wiki_id} to avoid
# FastAPI interpreting "tags" as a wiki_id integer)
# ---------------------------------------------------------------------------

@router.get("/wikis/tags/{tag}")
async def search_wiki_tag(tag: str, caller: dict = Depends(verify_api_key)):
    """Find all sections across all of the user's wikis matching a tag."""
    pool = await get_pool()
    rows = await pool.fetch(
        """
        SELECT w.wiki_id, w.title AS wiki_title,
               ws.section_id, ws.title, ws.description, ws.updated_at
        FROM wiki_section_tags wst
        JOIN wiki_sections ws ON wst.section_id = ws.section_id
        JOIN wikis w ON ws.wiki_id = w.wiki_id
        WHERE wst.tag = $1 AND w.user_id = $2
        ORDER BY w.wiki_id, ws.section_id
        """,
        tag, caller["user_id"]
    )

    section_ids = [r["section_id"] for r in rows]
    tags_by_section: dict[int, list[str]] = {sid: [] for sid in section_ids}
    if section_ids:
        tag_rows = await pool.fetch(
            "SELECT section_id, tag FROM wiki_section_tags WHERE section_id = ANY($1) ORDER BY section_id, tag",
            section_ids
        )
        for tr in tag_rows:
            tags_by_section[tr["section_id"]].append(tr["tag"])

    sections = []
    for r in rows:
        sections.append({
            "wiki_id": r["wiki_id"],
            "wiki_title": r["wiki_title"],
            "section_id": r["section_id"],
            "title": r["title"],
            "description": r["description"],
            "updated_at": r["updated_at"],
            "tags": tags_by_section[r["section_id"]]
        })

    return {"tag": tag, "sections": sections}


@router.get("/wikis/{wiki_id}/tags")
async def get_wiki_tags(wiki_id: int, caller: dict = Depends(verify_api_key)):
    """List all unique tags used in a specific wiki."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    rows = await pool.fetch(
        """
        SELECT DISTINCT wst.tag
        FROM wiki_section_tags wst
        JOIN wiki_sections ws ON wst.section_id = ws.section_id
        WHERE ws.wiki_id = $1
        ORDER BY wst.tag
        """,
        wiki_id
    )
    return {"wiki_id": wiki_id, "tags": [r["tag"] for r in rows]}


@router.get("/wikis/{wiki_id}")
async def get_wiki(wiki_id: int, caller: dict = Depends(verify_api_key)):
    """Wiki header with full section tree including tags and updated_at."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id, title, description, updated_at FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    sections = await pool.fetch(
        "SELECT section_id, parent_id, title, description, updated_at FROM wiki_sections WHERE wiki_id = $1 ORDER BY parent_id, section_id",
        wiki_id
    )

    section_ids = [r["section_id"] for r in sections]
    tags_by_section: dict[int, list[str]] = {sid: [] for sid in section_ids}
    if section_ids:
        tag_rows = await pool.fetch(
            "SELECT section_id, tag FROM wiki_section_tags WHERE section_id = ANY($1) ORDER BY section_id, tag",
            section_ids
        )
        for tr in tag_rows:
            tags_by_section[tr["section_id"]].append(tr["tag"])

    section_list = []
    for r in sections:
        s = dict(r)
        s["tags"] = tags_by_section.get(r["section_id"], [])
        section_list.append(s)

    return {
        "wiki": dict(wiki),
        "sections": section_list
    }


@router.post("/wikis")
async def create_wiki(wiki: WikiCreate, caller: dict = Depends(verify_api_key)):
    """Create a wiki."""
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO wikis (user_id, title, description) VALUES ($1, $2, $3) RETURNING wiki_id, title",
        caller["user_id"], wiki.title, wiki.description
    )
    return {"created": dict(row)}


@router.put("/wikis/{wiki_id}")
async def update_wiki(wiki_id: int, wiki: WikiUpdate, caller: dict = Depends(verify_api_key)):
    """Update wiki title/description."""
    pool = await get_pool()
    existing = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Wiki not found")

    updates = []
    values = []
    idx = 1
    if wiki.title is not None:
        updates.append(f"title = ${idx}")
        values.append(wiki.title)
        idx += 1
    if wiki.description is not None:
        updates.append(f"description = ${idx}")
        values.append(wiki.description)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append("updated_at = NOW()")
    values.append(wiki_id)
    sql = f"UPDATE wikis SET {', '.join(updates)} WHERE wiki_id = ${idx} RETURNING wiki_id, title"
    row = await pool.fetchrow(sql, *values)
    return {"updated": dict(row)}


@router.delete("/wikis/{wiki_id}")
async def delete_wiki(wiki_id: int, caller: dict = Depends(verify_api_key)):
    """Delete a wiki and all sections/tags (cascade)."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    sections_result = await pool.execute(
        "DELETE FROM wiki_sections WHERE wiki_id = $1",
        wiki_id
    )
    await pool.execute(
        "DELETE FROM wikis WHERE wiki_id = $1",
        wiki_id
    )
    sections_count = int(sections_result.split()[-1])
    return {"deleted": wiki_id, "sections_deleted": sections_count}


# ---------------------------------------------------------------------------
# Wiki Section CRUD
# ---------------------------------------------------------------------------

@router.post("/wikis/{wiki_id}/sections")
async def create_wiki_section(wiki_id: int, section: WikiSectionCreate, caller: dict = Depends(verify_api_key)):
    """Create a section under a wiki."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    async with pool.acquire() as conn:
        async with conn.transaction():
            row = await conn.fetchrow(
                "INSERT INTO wiki_sections (wiki_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING section_id, title",
                wiki_id, section.parent_id, section.title, section.description
            )
            if section.tags:
                await conn.executemany(
                    "INSERT INTO wiki_section_tags (section_id, tag) VALUES ($1, $2)",
                    [(row["section_id"], tag) for tag in section.tags]
                )
            await conn.execute(
                "UPDATE wikis SET updated_at = NOW() WHERE wiki_id = $1",
                wiki_id
            )

    result = dict(row)
    result["tags"] = section.tags or []
    return {"created": result}


@router.get("/wikis/{wiki_id}/sections/{section_id}")
async def get_wiki_section(wiki_id: int, section_id: int, caller: dict = Depends(verify_api_key)):
    """Section detail with children and tags."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    node = await pool.fetchrow(
        "SELECT section_id, parent_id, title, description, updated_at FROM wiki_sections WHERE wiki_id = $1 AND section_id = $2",
        wiki_id, section_id
    )
    if not node:
        raise HTTPException(status_code=404, detail="Section not found")

    children = await pool.fetch(
        "SELECT section_id, parent_id, title, description, updated_at FROM wiki_sections WHERE wiki_id = $1 AND parent_id = $2 ORDER BY section_id",
        wiki_id, section_id
    )

    all_ids = [node["section_id"]] + [c["section_id"] for c in children]
    tag_rows = await pool.fetch(
        "SELECT section_id, tag FROM wiki_section_tags WHERE section_id = ANY($1) ORDER BY section_id, tag",
        all_ids
    )
    tags_by_section: dict[int, list[str]] = {sid: [] for sid in all_ids}
    for tr in tag_rows:
        tags_by_section[tr["section_id"]].append(tr["tag"])

    section_dict = dict(node)
    section_dict["tags"] = tags_by_section[node["section_id"]]

    children_list = []
    for c in children:
        cd = dict(c)
        cd["tags"] = tags_by_section[c["section_id"]]
        children_list.append(cd)

    return {
        "section": section_dict,
        "children": children_list
    }


@router.put("/wikis/{wiki_id}/sections/{section_id}")
async def update_wiki_section(wiki_id: int, section_id: int, section: WikiSectionUpdate, caller: dict = Depends(verify_api_key)):
    """Update a wiki section. If tags[] provided, replaces full tag set."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    updates = []
    values = []
    idx = 1
    if section.title is not None:
        updates.append(f"title = ${idx}")
        values.append(section.title)
        idx += 1
    if section.description is not None:
        updates.append(f"description = ${idx}")
        values.append(section.description)
        idx += 1

    has_field_updates = bool(updates)
    has_tag_updates = section.tags is not None

    if not has_field_updates and not has_tag_updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    async with pool.acquire() as conn:
        async with conn.transaction():
            if has_field_updates:
                updates.append("updated_at = NOW()")
                values.append(wiki_id)
                values.append(section_id)
                sql = f"UPDATE wiki_sections SET {', '.join(updates)} WHERE wiki_id = ${idx} AND section_id = ${idx + 1} RETURNING section_id, title"
                row = await conn.fetchrow(sql, *values)
                if not row:
                    raise HTTPException(status_code=404, detail="Section not found")
            else:
                row = await conn.fetchrow(
                    "UPDATE wiki_sections SET updated_at = NOW() WHERE wiki_id = $1 AND section_id = $2 RETURNING section_id, title",
                    wiki_id, section_id
                )
                if not row:
                    raise HTTPException(status_code=404, detail="Section not found")

            if has_tag_updates:
                await conn.execute(
                    "DELETE FROM wiki_section_tags WHERE section_id = $1",
                    section_id
                )
                if section.tags:
                    await conn.executemany(
                        "INSERT INTO wiki_section_tags (section_id, tag) VALUES ($1, $2)",
                        [(section_id, tag) for tag in section.tags]
                    )

            await conn.execute(
                "UPDATE wikis SET updated_at = NOW() WHERE wiki_id = $1",
                wiki_id
            )

    result = dict(row)
    if has_tag_updates:
        result["tags"] = section.tags
    return {"updated": result}


@router.delete("/wikis/{wiki_id}/sections/{section_id}")
async def delete_wiki_section(wiki_id: int, section_id: int, caller: dict = Depends(verify_api_key)):
    """Delete a section and its descendants. Touches parent wiki updated_at."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    async with pool.acquire() as conn:
        async with conn.transaction():
            result = await conn.execute(
                """
                WITH RECURSIVE subtree AS (
                    SELECT section_id FROM wiki_sections WHERE wiki_id = $1 AND section_id = $2
                    UNION ALL
                    SELECT ws.section_id FROM wiki_sections ws
                    INNER JOIN subtree s ON ws.parent_id = s.section_id
                    WHERE ws.wiki_id = $1
                )
                DELETE FROM wiki_sections WHERE section_id IN (SELECT section_id FROM subtree)
                """,
                wiki_id, section_id
            )
            deleted_count = int(result.split()[-1])
            if deleted_count == 0:
                raise HTTPException(status_code=404, detail="Section not found")

            await conn.execute(
                "UPDATE wikis SET updated_at = NOW() WHERE wiki_id = $1",
                wiki_id
            )

    return {"deleted": section_id, "descendants_deleted": deleted_count - 1}


# ---------------------------------------------------------------------------
# HTML document rendering helpers
# ---------------------------------------------------------------------------

def _build_tree(sections: list[dict]) -> list[dict]:
    """Organize flat sections into a tree by parent_id."""
    by_id = {s["section_id"]: {**s, "children": []} for s in sections}
    roots = []
    for s in sections:
        node = by_id[s["section_id"]]
        if s["parent_id"] == 0 or s["parent_id"] not in by_id:
            roots.append(node)
        else:
            by_id[s["parent_id"]]["children"].append(node)
    return roots


def _is_numbered_line(line: str) -> bool:
    """Check if a line starts with a number followed by a period and space."""
    stripped = line.strip()
    return bool(stripped) and stripped[0].isdigit() and ". " in stripped[:4]


def _postprocess_html(html: str) -> str:
    """Apply lightweight markup conversions to rendered HTML.

    Runs AFTER html.escape() so we only operate on safe text.
    Converts URLs into clickable links and **bold** into <strong>.
    """
    # Match http:// or https:// followed by non-whitespace, stopping
    # at whitespace, angle brackets, ampersands, or quotes.
    html = re.sub(
        r'(https?://[^\s<>&\'"]+)',
        r'<a href="\1" target="_blank" rel="noopener">\1</a>',
        html
    )
    # Match **text** bold markers (non-greedy) and convert to <strong>.
    html = re.sub(
        r'\*\*(.+?)\*\*',
        r'<strong>\1</strong>',
        html
    )
    return html


def _render_description(text: str) -> str:
    """Convert description text to HTML paragraphs, preserving intentional structure."""
    if not text:
        return ""
    escaped = escape(text)
    paragraphs = escaped.split("\n\n")

    classified: list[tuple[str, str]] = []
    for para in paragraphs:
        lines = para.strip().split("\n")
        if any(line.lstrip().startswith("- ") for line in lines):
            items = []
            prose = []
            for line in lines:
                stripped = line.lstrip()
                if stripped.startswith("- "):
                    if prose:
                        items.append(f"<p>{' '.join(prose)}</p>")
                        prose = []
                    items.append(f"<li>{stripped[2:]}</li>")
                else:
                    prose.append(stripped)
            if prose:
                items.append(f"<p>{' '.join(prose)}</p>")
            classified.append(("ul", "\n".join(items)))
        elif any(_is_numbered_line(line) for line in lines):
            items = []
            prose = []
            for line in lines:
                stripped = line.strip()
                if _is_numbered_line(stripped):
                    if prose:
                        items.append(f"<p>{' '.join(prose)}</p>")
                        prose = []
                    item_text = stripped.split(". ", 1)[1] if ". " in stripped else stripped
                    items.append(f"<li>{item_text}</li>")
                else:
                    prose.append(stripped)
            if prose:
                items.append(f"<p>{' '.join(prose)}</p>")
            classified.append(("ol", "\n".join(items)))
        else:
            joined = "<br>\n".join(line for line in lines if line.strip())
            if joined:
                classified.append(("p", f"<p>{joined}</p>"))

    parts = []
    i = 0
    while i < len(classified):
        kind, html = classified[i]
        if kind in ("ol", "ul"):
            merged = [html]
            while i + 1 < len(classified) and classified[i + 1][0] == kind:
                i += 1
                merged.append(classified[i][1])
            parts.append(f"<{kind}>\n" + "\n".join(merged) + f"\n</{kind}>")
        else:
            parts.append(html)
        i += 1
    return _postprocess_html("\n".join(parts))


def _render_section_html(node: dict, level: int) -> str:
    """Render a wiki section and its children as HTML, including tags and timestamps."""
    tag = f"h{min(level, 6)}"
    section_id = f"section-{node['section_id']}"
    parts = [f'<section id="{section_id}">']
    parts.append(f"<{tag}>{escape(node['title'])}</{tag}>")

    # Tags as colored pills
    if node.get("tags"):
        pills = " ".join(f'<span class="tag-pill">{escape(t)}</span>' for t in node["tags"])
        parts.append(f'<div class="section-tags">{pills}</div>')

    # Subtle updated_at timestamp
    if node.get("updated_at"):
        ts = node["updated_at"]
        if hasattr(ts, "strftime"):
            ts_str = ts.strftime("%Y-%m-%d %H:%M UTC")
        else:
            ts_str = str(ts)
        parts.append(f'<div class="section-updated">Updated {ts_str}</div>')

    if node.get("description"):
        parts.append(f'<div class="section-body">{_render_description(node["description"])}</div>')
    for child in node.get("children", []):
        parts.append(_render_section_html(child, level + 1))
    parts.append("</section>")
    return "\n".join(parts)


def _build_toc(tree: list[dict], level: int = 0) -> str:
    """Build a table of contents from the section tree."""
    items = []
    for node in tree:
        anchor = f"section-{node['section_id']}"
        items.append(f'<li><a href="#{anchor}">{escape(node["title"])}</a>')
        children = node.get("children", [])
        if children:
            items.append(_build_toc(children, level + 1))
        items.append("</li>")
    return "<ul>\n" + "\n".join(items) + "\n</ul>"


_WIKI_HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Lexend:wght@300;400;500;600&display=swap" rel="stylesheet">
<style>
  :root {{
    --mountain-blue: #4A6FA5;
    --deep-slate: #3D5A80;
    --sky-blue: #87CEEB;
    --snow-white: #E8F4F8;
    --charcoal: #2D3748;
    --bg-body: #1a1a2e;
    --bg-card: #252540;
    --bg-section: #2d2d4a;
    --text-primary: #E8F4F8;
    --text-secondary: #b0bec5;
    --text-muted: #78909c;
    --border: rgba(74, 111, 165, 0.3);
    --divider: rgba(135, 206, 235, 0.15);
  }}

  * {{ margin: 0; padding: 0; box-sizing: border-box; }}

  body {{
    font-family: 'Lexend', sans-serif;
    font-weight: 300;
    background: var(--bg-body);
    color: var(--text-primary);
    line-height: 1.7;
    padding: 2rem 1rem;
  }}

  .container {{
    max-width: 900px;
    margin: 0 auto;
  }}

  .wiki-header {{
    border-bottom: 2px solid var(--mountain-blue);
    padding-bottom: 1.5rem;
    margin-bottom: 2rem;
  }}

  .wiki-header h1 {{
    font-weight: 600;
    font-size: 2rem;
    color: var(--sky-blue);
    margin-bottom: 0.75rem;
  }}

  .wiki-header .description {{
    color: var(--text-secondary);
    font-size: 0.95rem;
    line-height: 1.8;
  }}

  .meta {{
    margin-top: 1rem;
    font-size: 0.8rem;
    color: var(--text-muted);
    font-weight: 400;
    letter-spacing: 0.03em;
  }}

  .toc {{
    background: var(--bg-card);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 1.25rem 1.5rem;
    margin-bottom: 2.5rem;
  }}

  .toc-title {{
    font-weight: 500;
    font-size: 0.85rem;
    color: var(--text-muted);
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin-bottom: 0.75rem;
  }}

  .toc ul {{
    list-style: none;
    padding-left: 0;
  }}

  .toc ul ul {{
    padding-left: 1.25rem;
    border-left: 1px solid var(--divider);
    margin-left: 0.5rem;
  }}

  .toc li {{
    margin: 0.3rem 0;
  }}

  .toc a {{
    color: var(--mountain-blue);
    text-decoration: none;
    font-size: 0.9rem;
    font-weight: 400;
    transition: color 0.2s;
  }}

  .toc a:hover {{
    color: var(--sky-blue);
  }}

  section {{
    margin-bottom: 2rem;
  }}

  section > section {{
    margin-bottom: 1.25rem;
    padding-left: 1rem;
    border-left: 2px solid var(--divider);
  }}

  h2 {{
    font-weight: 500;
    font-size: 1.35rem;
    color: var(--sky-blue);
    padding-bottom: 0.4rem;
    border-bottom: 1px solid var(--divider);
    margin-bottom: 0.5rem;
  }}

  h3 {{
    font-weight: 500;
    font-size: 1.1rem;
    color: var(--mountain-blue);
    margin-bottom: 0.4rem;
  }}

  h4, h5, h6 {{
    font-weight: 500;
    font-size: 0.95rem;
    color: var(--text-secondary);
    margin-bottom: 0.4rem;
  }}

  .section-tags {{
    margin-bottom: 0.4rem;
  }}

  .tag-pill {{
    display: inline-block;
    background: rgba(74, 111, 165, 0.25);
    color: var(--sky-blue);
    font-size: 0.72rem;
    font-weight: 400;
    padding: 0.15rem 0.55rem;
    border-radius: 9999px;
    border: 1px solid rgba(135, 206, 235, 0.2);
    margin-right: 0.35rem;
    margin-bottom: 0.2rem;
    letter-spacing: 0.02em;
  }}

  .section-updated {{
    font-size: 0.75rem;
    color: var(--text-muted);
    font-style: italic;
    margin-bottom: 0.6rem;
  }}

  .section-body {{
    font-size: 0.92rem;
    color: var(--text-secondary);
    line-height: 1.8;
  }}

  .section-body p {{
    margin-bottom: 0.8rem;
  }}

  .section-body ul, .section-body ol {{
    margin: 0.6rem 0 0.8rem 1.5rem;
    color: var(--text-secondary);
  }}

  .section-body li {{
    margin-bottom: 0.35rem;
  }}

  .section-body a {{
    color: var(--sky-blue);
    text-decoration: none;
    border-bottom: 1px solid rgba(135, 206, 235, 0.3);
    transition: color 0.2s, border-color 0.2s;
  }}

  .section-body a:hover {{
    color: #b8e4f9;
    border-bottom-color: var(--sky-blue);
  }}

  .footer {{
    margin-top: 3rem;
    padding-top: 1rem;
    border-top: 1px solid var(--divider);
    text-align: center;
    font-size: 0.75rem;
    color: var(--text-muted);
  }}
</style>
</head>
<body>
<div class="container">
  <div class="wiki-header">
    <h1>{title}</h1>
    {description_html}
    <div class="meta">{meta}</div>
  </div>
  <nav class="toc">
    <div class="toc-title">Contents</div>
    {toc}
  </nav>
  <main>
    {body}
  </main>
  <div class="footer">
    Generated by LucyAPI &middot; Snowcap Systems
  </div>
</div>
</body>
</html>"""


@router.get("/wikis/{wiki_id}/document", response_class=HTMLResponse)
async def get_wiki_document(wiki_id: int, caller: dict = Depends(verify_api_key)):
    """Reconstitute a wiki as a self-contained HTML document."""
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id, title, description, created_at, updated_at FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, caller["user_id"]
    )
    if not wiki:
        raise HTTPException(status_code=404, detail="Wiki not found")

    sections = await pool.fetch(
        "SELECT section_id, parent_id, title, description, updated_at FROM wiki_sections WHERE wiki_id = $1 ORDER BY parent_id, section_id",
        wiki_id
    )

    # Load tags for all sections
    section_ids = [r["section_id"] for r in sections]
    tags_by_section: dict[int, list[str]] = {sid: [] for sid in section_ids}
    if section_ids:
        tag_rows = await pool.fetch(
            "SELECT section_id, tag FROM wiki_section_tags WHERE section_id = ANY($1) ORDER BY section_id, tag",
            section_ids
        )
        for tr in tag_rows:
            tags_by_section[tr["section_id"]].append(tr["tag"])

    section_dicts = []
    for r in sections:
        s = dict(r)
        s["tags"] = tags_by_section.get(r["section_id"], [])
        section_dicts.append(s)

    tree = _build_tree(section_dicts)

    description_html = ""
    if wiki.get("description"):
        description_html = f'<div class="description">{_render_description(wiki["description"])}</div>'

    now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    meta = f"Generated: {now} &middot; Sections: {len(sections)}"

    toc = _build_toc(tree) if tree else ""
    body_parts = [_render_section_html(node, 2) for node in tree]

    html = _WIKI_HTML_TEMPLATE.format(
        title=escape(wiki["title"]),
        description_html=description_html,
        meta=meta,
        toc=toc,
        body="\n".join(body_parts)
    )
    return HTMLResponse(content=html)
