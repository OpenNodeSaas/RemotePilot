# Code signing policy — RemotePilot

## Current status

RemotePilot releases are currently **unsigned**. Free Windows code signing
via SignPath Foundation has **not** been approved yet.

## Intended signer: SignPath Foundation

Once approved, Windows releases of RemotePilot will be signed through
[SignPath Foundation](https://signpath.org) (free code signing for
qualifying open-source projects).

- The signing certificate is issued to **SignPath Foundation**, so the
  publisher shown in Windows SmartScreen / installation dialogs will be
  **"SignPath Foundation"**, not RemotePilot or OpenNodeSaas.
- Signing happens **only in GitHub Actions**, from source code in this
  repository. Binaries are never signed locally and never signed from
  builds outside this repository.
- The signing workflow is `.github/workflows/sign.yaml`. It is disabled
  (`if: false`) until SignPath Foundation approves the project and the
  required repository secrets are set.
- **Every release requires manual approval** by a named human approver in
  SignPath before the signed artifact is published.

## Attribution

Free code signing provided by SignPath.io, certificate by SignPath Foundation

## What is signed

Only binaries built from this repository's own source code (the
RemotePilot WPF application and its installer/package, when applicable).
No third-party or upstream binaries are signed as RemotePilot binaries.

## Revocation

SignPath Foundation may revoke the certificate, including retroactively,
if the project violates its conditions (for example, malware, a
non-OSI-approved or dual license, or proprietary components). This project
is licensed under Apache License 2.0 (see `LICENSE`) and contains no
proprietary components; see the third-party table in `README.md`.
