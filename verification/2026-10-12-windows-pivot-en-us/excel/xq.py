"""One xl-server command from the shell: python3 xq.py <command line>. Logged to ../records/xs.jsonl."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

xs.log_to(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'records', 'xs.jsonl'))
print(xs.cmd(' '.join(sys.argv[1:])))
