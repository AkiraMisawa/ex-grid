"""Part F under Narrator: what it said at each step, from the recording loopback.ps1 made while part_f.py ran "paced".

The recording is cut at each step's time ("at" in records/part-f-narrator-<browser>.jsonl, the WSL clock; the recording's
start is Windows' clock, and the two differed by about +0.06 s when measured, 2026-10-12), from one step's key to the
next one's (the last, as long as a step's). Each piece is made 16 kHz mono with ffmpeg and transcribed by whisper.cpp (ggml-small.en), on this machine.
A piece whose loudest sample is below SILENT is reported as silence and not transcribed (Whisper invents words in
silence).

    nix shell nixpkgs#whisper-cpp nixpkgs#ffmpeg -c python3 part_f_heard.py <chrome|msedge> <recording.wav> [scanoff]
"""
import array
import json
import os
import struct
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL, WAV = sys.argv[1], sys.argv[2]
SUFFIX = '-scan-off' if 'scanoff' in sys.argv[3:] else ''
REC = os.path.join(HERE, 'records', f'part-f-narrator-{CHANNEL}{SUFFIX}.jsonl')
OUT = os.path.join(HERE, 'records', f'part-f-narrator-{CHANNEL}{SUFFIX}-heard.json')
MODEL = os.path.expanduser('~/.cache/whisper-cpp/ggml-small.en.bin')
CLOCK = 0.06    # Windows' clock minus WSL's
SILENT = 0.005

meta = json.load(open(WAV + '.json', encoding='utf-8-sig'))
start = meta['startUtcMs'] / 1000
length = meta['frames'] / meta['rate']
rows = [json.loads(l) for l in open(REC, encoding='utf-8') if l.strip()]
steps = [r for r in rows if r.get('at') is not None]


def peak(path):
    b = open(path, 'rb').read()
    i = b.index(b'data')
    n = struct.unpack('<I', b[i + 4:i + 8])[0]
    a = array.array('h')
    a.frombytes(b[i + 8:i + 8 + n - n % 2])
    return max((abs(x) for x in a), default=0) / 32768


out = []
with tempfile.TemporaryDirectory() as tmp:
    for k, r in enumerate(steps):
        t0 = r['at'] + CLOCK - start
        # The last step's piece is as long as a step's (6 s and the reading): after it the browser closes, and Narrator
        # reads the window that comes to the front, the session's terminal.
        t1 = (steps[k + 1]['at'] + CLOCK - start) if k + 1 < len(steps) else min(length, t0 + 6.2)
        piece = os.path.join(tmp, f'{k}.wav')
        subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-ss', f'{max(t0, 0):.3f}', '-t', f'{t1 - max(t0, 0):.3f}', '-i', WAV,
                        '-af', 'pan=mono|c0=0.5*c0+0.5*c1', '-ar', '16000', '-c:a', 'pcm_s16le', piece], check=True)
        level = peak(piece)
        if level < SILENT:
            text = ''
        else:
            w = subprocess.run(['whisper-cli', '-m', MODEL, '-t', '16', '-nt', '-np', '-f', piece], capture_output=True, text=True)
            text = ' '.join(w.stdout.split())
        row = {'start': r['start'], 'step': r['step'], 'from': round(t0, 2), 'to': round(t1, 2), 'peak': round(level, 4),
               'heard': text if level >= SILENT else '(silence)',
               'activeElement': (r.get('state') or {}).get('activeElement'), 'focus': (r.get('state') or {}).get('focus')}
        out.append(row)
        print(f"{row['start']:<14} {row['step']:<40} {row['from']:>7.2f}-{row['to']:<7.2f} peak {row['peak']:<7} "
              f"{row['activeElement']} {row['focus']} | {row['heard']}", flush=True)
json.dump({'recording': os.path.basename(WAV), 'meta': meta, 'clockOffset': CLOCK, 'model': os.path.basename(MODEL), 'steps': out},
          open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
