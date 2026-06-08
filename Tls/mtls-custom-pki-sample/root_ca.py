"""
RootCA: a self-signed certificate authority.

A root CA is just a keypair plus a certificate that says
"the holder of this private key is the trust anchor".
- It signs ITSELF (self-signed) -> subject == issuer.
- Anyone who has the CA's public certificate can verify any leaf
  cert this CA signed by checking the leaf's signature against
  the CA's public key.
"""

import datetime

from cryptography import x509
from cryptography.x509.oid import NameOID
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec


class RootCA:
    def __init__(self, name: str):
        self.name = name

        # 1) Generate the CA's keypair. The PRIVATE key never leaves this object.
        # ECC (Elliptic Curve Cryptography), SECP256R1 - eliptic curve
        self._private_key = ec.generate_private_key(ec.SECP256R1())

        # 2) Build a self-signed certificate.
        #    - subject == issuer (that's what "self-signed" means)
        #    - signed with our own private key
        subject = issuer = x509.Name([
            x509.NameAttribute(NameOID.COMMON_NAME, name),
        ])

        now = datetime.datetime.now(datetime.timezone.utc)

        builder = (
            x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(issuer)
            .public_key(self._private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(now)
            .not_valid_after(now + datetime.timedelta(days=10))
            # ca=True marks this cert as eligible to sign OTHER certs.
            # Without this extension, chain validation refuses to treat it as an issuer.
            .add_extension(
                x509.BasicConstraints(ca=True, path_length=None),
                critical=True,
            )
            # KeyUsage tells verifiers what this key is allowed to do.
            # A CA needs key_cert_sign (to sign leaf certs).
            .add_extension(
                x509.KeyUsage(
                    digital_signature=False,
                    content_commitment=False,
                    key_encipherment=False,
                    data_encipherment=False,
                    key_agreement=False,
                    key_cert_sign=True,
                    crl_sign=True,
                    encipher_only=False,
                    decipher_only=False,
                ),
                critical=True,
            )
        )

        self._ca_cert = builder.sign(
            private_key=self._private_key,
            algorithm=hashes.SHA256(),
        )

    @property
    def ca_cert(self) -> x509.Certificate:
        """The PUBLIC CA cert. This is what clients install as their trust anchor."""
        return self._ca_cert

    def issue_leaf(self, subject_name: str, public_key) -> x509.Certificate:
        """
        Sign a leaf cert binding `subject_name` to the given public key.
        Note: we never receive or store the client's private key. We only
        bind their public key into a certificate and sign it.
        """
        subject = x509.Name([
            x509.NameAttribute(NameOID.COMMON_NAME, subject_name),
        ])

        now = datetime.datetime.now(datetime.timezone.utc)

        builder = (
            x509.CertificateBuilder()
            .subject_name(subject)
            # issuer = OUR (the CA's) subject. That's the link the verifier follows
            # back from the leaf to the root.
            .issuer_name(self._ca_cert.subject)
            .public_key(public_key)
            .serial_number(x509.random_serial_number())
            .not_valid_before(now)
            .not_valid_after(now + datetime.timedelta(days=10))
            # Leaf is NOT a CA -- it cannot sign other certs.
            .add_extension(
                x509.BasicConstraints(ca=False, path_length=None),
                critical=True,
            )
            # Leaf key is used to sign TLS handshake messages (digital_signature).
            .add_extension(
                x509.KeyUsage(
                    digital_signature=True,
                    content_commitment=False,
                    key_encipherment=False,
                    data_encipherment=False,
                    key_agreement=False,
                    key_cert_sign=False,
                    crl_sign=False,
                    encipher_only=False,
                    decipher_only=False,
                ),
                critical=True,
            )
        )

        # Signed with the CA's PRIVATE key. This is what makes the leaf "issued by this CA".
        # Anyone with the CA's public cert can later verify this signature.
        return builder.sign(
            private_key=self._private_key,
            algorithm=hashes.SHA256(),
        )
