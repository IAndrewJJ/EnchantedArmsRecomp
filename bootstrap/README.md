# Portable Python bootstrap

The complete Windows release kit bundles the official Python embedded archive named in `launcher/python.lock.json`. Setup verifies its SHA-256 before extracting it locally. This avoids relying on Windows HTTPS for the first bootstrap download. The archive retains Python's `LICENSE.txt`, including its third-party notices.

This binary archive is a release asset, not tracked source. For source builds, setup can download it from the pinned publisher URL, or you can place the exact publisher archive in this directory. The packager checks the pinned size and digest before including it.
