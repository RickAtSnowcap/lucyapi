"""
Image generation and management routes for LucyAPI.
Uses Gemini API via the gemini.py wrapper module.
"""

import hashlib
import os
import logging
from datetime import datetime, timezone
from io import BytesIO
from typing import Optional

import httpx
from fastapi import APIRouter, HTTPException, Query
from pydantic import BaseModel, Field
from PIL import Image

from ..database import get_pool
from ..gemini import generate_image, edit_image, analyze_image

logger = logging.getLogger(__name__)

router = APIRouter()

IMAGES_DIR = "/opt/lucyapi/output/images"
BASE_URL = "https://lucyapi.snowcapsystems.com"


# ── Request/Response Models ───────────────────────────────────────

class GenImageRequest(BaseModel):
    prompt: str
    model: str = Field(default="nano-banana")
    aspect_ratio: str = Field(default="1:1")


class KeepRequest(BaseModel):
    keep: bool


class EditImageRequest(BaseModel):
    prompt: str
    image_id: Optional[int] = None
    image_url: Optional[str] = None
    model: str = Field(default="nano-banana")


class AnalyzeImageRequest(BaseModel):
    image_id: Optional[int] = None
    image_url: Optional[str] = None
    prompt: str = Field(default="Describe this image in detail")


# ── Helpers ───────────────────────────────────────────────────────

def _make_filename() -> str:
    """Generate a unique filename: gen_{timestamp}_{short_hash}.png"""
    ts = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    short_hash = hashlib.sha256(os.urandom(16)).hexdigest()[:8]
    return f"gen_{ts}_{short_hash}.png"


def _image_url(filename: str) -> str:
    return f"{BASE_URL}/nanoimages/{filename}"


async def _load_source_image(image_id: Optional[int], image_url: Optional[str]) -> tuple[bytes, str]:
    """Load image bytes from either an image_id (database lookup) or a URL.
    Returns (image_bytes, source_description).
    """
    if image_id is not None:
        pool = await get_pool()
        row = await pool.fetchrow(
            "SELECT filename FROM images WHERE image_id = $1", image_id
        )
        if not row:
            raise HTTPException(status_code=404, detail=f"Image {image_id} not found")
        filepath = os.path.join(IMAGES_DIR, row["filename"])
        try:
            with open(filepath, "rb") as f:
                return f.read(), f"image_id={image_id}"
        except FileNotFoundError:
            raise HTTPException(status_code=404, detail=f"Image file not found on disk for image_id={image_id}")
    elif image_url is not None:
        try:
            async with httpx.AsyncClient(timeout=30) as client:
                resp = await client.get(image_url)
                resp.raise_for_status()
                return resp.content, f"url={image_url}"
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"Failed to download image from URL: {e}")
    else:
        raise HTTPException(status_code=400, detail="Provide either image_id or image_url")


def _row_to_dict(row) -> dict:
    """Convert a database row to a response dict with URL."""
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
        "created_at": row["created_at"].isoformat(),
    }


# ── Generation ────────────────────────────────────────────────────

@router.post("/genimage")
async def gen_image(req: GenImageRequest):
    """Generate an image from a text prompt via Gemini."""
    try:
        result = generate_image(
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
        img = Image.open(BytesIO(image_bytes))
        width, height = img.size
    except Exception:
        pass

    size_bytes = len(image_bytes)

    pool = await get_pool()
    row = await pool.fetchrow(
        """
        INSERT INTO images (filename, prompt, model, size_bytes, width, height)
        VALUES ($1, $2, $3, $4, $5, $6)
        RETURNING image_id, filename, prompt, model, created_at, keep, size_bytes, width, height
        """,
        filename, req.prompt, result["model_used"], size_bytes, width, height,
    )

    return _row_to_dict(row)


# ── Edit ──────────────────────────────────────────────────────────

@router.post("/genimage/edit")
async def gen_image_edit(req: EditImageRequest):
    """Edit an existing image with a text prompt via Gemini."""
    source_bytes, source_desc = await _load_source_image(req.image_id, req.image_url)

    try:
        result = edit_image(
            source_bytes=source_bytes,
            prompt=req.prompt,
            model=req.model,
        )
    except Exception as e:
        logger.error(f"Gemini image edit failed ({source_desc}): {e}")
        raise HTTPException(status_code=502, detail=str(e))

    image_bytes = result["image_bytes"]
    filename = _make_filename()
    filepath = os.path.join(IMAGES_DIR, filename)

    with open(filepath, "wb") as f:
        f.write(image_bytes)

    width, height = None, None
    try:
        img = Image.open(BytesIO(image_bytes))
        width, height = img.size
    except Exception:
        pass

    size_bytes = len(image_bytes)
    edit_prompt = f"[edit] {req.prompt}"

    pool = await get_pool()
    row = await pool.fetchrow(
        """
        INSERT INTO images (filename, prompt, model, size_bytes, width, height)
        VALUES ($1, $2, $3, $4, $5, $6)
        RETURNING image_id, filename, prompt, model, created_at, keep, size_bytes, width, height
        """,
        filename, edit_prompt, result["model_used"], size_bytes, width, height,
    )

    return _row_to_dict(row)


# ── Analyze ───────────────────────────────────────────────────────

@router.post("/genimage/analyze")
async def gen_image_analyze(req: AnalyzeImageRequest):
    """Analyze an image and return a text description via Gemini."""
    source_bytes, source_desc = await _load_source_image(req.image_id, req.image_url)

    try:
        result = analyze_image(
            image_bytes=source_bytes,
            prompt=req.prompt,
        )
    except Exception as e:
        logger.error(f"Gemini image analysis failed ({source_desc}): {e}")
        raise HTTPException(status_code=502, detail=str(e))

    return {
        "text": result["text"],
        "model_used": result["model_used"],
        "source": source_desc,
    }


# ── Cleanup (before /{image_id} routes to avoid collision) ────────

@router.post("/images/cleanup")
async def cleanup_images():
    """Bulk delete all images where keep=false. Returns count deleted."""
    pool = await get_pool()

    rows = await pool.fetch(
        "SELECT image_id, filename FROM images WHERE keep = false"
    )

    if not rows:
        return {"deleted": 0}

    for row in rows:
        filepath = os.path.join(IMAGES_DIR, row["filename"])
        try:
            os.remove(filepath)
        except FileNotFoundError:
            pass
        except Exception as e:
            logger.error(f"Failed to delete image file {row['filename']}: {e}")

    result = await pool.execute("DELETE FROM images WHERE keep = false")
    count = int(result.split()[-1])

    return {"deleted": count}


# ── List ──────────────────────────────────────────────────────────

@router.get("/images")
async def list_images(
    keep: Optional[bool] = Query(default=None, description="Filter by keep flag"),
    limit: int = Query(default=50, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
):
    """List images with optional filtering."""
    pool = await get_pool()

    if keep is not None:
        rows = await pool.fetch(
            """
            SELECT image_id, filename, prompt, model, created_at, keep,
                   size_bytes, width, height
            FROM images WHERE keep = $1
            ORDER BY created_at DESC
            LIMIT $2 OFFSET $3
            """,
            keep, limit, offset,
        )
    else:
        rows = await pool.fetch(
            """
            SELECT image_id, filename, prompt, model, created_at, keep,
                   size_bytes, width, height
            FROM images
            ORDER BY created_at DESC
            LIMIT $1 OFFSET $2
            """,
            limit, offset,
        )

    return [_row_to_dict(row) for row in rows]


# ── Single Image ──────────────────────────────────────────────────

@router.get("/images/{image_id}")
async def get_image(image_id: int):
    """Get metadata for a single image."""
    pool = await get_pool()
    row = await pool.fetchrow(
        """
        SELECT image_id, filename, prompt, model, created_at, keep,
               size_bytes, width, height
        FROM images WHERE image_id = $1
        """,
        image_id,
    )
    if not row:
        raise HTTPException(status_code=404, detail=f"Image {image_id} not found")
    return _row_to_dict(row)


# ── Update Keep Flag ──────────────────────────────────────────────

@router.patch("/images/{image_id}")
async def update_image(image_id: int, req: KeepRequest):
    """Update the keep flag on an image."""
    pool = await get_pool()
    row = await pool.fetchrow(
        """
        UPDATE images SET keep = $1 WHERE image_id = $2
        RETURNING image_id, filename, prompt, model, created_at, keep,
                  size_bytes, width, height
        """,
        req.keep, image_id,
    )
    if not row:
        raise HTTPException(status_code=404, detail=f"Image {image_id} not found")
    return _row_to_dict(row)


# ── Delete ────────────────────────────────────────────────────────

@router.delete("/images/{image_id}")
async def delete_image(
    image_id: int,
    force: bool = Query(default=False, description="Force delete even if keep=true"),
):
    """Delete an image file and DB row. Rejects if keep=true unless force=true."""
    pool = await get_pool()

    row = await pool.fetchrow(
        "SELECT image_id, filename, keep FROM images WHERE image_id = $1",
        image_id,
    )
    if not row:
        raise HTTPException(status_code=404, detail=f"Image {image_id} not found")

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
    except Exception as e:
        logger.error(f"Failed to delete image file {row['filename']}: {e}")

    await pool.execute("DELETE FROM images WHERE image_id = $1", image_id)

    return {"deleted": True, "image_id": image_id}
