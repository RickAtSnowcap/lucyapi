#!/usr/bin/env python3
"""End-to-end ownership test for LucyAPI (migration 007). Creates two throwaway users (zzown = owner,
zzout = outsider) with agents, keys and data, checks every access path (REST key, admin JWT, legacy MCP),
then deletes everything. Prints no secrets."""
import base64, hashlib, json, os, secrets, subprocess, sys, urllib.request, urllib.error

B = "http://127.0.0.1:8100"
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

def mcp(tool, args, key):
    body = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": tool, "arguments": dict(args, agent_key=key)}}
    s, t = http("POST", "/mcp/", body=body)
    try:
        return json.loads(json.loads(t)["result"]["content"][0]["text"])
    except Exception:
        return {"error": f"unparsed {s}"}

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
