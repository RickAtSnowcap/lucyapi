import os
import smtplib
import time
import threading
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.mime.base import MIMEBase
from email import encoders
from datetime import datetime, timezone
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

router = APIRouter()

SAVE_TOKEN = os.environ.get("LUCYAPI_SAVE_TOKEN", "")
SMTP_HOST = "smtp.forwardemail.net"
SMTP_PORT = 465
SMTP_USER = "rick@snowcapsystems.com"
SMTP_PASS = os.environ.get("LUCYAPI_SMTP_PASS", "")
SEND_TO = "rick@snowcapsystems.com"

# Simple rate limiting: track last request time
_last_request: float = 0.0
RATE_LIMIT_SECONDS = 30


class SaveRequest(BaseModel):
    subject: str
    content: str


def _send_email(subject: str, content: str, timestamp: str, filename: str):
    """Send email in background thread."""
    msg = MIMEMultipart()
    msg["From"] = SMTP_USER
    msg["To"] = SEND_TO
    msg["Subject"] = subject

    msg.attach(MIMEText(
        f"Mobile Lucy saved a conversation summary.\n\nSubject: {subject}\nTimestamp: {timestamp} UTC",
        "plain"
    ))

    attachment = MIMEBase("application", "octet-stream")
    attachment.set_payload(content.encode("utf-8"))
    encoders.encode_base64(attachment)
    attachment.add_header("Content-Disposition", f'attachment; filename="{filename}"')
    msg.attach(attachment)

    try:
        with smtplib.SMTP_SSL(SMTP_HOST, SMTP_PORT) as server:
            server.login(SMTP_USER, SMTP_PASS)
            server.sendmail(SMTP_USER, SEND_TO, msg.as_string())
    except Exception:
        pass  # Fire and forget — no one to report to


@router.get("/save/{token}")
async def save_and_email_get(token: str, subject: str = "Mobile Lucy Notes", content: str = ""):
    """GET variant for mobile Lucy — subject and content via query params."""
    global _last_request

    if not SAVE_TOKEN or token != SAVE_TOKEN:
        raise HTTPException(status_code=404, detail="Not found")

    if not content:
        raise HTTPException(status_code=400, detail="content parameter is required")

    now = time.time()
    if now - _last_request < RATE_LIMIT_SECONDS:
        raise HTTPException(status_code=429, detail="Rate limited. Try again shortly.")
    _last_request = now

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    filename = f"lucy-notes-{timestamp}.md"

    threading.Thread(
        target=_send_email,
        args=(subject, content, timestamp, filename),
        daemon=True
    ).start()

    return {"status": "accepted", "filename": filename, "to": SEND_TO}


@router.post("/save/{token}")
async def save_and_email(token: str, req: SaveRequest):
    """Accept markdown content and email it as an attachment. Rate-limited, token-guarded."""
    global _last_request

    if not SAVE_TOKEN or token != SAVE_TOKEN:
        raise HTTPException(status_code=404, detail="Not found")

    now = time.time()
    if now - _last_request < RATE_LIMIT_SECONDS:
        raise HTTPException(status_code=429, detail="Rate limited. Try again shortly.")
    _last_request = now

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    filename = f"lucy-notes-{timestamp}.md"

    threading.Thread(
        target=_send_email,
        args=(req.subject, req.content, timestamp, filename),
        daemon=True
    ).start()

    return {"status": "accepted", "filename": filename, "to": SEND_TO}
