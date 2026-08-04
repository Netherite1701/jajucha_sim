#!/usr/bin/env bash
# setup_python.sh - Create the project-local Python virtual environment (Step 11.20)
#
#   1. find a supported Python installation
#   2. create .venv
#   3. upgrade pip inside .venv
#   4. install python/requirements.txt
#   5. verify imports
#   6. print the exact command for running examples
#
# Usage:
#   ./scripts/setup_python.sh

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV_DIR="$ROOT/.venv"
REQUIREMENTS="$ROOT/python/requirements.txt"

echo "[setup] Jajucha Simulator Python setup"
echo "[setup] Project root : $ROOT"

# 1. Find a supported Python installation (3.9+).
PY=""
for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1; then
        VER="$("$candidate" -c 'import sys; print("%d.%d" % sys.version_info[:2])' 2>/dev/null || true)"
        case "$VER" in
            3.9*|3.1[0-9]*)
                PY="$candidate"
                echo "[setup] Using Python $VER : $(command -v "$candidate")"
                break
                ;;
        esac
    fi
done
if [ -z "$PY" ]; then
    echo "[setup][ERROR] No supported Python (3.9+) found. Install Python 3.10+ and retry." >&2
    exit 1
fi

# 2. Create .venv.
if [ ! -x "$VENV_DIR/bin/python" ]; then
    echo "[setup] Creating virtual environment at .venv"
    "$PY" -m venv "$VENV_DIR"
else
    echo "[setup] .venv already exists"
fi

VENV_PY="$VENV_DIR/bin/python"

# 3. Upgrade pip.
echo "[setup] Upgrading pip"
"$VENV_PY" -m pip install --upgrade pip

# 4. Install requirements.
echo "[setup] Installing python/requirements.txt"
"$VENV_PY" -m pip install -r "$REQUIREMENTS"

# 5. Verify imports.
echo "[setup] Verifying imports"
(cd "$ROOT" && "$VENV_PY" -c 'import sys; sys.path.insert(0, "python"); import jchm, jchm_sim; print("jchm OK, jchm_sim OK")')

# 6. Print the exact commands.
echo ""
echo "[setup] Done. To run an example:"
echo "    ./.venv/bin/python python/examples/01_motor_test.py"
echo "    ./.venv/bin/python python/user/main.py"
echo ""
echo "[setup] To run the tests:"
echo "    ./.venv/bin/python -m pytest python/tests/ -q"
