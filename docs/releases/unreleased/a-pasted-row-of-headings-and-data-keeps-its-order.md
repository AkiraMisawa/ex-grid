Product: ExGrid
Kind: Fixed
ADR: 0005

A pasted HTML table whose rows mix heading and data cells, a web page's row headings `<tr><th>Name</th><td>1</td></tr>`, is read in the order its cells stand. It was read data cells first, so the headings were dropped and the values landed one column to the left. A large table from Excel is also read in time proportional to its size, where each row had searched the rest of the document for a heading cell it did not have.
