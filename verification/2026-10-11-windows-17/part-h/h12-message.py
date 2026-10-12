import sys, time, json
sys.path.insert(0, '/home/akira/src/hobby/ex-grid-run17/verification/2026-10-11-windows-17/excel')
import part_h_excel as h
xs = h.xs
for code in ['@;0', '0;@;0']:
    case = f'h12-{code}-message'
    h.record(case, 'start', excel=h.fresh())
    xs.cmd('setup A1 12'); xs.cmd('setup A2 abc'); xs.cmd('select A1:A2')
    xs.keys('^1'); time.sleep(1.5)
    xs.keys('%c'); time.sleep(0.3); xs.keys('{END}'); time.sleep(0.5)
    xs.keys('%t'); time.sleep(0.3); xs.keys('^a'); xs.keys(xs.lit(code), gap=40); time.sleep(0.5)
    xs.keys('{ENTER}'); time.sleep(0.6)
    wins = xs.cmd('windows')
    names = xs.cmd(r'uianames ^(?!.*(Ribbon|Excel \||Book1)).*(format|cannot|Excel|OK)')
    xs.cmd(f'shot {h.name_of(case, "message")}')
    h.record(case, 'message', windows=wins, names=names)
    xs.keys('{ENTER}'); time.sleep(0.8)
    h.record(case, 'after-message-ok', ready=xs.ready(), windows=xs.cmd('windows'))
    xs.keys('{ESC}'); time.sleep(0.8)
    h.state(case, 'cells', 'A1', 'A2', pic=('A1', 'C4'))
xs.cmd('stop')
