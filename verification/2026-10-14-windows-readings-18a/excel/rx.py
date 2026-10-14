"""One or more xl-server commands from the command line, each logged to records/steps.jsonl.

    python3 rx.py '<command>' ['<command>' ...]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

xs.log_to(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'records', 'steps.jsonl'))
for c in sys.argv[1:]:
    try:
        print(xs.cmd(c))
    except Exception as e:
        print('ERR', e)
