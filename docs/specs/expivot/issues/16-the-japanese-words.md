# 16: The words of Excel's Japanese edition

Status: ready-for-agent

**What to build:** the words of Excel's Japanese edition, bundled (ADR-0059).

- They cover every id ExPivot paints, and the ExGrid commands in the report's Context Menu.
- The Consumer chooses them in one line.
- They never follow the culture on their own.

**Blocked by:** 12

- [x] PV-33: no English word left on `/pivot` in Japanese

## Comments

2026-10-01: Built. `PivotWords.Japanese` gives the word of Excel's Japanese edition for every id
ExPivot paints, and for ExGrid's Copy and Copy with headers in the report's Context Menu. The
Consumer chooses them in one line (`Label="PivotWords.Japanese"`); the culture alone changes no
word. Layer 1 holds every id and every template's arguments; layer 2 walks every surface over
Japanese data and finds no Latin word but OK, and fails on any id asked for that has no Japanese
word. `/pivot` has a words switch (`#pivot-words`, or `?words=ja`), which layer 3 exercises.
