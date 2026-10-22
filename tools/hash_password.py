#!/usr/bin/env python3
"""Hash a LucyAPI login password in ASP.NET Core Identity v3 format (what PasswordHasher<string> stores in
users.password_hash): PBKDF2-HMAC-SHA512, 100,000 iterations, 16-byte salt, 32-byte subkey, the .NET 7+ defaults.
Reads the password with a hidden prompt (never argv) and prints only the hash. Used by database/bootstrap/first_user.sql.
Usage: hash_password.py"""
import base64, getpass, hashlib, os, sys

PRF_HMACSHA512 = 2
ITERATIONS = 100_000

def identity_v3_hash(password):
    salt = os.urandom(16)
    sub = hashlib.pbkdf2_hmac("sha512", password.encode(), salt, ITERATIONS, 32)
    blob = (bytes([1]) + PRF_HMACSHA512.to_bytes(4, "big") + ITERATIONS.to_bytes(4, "big")
            + len(salt).to_bytes(4, "big") + salt + sub)
    return base64.b64encode(blob).decode()

def main():
    pw = getpass.getpass("Password: ")
    if not pw:
        sys.exit("Empty password.")
    if getpass.getpass("Again: ") != pw:
        sys.exit("Passwords don't match.")
    print(identity_v3_hash(pw))

if __name__ == "__main__":
    main()
