#!/bin/bash
# One xl-server command: x.sh <command line>
exec python3 -I "$(dirname "$(readlink -f "$0")")/xq.py" "$@"
