"""
Custom PKI + mTLS demo, four scenarios.

Run:  python main.py
Expected outcomes: SUCCESS, FAIL, SUCCESS, FAIL.

The whole point: mTLS only works when both sides' trust anchors agree.
The moment one side rotates to a new RootCA without the other learning
about it, the handshake breaks.
"""

from root_ca import RootCA
from client import Client
from mtls import perform_mtls


def scenario(title: str, expect_success: bool, runner):
    print()
    print("=" * 70)
    print(title)
    print("=" * 70)
    actual = runner()
    expected = "SUCCESS" if expect_success else "FAIL"
    got = "SUCCESS" if actual else "FAIL"
    status = "MATCH" if (actual == expect_success) else "MISMATCH"
    print(f"-> expected: {expected}, actual: {got}   [{status}]")


def main():
    # ------------------------------------------------------------------
    # Scenario 1: One CA, both clients enrolled under it. mTLS should pass.
    # ------------------------------------------------------------------
    print("Creating RootCA-1, enrolling client1 and client2 under it...")
    ca1 = RootCA("RootCA-1")
    client1 = Client("client1")
    client2 = Client("client2")
    client1.enroll(ca1)
    client2.enroll(ca1)

    scenario(
        title="Scenario 1: shared root, both enrolled  ->  expect SUCCESS",
        expect_success=True,
        runner=lambda: perform_mtls(client1, client2),
    )

    # ------------------------------------------------------------------
    # Scenario 2: New CA. Only client1 re-enrolls.
    #   client1: new leaf (signed by ca2), trusts ca2
    #   client2: STILL old leaf (signed by ca1), STILL trusts ca1
    # client2 sees client1's new leaf -> not signed by ca1 -> rejected.
    # client1 sees client2's old leaf -> not signed by ca2 -> rejected.
    # ------------------------------------------------------------------
    print()
    print("Creating RootCA-2. Only client1 re-enrolls; client2 is unaware.")
    ca2 = RootCA("RootCA-2")
    client1.enroll(ca2)

    scenario(
        title="Scenario 2: new root, only client1 re-enrolls  ->  expect FAIL",
        expect_success=False,
        runner=lambda: perform_mtls(client1, client2),
    )

    # ------------------------------------------------------------------
    # Scenario 3: bring client2 onto ca2 too. Both back in sync. Should pass.
    # ------------------------------------------------------------------
    print()
    print("client2 re-enrolls under RootCA-2. Trust anchors now agree again.")
    client2.enroll(ca2)

    scenario(
        title="Scenario 3: both re-enrolled under RootCA-2  ->  expect SUCCESS",
        expect_success=True,
        runner=lambda: perform_mtls(client1, client2),
    )

    # ------------------------------------------------------------------
    # Scenario 4: yet another new CA. This time only client2 re-enrolls.
    # Symmetric to scenario 2 but with the roles swapped. Should fail.
    # ------------------------------------------------------------------
    print()
    print("Creating RootCA-3. Only client2 re-enrolls; client1 is unaware.")
    ca3 = RootCA("RootCA-3")
    client2.enroll(ca3)

    scenario(
        title="Scenario 4: new root, only client2 re-enrolls  ->  expect FAIL",
        expect_success=False,
        runner=lambda: perform_mtls(client1, client2),
    )


if __name__ == "__main__":
    main()
