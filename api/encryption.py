"""
AES-256-GCM encryption for LucyAPI secrets.

Key file: /opt/lucyapi/keys/secrets.key (32 bytes, permissions 600, owned by rfry)
Nonce is prepended to ciphertext on encrypt, split off on decrypt.
"""

import os
import logging

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

logger = logging.getLogger(__name__)

KEY_FILE = os.environ.get("LUCYAPI_SECRETS_KEY", "/opt/lucyapi/keys/secrets.key")

_aesgcm: AESGCM | None = None


def _load_key() -> AESGCM:
    """Load the AES-256 key from disk. Called once at first use."""
    global _aesgcm
    if _aesgcm is not None:
        return _aesgcm

    if not os.path.exists(KEY_FILE):
        raise RuntimeError(f"Secrets key file not found: {KEY_FILE}")

    with open(KEY_FILE, "rb") as f:
        key = f.read()

    if len(key) != 32:
        raise RuntimeError(f"Secrets key must be 32 bytes, got {len(key)}")

    _aesgcm = AESGCM(key)
    return _aesgcm


def encrypt(plaintext: str) -> bytes:
    """Encrypt a string. Returns nonce (12 bytes) + ciphertext."""
    aesgcm = _load_key()
    nonce = os.urandom(12)
    ciphertext = aesgcm.encrypt(nonce, plaintext.encode("utf-8"), None)
    return nonce + ciphertext


def decrypt(data: bytes) -> str:
    """Decrypt nonce-prefixed ciphertext back to string."""
    if len(data) < 13:
        raise ValueError("Ciphertext too short")
    aesgcm = _load_key()
    nonce = data[:12]
    ciphertext = data[12:]
    return aesgcm.decrypt(nonce, ciphertext, None).decode("utf-8")
