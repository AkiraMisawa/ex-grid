"""Generate a deterministic million-record, 12-column CSV with 1,000 aggregate leaves."""
from pathlib import Path
import sys
out=Path(sys.argv[1]);out.parent.mkdir(parents=True,exist_ok=True)
with out.open('w',newline='') as f:
 f.write('Id,Account,Region,Desk,Book,Product,Currency,Trade date,Notional,P&L,Quantity,Confirmed\r\n')
 for i in range(1_000_000):
  region=i%10;desk=(i//10)%10;product=(i//100)%10
  f.write(f'T{i:07d},{100+region*10+desk:06d},Region{region},Desk{desk},Book{region:02d}{desk:02d},Product{product},USD,2026-01-01,1000.00,1.00,10,TRUE\r\n')
print(out.stat().st_size)
