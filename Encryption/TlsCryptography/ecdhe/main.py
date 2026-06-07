"""
ECDHE demo -- Elliptic Curve Diffie-Hellman Ephemeral key agreement,
the key-exchange used in every modern TLS 1.3 handshake.

The whole point of (EC)DHE: client and server each keep a PRIVATE key secret
and exchange only their PUBLIC keys over the wire. Each side then combines
*its own private* key with *the other side's public* key and -- thanks to the
maths of the curve -- both independently arrive at the SAME shared secret,
without that secret ever travelling across the network.

Steps:
  1. Client generates an ephemeral key pair (priv_c, pub_c).
  2. Server generates an ephemeral key pair (priv_s, pub_s).
  3. pub_c and pub_s are exchanged (in TLS: ClientHello / ServerHello
     key_share extensions).
  4. Client derives:  shared = ECDH(priv_c, pub_s)
     Server derives:  shared = ECDH(priv_s, pub_c)
  5. Both run the raw shared point through a KDF (HKDF) -- TLS never uses the
     raw ECDH output directly; it always feeds it through a key-derivation
     function. The two derived keys must be identical.
"""

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.kdf.hkdf import HKDF


def derive_key(shared_secret: bytes) -> bytes:
    """Turn the raw ECDH shared point into a symmetric key, like TLS does."""
    return HKDF(
        algorithm=hashes.SHA256(),
        length=32,                 # 32 bytes -> a 256-bit AES key
        salt=None,
        info=b"tls 1.3 handshake demo",
    ).derive(shared_secret)


def ecdhe_demo() -> None:
    curve = ec.SECP256R1()  # P-256, a standard TLS named group

    # 1. Client's ephemeral key pair.
    client_private = ec.generate_private_key(curve)
    client_public = client_private.public_key()

    # 2. Server's ephemeral key pair.
    server_private = ec.generate_private_key(curve)
    server_public = server_private.public_key()

    # 3 + 4a. Client side: its own private + server's public.
    client_shared_raw = client_private.exchange(ec.ECDH(), server_public)
    client_key = derive_key(client_shared_raw)

    # 3 + 4b. Server side: its own private + client's public.
    server_shared_raw = server_private.exchange(ec.ECDH(), client_public)
    server_key = derive_key(server_shared_raw)

    print(f"client derived key: {client_key.hex()}")
    print(f"server derived key: {server_key.hex()}")

    if client_key == server_key:
        print("[OK]   shared keys MATCH  -- both sides agreed on the same secret")
    else:
        print("[FAIL] shared keys differ")


if __name__ == "__main__":
    ecdhe_demo()
