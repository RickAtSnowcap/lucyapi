import os
import hashlib
import logging
from datetime import datetime, timezone
from io import BytesIO
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field
from PIL import Image as PILImage

from ..user_auth import verify_user_token
from ..database import get_pool
from .sharing import check_share_permission
from ..encryption import encrypt, decrypt
from ..gemini import generate_image as gemini_generate

logger = logging.getLogger(__name__)

IMAGES_DIR = "/opt/lucyapi/output/images"
IMAGES_BASE_URL = "https://lucyapi.snowcapsystems.com"

router = APIRouter(prefix="/admin", tags=["admin"])


# ── Helpers ──────────────────────────────────────────────────────

def _build_tree(rows):
    """Build nested tree from flat rows with parent_id."""
    nodes = {r["pkid"]: {**dict(r), "children": []} for r in rows}
    roots = []
    for r in rows:
        node = nodes[r["pkid"]]
        if r["parent_id"] == 0:
            roots.append(node)
        elif r["parent_id"] in nodes:
            nodes[r["parent_id"]]["children"].append(node)
    return roots


# ── Pydantic models ─────────────────────────────────────────────

class ProjectCreate(BaseModel):
    title: str
    description: Optional[str] = None
    status_id: Optional[int] = None

class ProjectUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None
    status_id: Optional[int] = None

class SectionCreate(BaseModel):
    parent_id: int = 0
    title: str
    description: Optional[str] = None
    file_path: Optional[str] = None

class SectionUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None
    file_path: Optional[str] = None

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

class HintCategoryCreate(BaseModel):
    title: str
    description: Optional[str] = None

class HintCreate(BaseModel):
    parent_id: int = 0
    title: str
    description: Optional[str] = None

class HintUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None

class SecretCreate(BaseModel):
    value: str

class ShareCreate(BaseModel):
    shared_to_user_id: int
    object_type_id: int  # 1=project, 2=hint, 3=wiki
    object_id: int
    permission_level: int = 1  # 1=read, 2=read+edit, 3=full

class ShareUpdate(BaseModel):
    permission_level: int

class GenImageRequest(BaseModel):
    prompt: str
    model: str = Field(default="nano-banana")
    aspect_ratio: str = Field(default="1:1")

class KeepRequest(BaseModel):
    keep: bool


# ── Projects ────────────────────────────────────────────────────

@router.get("/project-statuses")
async def list_project_statuses(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch("SELECT status_id, code, label, sort_order FROM project_statuses ORDER BY sort_order")
    return {"statuses": [dict(r) for r in rows]}


@router.get("/projects")
async def list_projects(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        """SELECT p.project_id, p.title, p.description, ps.code as status, ps.label as status_label, 'owned' as access, 3 as permission_level
           FROM projects p
           JOIN project_statuses ps ON p.status_id = ps.status_id
           WHERE p.user_id = $1
           UNION ALL
           SELECT p.project_id, p.title, p.description, ps.code as status, ps.label as status_label, 'shared' as access, so.permission_level
           FROM projects p
           JOIN project_statuses ps ON p.status_id = ps.status_id
           JOIN shared_objects so ON so.object_id = p.project_id AND so.object_type_id = 1
           WHERE so.shared_to_user_id = $1
           ORDER BY project_id""",
        user["user_id"]
    )
    return {"projects": [dict(r) for r in rows]}


@router.get("/projects/{project_id}")
async def get_project(project_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    project = await pool.fetchrow(
        """SELECT p.project_id, p.title, p.description, ps.code as status, ps.label as status_label
           FROM projects p JOIN project_statuses ps ON p.status_id = ps.status_id
           WHERE p.project_id = $1 AND p.user_id = $2""",
        project_id, user["user_id"]
    )
    access = "owned"
    permission_level = 3
    if not project:
        perm = await check_share_permission(pool, user["user_id"], 1, project_id, 1)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")
        project = await pool.fetchrow(
            """SELECT p.project_id, p.title, p.description, ps.code as status, ps.label as status_label
               FROM projects p JOIN project_statuses ps ON p.status_id = ps.status_id
               WHERE p.project_id = $1""",
            project_id
        )
        access = "shared"
        permission_level = perm

    sections = await pool.fetch(
        "SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 ORDER BY parent_id, section_id",
        project_id
    )
    if access == "owned":
        agent_row = await pool.fetchrow(
            "SELECT api_key FROM agents WHERE user_id = $1 ORDER BY agent_id LIMIT 1",
            user["user_id"]
        )
    else:
        owner_row = await pool.fetchrow(
            "SELECT user_id FROM projects WHERE project_id = $1", project_id
        )
        agent_row = await pool.fetchrow(
            "SELECT api_key FROM agents WHERE user_id = $1 ORDER BY agent_id LIMIT 1",
            owner_row["user_id"]
        )

    result = dict(project)
    result["access"] = access
    result["permission_level"] = permission_level
    result["document_url"] = f"https://lucyapi.snowcapsystems.com/projects/{project_id}/document?agent_key={agent_row['api_key']}" if agent_row else None
    return {
        "project": result,
        "sections": [dict(r) for r in sections]
    }


@router.post("/projects")
async def create_project(project: ProjectCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    if project.status_id is not None:
        row = await pool.fetchrow(
            "INSERT INTO projects (user_id, title, description, status_id) VALUES ($1, $2, $3, $4) RETURNING project_id, title",
            user["user_id"], project.title, project.description, project.status_id
        )
    else:
        row = await pool.fetchrow(
            "INSERT INTO projects (user_id, title, description) VALUES ($1, $2, $3) RETURNING project_id, title",
            user["user_id"], project.title, project.description
        )
    result = dict(row)
    ps = await pool.fetchrow(
        "SELECT ps.code as status, ps.label as status_label FROM projects p JOIN project_statuses ps ON p.status_id = ps.status_id WHERE p.project_id = $1",
        result["project_id"]
    )
    if ps:
        result["status"] = ps["status"]
        result["status_label"] = ps["status_label"]
    return {"created": result}


@router.put("/projects/{project_id}")
async def update_project(project_id: int, project: ProjectUpdate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    existing = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, user["user_id"]
    )
    if not existing:
        perm = await check_share_permission(pool, user["user_id"], 1, project_id, 2)
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
    if project.status_id is not None:
        updates.append(f"status_id = ${idx}")
        values.append(project.status_id)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append("updated_at = NOW()")
    values.append(project_id)
    sql = f"UPDATE projects SET {', '.join(updates)} WHERE project_id = ${idx} RETURNING project_id, title"
    row = await pool.fetchrow(sql, *values)
    result = dict(row)
    ps = await pool.fetchrow(
        "SELECT ps.code as status, ps.label as status_label FROM projects p JOIN project_statuses ps ON p.status_id = ps.status_id WHERE p.project_id = $1",
        project_id
    )
    if ps:
        result["status"] = ps["status"]
        result["status_label"] = ps["status_label"]
    return {"updated": result}


@router.delete("/projects/{project_id}")
async def delete_project(project_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, user["user_id"]
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


@router.post("/projects/{project_id}/sections")
async def create_section(project_id: int, section: SectionCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, user["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, user["user_id"], 1, project_id, 2)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")

    row = await pool.fetchrow(
        "INSERT INTO project_sections (project_id, parent_id, title, description, file_path) VALUES ($1, $2, $3, $4, $5) RETURNING section_id, title",
        project_id, section.parent_id, section.title, section.description, section.file_path
    )
    return {"created": dict(row)}


@router.put("/projects/{project_id}/sections/{section_id}")
async def update_section(project_id: int, section_id: int, section: SectionUpdate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, user["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, user["user_id"], 1, project_id, 2)
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


@router.delete("/projects/{project_id}/sections/{section_id}")
async def delete_section(project_id: int, section_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    project = await pool.fetchrow(
        "SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2",
        project_id, user["user_id"]
    )
    if not project:
        perm = await check_share_permission(pool, user["user_id"], 1, project_id, 3)
        if not perm:
            raise HTTPException(status_code=404, detail="Project not found")

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


# ── Wikis ───────────────────────────────────────────────────────

@router.get("/wikis")
async def list_wikis(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        """SELECT wiki_id, title, description, updated_at, 'owned' as access, 3 as permission_level
           FROM wikis WHERE user_id = $1
           UNION ALL
           SELECT w.wiki_id, w.title, w.description, w.updated_at, 'shared' as access, so.permission_level
           FROM wikis w
           JOIN shared_objects so ON so.object_id = w.wiki_id AND so.object_type_id = 3
           WHERE so.shared_to_user_id = $1
           ORDER BY wiki_id""",
        user["user_id"]
    )
    return {"wikis": [dict(r) for r in rows]}


@router.get("/wikis/{wiki_id}/tags")
async def get_wiki_tags(wiki_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, user["user_id"]
    )
    if not wiki:
        perm = await check_share_permission(pool, user["user_id"], 3, wiki_id, 1)
        if not perm:
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
async def get_wiki(wiki_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id, title, description, updated_at FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, user["user_id"]
    )
    access = "owned"
    permission_level = 3
    if not wiki:
        perm = await check_share_permission(pool, user["user_id"], 3, wiki_id, 1)
        if not perm:
            raise HTTPException(status_code=404, detail="Wiki not found")
        wiki = await pool.fetchrow(
            "SELECT wiki_id, title, description, updated_at FROM wikis WHERE wiki_id = $1",
            wiki_id
        )
        access = "shared"
        permission_level = perm

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

    result = dict(wiki)
    result["access"] = access
    result["permission_level"] = permission_level
    return {
        "wiki": result,
        "sections": section_list
    }


@router.post("/wikis")
async def create_wiki(wiki: WikiCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO wikis (user_id, title, description) VALUES ($1, $2, $3) RETURNING wiki_id, title",
        user["user_id"], wiki.title, wiki.description
    )
    return {"created": dict(row)}


@router.put("/wikis/{wiki_id}")
async def update_wiki(wiki_id: int, wiki: WikiUpdate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    existing = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, user["user_id"]
    )
    if not existing:
        perm = await check_share_permission(pool, user["user_id"], 3, wiki_id, 2)
        if not perm:
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
async def delete_wiki(wiki_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, user["user_id"]
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


@router.post("/wikis/{wiki_id}/sections")
async def create_wiki_section(wiki_id: int, section: WikiSectionCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, user["user_id"]
    )
    if not wiki:
        perm = await check_share_permission(pool, user["user_id"], 3, wiki_id, 2)
        if not perm:
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


@router.put("/wikis/{wiki_id}/sections/{section_id}")
async def update_wiki_section(wiki_id: int, section_id: int, section: WikiSectionUpdate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, user["user_id"]
    )
    if not wiki:
        perm = await check_share_permission(pool, user["user_id"], 3, wiki_id, 2)
        if not perm:
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
async def delete_wiki_section(wiki_id: int, section_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    wiki = await pool.fetchrow(
        "SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2",
        wiki_id, user["user_id"]
    )
    if not wiki:
        perm = await check_share_permission(pool, user["user_id"], 3, wiki_id, 3)
        if not perm:
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


# ── Hints ───────────────────────────────────────────────────────

@router.get("/hints")
async def list_hints(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        """SELECT hint_id, parent_id, title, description, hint_category_id, 'owned' as access, 3 as permission_level
           FROM hints WHERE user_id = $1
           UNION ALL
           SELECT h.hint_id, h.parent_id, h.title, h.description, h.hint_category_id, 'shared' as access, so.permission_level
           FROM hints h
           JOIN shared_objects so ON so.object_id = h.hint_category_id AND so.object_type_id = 2
           WHERE so.shared_to_user_id = $1
           ORDER BY parent_id, hint_id""",
        user["user_id"]
    )
    return {"hints": [dict(r) for r in rows]}


@router.get("/hints/{hint_id}")
async def get_hint(hint_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    node = await pool.fetchrow(
        "SELECT hint_id, user_id, parent_id, title, description, hint_category_id FROM hints WHERE hint_id = $1",
        hint_id
    )
    if not node:
        raise HTTPException(status_code=404, detail="Hint not found")

    if node["user_id"] != user["user_id"]:
        perm = await check_share_permission(pool, user["user_id"], 2, node["hint_category_id"], 1)
        if not perm:
            raise HTTPException(status_code=404, detail="Hint not found")

    children = await pool.fetch(
        "SELECT hint_id, parent_id, title, description, hint_category_id FROM hints WHERE parent_id = $1 ORDER BY hint_id",
        hint_id
    )
    result = dict(node)
    del result["user_id"]
    return {"node": result, "children": [dict(r) for r in children]}


@router.post("/hint-categories")
async def create_hint_category(cat: HintCategoryCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO hints (user_id, parent_id, title, description, hint_category_id) VALUES ($1, 0, $2, $3, 0) RETURNING hint_id, title",
        user["user_id"], cat.title, cat.description
    )
    await pool.execute(
        "UPDATE hints SET hint_category_id = $1 WHERE hint_id = $1",
        row["hint_id"]
    )
    result = dict(row)
    result["hint_category_id"] = row["hint_id"]
    return {"created": result}


@router.post("/hints")
async def create_hint(hint: HintCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()

    if hint.parent_id == 0:
        row = await pool.fetchrow(
            "INSERT INTO hints (user_id, parent_id, title, description, hint_category_id) VALUES ($1, 0, $2, $3, 0) RETURNING hint_id, parent_id, title",
            user["user_id"], hint.title, hint.description
        )
        await pool.execute(
            "UPDATE hints SET hint_category_id = $1 WHERE hint_id = $1",
            row["hint_id"]
        )
        result = dict(row)
        result["hint_category_id"] = row["hint_id"]
        return {"created": result}
    else:
        parent = await pool.fetchrow(
            "SELECT hint_id, user_id, hint_category_id FROM hints WHERE hint_id = $1",
            hint.parent_id
        )
        if not parent:
            raise HTTPException(status_code=404, detail="Parent hint not found")

        if parent["user_id"] != user["user_id"]:
            perm = await check_share_permission(pool, user["user_id"], 2, parent["hint_category_id"], 2)
            if not perm:
                raise HTTPException(status_code=404, detail="Parent hint not found")

        row = await pool.fetchrow(
            "INSERT INTO hints (user_id, parent_id, title, description, hint_category_id) VALUES ($1, $2, $3, $4, $5) RETURNING hint_id, parent_id, title, hint_category_id",
            parent["user_id"], hint.parent_id, hint.title, hint.description, parent["hint_category_id"]
        )
        return {"created": dict(row)}


@router.put("/hints/{hint_id}")
async def update_hint(hint_id: int, hint: HintUpdate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    existing = await pool.fetchrow(
        "SELECT hint_id, user_id, hint_category_id FROM hints WHERE hint_id = $1",
        hint_id
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Hint not found")

    if existing["user_id"] != user["user_id"]:
        perm = await check_share_permission(pool, user["user_id"], 2, existing["hint_category_id"], 2)
        if not perm:
            raise HTTPException(status_code=404, detail="Hint not found")

    updates = []
    values = []
    idx = 1
    if hint.title is not None:
        updates.append(f"title = ${idx}")
        values.append(hint.title)
        idx += 1
    if hint.description is not None:
        updates.append(f"description = ${idx}")
        values.append(hint.description)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append("updated_at = NOW()")
    values.append(hint_id)
    sql = f"UPDATE hints SET {', '.join(updates)} WHERE hint_id = ${idx} RETURNING hint_id, title"
    row = await pool.fetchrow(sql, *values)
    return {"updated": dict(row)}


@router.delete("/hints/{hint_id}")
async def delete_hint(hint_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    existing = await pool.fetchrow(
        "SELECT hint_id, user_id, parent_id, hint_category_id FROM hints WHERE hint_id = $1",
        hint_id
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Hint not found")

    if existing["user_id"] != user["user_id"]:
        if existing["parent_id"] == 0:
            raise HTTPException(status_code=404, detail="Hint not found")
        perm = await check_share_permission(pool, user["user_id"], 2, existing["hint_category_id"], 3)
        if not perm:
            raise HTTPException(status_code=404, detail="Hint not found")

    result = await pool.execute(
        """
        WITH RECURSIVE subtree AS (
            SELECT hint_id FROM hints WHERE hint_id = $1
            UNION ALL
            SELECT h.hint_id FROM hints h
            INNER JOIN subtree s ON h.parent_id = s.hint_id
        )
        DELETE FROM hints WHERE hint_id IN (SELECT hint_id FROM subtree)
        """,
        hint_id
    )
    deleted_count = int(result.split()[-1])
    return {"deleted": hint_id, "descendants_deleted": deleted_count - 1}


# ── Secrets ─────────────────────────────────────────────────────

@router.get("/secrets")
async def list_secrets(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT key, created_at, updated_at FROM secrets WHERE user_id = $1 ORDER BY key",
        user["user_id"]
    )
    return {"secrets": [dict(r) for r in rows]}


@router.get("/secrets/{key}")
async def get_secret(key: str, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT secret_id, key, encrypted_value, created_at, updated_at FROM secrets WHERE user_id = $1 AND key = $2",
        user["user_id"], key
    )
    if not row:
        raise HTTPException(status_code=404, detail="Secret not found")
    return {"key": row["key"], "value": decrypt(row["encrypted_value"])}


@router.put("/secrets/{key}")
async def set_secret(key: str, body: SecretCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    encrypted = encrypt(body.value)
    row = await pool.fetchrow(
        """
        INSERT INTO secrets (user_id, key, encrypted_value)
        VALUES ($1, $2, $3)
        ON CONFLICT ON CONSTRAINT uq_secrets_user_key
        DO UPDATE SET encrypted_value = EXCLUDED.encrypted_value, updated_at = NOW()
        RETURNING secret_id, key, created_at, updated_at
        """,
        user["user_id"], key, encrypted
    )
    return {"saved": dict(row)}


@router.delete("/secrets/{key}")
async def delete_secret(key: str, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    result = await pool.execute(
        "DELETE FROM secrets WHERE user_id = $1 AND key = $2",
        user["user_id"], key
    )
    if result == "DELETE 0":
        raise HTTPException(status_code=404, detail="Secret not found")
    return {"deleted": key}


# ── Users ──────────────────────────────────────────────────────

@router.get("/users")
async def list_users(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT user_id, name, username FROM users WHERE user_id != $1 ORDER BY name",
        user["user_id"]
    )
    return {"users": [dict(r) for r in rows]}


# ── Sharing ────────────────────────────────────────────────────

_OBJECT_TABLES = {
    1: ("projects", "project_id"),
    2: ("hints", "hint_id"),
    3: ("wikis", "wiki_id"),
}


@router.get("/sharing/by-me")
async def admin_shares_by_me(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        """SELECT so.share_id, so.object_type_id, ot.name as object_type, so.object_id,
                  so.shared_to_user_id, u.name as shared_to_name, so.permission_level,
                  COALESCE(p.title, h.title, w.title) as object_title
           FROM shared_objects so
           JOIN object_types ot ON so.object_type_id = ot.object_type_id
           JOIN users u ON so.shared_to_user_id = u.user_id
           LEFT JOIN projects p ON so.object_type_id = 1 AND so.object_id = p.project_id
           LEFT JOIN hints h ON so.object_type_id = 2 AND so.object_id = h.hint_id
           LEFT JOIN wikis w ON so.object_type_id = 3 AND so.object_id = w.wiki_id
           WHERE so.shared_by_user_id = $1
           ORDER BY so.object_type_id, so.object_id""",
        user["user_id"]
    )
    return {"shares": [dict(r) for r in rows]}


@router.get("/sharing/to-me")
async def admin_shares_to_me(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        """SELECT so.share_id, so.object_type_id, ot.name as object_type, so.object_id,
                  so.shared_by_user_id, u.name as shared_by_name, so.permission_level,
                  COALESCE(p.title, h.title, w.title) as object_title
           FROM shared_objects so
           JOIN object_types ot ON so.object_type_id = ot.object_type_id
           JOIN users u ON so.shared_by_user_id = u.user_id
           LEFT JOIN projects p ON so.object_type_id = 1 AND so.object_id = p.project_id
           LEFT JOIN hints h ON so.object_type_id = 2 AND so.object_id = h.hint_id
           LEFT JOIN wikis w ON so.object_type_id = 3 AND so.object_id = w.wiki_id
           WHERE so.shared_to_user_id = $1
           ORDER BY so.object_type_id, so.object_id""",
        user["user_id"]
    )
    return {"shares": [dict(r) for r in rows]}


@router.post("/sharing")
async def admin_create_share(share: ShareCreate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()

    if share.object_type_id not in _OBJECT_TABLES:
        raise HTTPException(status_code=400, detail="object_type_id must be 1 (project), 2 (hint), or 3 (wiki)")

    if share.permission_level not in (1, 2, 3):
        raise HTTPException(status_code=400, detail="permission_level must be 1, 2, or 3")

    if share.shared_to_user_id == user["user_id"]:
        raise HTTPException(status_code=400, detail="Cannot share to yourself")

    target_user = await pool.fetchrow(
        "SELECT user_id FROM users WHERE user_id = $1",
        share.shared_to_user_id
    )
    if not target_user:
        raise HTTPException(status_code=404, detail="Target user not found")

    table, pk_col = _OBJECT_TABLES[share.object_type_id]
    owner = await pool.fetchrow(
        f"SELECT {pk_col} FROM {table} WHERE {pk_col} = $1 AND user_id = $2",
        share.object_id, user["user_id"]
    )
    if not owner:
        raise HTTPException(status_code=404, detail="Object not found or you are not the owner")

    row = await pool.fetchrow(
        """INSERT INTO shared_objects (shared_by_user_id, shared_to_user_id, object_type_id, object_id, permission_level)
           VALUES ($1, $2, $3, $4, $5)
           ON CONFLICT ON CONSTRAINT uq_shared_objects
           DO UPDATE SET permission_level = EXCLUDED.permission_level
           RETURNING share_id, object_type_id, object_id, shared_to_user_id, permission_level""",
        user["user_id"], share.shared_to_user_id, share.object_type_id, share.object_id, share.permission_level
    )
    return {"shared": dict(row)}


@router.put("/sharing/{share_id}")
async def admin_update_share(share_id: int, body: ShareUpdate, user: dict = Depends(verify_user_token)):
    pool = await get_pool()

    if body.permission_level not in (1, 2, 3):
        raise HTTPException(status_code=400, detail="permission_level must be 1, 2, or 3")

    row = await pool.fetchrow(
        """UPDATE shared_objects SET permission_level = $1
           WHERE share_id = $2 AND shared_by_user_id = $3
           RETURNING share_id, object_type_id, object_id, shared_to_user_id, permission_level""",
        body.permission_level, share_id, user["user_id"]
    )
    if not row:
        raise HTTPException(status_code=404, detail="Share not found or you are not the owner")
    return {"updated": dict(row)}


@router.delete("/sharing/{share_id}")
async def admin_revoke_share(share_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    result = await pool.execute(
        "DELETE FROM shared_objects WHERE share_id = $1 AND shared_by_user_id = $2",
        share_id, user["user_id"]
    )
    if result == "DELETE 0":
        raise HTTPException(status_code=404, detail="Share not found or you are not the owner")
    return {"revoked": share_id}


# ── Images ─────────────────────────────────────────────────────

def _make_filename() -> str:
    ts = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    short_hash = hashlib.sha256(os.urandom(16)).hexdigest()[:8]
    return f"gen_{ts}_{short_hash}.png"


def _image_url(filename: str) -> str:
    return f"{IMAGES_BASE_URL}/nanoimages/{filename}"


def _img_to_dict(row) -> dict:
    return {
        "image_id": row["image_id"],
        "url": _image_url(row["filename"]),
        "filename": row["filename"],
        "prompt": row["prompt"],
        "model": row["model"],
        "keep": row["keep"],
        "size_bytes": row["size_bytes"],
        "width": row["width"],
        "height": row["height"],
        "created_at": row["created_at"].isoformat() if row["created_at"] else None,
    }


@router.get("/images")
async def admin_list_images(
    keep: Optional[bool] = Query(default=None),
    limit: int = Query(default=50, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    user: dict = Depends(verify_user_token),
):
    pool = await get_pool()

    if keep is not None:
        rows = await pool.fetch(
            """SELECT image_id, filename, prompt, model, created_at, keep,
                      size_bytes, width, height
               FROM images WHERE user_id = $1 AND keep = $2
               ORDER BY created_at DESC
               LIMIT $3 OFFSET $4""",
            user["user_id"], keep, limit, offset,
        )
    else:
        rows = await pool.fetch(
            """SELECT image_id, filename, prompt, model, created_at, keep,
                      size_bytes, width, height
               FROM images WHERE user_id = $1
               ORDER BY created_at DESC
               LIMIT $2 OFFSET $3""",
            user["user_id"], limit, offset,
        )

    return {"images": [_img_to_dict(r) for r in rows]}


@router.get("/images/{image_id}")
async def admin_get_image(image_id: int, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    row = await pool.fetchrow(
        """SELECT image_id, filename, prompt, model, created_at, keep,
                  size_bytes, width, height
           FROM images WHERE image_id = $1 AND user_id = $2""",
        image_id, user["user_id"],
    )
    if not row:
        raise HTTPException(status_code=404, detail="Image not found")
    return _img_to_dict(row)


@router.post("/images/generate")
async def admin_generate_image(req: GenImageRequest, user: dict = Depends(verify_user_token)):
    pool = await get_pool()

    try:
        result = gemini_generate(
            prompt=req.prompt,
            model=req.model,
            aspect_ratio=req.aspect_ratio,
        )
    except Exception as e:
        logger.error(f"Gemini image generation failed: {e}")
        raise HTTPException(status_code=502, detail=str(e))

    image_bytes = result["image_bytes"]
    filename = _make_filename()
    filepath = os.path.join(IMAGES_DIR, filename)

    with open(filepath, "wb") as f:
        f.write(image_bytes)

    width, height = None, None
    try:
        img = PILImage.open(BytesIO(image_bytes))
        width, height = img.size
    except Exception:
        pass

    size_bytes = len(image_bytes)

    row = await pool.fetchrow(
        """INSERT INTO images (user_id, filename, prompt, model, size_bytes, width, height)
           VALUES ($1, $2, $3, $4, $5, $6, $7)
           RETURNING image_id, filename, prompt, model, created_at, keep, size_bytes, width, height""",
        user["user_id"], filename, req.prompt, result["model_used"], size_bytes, width, height,
    )

    return _img_to_dict(row)


@router.patch("/images/{image_id}")
async def admin_update_image(image_id: int, req: KeepRequest, user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    row = await pool.fetchrow(
        """UPDATE images SET keep = $1
           WHERE image_id = $2 AND user_id = $3
           RETURNING image_id, filename, prompt, model, created_at, keep,
                     size_bytes, width, height""",
        req.keep, image_id, user["user_id"],
    )
    if not row:
        raise HTTPException(status_code=404, detail="Image not found")
    return _img_to_dict(row)


@router.delete("/images/{image_id}")
async def admin_delete_image(
    image_id: int,
    force: bool = Query(default=False),
    user: dict = Depends(verify_user_token),
):
    pool = await get_pool()

    row = await pool.fetchrow(
        "SELECT image_id, filename, keep FROM images WHERE image_id = $1 AND user_id = $2",
        image_id, user["user_id"],
    )
    if not row:
        raise HTTPException(status_code=404, detail="Image not found")

    if row["keep"] and not force:
        raise HTTPException(
            status_code=409,
            detail=f"Image {image_id} is marked keep=true. Use ?force=true to delete.",
        )

    filepath = os.path.join(IMAGES_DIR, row["filename"])
    try:
        os.remove(filepath)
    except FileNotFoundError:
        pass

    await pool.execute("DELETE FROM images WHERE image_id = $1", image_id)
    return {"deleted": True, "image_id": image_id}


@router.post("/images/cleanup")
async def admin_cleanup_images(user: dict = Depends(verify_user_token)):
    pool = await get_pool()

    rows = await pool.fetch(
        "SELECT image_id, filename FROM images WHERE user_id = $1 AND keep = false",
        user["user_id"]
    )

    if not rows:
        return {"deleted": 0}

    for row in rows:
        filepath = os.path.join(IMAGES_DIR, row["filename"])
        try:
            os.remove(filepath)
        except FileNotFoundError:
            pass

    result = await pool.execute(
        "DELETE FROM images WHERE user_id = $1 AND keep = false",
        user["user_id"]
    )
    count = int(result.split()[-1])

    return {"deleted": count}


# ── Dashboard ──────────────────────────────────────────────────

@router.get("/dashboard")
async def admin_dashboard(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    stats_row = await pool.fetchrow(
        """
        WITH agent_count AS (
            SELECT count(*) AS n FROM agents WHERE user_id = $1
        ),
        project_count AS (
            SELECT count(*) AS n FROM (
                SELECT project_id FROM projects WHERE user_id = $1
                UNION
                SELECT object_id FROM shared_objects WHERE shared_to_user_id = $1 AND object_type_id = 1
            ) t
        ),
        wiki_count AS (
            SELECT count(*) AS n FROM (
                SELECT wiki_id FROM wikis WHERE user_id = $1
                UNION
                SELECT object_id FROM shared_objects WHERE shared_to_user_id = $1 AND object_type_id = 3
            ) t
        ),
        hint_cat_count AS (
            SELECT count(*) AS n FROM (
                SELECT DISTINCT hint_category_id FROM hints WHERE user_id = $1 AND parent_id = 0
                UNION
                SELECT object_id FROM shared_objects WHERE shared_to_user_id = $1 AND object_type_id = 2
            ) t
        ),
        secret_count AS (
            SELECT count(*) AS n FROM secrets WHERE user_id = $1
        ),
        image_count AS (
            SELECT count(*) AS n FROM images WHERE user_id = $1
        ),
        handoff_count AS (
            SELECT count(*) AS n FROM handoffs
            WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id = $1)
            AND picked_up_at IS NULL
        ),
        shared_to_me_count AS (
            SELECT count(*) AS n FROM shared_objects WHERE shared_to_user_id = $1
        )
        SELECT
            (SELECT n FROM agent_count) AS agents,
            (SELECT n FROM project_count) AS projects,
            (SELECT n FROM wiki_count) AS wikis,
            (SELECT n FROM hint_cat_count) AS hint_categories,
            (SELECT n FROM secret_count) AS secrets,
            (SELECT n FROM image_count) AS images,
            (SELECT n FROM handoff_count) AS pending_handoffs,
            (SELECT n FROM shared_to_me_count) AS shared_to_me
        """,
        user["user_id"]
    )

    session_rows = await pool.fetch(
        """
        SELECT s.session_id, a.name AS agent_name, s.started_at, s.project
        FROM sessions s
        JOIN agents a ON a.agent_id = s.agent_id
        WHERE a.user_id = $1
        ORDER BY s.started_at DESC
        LIMIT 5
        """,
        user["user_id"]
    )

    return {
        "stats": {
            "agents": stats_row["agents"],
            "projects": stats_row["projects"],
            "wikis": stats_row["wikis"],
            "hint_categories": stats_row["hint_categories"],
            "secrets": stats_row["secrets"],
            "images": stats_row["images"],
            "pending_handoffs": stats_row["pending_handoffs"],
            "shared_to_me": stats_row["shared_to_me"],
        },
        "recent_sessions": [
            {
                "session_id": r["session_id"],
                "agent_name": r["agent_name"],
                "started_at": r["started_at"].isoformat() if r["started_at"] else None,
                "project": r["project"],
            }
            for r in session_rows
        ],
    }
