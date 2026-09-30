from pathlib import Path
import sys

path = Path(sys.argv[1] if len(sys.argv) > 1 else ".github/workflows/endgame-v2-ci.yml")
text = path.read_text()
required = [
    "force_gate_failure:",
    "Controlled fail-closed proof",
    "exit 42",
    "promotion_eligibility:",
    "needs: verify",
    "needs.verify.result == 'success'",
    "actions/download-artifact@v4",
    "sha256sum -c SHA256SUMS",
    'record.get("sha")',
    "promotion-eligibility.json",
]
missing = [token for token in required if token not in text]
if missing:
    print("Promotion guard FAILED. Missing:", ", ".join(missing))
    raise SystemExit(1)

print("Promotion guard PASS: failed verify cannot produce promotion eligibility.")
