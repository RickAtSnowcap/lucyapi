#!/usr/bin/env python3
"""End-to-end ownership test for LucyAPI (migration 007). Creates two throwaway users (zzown = owner,
zzout = outsider) with agents, keys and data, checks every access path (REST key, admin JWT, legacy MCP),
then deletes everything. Prints no secrets.
Also covers image uploads (migration 010, project #3 §450): upload_image + /admin/images/upload, WebP → PNG,
GIF stored byte-for-byte, and rejection of bad magic bytes, truncated, oversize and over-pixel-cap images."""
import base64, hashlib, json, os, secrets, struct, subprocess, sys, urllib.request, urllib.error, uuid, zlib

B = "http://127.0.0.1:8100"
IMAGES_DIR = "/opt/lucyapi/output/images"
FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")
results = []

def psql(sql):
    r = subprocess.run(["sudo", "-u", "postgres", "psql", "-d", "lucyapi", "-v", "ON_ERROR_STOP=1", "-Atq", "-c", sql],
                       capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(r.stderr.strip())
    return r.stdout.strip()

def identity_v3_hash(password):
    salt = os.urandom(16); it = 100_000
    sub = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, it, 32)
    blob = bytes([1]) + (1).to_bytes(4, "big") + it.to_bytes(4, "big") + (16).to_bytes(4, "big") + salt + sub
    return base64.b64encode(blob).decode()

def http(method, path, key=None, jwt=None, body=None):
    h = {"Content-Type": "application/json"}
    if key: h["X-Api-Key"] = key
    if jwt: h["Authorization"] = "Bearer " + jwt
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(B + path, data=data, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()
    except (urllib.error.URLError, ConnectionError):   # server may close an over-limit body before reading it
        return 413, ""

def mcp(tool, args, key):
    body = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": tool, "arguments": dict(args, agent_key=key)}}
    s, t = http("POST", "/mcp/", body=body)
    try:
        return json.loads(json.loads(t)["result"]["content"][0]["text"])
    except Exception:
        return {"error": f"unparsed {s}"}

def fixture(name):
    with open(os.path.join(FIXTURES, name), "rb") as f:
        return f.read()

def make_png(w, h):
    """Black RGB PNG of any size; rows of zeros compress to almost nothing (pixel-cap test)."""
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    raw = zlib.compress(b"\x00" * ((w * 3 + 1) * h), 9)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", raw) + chunk(b"IEND", b"")

def make_animated_gif(frames=3):
    """1x1, 2-colour animated GIF (NETSCAPE loop) with `frames` frames."""
    g = b"GIF89a" + struct.pack("<HH", 1, 1) + bytes([0x80, 0, 0]) + b"\x00\x00\x00\xff\xff\xff"
    g += b"\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00"
    for i in range(frames):
        g += b"\x21\xf9\x04\x00\x0a\x00\x00\x00" + b"\x2c" + struct.pack("<HHHH", 0, 0, 1, 1) + b"\x00"
        g += bytes([0x02, 0x02, 0x44 if i % 2 == 0 else 0x4c, 0x01, 0x00])
    return g + b"\x3b"

def multipart(path, filename, data, jwt):
    boundary = uuid.uuid4().hex
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{filename}\"\r\n"
            f"Content-Type: application/octet-stream\r\n\r\n").encode() + data + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(B + path, data=body, method="POST", headers={
        "Content-Type": f"multipart/form-data; boundary={boundary}", "Authorization": "Bearer " + jwt})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()
    except (urllib.error.URLError, ConnectionError) as e:   # server may close an over-limit body early
        return 413, str(e)

def disk(filename):
    with open(os.path.join(IMAGES_DIR, filename), "rb") as f:
        return f.read()

def check(name, ok, detail=""):
    results.append((ok, name, detail))

def denied_http(name, status, allowed=(404,)):
    check(name, status in allowed, f"got {status}")

A_KEY, B_KEY = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
B_PW = secrets.token_urlsafe(18)
ids = {}
try:
    # ---------- setup ----------
    psql(f"""INSERT INTO users (name, username, password_hash) VALUES ('zz owner','zzown',NULL), ('zz outsider','zzout','{identity_v3_hash(B_PW)}');""")
    ua = int(psql("SELECT user_id FROM users WHERE username='zzown'")); ub = int(psql("SELECT user_id FROM users WHERE username='zzout'"))
    psql(f"INSERT INTO agents (user_id, name, api_key) VALUES ({ua},'zzown-agent','{A_KEY}'), ({ub},'zzout-agent','{B_KEY}')")
    st = psql("SELECT min(status_id) FROM project_statuses")
    P = int(psql(f"INSERT INTO projects (user_id, title, status_id) VALUES ({ua},'zz-P',{st}) RETURNING project_id"))
    S = int(psql(f"SELECT section_id FROM lucyapi.fn_section_create({ua},{P},0,'zz-S','d',NULL)"))
    W = int(psql(f"INSERT INTO wikis (user_id, title) VALUES ({ua},'zz-W') RETURNING wiki_id"))
    WS = int(psql(f"SELECT section_id FROM lucyapi.fn_wiki_section_create({ua},{W},0,'zz-WS','d',ARRAY['zztag'])"))
    W2 = int(psql(f"INSERT INTO wikis (user_id, title) VALUES ({ua},'zz-W2') RETURNING wiki_id"))
    WS2 = int(psql(f"SELECT section_id FROM lucyapi.fn_wiki_section_create({ua},{W2},0,'zz-WS2','d',NULL)"))
    HC = int(psql(f"SELECT hint_id FROM lucyapi.fn_hint_category_create({ua},0,'zz-HC','d',0)"))
    H = int(psql(f"SELECT hint_id FROM lucyapi.fn_hint_create({ua},{HC},'zz-H','d',0)"))
    I = int(psql(f"INSERT INTO images (user_id, filename, prompt, model) VALUES ({ua},'zz-none.png','zz','m') RETURNING image_id"))
    psql(f"INSERT INTO shared_objects (shared_by_user_id, shared_to_user_id, object_type_id, object_id, permission_level) VALUES ({ua},{ub},3,{W2},2)")
    ids.update(ua=ua, ub=ub)

    s, t = http("POST", "/auth/login", body={"username": "zzout", "password": B_PW})
    jwt = json.loads(t).get("token") if s == 200 else None
    check("outsider admin login works", jwt is not None, f"status {s}")

    # ---------- outsider over REST (agent key) ----------
    R = lambda m, p, b=None: http(m, p, key=B_KEY, body=b)[0]
    denied_http("REST get project", R("GET", f"/projects/{P}"))
    denied_http("REST update project", R("PUT", f"/projects/{P}", {"title": "pwn"}))
    denied_http("REST delete project", R("DELETE", f"/projects/{P}"))
    denied_http("REST get section", R("GET", f"/projects/{P}/sections/{S}"))
    denied_http("REST create section", R("POST", f"/projects/{P}/sections", {"parent_id": 0, "title": "pwn", "description": "x"}))
    denied_http("REST update section", R("PUT", f"/projects/{P}/sections/{S}", {"title": "pwn"}))
    denied_http("REST delete section", R("DELETE", f"/projects/{P}/sections/{S}"))
    denied_http("REST get wiki", R("GET", f"/wikis/{W}"))
    denied_http("REST update wiki", R("PUT", f"/wikis/{W}", {"title": "pwn"}))
    denied_http("REST delete wiki", R("DELETE", f"/wikis/{W}"))
    denied_http("REST get wiki section", R("GET", f"/wikis/{W}/sections/{WS}"))
    denied_http("REST update wiki section", R("PUT", f"/wikis/{W}/sections/{WS}", {"title": "pwn", "tags": ["pwn"]}))
    denied_http("REST delete wiki section", R("DELETE", f"/wikis/{W}/sections/{WS}"))
    s, t = http("GET", f"/wikis/{W}/tags", key=B_KEY); check("REST wiki tags hidden", "zztag" not in t, f"{s}")
    denied_http("REST get hint", R("GET", f"/hints/{H}"))
    denied_http("REST update hint", R("PUT", f"/hints/{H}", {"title": "pwn"}))
    denied_http("REST delete hint", R("DELETE", f"/hints/{H}"))
    denied_http("REST inject hint", R("POST", "/hints", {"parent_id": HC, "title": "pwn", "description": "x"}))
    denied_http("REST inject category", R("POST", "/hints/categories", {"parent_id": HC, "title": "pwn", "description": "x"}))
    denied_http("REST delete category", R("DELETE", f"/hints/categories/{HC}"))
    denied_http("REST get image", R("GET", f"/images/{I}"))
    denied_http("REST keep image", R("PATCH", f"/images/{I}", {"keep": True}))
    denied_http("REST delete image", R("DELETE", f"/images/{I}?force=true"))
    denied_http("REST analyze image", R("POST", "/genimage/analyze", {"image_id": I, "prompt": "describe"}))
    denied_http("REST share owner's project", R("POST", "/sharing", {"shared_to_user_id": ua, "object_type_id": 1, "object_id": P, "permission_level": 3}))
    denied_http("REST other user's agent memories", R("GET", "/agents/zzown-agent/memories"))

    # ---------- outsider over admin (JWT) ----------
    if jwt:
        A_ = lambda m, p, b=None: http(m, p, jwt=jwt, body=b)[0]
        denied_http("admin get project", A_("GET", f"/admin/projects/{P}"))
        denied_http("admin update project", A_("PUT", f"/admin/projects/{P}", {"title": "pwn"}))
        denied_http("admin delete project", A_("DELETE", f"/admin/projects/{P}"))
        denied_http("admin update section", A_("PUT", f"/admin/projects/{P}/sections/{S}", {"title": "pwn"}))
        denied_http("admin delete section", A_("DELETE", f"/admin/projects/{P}/sections/{S}"))
        denied_http("admin update wiki", A_("PUT", f"/admin/wikis/{W}", {"title": "pwn"}))
        denied_http("admin delete wiki", A_("DELETE", f"/admin/wikis/{W}"))
        denied_http("admin update wiki section", A_("PUT", f"/admin/wikis/{W}/sections/{WS}", {"title": "pwn"}))
        denied_http("admin delete wiki section", A_("DELETE", f"/admin/wikis/{W}/sections/{WS}"))
        denied_http("admin get hint", A_("GET", f"/admin/hints/{H}"))
        denied_http("admin update hint", A_("PUT", f"/admin/hints/{H}", {"title": "pwn"}))
        denied_http("admin delete hint", A_("DELETE", f"/admin/hints/{H}"))
        denied_http("admin inject hint", A_("POST", "/admin/hints", {"parent_id": HC, "title": "pwn", "description": "x"}))
        denied_http("admin get image", A_("GET", f"/admin/images/{I}"))
        denied_http("admin keep image", A_("PATCH", f"/admin/images/{I}", {"keep": True}))
        denied_http("admin delete image", A_("DELETE", f"/admin/images/{I}?force=true"))
        denied_http("admin share owner's project", A_("POST", "/admin/sharing", {"shared_to_user_id": ua, "object_type_id": 1, "object_id": P, "permission_level": 3}))

    # ---------- MCP (legacy key path; same dispatcher/handlers as OAuth) — owner control + outsider ----------
    calls = [("get_hint", {"hint_id": H}), ("get_section", {"project_id": P, "section_id": S}),
             ("get_wiki_section", {"wiki_id": W, "section_id": WS})]
    for tool, args in calls:
        ok_owner = "error" not in mcp(tool, args, A_KEY)
        check(f"MCP {tool}: owner allowed (control)", ok_owner)
        check(f"MCP {tool}: outsider denied", "error" in mcp(tool, args, B_KEY))
    for tool, args in [("update_hint", {"hint_id": H, "title": "pwn"}), ("delete_hint", {"hint_id": H}),
                       ("create_hint", {"parent_id": HC, "title": "pwn", "description": "x"}),
                       ("update_section", {"project_id": P, "section_id": S, "title": "pwn"}),
                       ("delete_section", {"project_id": P, "section_id": S}),
                       ("update_project", {"project_id": P, "title": "pwn"}), ("delete_project", {"project_id": P}),
                       ("update_wiki_section", {"wiki_id": W, "section_id": WS, "title": "pwn"}),
                       ("delete_wiki", {"wiki_id": W}), ("keep_image", {"image_id": I}), ("delete_image", {"image_id": I, "force": True}),
                       ("analyze_image", {"image_id": I, "prompt": "describe"}),
                       ("share_object", {"shared_to_user_id": ua, "object_type_id": 1, "object_id": P, "permission_level": 3})]:
        check(f"MCP {tool}: outsider denied", "error" in mcp(tool, args, B_KEY))

    # ---------- image uploads (migration 010): MCP upload_image as the owner ----------
    agent_a = int(psql("SELECT agent_id FROM agents WHERE name='zzown-agent'"))
    up = lambda data, **kw: mcp("upload_image", dict({"data": base64.b64encode(data).decode(), "title": "zz"}, **kw), A_KEY)
    rows_before = int(psql(f"SELECT count(*) FROM images WHERE user_id={ua}"))

    r = up(fixture("sample.png"), description="zz desc")
    check("upload PNG", r.get("mime_type") == "image/png" and r.get("url", "").endswith(".png") and r.get("source") == "uploaded"
          and r.get("agent_id") == agent_a and r.get("keep") is True and r.get("description") == "zz desc"
          and (r.get("width"), r.get("height")) == (320, 200) and r.get("notice") is None, str(r)[:200])
    check("upload PNG stored byte-for-byte", "filename" in r and disk(r["filename"]) == fixture("sample.png"))
    UP_PNG = r.get("image_id")

    r = up(fixture("sample.jpg"))
    check("upload JPEG", r.get("mime_type") == "image/jpeg" and r.get("url", "").endswith(".jpg"), str(r)[:200])

    r = up(fixture("lossy.webp"))
    check("upload lossy WebP lands as PNG", r.get("mime_type") == "image/png" and r.get("url", "").endswith(".png")
          and r.get("notice") == "WebP was converted to PNG." and disk(r["filename"])[:8] == b"\x89PNG\r\n\x1a\n", str(r)[:200])

    r = up(fixture("lossless-alpha.webp"))
    check("upload lossless+alpha WebP keeps alpha (PNG colour type 6)", "filename" in r and disk(r["filename"])[25] == 6, str(r)[:200])

    r = up(fixture("animated.webp"))
    check("upload animated WebP: first frame as PNG + notice", r.get("mime_type") == "image/png"
          and "100 frames" in (r.get("notice") or "") and (r.get("width"), r.get("height")) == (300, 225), str(r)[:200])

    gif = make_animated_gif()
    r = up(gif)
    check("upload animated GIF stored byte-for-byte", r.get("mime_type") == "image/gif" and r.get("url", "").endswith(".gif")
          and disk(r["filename"]) == gif and r.get("notice") is None, str(r)[:200])

    r = mcp("upload_image", {"data": "data:image/png;base64," + base64.b64encode(fixture("sample.png")).decode(), "title": "zz"}, A_KEY)
    check("upload accepts a data: URL", r.get("mime_type") == "image/png", str(r)[:200])
    uploaded_ok = 7

    png = fixture("sample.png")
    # "over 25 MB": this script uses the legacy /mcp/ route (Kestrel default ~28.6 MB body), which refuses the
    # ~35 MB request outright (413); the OAuth connector allows 36 MB and the dispatcher returns the 25 MB error.
    for name, data, expect in [("garbage bytes", os.urandom(4096), ("Not a supported image",)),
                               ("text file", b"hello, I am not a PNG\n" * 50, ("Not a supported image",)),
                               ("truncated PNG", png[:len(png) // 2], ("truncated",)),
                               ("over pixel cap (7000x6000)", make_png(7000, 6000), ("MP",)),
                               ("over 25 MB", b"\x89PNG\r\n\x1a\n" + os.urandom(26 * 1024 * 1024), ("25 MB", "unparsed 413"))]:
        r = up(data)
        check(f"upload rejects {name}", any(e in r.get("error", "") for e in expect), str(r)[:200])
    r = mcp("upload_image", {"data": "not base64!!", "title": "zz"}, A_KEY)
    check("upload rejects invalid base64", "base64" in r.get("error", ""), str(r)[:200])
    check("rejected uploads leave no rows", int(psql(f"SELECT count(*) FROM images WHERE user_id={ua}")) == rows_before + uploaded_ok)

    # ---------- image uploads: LucyAdmin multipart as zzout ----------
    UP_ADMIN = None
    if jwt:
        s, r = multipart("/admin/images/upload", "phone-photo.jpg", fixture("sample.jpg"), jwt)
        check("admin upload JPEG", s == 200 and r.get("mime_type") == "image/jpeg" and r.get("title") == "phone-photo"
              and r.get("agent_id") is None and r.get("source") == "uploaded" and r.get("keep") is True, f"{s} {str(r)[:200]}")
        UP_ADMIN = r.get("image_id") if s == 200 else None
        s, r = multipart("/admin/images/upload", "a.webp", fixture("lossy.webp"), jwt)
        check("admin upload WebP lands as PNG + notice", s == 200 and r.get("mime_type") == "image/png" and r.get("notice"), f"{s} {str(r)[:200]}")
        s, r = multipart("/admin/images/upload", "x.png", os.urandom(2048), jwt)
        check("admin upload rejects garbage", s == 400, f"{s}")
        s, r = multipart("/admin/images/upload", "big.png", b"\x89PNG\r\n\x1a\n" + os.urandom(27 * 1024 * 1024), jwt)
        check("admin upload rejects over 25 MB", s in (400, 413), f"{s}")

        # ownership of uploads, both directions
        denied_http("admin get owner's upload", A_("GET", f"/admin/images/{UP_PNG}"))
        denied_http("admin delete owner's upload", A_("DELETE", f"/admin/images/{UP_PNG}?force=true"))
        if UP_ADMIN:
            for tool, args in [("keep_image", {"image_id": UP_ADMIN}), ("delete_image", {"image_id": UP_ADMIN, "force": True}),
                               ("analyze_image", {"image_id": UP_ADMIN, "prompt": "describe"}),
                               ("append_doc_image", {"doc_id": "zzNotARealDoc", "image_id": UP_ADMIN})]:
                check(f"MCP {tool}: other user's upload denied", "not found" in mcp(tool, args, A_KEY).get("error", "").lower())
            listed = [i["image_id"] for i in mcp("list_images", {"limit": 200}, A_KEY).get("images", [])]
            check("owner's list_images excludes other user's upload", UP_ADMIN not in listed and UP_PNG in listed)
    check("append_doc_image rejects a bad doc_id", "valid Google ID" in mcp("append_doc_image", {"doc_id": "../x?y", "image_id": UP_PNG}, A_KEY).get("error", ""))

    # ---------- level-2 share (W2): edit + delete sections, not the wiki ----------
    s, _ = http("PUT", f"/wikis/{W2}/sections/{WS2}", key=B_KEY, body={"title": "edited by L2"}); check("L2 edits shared wiki section", s == 200, f"{s}")
    s, _ = http("DELETE", f"/wikis/{W2}/sections/{WS2}", key=B_KEY); check("L2 deletes shared wiki section", s == 200, f"{s}")
    s, _ = http("DELETE", f"/wikis/{W2}", key=B_KEY); check("L2 cannot delete the shared wiki", s == 404, f"{s}")

    # ---------- owner's data untouched; owner still works ----------
    check("owner data intact", psql(f"""SELECT (SELECT title FROM projects WHERE project_id={P}) || '|' ||
        (SELECT title FROM project_sections WHERE section_id={S}) || '|' || (SELECT title FROM wiki_sections WHERE section_id={WS}) || '|' ||
        (SELECT title FROM hints WHERE hint_id={H}) || '|' || (SELECT count(*) FROM hints WHERE hint_category_id={HC}) || '|' ||
        (SELECT count(*) FROM images WHERE image_id={I} AND NOT keep) || '|' || (SELECT count(*) FROM wikis WHERE wiki_id IN ({W},{W2})) || '|' ||
        (SELECT string_agg(tag, ',') FROM wiki_section_tags WHERE section_id={WS}) || '|' ||
        (SELECT count(*) FROM shared_objects WHERE object_id={P} AND object_type_id=1)""") == "zz-P|zz-S|zz-WS|zz-H|2|1|2|zztag|0")
    s, _ = http("PUT", f"/projects/{P}", key=A_KEY, body={"title": "zz-P2"}); check("owner updates project", s == 200, f"{s}")
    s, _ = http("GET", f"/images/{I}", key=A_KEY); check("owner reads image", s == 200, f"{s}")
    s, _ = http("DELETE", f"/projects/{P}/sections/{S}", key=A_KEY); check("owner deletes section", s == 200, f"{s}")
finally:
    try:
        u = "(SELECT user_id FROM users WHERE username IN ('zzown','zzout'))"
        for fn in psql(f"SELECT filename FROM images WHERE user_id IN {u}").splitlines():
            p = os.path.join(IMAGES_DIR, os.path.basename(fn))
            if os.path.isfile(p): os.remove(p)
        psql(f"""DELETE FROM shared_objects WHERE shared_by_user_id IN {u} OR shared_to_user_id IN {u};
                 DELETE FROM project_sections WHERE project_id IN (SELECT project_id FROM projects WHERE user_id IN {u});
                 DELETE FROM projects WHERE user_id IN {u};
                 DELETE FROM wiki_sections WHERE wiki_id IN (SELECT wiki_id FROM wikis WHERE user_id IN {u});
                 DELETE FROM wikis WHERE user_id IN {u};
                 DELETE FROM hints WHERE user_id IN {u};
                 DELETE FROM images WHERE user_id IN {u};
                 DELETE FROM always_load WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u});
                 DELETE FROM memories WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u});
                 DELETE FROM handoffs WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u});
                 DELETE FROM agents WHERE user_id IN {u};
                 DELETE FROM users WHERE username IN ('zzown','zzout');""")
        left = psql("SELECT count(*) FROM users WHERE username IN ('zzown','zzout')")
        print(f"cleanup: temp users remaining = {left}")
    except Exception as e:
        print(f"CLEANUP FAILED: {e}")

fails = [r for r in results if not r[0]]
for ok, name, detail in results:
    if not ok: print(f"FAIL  {name}  {detail}")
print(f"{len(results) - len(fails)}/{len(results)} checks passed")
sys.exit(1 if fails else 0)
