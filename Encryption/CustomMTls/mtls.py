"""
perform_mtls: emulate a mutual-TLS handshake between two clients.

Real mTLS does two things on each side:
  1) Chain validation   -- "is the peer's leaf cert signed by a CA I trust?"
  2) Proof-of-possession -- "does the peer actually hold the private key that
                            matches the public key in their cert?"

(1) alone is not enough: certs are public, so anyone could replay one.
(2) is why TLS includes a signed handshake message ('CertificateVerify' in
real TLS). Here we simplify it to "sign a random nonce".

We run BOTH checks BOTH directions. If any of the four checks fail, mTLS fails.
"""

import os

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec


def perform_mtls(a, b) -> bool:
    print(f"-- mTLS handshake between '{a.name}' and '{b.name}' --")

    # Step 1: cert exchange. In real TLS these go inside the 'Certificate'
    # handshake message. Both sides send their leaves.
    print("Step 1: exchange leaf certs")
    a_leaf = a.leaf_cert
    b_leaf = b.leaf_cert
    print(f"  {a.name} -> {b.name}: leaf '{a_leaf.subject.rfc4514_string()}' "
          f"(issued by '{a_leaf.issuer.rfc4514_string()}')")
    print(f"  {b.name} -> {a.name}: leaf '{b_leaf.subject.rfc4514_string()}' "
          f"(issued by '{b_leaf.issuer.rfc4514_string()}')")

    # Step 2: chain validation. Each side checks the OTHER's leaf was signed
    # by a CA it (the local side) trusts.
    print("Step 2: validate each peer cert against the local trusted root")
    a_trusts_b = a.validate_peer_cert(b_leaf)
    b_trusts_a = b.validate_peer_cert(a_leaf)

    if not (a_trusts_b and b_trusts_a):
        print(">> mTLS FAILED at chain validation")
        return False

    # Step 3: proof of possession. Each side sends a random nonce; the peer
    # signs it with its PRIVATE key; the original side verifies using the
    # PUBLIC key inside the peer's leaf.
    #
    # Why this matters: chain validation only proves the cert is legitimate.
    # It doesn't prove the entity we're talking to actually owns the matching
    # private key. Without this step, anyone who copied a cert could impersonate.
    print("Step 3: proof-of-possession (nonce + signature, both directions)")

    if not _verify_holds_private_key(challenger=a, holder=b):
        print(">> mTLS FAILED at proof-of-possession (b couldn't prove key ownership)")
        return False

    if not _verify_holds_private_key(challenger=b, holder=a):
        print(">> mTLS FAILED at proof-of-possession (a couldn't prove key ownership)")
        return False

    print(">> mTLS SUCCEEDED")
    return True


def _verify_holds_private_key(challenger, holder) -> bool:
    """
    challenger picks a random nonce, holder signs it, challenger verifies
    the signature using the public key from holder's leaf cert.
    """
    nonce = os.urandom(32)
    print(f"  {challenger.name} -> {holder.name}: nonce ({nonce.hex()[:16]}...)")

    signature = holder.sign(nonce)
    print(f"  {holder.name} -> {challenger.name}: signature over nonce ({signature.hex()[:16]}...)")

    holder_public_key = holder.leaf_cert.public_key()
    try:
        holder_public_key.verify(signature, nonce, ec.ECDSA(hashes.SHA256()))
        print(f"  {challenger.name} OK  - signature verifies against pubkey in "
              f"'{holder.leaf_cert.subject.rfc4514_string()}' -> {holder.name} holds the matching private key")
        return True
    except Exception as e:
        print(f"  {challenger.name} FAIL - signature does NOT verify ({type(e).__name__})")
        return False
