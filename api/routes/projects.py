from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import HTMLResponse
from html import escape
import re
from pydantic import BaseModel
from typing import Optional
from datetime import datetime, timezone
from ..auth import verify_api_key
from ..database import get_pool
from .sharing import check_share_permission

router = APIRouter()


class ProjectCreate(BaseModel):
    title: str
    description: Optional[str] = None


class ProjectUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None


class SectionCreate(BaseModel):
    parent_id: int = 0
    title: str
    description: Optional[str] = None
    file_path: Optional[str] = None


class SectionUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None
    file_path: Optional[str] = None


@router.get("/projects")
async def get_projects(caller: dict = Depends(verify_api_key)):
    """All projects for the caller's user, including shared projects."""
    pool = await get_pool()
    rows = await pool.fetch(
        """SELECT project_id, title, description, 'owned' as access, 3 as permission_level
           FROM projects WHERE user_id = $1
           UNION ALL
           SELECT p.project_id, p.title, p.description, 'shared' as access, so.permission_level
           FROM projects p
           JOIN shared_objects so ON so.object_id = p.project_id AND so.object_type_id = 1
           WHERE so.shared_to_user_id = $1
           ORDER BY project_id""",
        caller["user_id"]
    )
    return {"projects": [dict(r) for r in rows]}


@router.get("/projects/{project_id}")
async def get_project(project_id: int, caller: dict = Depends(verify_api_key)):
    """Project header with section tree."""
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id, title, description FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    access = "owned"
    permission_level = 3
    if not project:
        perm = await check_share_permission(pool, caller["user_id"], 1, project_id, 1)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")
        project = await pool.fetchrow(
            "SELECT project_id, title, description FROM projects WHERE project_id = $1",
            project_id
        )
        access = "shared"
        permission_level = perm

    sections = await pool.fetch(
        "SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 ORDER BY parent_id, section_id",
        project_id
    )
    result = dict(project)
    result["access"] = access
    result["permission_level"] = permission_level
    return {
        "project": result,
        "sections": [dict(r) for r in sections]
    }


@router.get("/projects/{project_id}/sections/{section_id}")
async def get_section(project_id: int, section_id: int, caller: dict = Depends(verify_api_key)):
    """A section and its immediate children."""
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, caller["user_id"], 1, project_id, 1)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")

    node = await pool.fetchrow(
        "SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 AND section_id = $2",
        project_id, section_id
    )
    if not node:
        raise HTTPException(status_code=404, detail="Section not found")

    children = await pool.fetch(
        "SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 AND parent_id = $2 ORDER BY section_id",
        project_id, section_id
    )
    return {
        "section": dict(node),
        "children": [dict(r) for r in children]
    }


@router.post("/projects")
async def create_project(project: ProjectCreate, caller: dict = Depends(verify_api_key)):
    """Create a project. User approval enforced by agent behavior."""
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO projects (user_id, title, description) VALUES ($1, $2, $3) RETURNING project_id, title",
        caller["user_id"], project.title, project.description
    )
    return {"created": dict(row)}


@router.post("/projects/{project_id}/sections")
async def create_section(project_id: int, section: SectionCreate, caller: dict = Depends(verify_api_key)):
    """Create a project section. User approval enforced by agent behavior."""
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, caller["user_id"], 1, project_id, 2)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")

    row = await pool.fetchrow(
        "INSERT INTO project_sections (project_id, parent_id, title, description, file_path) VALUES ($1, $2, $3, $4, $5) RETURNING section_id, title",
        project_id, section.parent_id, section.title, section.description, section.file_path
    )
    return {"created": dict(row)}


@router.put("/projects/{project_id}")
async def update_project(project_id: int, project: ProjectUpdate, caller: dict = Depends(verify_api_key)):
    """Update project header. User approval enforced by agent behavior."""
    pool = await get_pool()
    existing = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    if not existing:
        perm = await check_share_permission(pool, caller["user_id"], 1, project_id, 2)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")

    updates = []
    values = []
    idx = 1
    if project.title is not None:
        updates.append(f"title = ${idx}")
        values.append(project.title)
        idx += 1
    if project.description is not None:
        updates.append(f"description = ${idx}")
        values.append(project.description)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append("updated_at = NOW()")
    values.append(project_id)
    sql = f"UPDATE projects SET {', '.join(updates)} WHERE project_id = ${idx} RETURNING project_id, title"
    row = await pool.fetchrow(sql, *values)
    return {"updated": dict(row)}


@router.put("/projects/{project_id}/sections/{section_id}")
async def update_section(project_id: int, section_id: int, section: SectionUpdate, caller: dict = Depends(verify_api_key)):
    """Update a section. User approval enforced by agent behavior."""
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, caller["user_id"], 1, project_id, 2)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")

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
    if section.file_path is not None:
        updates.append(f"file_path = ${idx}")
        values.append(section.file_path)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append("updated_at = NOW()")
    values.append(project_id)
    values.append(section_id)
    sql = f"UPDATE project_sections SET {', '.join(updates)} WHERE project_id = ${idx} AND section_id = ${idx + 1} RETURNING section_id, title"
    row = await pool.fetchrow(sql, *values)
    if not row:
        raise HTTPException(status_code=404, detail="Section not found")
    return {"updated": dict(row)}


@router.delete("/projects/{project_id}")
async def delete_project(project_id: int, caller: dict = Depends(verify_api_key)):
    """Delete a project and all its sections. User approval enforced by agent behavior."""
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")

    sections_result = await pool.execute(
        "DELETE FROM project_sections WHERE project_id = $1",
        project_id
    )
    await pool.execute(
        "DELETE FROM projects WHERE project_id = $1",
        project_id
    )
    sections_count = int(sections_result.split()[-1])
    return {"deleted": project_id, "sections_deleted": sections_count}


@router.delete("/projects/{project_id}/sections/{section_id}")
async def delete_section(project_id: int, section_id: int, caller: dict = Depends(verify_api_key)):
    """Delete a section. User approval enforced by agent behavior."""
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, caller["user_id"], 1, project_id, 3)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")

    # Recursive delete: collect entire subtree then delete in one shot
    result = await pool.execute(
        """
        WITH RECURSIVE subtree AS (
            SELECT section_id FROM project_sections WHERE project_id = $1 AND section_id = $2
            UNION ALL
            SELECT ps.section_id FROM project_sections ps
            INNER JOIN subtree s ON ps.parent_id = s.section_id
            WHERE ps.project_id = $1
        )
        DELETE FROM project_sections WHERE section_id IN (SELECT section_id FROM subtree)
        """,
        project_id, section_id
    )
    deleted_count = int(result.split()[-1])
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Section not found")

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
    """Convert description text to HTML paragraphs, preserving intentional structure.

    Handles bullet lists (- prefix), numbered lists (1. prefix), and plain
    prose paragraphs. Consecutive numbered-item paragraphs separated by blank
    lines are coalesced into a single <ol>.
    """
    if not text:
        return ""
    escaped = escape(text)
    paragraphs = escaped.split("\n\n")

    # First pass: classify each paragraph
    classified: list[tuple[str, str]] = []  # (type, rendered_html)
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

    # Second pass: coalesce consecutive same-type list blocks
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
    """Render a section and its children as HTML."""
    tag = f"h{min(level, 6)}"
    section_id = f"section-{node['section_id']}"
    parts = [f'<section id="{section_id}">']
    parts.append(f"<{tag}>{escape(node['title'])}</{tag}>")
    if node.get("description"):
        parts.append(f'<div class="section-body">{_render_description(node["description"])}</div>')
    if node.get("file_path"):
        parts.append(f'<p class="file-path">Associated file: {escape(node["file_path"])}</p>')
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


_HTML_TEMPLATE = """<!DOCTYPE html>
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

  .project-header {{
    border-bottom: 2px solid var(--mountain-blue);
    padding-bottom: 1.5rem;
    margin-bottom: 2rem;
  }}

  .project-header h1 {{
    font-weight: 600;
    font-size: 2rem;
    color: var(--sky-blue);
    margin-bottom: 0.75rem;
  }}

  .project-header .description {{
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
    margin-bottom: 1rem;
  }}

  h3 {{
    font-weight: 500;
    font-size: 1.1rem;
    color: var(--mountain-blue);
    margin-bottom: 0.6rem;
  }}

  h4, h5, h6 {{
    font-weight: 500;
    font-size: 0.95rem;
    color: var(--text-secondary);
    margin-bottom: 0.5rem;
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

  .file-path {{
    font-size: 0.8rem;
    color: var(--text-muted);
    font-style: italic;
    margin-top: 0.5rem;
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
  <div class="project-header">
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


@router.get("/projects/{project_id}/document", response_class=HTMLResponse)
async def get_project_document(project_id: int, caller: dict = Depends(verify_api_key)):
    """Reconstitute a project as a self-contained HTML document."""
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id, title, description, created_at, updated_at FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, caller["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, caller["user_id"], 1, project_id, 1)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")
        project = await pool.fetchrow(
            "SELECT project_id, title, description, created_at, updated_at FROM projects WHERE project_id = $1",
            project_id
        )

    sections = await pool.fetch(
        "SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 ORDER BY parent_id, section_id",
        project_id
    )

    tree = _build_tree([dict(r) for r in sections])

    description_html = ""
    if project.get("description"):
        description_html = f'<div class="description">{_render_description(project["description"])}</div>'

    now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    meta = f"Generated: {now} &middot; Sections: {len(sections)}"

    toc = _build_toc(tree) if tree else ""
    body_parts = [_render_section_html(node, 2) for node in tree]

    html = _HTML_TEMPLATE.format(
        title=escape(project["title"]),
        description_html=description_html,
        meta=meta,
        toc=toc,
        body="\n".join(body_parts)
    )
    return HTMLResponse(content=html)
