| Case | F4 | Reading | Excel, typed | Excel, F2 first | ExSheet, cell typed | ExSheet, cell F2 first | ExSheet, Formula Bar | Configurations agreeing |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | `=$B$2` | `=$B$2 [5]` | `—` | `=$B$2 [5]` | `—` | `=$B$2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 1 | 2 | `=B$2` | `=B$2 [4]` | `—` | `=B$2 [4]` | `—` | `=B$2 [4]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 1 | 3 | `=$B2` | `=$B2 [4]` | `—` | `=$B2 [4]` | `—` | `=$B2 [4]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 1 | 4 | `=B2` | `=B2 [3]` | `—` | `=B2 [3]` | `—` | `=B2 [3]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 2 | 1 | `=$A$1+B2` | `committed` | `=$A$1+B2 [1-5]` | `committed` | `=$A$1+B2 [5]` | `=$A$1+B2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 3 | 1 | `=$A$1+B2` | `committed` | `=$A$1+B2 [1-5]` | `committed` | `=$A$1+B2 [5]` | `=$A$1+B2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 4 | 1 | `=$A$1+B2` | `committed` | `=$A$1+B2 [1-5]` | `committed` | `=$A$1+B2 [5]` | `=$A$1+B2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 1 | `=SUM($A$1:$B$2)` | `committed` | `=SUM(A1:$B$2) [8-12]` | `committed` | `=SUM($A$1:$B$2) [14]` | `=SUM($A$1:$B$2) [14]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 2 | `=SUM(A$1:B$2)` | `committed` | `=SUM(A1:B$2) [8-11]` | `committed` | `=SUM(A$1:B$2) [12]` | `=SUM(A$1:B$2) [12]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 3 | `=SUM($A1:$B2)` | `committed` | `=SUM(A1:$B2) [8-11]` | `committed` | `=SUM($A1:$B2) [12]` | `=SUM($A1:$B2) [12]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 5 | 4 | `=SUM(A1:B2)` | `committed` | `=SUM(A1:B2) [8-10]` | `committed` | `=SUM(A1:B2) [11]` | `=SUM(A1:B2) [11]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 6 | 1 | `=A1:B2` | `committed` | `=$A1:$B$2 [5-9]` | `committed` | `=A1:B2 [6]` | `=A1:B2 [6]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 7 | 1 | `=$A$1+$B$2 (the span selected)` | `committed` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 8 | 1 | `=SUM($A:$A)` | `committed` | `=SUM($A:$A) [5-10]` | `committed` | `=SUM($A:$A) [10]` | `=SUM($A:$A) [10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 8 | 2 | `=SUM(A:A)` | `committed` | `=SUM(A:A) [5-8]` | `committed` | `=SUM(A:A) [9]` | `=SUM(A:A) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 8 | 3 | `=SUM($A:$A)` | `committed` | `=SUM($A:$A) [5-10]` | `committed` | `=SUM(A:A) [9]` | `=SUM(A:A) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 9 | 1 | `=SUM($1:$1)` | `committed` | `=SUM($1:$1) [5-10]` | `committed` | `=SUM($1:$1) [10]` | `=SUM($1:$1) [10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 9 | 2 | `=SUM(1:1)` | `committed` | `=SUM(1:1) [5-8]` | `committed` | `=SUM(1:1) [9]` | `=SUM(1:1) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 9 | 3 | `=SUM($1:$1)` | `committed` | `=SUM($1:$1) [5-10]` | `committed` | `=SUM(1:1) [9]` | `=SUM(1:1) [9]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 10 | 1 | `=Sheet2!$A$1` | `=Sheet2!$A$1 [12]` | `—` | `=Sheet2!$A$1 [12]` | `—` | `=Sheet2!$A$1 [12]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 11 | 1 | `unchanged` | `committed` | `=SUM(Positions[PV]) [18]` | `committed` | `=SUM(Positions[PV]) [18]` | `=SUM(Positions[PV]) [18]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 12 | 1 | `unchanged` | `committed` | `=SUM(A1) [2]` | `committed` | `=SUM(A1) [2]` | `=SUM(A1) [2]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 13 | 1 | `unchanged` | `=1+2 [4]` | `—` | `=1+2 [4]` | `—` | `=1+2 [4]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 14 | 1 | `unchanged` | `B2 [2]` | `—` | `B2 [2]` | `—` | `B2 [2]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 15 | 1 | `=$A$2` | `=$A$2 [1-5]` | `—` | `=$D$11 [6]` | `—` | `= [1]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 15b | 1 | `=A3 (or =$A$3: record which)` | `=A3 [1-3]` | `—` | `=D12 [4]` | `—` | `= [1]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 16 | 1 | `=$A$2+, pointing ends` | `=$A$2+ [6]` | `—` | `=$D$11+ [7]` | `—` | `=+ [2]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 18 | 1 | `=$A$1+$B$1` | `committed` | `=$A$1+$B$1 [1-10]` | `committed` | `=$A$1+$B$1 [1-10]` | `=$A$1+$B$1 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 19 | 1 | `=$A$1+$B$2` | `committed` | `=$A$1+$B$2 [1-10]` | `committed` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 20 | 1 | `=$A$1+$B$2` | `committed` | `=$A$1+$B$2 [1-10]` | `committed` | `=$A$1+$B$2 [1-10]` | `=$A$1+$B$2 [1-10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 21 | 1 | `=$b$2` | `=$B$2 [5]` | `—` | `=$b$2 [5]` | `—` | `=$b$2 [5]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
| 22 | 1 | `=$B$2:$A$1` | `=B2:$A$1 [8]` | `—` | `=$B$2:$A$1 [10]` | `—` | `=$B$2:$A$1 [10]` | all (server-chrome, server-msedge, wasm-chrome, wasm-msedge, wasm-msedge-interrupted) |
