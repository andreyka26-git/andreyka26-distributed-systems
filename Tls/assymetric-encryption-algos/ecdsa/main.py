"""
ECDSA demo (the signing primitive TLS uses for ECDSA certificates).

We:
  1. Generate the server's EC public/private key pair.
  2. Sign a test_string with the PRIVATE key  -> produces a signature.
  3. Verify the signature against the PUBLIC key:
        - it MUST verify for the original test_string
        - it MUST NOT verify for some other string

Note: ECDSA is the *signature* algorithm. ECDH(E) is the *key agreement*
algorithm. They are different things even though both live on elliptic curves
-- you do NOT "verify a signature with ECDH". Verification is done with the
public key via ECDSA's verify routine, which is what we do below.
"""

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.exceptions import InvalidSignature


def ecdsa_demo() -> None:
    # 1. Server key pair (SECP256R1 == NIST P-256, the most common TLS curve).
    private_key = ec.generate_private_key(ec.SECP256R1())
    public_key = private_key.public_key()

    test_string = b"hello, this is the message the server signs"
    other_string = b"hello, this is a DIFFERENT message"

    # 2. Sign test_string with the private key.
    signature = private_key.sign(test_string, ec.ECDSA(hashes.SHA256()))
    print(f"signature ({len(signature)} bytes): {signature.hex()}")

    # 3a. Verify the signature for the ORIGINAL string -> should succeed.
    try:
        public_key.verify(signature, test_string, ec.ECDSA(hashes.SHA256()))
        print("[OK]   signature is VALID for test_string  (as expected)")
    except InvalidSignature:
        print("[FAIL] signature unexpectedly invalid for test_string")

    # 3b. Verify the same signature for a DIFFERENT string -> should fail.
    try:
        public_key.verify(signature, other_string, ec.ECDSA(hashes.SHA256()))
        print("[FAIL] signature unexpectedly VALID for other_string")
    except InvalidSignature:
        print("[OK]   signature is INVALID for other_string  (as expected)")


if __name__ == "__main__":
    ecdsa_demo()
