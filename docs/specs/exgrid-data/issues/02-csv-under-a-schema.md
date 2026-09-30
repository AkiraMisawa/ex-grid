# 02: A CSV read under a Schema, and a suggested Schema

Status: ready-for-agent

**What to build:** `ExGrid.Data`'s CSV reader, which reads a stream of bytes straight into a
Snapshot's columns, under a Schema the Consumer declares.

- **Quoting** follows RFC 4180.
- **The separator** is a comma, a tab or a semicolon.
- **The header row** is optional. When there is one, it is matched per declared column: a missing
  column is refused by name, and an undeclared one is skipped.
- **Per column**, the Schema gives the kind; the decimal point, the thousands separator or a culture;
  a date's format; and the strings that count as a Blank.
- **An empty field is a Blank** in every kind.
- **Encodings:** UTF-8 is read with or without its byte-order mark. Shift-JIS is read through an
  opt-in encoding that alone refers to the code pages.
- **A suggestion** builds a Schema from a file's first rows and marks each column whose kind is not
  clear.

**Blocked by:** 01

- [ ] DA-7: each clause a named test, including a record cut across every read boundary
- [ ] DA-8: UTF-8 with and without its byte-order mark; Shift-JIS declared; the code pages referred
  to by the opt-in encoding alone
- [ ] DA-9: the suggestion marks unclear columns, and applies nothing
- [ ] DA-5 and DA-6 hold for the reader: progress, cancellation, and a malformed row named

## Comments
