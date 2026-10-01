#!/usr/bin/env python3
"""One-off backfill after migration 010 (project #3 §450): fills images.mime_type, source and missing
width/height for rows created before 010, reading each file's real bytes (many old "*.png" files are JPEG).

Only touches rows where mime_type IS NULL, so it's safe to re-run. Filenames/URLs are never changed.
source = 'edited' if the prompt starts with "[edit]", else 'generated' (no uploads existed before 010).

Usage (on the LucyAPI server):  python3 tools/backfill_image_metadata.py [--dry-run] [--images-dir /opt/lucyapi/output/images]
"""
import argparse
import os
import struct
import subprocess
import sys


def psql(sql: str) -> str:
    out = subprocess.run(
        ["sudo", "-u", "postgres", "psql", "-d", "lucyapi", "-Atq", "-v", "ON_ERROR_STOP=1", "-F", "\t"],
        input=sql, capture_output=True, text=True, check=True)
    return out.stdout


def sniff(path: str):
    """(mime, width, height) from the file header, or None."""
    with open(path, "rb") as f:
        head = f.read(64)
        if head[:8] == b"\x89PNG\r\n\x1a\n":
            w, h = struct.unpack(">II", head[16:24])
            return "image/png", w, h
        if head[:6] in (b"GIF87a", b"GIF89a"):
            w, h = struct.unpack("<HH", head[6:10])
            return "image/gif", w, h
        if head[:4] == b"RIFF" and head[8:12] == b"WEBP":
            return "image/webp", None, None
        if head[:3] == b"\xff\xd8\xff":
            return ("image/jpeg",) + jpeg_size(f)
    return None


def jpeg_size(f):
    """Scan JPEG markers for the first SOFn frame header."""
    f.seek(2)
    while True:
        b = f.read(1)
        while b and b != b"\xff":
            b = f.read(1)
        while b == b"\xff":
            b = f.read(1)
        if not b:
            return None, None
        marker = b[0]
        if marker in (0xD8, 0x01) or 0xD0 <= marker <= 0xD7:
            continue
        seg_len = struct.unpack(">H", f.read(2))[0]
        if 0xC0 <= marker <= 0xCF and marker not in (0xC4, 0xC8, 0xCC):
            h, w = struct.unpack(">xHH", f.read(5))
            return w, h
        f.seek(seg_len - 2, 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--images-dir", default="/opt/lucyapi/output/images")
    args = ap.parse_args()

    rows = psql("SELECT image_id, filename, COALESCE(prompt, ''), width, height "
                "FROM public.images WHERE mime_type IS NULL ORDER BY image_id;").splitlines()
    updates, problems = [], []
    for row in rows:
        image_id, filename, prompt, width, height = row.split("\t")
        path = os.path.join(args.images_dir, os.path.basename(filename))
        info = sniff(path) if os.path.isfile(path) else None
        if info is None:
            problems.append(f"{image_id} {filename}: file missing or not a recognised image")
            continue
        mime, w, h = info
        source = "edited" if prompt.startswith("[edit]") else "generated"
        sets = [f"mime_type = '{mime}'", f"source = '{source}'"]
        if not width and w:
            sets.append(f"width = {int(w)}")
        if not height and h:
            sets.append(f"height = {int(h)}")
        updates.append(f"UPDATE public.images SET {', '.join(sets)} WHERE image_id = {int(image_id)} AND mime_type IS NULL;")
        print(f"{image_id:>4} {filename}: {mime} {w}x{h} {source}")

    for p in problems:
        print("SKIP", p, file=sys.stderr)
    if not updates:
        print("Nothing to backfill.")
        return
    if args.dry_run:
        print(f"Dry run: {len(updates)} row(s) would be updated.")
        return
    psql("SET ROLE leaddev;\nBEGIN;\n" + "\n".join(updates) + "\nCOMMIT;\n")
    print(f"Updated {len(updates)} row(s).")


if __name__ == "__main__":
    main()
