#!/usr/bin/env python3
"""End-to-end ownership test for LucyAPI (migration 007). Creates two throwaway users (zzown = owner with two agents,
zzout = outsider) with data, checks every access path (OAuth MCP connector, admin JWT), then deletes everything.
Agents have no keys (migration 014): the script mints OAuth access tokens for its agents straight into the DB under a
temporary client, the same way /oauth/token stores them (SHA-256 hashes only). Prints no secrets.
Also covers image uploads (migration 010, project #3 §450): upload_image + /admin/images/upload, WebP → PNG,
GIF stored byte-for-byte, and rejection of bad magic bytes, truncated, oversize and over-pixel-cap images;
handoffs between two agents of the same user (creator edit/delete while pending, migration 015); and that key auth (legacy /mcp/, REST routes) is gone.
Usage: ownership_e2e.py [base_url]   (default http://127.0.0.1:8100; needs passwordless sudo for psql as postgres)"""
import base64, hashlib, json, os, re, secrets, struct, subprocess, sys, urllib.request, urllib.error, uuid, zlib

B = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://127.0.0.1:8100"
RESOURCE = "https://lucyapi.snowcapsystems.com/mcp/connector"   # OAuthSettings.Resource: tokens are audience-bound to it
CLIENT_ID = "zz-e2e-" + uuid.uuid4().hex
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

def sha256_hex(value):
    return hashlib.sha256(value.encode()).hexdigest()   # = OAuthSettings.Hash

def mint_token(user_id, agent_id):
    """A 1-hour access token for one agent, stored like /oauth/token does (hash only, bound to the connector)."""
    access, refresh = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
    psql(f"""SELECT lucyapi.fn_oauth_tokens_issue(gen_random_uuid(), '{sha256_hex(access)}', '{sha256_hex(refresh)}',
             '{CLIENT_ID}', {user_id}, {agent_id}, '{RESOURCE}', 'lucyapi', NOW() + INTERVAL '1 hour', NOW() + INTERVAL '1 hour')""")
    return access

def mcp(tool, args, token):
    body = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": tool, "arguments": args}}
    s, t = http("POST", "/mcp/connector", jwt=token, body=body)   # bearer header; the connector answers plain JSON
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

B_PW = secrets.token_urlsafe(18)
ids = {}
try:
    # ---------- setup ----------
    psql(f"""INSERT INTO users (name, username, password_hash) VALUES ('zz owner','zzown',NULL), ('zz outsider','zzout','{identity_v3_hash(B_PW)}');""")
    ua = int(psql("SELECT user_id FROM users WHERE username='zzown'")); ub = int(psql("SELECT user_id FROM users WHERE username='zzout'"))
    psql(f"INSERT INTO agents (user_id, name) VALUES ({ua},'zzown-agent'), ({ua},'zzown2-agent'), ({ub},'zzout-agent')")
    agent_a = int(psql("SELECT agent_id FROM agents WHERE name='zzown-agent'"))
    agent_a2 = int(psql("SELECT agent_id FROM agents WHERE name='zzown2-agent'"))
    agent_b = int(psql("SELECT agent_id FROM agents WHERE name='zzout-agent'"))
    psql(f"INSERT INTO oauth_clients (client_id, registration_type, client_name, redirect_uris) "
         f"VALUES ('{CLIENT_ID}', 'dcr', 'zz ownership e2e', ARRAY['http://127.0.0.1:9/callback'])")
    A_TOK, A2_TOK, B_TOK = mint_token(ua, agent_a), mint_token(ua, agent_a2), mint_token(ub, agent_b)
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

    # ---------- key auth is gone: only the connector (bearer) reaches agent data ----------
    s, _ = http("POST", "/mcp/", body={"jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}})
    check("legacy /mcp/ is gone", s in (404, 405), f"got {s}")
    s, _ = http("GET", "/agents/zzown-agent/memories", key="zz-not-a-key")
    check("former REST route (X-Api-Key) is gone", s == 404, f"got {s}")
    s, _ = http("GET", f"/projects/{P}", key="zz-not-a-key")
    check("former REST project route is gone", s == 404, f"got {s}")
    s, _ = http("POST", "/mcp/connector", jwt="zz-bogus-token", body={"jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}})
    check("connector refuses a bogus bearer token", s == 401, f"got {s}")
    s, t = http("POST", "/mcp/connector", jwt=A_TOK, body={"jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}})
    check("connector tools/list with a minted token, no agent_key", s == 200 and '"get_context"' in t and "agent_key" not in t, f"{s}")

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

    # ---------- MCP connector — owner control + outsider (these replace the old REST outsider checks) ----------
    calls = [("get_project", {"project_id": P}), ("get_project_compact", {"project_id": P}),
             ("get_section", {"project_id": P, "section_id": S}), ("get_wiki", {"wiki_id": W}),
             ("get_wiki_section", {"wiki_id": W, "section_id": WS}), ("get_hint", {"hint_id": H}), ("get_image", {"image_id": I})]
    for tool, args in calls:
        ok_owner = "error" not in mcp(tool, args, A_TOK)
        check(f"MCP {tool}: owner allowed (control)", ok_owner)
        check(f"MCP {tool}: outsider denied", "error" in mcp(tool, args, B_TOK))
    check("MCP get_wiki_tags: outsider sees no tags", "zztag" not in json.dumps(mcp("get_wiki_tags", {"wiki_id": W}, B_TOK)))
    check("MCP get_wiki_tags: owner sees tags (control)", "zztag" in json.dumps(mcp("get_wiki_tags", {"wiki_id": W}, A_TOK)))
    for tool, args in [("update_hint", {"hint_id": H, "title": "pwn"}), ("delete_hint", {"hint_id": H}),
                       ("create_hint", {"parent_id": HC, "title": "pwn", "description": "x"}),
                       ("create_hint_category", {"parent_id": HC, "title": "pwn", "description": "x"}),
                       ("delete_hint_category", {"hint_id": HC}),
                       ("create_section", {"project_id": P, "parent_id": 0, "title": "pwn", "description": "x"}),
                       ("update_section", {"project_id": P, "section_id": S, "title": "pwn"}),
                       ("delete_section", {"project_id": P, "section_id": S}),
                       ("update_project", {"project_id": P, "title": "pwn"}), ("delete_project", {"project_id": P}),
                       ("update_wiki", {"wiki_id": W, "title": "pwn"}),
                       ("update_wiki_section", {"wiki_id": W, "section_id": WS, "title": "pwn", "tags": ["pwn"]}),
                       ("delete_wiki_section", {"wiki_id": W, "section_id": WS}),
                       ("delete_wiki", {"wiki_id": W}), ("keep_image", {"image_id": I}), ("delete_image", {"image_id": I, "force": True}),
                       ("analyze_image", {"image_id": I, "prompt": "describe"}),
                       ("share_object", {"shared_to_user_id": ua, "object_type_id": 1, "object_id": P, "permission_level": 3})]:
        check(f"MCP {tool}: outsider denied", "error" in mcp(tool, args, B_TOK))

    # ---------- agent_name may only name the token's own agent ----------
    check("other user's agent memories refused", "error" in mcp("get_memories", {"agent_name": "zzown-agent"}, B_TOK))
    check("same user's OTHER agent refused (get_memories)", "error" in mcp("get_memories", {"agent_name": "zzown2-agent"}, A_TOK))
    check("same user's OTHER agent refused (create_memory)",
          "error" in mcp("create_memory", {"agent_name": "zzown2-agent", "title": "pwn", "description": "x"}, A_TOK)
          and psql(f"SELECT count(*) FROM memories WHERE agent_id={agent_a2}") == "0")
    r = mcp("get_memories", {"agent_name": "ZZOWN-Agent"}, A_TOK)
    check("own agent name accepted, case-insensitive", "error" not in r and r.get("agent") == "zzown-agent", str(r)[:200])

    # ---------- check_access (owner = 3) ----------
    r = mcp("check_access", {"object_type_id": 1, "object_id": P}, A_TOK)
    check("check_access: owner = 3", r.get("has_access") is True and r.get("permission_level") == 3, str(r))
    r = mcp("check_access", {"object_type_id": 1, "object_id": P}, B_TOK)
    check("check_access: outsider = 0", r.get("has_access") is False and r.get("permission_level") == 0, str(r))
    r = mcp("check_access", {"object_type_id": 3, "object_id": W2}, B_TOK)
    check("check_access: level-2 share = 2", r.get("has_access") is True and r.get("permission_level") == 2, str(r))

    # ---------- signed document links (get_project / get_wiki document_url → public /doc/...) ----------
    for tool, args, route, oid, key in [("get_project", {"project_id": P}, "projects", P, "project"),
                                        ("get_wiki", {"wiki_id": W}, "wikis", W, "wiki")]:
        url = mcp(tool, args, A_TOK).get(key, {}).get("document_url") or ""
        m = re.fullmatch(r"https://lucyapi\.snowcapsystems\.com(/doc/%s/%d)\?u=(\d+)&exp=(\d+)&sig=([A-Za-z0-9_-]+)" % (route, oid), url)
        check(f"{tool} document_url shape", m is not None and int(m.group(2)) == ua, url[:80])
        if m:
            path = url[len("https://lucyapi.snowcapsystems.com"):]
            s, t = http("GET", path)
            check(f"{tool} document_url opens with no credential", s == 200 and ("zz-P" in t or "zz-W" in t), f"{s}")
            s, t = http("GET", path[:-2] + ("AA" if not path.endswith("AA") else "BB"))
            check(f"{tool} document_url with a bad signature -> 403 page", s == 403 and "24 hours" in t, f"{s}")

    # ---------- image uploads (migration 010): MCP upload_image as the owner ----------
    up = lambda data, **kw: mcp("upload_image", dict({"data": base64.b64encode(data).decode(), "title": "zz"}, **kw), A_TOK)
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

    r = mcp("upload_image", {"data": "data:image/png;base64," + base64.b64encode(fixture("sample.png")).decode(), "title": "zz"}, A_TOK)
    check("upload accepts a data: URL", r.get("mime_type") == "image/png", str(r)[:200])
    uploaded_ok = 7

    png = fixture("sample.png")
    # "over 25 MB": the connector lets an authenticated request carry 36 MB (25 MB as base64 + envelope), so the
    # ~34.7 MB request reaches the dispatcher, which refuses it before decoding: "Image is over the 25 MB upload limit".
    for name, data, expect in [("garbage bytes", os.urandom(4096), ("Not a supported image",)),
                               ("text file", b"hello, I am not a PNG\n" * 50, ("Not a supported image",)),
                               ("truncated PNG", png[:len(png) // 2], ("truncated",)),
                               ("over pixel cap (7000x6000)", make_png(7000, 6000), ("MP",)),
                               ("over 25 MB", b"\x89PNG\r\n\x1a\n" + os.urandom(26 * 1024 * 1024), ("over the 25 MB upload limit",))]:
        r = up(data)
        check(f"upload rejects {name}", any(e in r.get("error", "") for e in expect), str(r)[:200])
    r = mcp("upload_image", {"data": "not base64!!", "title": "zz"}, A_TOK)
    check("upload rejects invalid base64", "base64" in r.get("error", ""), str(r)[:200])
    check("rejected uploads leave no rows", int(psql(f"SELECT count(*) FROM images WHERE user_id={ua}")) == rows_before + uploaded_ok)

    # ---------- phase 1 image tools: get_image, keep_image keep=false, list_images offset ----------
    r = mcp("get_image", {"image_id": UP_PNG}, A_TOK)
    check("get_image returns the owner's upload", r.get("image_id") == UP_PNG and r.get("mime_type") == "image/png", str(r)[:200])
    r = mcp("keep_image", {"image_id": UP_PNG, "keep": False}, A_TOK)
    check("keep_image keep=false releases it", r.get("keep") is False
          and psql(f"SELECT keep FROM images WHERE image_id={UP_PNG}") == "f", str(r)[:200])
    r = mcp("keep_image", {"image_id": UP_PNG}, A_TOK)
    check("keep_image default keep=true", r.get("keep") is True, str(r)[:200])
    p0 = [i["image_id"] for i in mcp("list_images", {"limit": 1, "offset": 0}, A_TOK).get("images", [])]
    p1 = [i["image_id"] for i in mcp("list_images", {"limit": 1, "offset": 1}, A_TOK).get("images", [])]
    check("list_images offset pages", len(p0) == 1 and len(p1) == 1 and p0 != p1, f"{p0} {p1}")
    check("list_images rejects a negative offset", "offset" in mcp("list_images", {"offset": -1}, A_TOK).get("error", ""))

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
                check(f"MCP {tool}: other user's upload denied", "not found" in mcp(tool, args, A_TOK).get("error", "").lower())
            check("MCP get_image: other user's upload denied", "not found" in mcp("get_image", {"image_id": UP_ADMIN}, A_TOK).get("error", "").lower())
            listed = [i["image_id"] for i in mcp("list_images", {"limit": 200}, A_TOK).get("images", [])]
            check("owner's list_images excludes other user's upload", UP_ADMIN not in listed and UP_PNG in listed)
    check("append_doc_image rejects a bad doc_id", "valid Google ID" in mcp("append_doc_image", {"doc_id": "../x?y", "image_id": UP_PNG}, A_TOK).get("error", ""))

    # ---------- sessions (migration 011): get_context opens one; get_project records the focus ----------
    c1 = mcp("get_context", {"agent_name": "zzown-agent"}, A_TOK).get("session", {})
    c2 = mcp("get_context", {"agent_name": "zzown-agent"}, A_TOK).get("session", {})
    check("get_context opens a session each call", bool(c1.get("session_id")) and c2.get("session_id", 0) > c1["session_id"], f"{c1} {c2}")
    check("get_context reports the previous session", c1.get("previous_started_at") is None and c2.get("previous_started_at") == c1.get("started_at"), f"{c1} {c2}")
    mcp("get_project", {"project_id": P}, A_TOK); mcp("get_project_compact", {"project_id": P}, A_TOK)
    check("get_project records the project once in the current session",
          psql(f"SELECT count(*) FROM session_projects WHERE session_id={c2.get('session_id', 0)} AND project_id={P}") == "1")
    mcp("get_project", {"project_id": P}, B_TOK)   # outsider: denied, so nothing recorded anywhere
    check("denied get_project records nothing", psql(f"SELECT count(*) FROM session_projects WHERE project_id={P}") == "1")
    r = mcp("set_session_description", {"description": "  zz first focus  "}, A_TOK)
    check("set_session_description sets the current session", r.get("session_id") == c2.get("session_id") and r.get("description") == "zz first focus", str(r))
    r = mcp("set_session_description", {"description": "zz wrap-up"}, A_TOK)
    check("set_session_description overwrites", psql(f"SELECT description FROM sessions WHERE session_id={c2.get('session_id', 0)}") == "zz wrap-up", str(r))
    check("set_session_description rejects empty", "error" in mcp("set_session_description", {"description": "   "}, A_TOK))
    check("set_session_description rejects over 500 chars", "500" in mcp("set_session_description", {"description": "x" * 501}, A_TOK).get("error", ""))
    check("set_session_description needs a session first", "get_context" in mcp("set_session_description", {"description": "zz"}, B_TOK).get("error", ""))
    for tool in ("create_session", "get_last_session"):
        check(f"{tool} is retired", "error" in mcp(tool, {}, A_TOK))
    if jwt:
        denied_http("admin list another user's agent sessions", A_("GET", "/admin/agents/zzown-agent/sessions"))

    # ---------- handoffs: two agents of the SAME user (migration 015: creators may edit/delete while pending) ----------
    def ho_ids(resp): return [h["handoff_id"] for h in resp.get("handoffs", [])]
    r = mcp("create_handoff", {"agent_name": "zzown2-agent", "title": "zz handoff", "prompt": "zz"}, A_TOK)
    HO = r.get("handoff_id")
    check("create_handoff to a same-user agent allowed", isinstance(HO, int)
          and psql(f"SELECT agent_id FROM handoffs WHERE handoff_id={HO or 0}") == str(agent_a2), str(r)[:200])
    check("create_handoff records the creator",
          psql(f"SELECT created_by_agent_id FROM handoffs WHERE handoff_id={HO or 0}") == str(agent_a))
    check("create_handoff to another user's agent refused",
          "error" in mcp("create_handoff", {"agent_name": "zzown-agent", "title": "pwn", "prompt": "x"}, B_TOK))
    if HO:
        for tool in ("get_handoff", "pickup_handoff", "delete_handoff", "update_handoff"):
            check(f"{tool} naming the other agent refused",
                  "error" in mcp(tool, {"agent_name": "zzown2-agent", "handoff_id": HO, "title": "x"}, A_TOK))
        check("list_handoffs naming the other agent refused", "error" in mcp("list_handoffs", {"agent_name": "zzown2-agent"}, A_TOK))
        check("list_sent_handoffs naming the other agent refused", "error" in mcp("list_sent_handoffs", {"agent_name": "zzown2-agent"}, A_TOK))
        # the creator (A)
        r = mcp("get_handoff", {"handoff_id": HO}, A_TOK)
        check("creator gets its handoff, with to/from", r.get("prompt") == "zz" and r.get("to_agent") == "zzown2-agent"
              and r.get("from_agent") == "zzown-agent", str(r)[:200])
        check("creator can't pick it up", "error" in mcp("pickup_handoff", {"handoff_id": HO}, A_TOK))
        check("sender's list_handoffs excludes it", HO not in ho_ids(mcp("list_handoffs", {}, A_TOK)))
        r = mcp("list_sent_handoffs", {"pending_only": True}, A_TOK)
        check("creator's list_sent_handoffs has it", any(h["handoff_id"] == HO and h["to_agent"] == "zzown2-agent"
              and h["picked_up_at"] is None for h in r.get("handoffs", [])), str(r)[:200])
        check("update_handoff with nothing to change refused", "error" in mcp("update_handoff", {"handoff_id": HO}, A_TOK))
        check("update_handoff to an empty title refused", "error" in mcp("update_handoff", {"handoff_id": HO, "title": " "}, A_TOK))
        r = mcp("update_handoff", {"handoff_id": HO, "prompt": "zz edited"}, A_TOK)
        check("creator edits it while pending", r.get("status") == "updated", str(r)[:200])
        check("the edit kept the title and changed the prompt",
              psql(f"SELECT title || '|' || prompt FROM handoffs WHERE handoff_id={HO}") == "zz handoff|zz edited")
        # the recipient (A2) and an outsider (B)
        r = mcp("update_handoff", {"handoff_id": HO, "title": "x"}, A2_TOK)
        check("recipient can't edit it (names the creator)", "zzown-agent" in r.get("error", ""), str(r)[:200])
        for tool in ("get_handoff", "update_handoff", "delete_handoff"):
            r = mcp(tool, {"handoff_id": HO, "title": "x"}, B_TOK)
            check(f"other user's {tool} not found", "not found" in r.get("error", ""), str(r)[:200])
        check("handoff still pending and intact after the refusals",
              psql(f"SELECT title FROM handoffs WHERE handoff_id={HO} AND picked_up_at IS NULL") == "zz handoff")
        check("recipient lists it, with the sender", any(h["handoff_id"] == HO and h.get("from_agent") == "zzown-agent"
              for h in mcp("list_handoffs", {}, A2_TOK).get("handoffs", [])))
        check("recipient gets the edited version", mcp("get_handoff", {"handoff_id": HO}, A2_TOK).get("prompt") == "zz edited")
        r = mcp("pickup_handoff", {"agent_name": "zzown2-agent", "handoff_id": HO}, A2_TOK)
        check("recipient picks it up", r.get("handoff_id") == HO and r.get("picked_up_at"), str(r)[:200])
        # after pickup
        r = mcp("update_handoff", {"handoff_id": HO, "title": "x"}, A_TOK)
        check("creator can't edit after pickup", "picked up" in r.get("error", ""), str(r)[:200])
        r = mcp("delete_handoff", {"handoff_id": HO}, A_TOK)
        check("creator can't delete after pickup", "picked up" in r.get("error", ""), str(r)[:200])
        check("list_sent_handoffs pending_only drops it", HO not in ho_ids(mcp("list_sent_handoffs", {"pending_only": True}, A_TOK)))
        check("list_sent_handoffs (all) still shows it", HO in ho_ids(mcp("list_sent_handoffs", {}, A_TOK)))
        r = mcp("delete_handoff", {"handoff_id": HO}, A2_TOK)
        check("recipient deletes it after pickup", r.get("status") == "deleted" and r.get("deleted_count") == 1, str(r)[:200])
    # the creator deletes a pending one
    HO2 = mcp("create_handoff", {"agent_name": "zzown2-agent", "title": "zz 2", "prompt": "zz"}, A_TOK).get("handoff_id")
    r = mcp("delete_handoff", {"handoff_id": HO2 or 0}, A_TOK)
    check("creator deletes its pending handoff", r.get("status") == "deleted"
          and psql(f"SELECT count(*) FROM handoffs WHERE handoff_id={HO2 or 0}") == "0", str(r)[:200])
    # a handoff with no recorded creator (pre-015, or admin-created)
    HO3 = int(psql(f"INSERT INTO handoffs (agent_id, title, prompt) VALUES ({agent_a2}, 'zz legacy', 'zz') RETURNING handoff_id"))
    r = mcp("update_handoff", {"handoff_id": HO3, "title": "x"}, A2_TOK)
    check("no-creator handoff: recipient told nobody can edit it", "no recorded creator" in r.get("error", ""), str(r)[:200])
    check("no-creator handoff: other agent can't see it", "not found" in mcp("get_handoff", {"handoff_id": HO3}, A_TOK).get("error", ""))
    check("no-creator handoff: recipient deletes it", mcp("delete_handoff", {"handoff_id": HO3}, A2_TOK).get("status") == "deleted")
    # a self-handoff: creator and recipient at once
    HO4 = mcp("create_handoff", {"title": "zz self", "prompt": "zz"}, A_TOK).get("handoff_id")
    check("self-handoff editable while pending", mcp("update_handoff", {"handoff_id": HO4 or 0, "title": "zz self 2"}, A_TOK).get("status") == "updated")
    mcp("pickup_handoff", {"handoff_id": HO4 or 0}, A_TOK)
    check("self-handoff deletable after pickup (as recipient)", mcp("delete_handoff", {"handoff_id": HO4 or 0}, A_TOK).get("status") == "deleted")

    # ---------- level-2 share (W2): edit + delete sections, not the wiki ----------
    r = mcp("update_wiki_section", {"wiki_id": W2, "section_id": WS2, "title": "edited by L2"}, B_TOK)
    check("L2 edits shared wiki section", "error" not in r, str(r)[:200])
    r = mcp("delete_wiki_section", {"wiki_id": W2, "section_id": WS2}, B_TOK)
    check("L2 deletes shared wiki section", r.get("deleted_count", 0) > 0, str(r)[:200])
    check("L2 cannot delete the shared wiki", "error" in mcp("delete_wiki", {"wiki_id": W2}, B_TOK))

    # ---------- owner's data untouched; owner still works ----------
    check("owner data intact", psql(f"""SELECT (SELECT title FROM projects WHERE project_id={P}) || '|' ||
        (SELECT title FROM project_sections WHERE section_id={S}) || '|' || (SELECT title FROM wiki_sections WHERE section_id={WS}) || '|' ||
        (SELECT title FROM hints WHERE hint_id={H}) || '|' || (SELECT count(*) FROM hints WHERE hint_category_id={HC}) || '|' ||
        (SELECT count(*) FROM images WHERE image_id={I} AND NOT keep) || '|' || (SELECT count(*) FROM wikis WHERE wiki_id IN ({W},{W2})) || '|' ||
        (SELECT string_agg(tag, ',') FROM wiki_section_tags WHERE section_id={WS}) || '|' ||
        (SELECT count(*) FROM shared_objects WHERE object_id={P} AND object_type_id=1)""") == "zz-P|zz-S|zz-WS|zz-H|2|1|2|zztag|0")
    r = mcp("update_project", {"project_id": P, "title": "zz-P2"}, A_TOK); check("owner updates project", r.get("title") == "zz-P2", str(r)[:200])
    r = mcp("get_image", {"image_id": I}, A_TOK); check("owner reads image", r.get("image_id") == I, str(r)[:200])
    r = mcp("delete_section", {"project_id": P, "section_id": S}, A_TOK); check("owner deletes section", r.get("sections_deleted", 0) > 0, str(r)[:200])
finally:
    try:
        u = "(SELECT user_id FROM users WHERE username IN ('zzown','zzout'))"
        for fn in psql(f"SELECT filename FROM images WHERE user_id IN {u}").splitlines():
            p = os.path.join(IMAGES_DIR, os.path.basename(fn))
            if os.path.isfile(p): os.remove(p)
        psql(f"""DELETE FROM oauth_tokens WHERE client_id = '{CLIENT_ID}' OR user_id IN {u};
                 DELETE FROM oauth_clients WHERE client_id = '{CLIENT_ID}';
                 DELETE FROM shared_objects WHERE shared_by_user_id IN {u} OR shared_to_user_id IN {u};
                 DELETE FROM project_sections WHERE project_id IN (SELECT project_id FROM projects WHERE user_id IN {u});
                 DELETE FROM projects WHERE user_id IN {u};
                 DELETE FROM wiki_sections WHERE wiki_id IN (SELECT wiki_id FROM wikis WHERE user_id IN {u});
                 DELETE FROM wikis WHERE user_id IN {u};
                 DELETE FROM hints WHERE user_id IN {u};
                 DELETE FROM images WHERE user_id IN {u};
                 DELETE FROM always_load WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u});
                 DELETE FROM memories WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u});
                 DELETE FROM handoffs WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u})
                    OR created_by_agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u});
                 DELETE FROM sessions WHERE agent_id IN (SELECT agent_id FROM agents WHERE user_id IN {u});
                 DELETE FROM agents WHERE user_id IN {u};
                 DELETE FROM users WHERE username IN ('zzown','zzout');""")
        left = psql("SELECT count(*) FROM users WHERE username IN ('zzown','zzout')")
        left_client = psql(f"SELECT count(*) FROM oauth_clients WHERE client_id = '{CLIENT_ID}'")
        print(f"cleanup: temp users remaining = {left}, temp OAuth clients remaining = {left_client}")
    except Exception as e:
        print(f"CLEANUP FAILED: {e}")

fails = [r for r in results if not r[0]]
for ok, name, detail in results:
    if not ok: print(f"FAIL  {name}  {detail}")
print(f"{len(results) - len(fails)}/{len(results)} checks passed")
sys.exit(1 if fails else 0)
