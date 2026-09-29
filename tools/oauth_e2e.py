#!/usr/bin/env python3
"""End-to-end OAuth test client for LucyAPI's MCP connector (project #62).
Walks the flow Claude performs: 401 challenge -> discovery -> DCR -> authorize (sign in, choose
agent, approve) -> token -> MCP calls -> refresh rotation -> reuse detection. Prints PASS/FAIL lines.
Usage: oauth_e2e.py <base_url> <username> <password> <agent_name>
"""
import base64, hashlib, html, json, os, re, sys, urllib.parse, urllib.request, urllib.error

BASE, USER, PW, AGENT = sys.argv[1:5]
RESOURCE = "https://lucyapi.snowcapsystems.com/mcp/connector"
REDIRECT = "http://127.0.0.1:9999/callback"
fails = 0

def check(name, cond, detail=""):
    global fails
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else f"  -> {detail}"))
    if not cond: fails += 1

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k): return None
opener = urllib.request.build_opener(NoRedirect)

def req(method, path, data=None, headers=None, form=False):
    h = dict(headers or {})
    body = None
    if data is not None:
        if form:
            body = urllib.parse.urlencode(data).encode(); h["Content-Type"] = "application/x-www-form-urlencoded"
        else:
            body = json.dumps(data).encode(); h["Content-Type"] = "application/json"
    r = urllib.request.Request(BASE + path, data=body, method=method, headers=h)
    try:
        resp = opener.open(r, timeout=15)
        return resp.status, {k.title(): v for k, v in resp.headers.items()}, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, {k.title(): v for k, v in e.headers.items()}, e.read().decode()

def rpc(method, params=None, token=None, rid=1):
    h = {"Accept": "application/json, text/event-stream"}
    if token: h["Authorization"] = "Bearer " + token
    return req("POST", "/mcp/connector", {"jsonrpc": "2.0", "id": rid, "method": method, "params": params or {}}, h)

def pkce():
    v = base64.urlsafe_b64encode(os.urandom(40)).rstrip(b"=").decode()
    c = base64.urlsafe_b64encode(hashlib.sha256(v.encode()).digest()).rstrip(b"=").decode()
    return v, c

def hidden(htmltext, name):
    m = re.search(r'name="%s" value="([^"]*)"' % name, htmltext)
    return html.unescape(m.group(1)) if m else None

# 1. unauthenticated initialize -> 401 + discovery pointer
s, h, b = rpc("initialize")
www = h.get("Www-Authenticate", "")
check("initialize without token -> 401", s == 401, s)
check("WWW-Authenticate names resource_metadata", "resource_metadata=" in www, www)
s, h, b = rpc("initialize", token="bogus-token")
check("bogus token -> 401 invalid_token", s == 401 and "invalid_token" in h.get("Www-Authenticate", ""), (s, h.get("Www-Authenticate")))

# 2. discovery
s, _, b = req("GET", "/.well-known/oauth-protected-resource/mcp/connector")
prm = json.loads(b) if s == 200 else {}
check("PRM served", s == 200 and prm.get("resource") == RESOURCE, b[:200])
s, _, b = req("GET", "/.well-known/oauth-authorization-server")
asm = json.loads(b) if s == 200 else {}
check("AS metadata: S256 + none + CIMD flag",
      asm.get("code_challenge_methods_supported") == ["S256"] and "none" in asm.get("token_endpoint_auth_methods_supported", [])
      and asm.get("client_id_metadata_document_supported") is True, b[:300])

# 3. DCR
s, _, b = req("POST", "/oauth/register", {"redirect_uris": ["https://evil.example/cb"], "client_name": "evil"})
check("DCR rejects non-allowlisted redirect", s == 400 and "invalid_redirect_uri" in b, (s, b[:120]))
s, _, b = req("POST", "/oauth/register", {"redirect_uris": [REDIRECT], "client_name": "OAuth E2E Test", "token_endpoint_auth_method": "none"})
client_id = json.loads(b).get("client_id") if s == 201 else None
check("DCR registers loopback client", s == 201 and client_id, (s, b[:200]))

# 4. authorize: SSRF guard on CIMD + bad resource
s, _, b = req("GET", "/oauth/authorize?" + urllib.parse.urlencode({"client_id": "https://127.0.0.1/meta.json", "response_type": "code", "redirect_uri": REDIRECT, "code_challenge": "x", "code_challenge_method": "S256"}))
check("CIMD pointing at loopback refused (SSRF guard)", s == 400 and "Unknown or unreachable client" in b, (s, b[:100]))
v, c = pkce()
q = {"client_id": client_id, "response_type": "code", "redirect_uri": REDIRECT, "code_challenge": c,
     "code_challenge_method": "S256", "state": "st-123", "resource": "https://other.example/mcp", "scope": "lucyapi"}
s, h, b = req("GET", "/oauth/authorize?" + urllib.parse.urlencode(q))
check("wrong resource -> redirect error=invalid_target", s == 302 and "error=invalid_target" in h.get("Location", ""), (s, h.get("Location")))

q["resource"] = RESOURCE
s, h, b = req("GET", "/oauth/authorize?" + urllib.parse.urlencode(q))
check("authorize page renders (200, unframable)", s == 200 and h.get("X-Frame-Options") == "DENY", (s, h.get("X-Frame-Options")))
st = hidden(b, "st")

# 5. sign in
s, _, b = req("POST", "/oauth/authorize", {"step": "signin", "st": st, "username": USER, "password": "wrong-password"}, form=True)
check("wrong password rejected", s == 200 and "Incorrect username or password" in b, s)
s, _, b = req("POST", "/oauth/authorize", {"step": "signin", "st": st + "x", "username": USER, "password": PW}, form=True)
check("tampered state rejected", "expired" in b, b[:200])
s, _, b = req("POST", "/oauth/authorize", {"step": "signin", "st": st, "username": USER, "password": PW}, form=True)
st2 = hidden(b, "st")
agents = dict((html.unescape(n).strip(), i) for i, n in re.findall(r'name="agent_id" value="(\d+)"[^>]*> ([^<]+)</label>', b))
check("sign-in -> agent chooser lists " + AGENT, AGENT in agents, list(agents))

# 6. consent
s, h, _ = req("POST", "/oauth/authorize", {"step": "consent", "st": st2, "agent_id": agents.get(AGENT, "0"), "decision": "approve"}, form=True)
loc = h.get("Location", "")
params = urllib.parse.parse_qs(urllib.parse.urlparse(loc).query)
code = params.get("code", [None])[0]
check("approve -> redirect with code, state, iss", s == 302 and code and params.get("state") == ["st-123"] and params.get("iss"), loc[:150])

# 7. token exchange
tok = {"grant_type": "authorization_code", "code": code, "redirect_uri": REDIRECT, "client_id": client_id, "code_verifier": "wrong" * 10, "resource": RESOURCE}
s, _, b = req("POST", "/oauth/token", tok, form=True)
check("wrong PKCE verifier -> invalid_grant (code now burned)", s == 400 and "invalid_grant" in b, (s, b[:120]))
# fresh code for the real exchange
s, h, b = req("GET", "/oauth/authorize?" + urllib.parse.urlencode(q)); st = hidden(b, "st")
s, _, b = req("POST", "/oauth/authorize", {"step": "signin", "st": st, "username": USER, "password": PW}, form=True); st2 = hidden(b, "st")
s, h, _ = req("POST", "/oauth/authorize", {"step": "consent", "st": st2, "agent_id": agents.get(AGENT, "0"), "decision": "approve"}, form=True)
code = urllib.parse.parse_qs(urllib.parse.urlparse(h.get("Location", "")).query).get("code", [None])[0]
tok.update({"code": code, "code_verifier": v})
s, h, b = req("POST", "/oauth/token", tok, form=True)
t = json.loads(b) if s == 200 else {}
check("code + PKCE -> tokens (no-store)", s == 200 and t.get("access_token") and t.get("refresh_token") and t.get("expires_in") == 3600
      and "no-store" in h.get("Cache-Control", ""), (s, b[:150]))
s, _, b = req("POST", "/oauth/token", tok, form=True)
check("code replay -> invalid_grant", s == 400 and "invalid_grant" in b, (s, b[:100]))
access, refresh = t.get("access_token"), t.get("refresh_token")

# 8. MCP with the token
s, _, b = rpc("initialize", token=access)
check("initialize with token -> 200", s == 200 and "protocolVersion" in b, (s, b[:100]))
s, _, b = rpc("tools/list", token=access, rid=2)
check("tools/list has NO agent_key anywhere", s == 200 and "agent_key" not in b and '"get_context"' in b, (s, "agent_key" in b))
s, _, b = rpc("tools/call", {"name": "get_context", "arguments": {}}, token=access, rid=3)
check("get_context works with no key", s == 200 and "projects_manifest" in b, b[:200])
s, _, b = rpc("tools/call", {"name": "get_memories", "arguments": {"agent_name": "no-such-agent"}}, token=access, rid=4)
check("acting as another agent (no-such-agent) refused", "Agent not found" in b or "error" in b.lower(), b[:200])
s, _, b = rpc("tools/call", {"name": "create_handoff", "arguments": {"agent_name": AGENT, "title": "e2e test", "prompt": "delete me"}}, token=access, rid=5)
hid = (re.search(r'handoff_id\D{1,12}?(\d+)', b) or [None, None])[1]   # tool text is JSON-in-JSON (" escapes)
check("create_handoff (recipient path) works", hid is not None, b[:200])
if hid:
    rpc("tools/call", {"name": "delete_handoff", "arguments": {"handoff_id": int(hid)}}, token=access, rid=6)

# 9. refresh rotation + reuse detection
r = {"grant_type": "refresh_token", "refresh_token": refresh, "client_id": client_id}
s, _, b = req("POST", "/oauth/token", r, form=True)
t2 = json.loads(b) if s == 200 else {}
check("refresh -> new pair", s == 200 and t2.get("refresh_token") and t2["refresh_token"] != refresh, (s, b[:120]))
s, _, _ = rpc("initialize", token=t2.get("access_token"))
check("new access token works", s == 200, s)
s, _, b = req("POST", "/oauth/token", r, form=True)
check("reusing rotated refresh -> invalid_grant", s == 400 and "reuse" in b, (s, b[:150]))
s, _, _ = rpc("initialize", token=t2.get("access_token"))
check("after reuse, whole family revoked (new access -> 401)", s == 401, s)

print(f"\n{'ALL PASSED' if fails == 0 else str(fails) + ' FAILED'}")
print("CLIENT_ID=" + str(client_id))
sys.exit(1 if fails else 0)
