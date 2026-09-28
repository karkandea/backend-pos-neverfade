#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

if len(sys.argv) != 2:
    print("usage: verify-staging-policy.py <env-file>", file=sys.stderr)
    raise SystemExit(2)

path = Path(sys.argv[1])
if not path.is_file():
    print(f"staging policy FAILED: {path} not found", file=sys.stderr)
    raise SystemExit(2)

values: dict[str, str] = {}
for raw in path.read_text(encoding="utf-8").splitlines():
    line = raw.strip()
    if not line or line.startswith("#") or "=" not in line:
        continue
    key, value = line.split("=", 1)
    values[key.strip()] = value.strip().strip("'\"")

errors: list[str] = []
payment_mode = values.get("Payments__Mode", "").lower()
if payment_mode not in {"disabled", "sandbox"}:
    errors.append("Payments__Mode must be Disabled or Sandbox")

if values.get("Payments__LiveEnabled", "false").lower() in {"1", "true", "yes", "on"}:
    errors.append("Payments__LiveEnabled must be false")

if values.get("Payments__LiveAllowedTenantIds", "").strip():
    errors.append("Payments__LiveAllowedTenantIds must be empty")

if values.get("Waha__Enabled", "").lower() not in {"0", "false", "no", "off"}:
    errors.append("Waha__Enabled must be false")

if errors:
    print("Staging policy FAILED:")
    for error in errors:
        print(f"- {error}")
    raise SystemExit(1)

print("Staging policy PASS: live payments and customer WhatsApp delivery are disabled.")
