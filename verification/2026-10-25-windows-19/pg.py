"""The WSL side of pg-server.mjs: one JSON command to the running browser, its one JSON answer back.

    import pg
    p = pg.Page.start('chrome')            # pg-server.mjs on Windows' node, a headed Chrome
    p.cmd(op='goto', url='http://localhost:5318/sheet', ready='...')
    p.stop()

Every exchange is appended to the log given, with the time.
"""
import json
import os
import subprocess
import time

WIN = '/mnt/c/Users/amisa/AppData/Local/exgrid-layer3/run-2026-10-25-19'
WIN_PATH = 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\run-2026-10-25-19'
NODE = '/mnt/c/Users/amisa/AppData/Local/exgrid-layer3/node-v24.14.1-win-x64/node.exe'
HERE = os.path.dirname(os.path.abspath(__file__))


class Page:
    def __init__(self, d, log=None):
        self.d, self.log = d, log

    @staticmethod
    def start(channel, name=None, log=None, prefix='run18'):
        name = name or f'pg-{channel}'
        d = os.path.join(WIN, name)
        os.makedirs(d, exist_ok=True)
        for f in ('cmd.txt', 'out.txt'):
            open(os.path.join(d, f), 'w').close()
        # The script runs from the copy of tests/ExGrid.Browser, for its node_modules.
        subprocess.run(['cp', os.path.join(HERE, 'pg-server.mjs'), os.path.join(WIN, 'browser', 'pg-server.mjs')], check=True)
        subprocess.run(['cp', os.path.join(HERE, 'input-server.ps1'), os.path.join(WIN, 'input-server.ps1')], check=True)
        cmd = (f"cd {WIN}/browser && tail -n +1 -f {d}/cmd.txt | '{NODE}' pg-server.mjs {channel} "
               f"'{WIN_PATH}\\input-server.ps1' {prefix}-{channel} >> {d}/out.txt 2>&1")
        subprocess.Popen(['bash', '-c', cmd], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
        p = Page(d, log)
        t0 = time.time()
        while time.time() - t0 < 90:
            ls = p._lines()
            if ls:
                first = json.loads(ls[0])
                if not first.get('ready'):
                    raise RuntimeError(ls[0])
                p.version = first.get('version')
                return p
            time.sleep(0.2)
        raise TimeoutError('pg-server did not start')

    @staticmethod
    def attach(channel, name=None, log=None):
        """The pg-server already running under that name (started by start() in an earlier process)."""
        return Page(os.path.join(WIN, name or f'pg-{channel}'), log)

    def _lines(self):
        with open(os.path.join(self.d, 'out.txt'), encoding='utf-8', errors='replace') as f:
            return [l for l in f.read().replace('\r', '').splitlines() if l.startswith('{')]

    def cmd(self, timeout=180, **c):
        n = len(self._lines())
        with open(os.path.join(self.d, 'cmd.txt'), 'a', encoding='utf-8') as f:
            f.write(json.dumps(c, ensure_ascii=False) + '\n')
        t0 = time.time()
        while time.time() - t0 < timeout:
            ls = self._lines()
            if len(ls) > n:
                a = json.loads(ls[n])
                if self.log:
                    with open(self.log, 'a', encoding='utf-8') as f:
                        f.write(json.dumps({'t': time.strftime('%H:%M:%S'), 'cmd': c if c.get('op') != 'eval' else {'op': 'eval', 'arg': c.get('arg')}, 'answer': a}, ensure_ascii=False)[:20000] + '\n')
                if 'error' in a:
                    raise RuntimeError(f"{c.get('op')}: {a['error']}")
                return a
            time.sleep(0.03)
        raise TimeoutError(json.dumps(c)[:200])

    def input(self, line):
        return self.cmd(op='input', cmd=line)['ok']

    def keys(self, spec, gap=40):
        return self.input(f'type {gap} {spec}')

    def eval(self, fn, arg=None):
        return self.cmd(op='eval', fn=fn, arg=arg)['value']

    def stop(self):
        try:
            self.cmd(op='quit', timeout=30)
        except Exception:
            pass


def lit(text):
    """Literal text in input-server.ps1's notation (SendKeys'): + ^ % ~ ( ) [ ] { } braced."""
    return ''.join('{' + c + '}' if c in '+^%~(){}[]' else c for c in text)
