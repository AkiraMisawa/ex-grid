Product: ExGrid
Kind: Behaviour
Docs: exgrid/appearance#density-and-row-height
ADR: 0053

A result whose rows fit under the browser's 33,554,428 px scroll limit but whose header band takes it past is now
refused by name, as a result whose rows alone are too tall is; it was compressed. A Consumer that meets it chooses a
shorter row or pages the result.
