"""
Client: an entity that owns a private key and (after enrolling) a leaf cert
signed by a Root CA it trusts.

In mTLS each side has BOTH:
  - its own leaf cert + private key  (so it can prove its identity)
  - a trusted root CA cert           (so it can validate the peer's leaf)

When a client "re-enrolls" with a new RootCA, BOTH halves update:
  - new leaf signed by the new CA
  - new trusted root anchor

The other party doesn't automatically learn about the new CA -- that's
the whole point of the failure scenarios in main.py.
"""

from cryptography import x509
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec


class Client:
    def __init__(self, name: str):
        self.name = name

        # The client's PRIVATE key. Generated once, lives only inside this object.
        # The CA never sees it. The peer never sees it. Only the matching PUBLIC
        # key gets baked into the leaf cert.
        self._private_key = ec.generate_private_key(ec.SECP256R1())

        # Populated on enroll().
        self.leaf_cert: x509.Certificate | None = None
        self.trusted_root_cert: x509.Certificate | None = None

    def enroll(self, root_ca) -> None:
        """
        Ask `root_ca` to issue a leaf cert for our public key.
        Atomically update BOTH:
          - our own leaf cert (signed by root_ca)
          - our trusted root anchor (root_ca's public cert)
        """
        public_key = self._private_key.public_key()

        # Hand the CA only our PUBLIC key + chosen subject name.
        leaf = root_ca.issue_leaf(subject_name=self.name, public_key=public_key)

        self.leaf_cert = leaf
        self.trusted_root_cert = root_ca.ca_cert

        print(f"  [{self.name}] enrolled with {root_ca.name}: "
              f"got leaf signed by '{leaf.issuer.rfc4514_string()}', "
              f"trust anchor = '{root_ca.ca_cert.subject.rfc4514_string()}'")

    def sign(self, data: bytes) -> bytes:
        """Sign arbitrary bytes with our PRIVATE key (used for proof-of-possession)."""
        return self._private_key.sign(data, ec.ECDSA(hashes.SHA256()))

    def validate_peer_cert(self, peer_cert: x509.Certificate) -> bool:
        """
        Verify that `peer_cert` was signed by OUR trusted root CA.

        The check: take the CA's public key (from our trust anchor) and use it
        to verify the signature embedded in the leaf cert. If the signatures
        chain, this cert legitimately came from "our" CA.
        """
        assert self.trusted_root_cert is not None, "client has no trust anchor yet"

        ca_public_key = self.trusted_root_cert.public_key()

        try:
            ca_public_key.verify(
                signature=peer_cert.signature,
                # tbs_certificate_bytes = exactly the bytes the CA signed over.
                # NOT the full DER -- the signature itself is appended after these bytes.
                data=peer_cert.tbs_certificate_bytes,
                signature_algorithm=ec.ECDSA(peer_cert.signature_hash_algorithm),
            )
            print(f"  [{self.name}] OK  - peer cert '{peer_cert.subject.rfc4514_string()}' "
                  f"chains to my trusted root '{self.trusted_root_cert.subject.rfc4514_string()}'")
            return True
        except Exception as e:
            print(f"  [{self.name}] FAIL - peer cert '{peer_cert.subject.rfc4514_string()}' "
                  f"does NOT chain to my trusted root "
                  f"'{self.trusted_root_cert.subject.rfc4514_string()}' ({type(e).__name__})")
            return False
