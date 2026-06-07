"""
RSA signature demo (the signing primitive TLS uses for RSA certificates).

Same shape as the ECDSA demo, but with RSA keys:
  1. Generate the server's RSA public/private key pair.
  2. Sign a test_string with the PRIVATE key  -> produces a signature.
  3. Verify the signature against the PUBLIC key:
        - it MUST verify for the original test_string
        - it MUST NOT verify for some other string

We use RSA-PSS padding, which is what modern TLS 1.3 mandates for RSA
signatures (rsa_pss_rsae_* schemes).
"""

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import rsa, padding
from cryptography.exceptions import InvalidSignature


def rsa_demo() -> None:
    # 1. Server key pair. 2048-bit modulus, public exponent 65537 (standard).
    private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    public_key = private_key.public_key()

    test_string = b"hello, this is the message the server signs"
    other_string = b"hello, this is a DIFFERENT message"

    pss_padding = padding.PSS(
        mgf=padding.MGF1(hashes.SHA256()),
        salt_length=padding.PSS.MAX_LENGTH,
    )

    # 2. Sign test_string with the private key.
    signature = private_key.sign(test_string, pss_padding, hashes.SHA256())
    print(f"signature ({len(signature)} bytes): {signature.hex()}")

    # 3a. Verify the signature for the ORIGINAL string -> should succeed.
    try:
        public_key.verify(signature, test_string, pss_padding, hashes.SHA256())
        print("[OK]   signature is VALID for test_string  (as expected)")
    except InvalidSignature:
        print("[FAIL] signature unexpectedly invalid for test_string")

    # 3b. Verify the same signature for a DIFFERENT string -> should fail.
    try:
        public_key.verify(signature, other_string, pss_padding, hashes.SHA256())
        print("[FAIL] signature unexpectedly VALID for other_string")
    except InvalidSignature:
        print("[OK]   signature is INVALID for other_string  (as expected)")


if __name__ == "__main__":
    rsa_demo()
