"""The steps this session drove through rx.sh (one command at a time to xl-server.ps1) and their answers, read from
the session's own transcript: R2, R3 (b) and (d), R5, R7 and the R1 probe, which no script logs. One JSON line per
shell call: when it ran (UTC), the command as typed, and its output (the shell's zoxide notice left out).

    python3 steps-from-session.py <transcript.jsonl> <since, ISO UTC> > records/steps-by-hand.jsonl
"""
import json
import sys

path, since = sys.argv[1], sys.argv[2]
calls, results = {}, {}
for line in open(path, encoding='utf-8'):
    try:
        j = json.loads(line)
    except ValueError:
        continue
    m = j.get('message') or {}
    for c in m.get('content') or [] if isinstance(m.get('content'), list) else []:
        if c.get('type') == 'tool_use' and c.get('name') == 'Bash' and 'rx.sh' in c['input'].get('command', ''):
            if j.get('timestamp', '') >= since:
                calls[c['id']] = (j['timestamp'], c['input']['command'])
        if c.get('type') == 'tool_result' and c.get('tool_use_id'):
            body = c.get('content')
            if isinstance(body, list):
                body = '\n'.join(x.get('text', '') for x in body if isinstance(x, dict))
            results[c['tool_use_id']] = body or ''
noise = ('zoxide:', 'Please ensure', 'If the issue', 'https://github.com/ajeetdsouza', 'Disable this message')
for i, (t, cmd) in sorted(calls.items(), key=lambda kv: kv[1][0]):
    out = '\n'.join(l for l in results.get(i, '').splitlines() if l.strip() and not l.startswith(noise) and 'Shell cwd was reset' not in l)
    print(json.dumps({'t': t, 'command': cmd.replace('/tmp/claude-1000/-home-akira-src-hobby-ex-grid/9a3f6c84-414c-4812-99fe-6af2ba8d9fec/scratchpad/', ''), 'output': out}, ensure_ascii=False))
