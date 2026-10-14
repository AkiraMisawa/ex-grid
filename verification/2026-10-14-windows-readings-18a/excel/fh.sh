#!/bin/bash
# One fill-handle drag (fill-handle-axis.md, run 18a): the selection made, a picture, the handle found (handle.ps1)
# inside a box, then dragged from its middle to a point.  fh.sh <name> <select: range|cols A B|rows a b> <box x0 y0 x1 y1> <to x y>
H=$(dirname "$(readlink -f "$0")"); cd "$H"
name=$1; shift
case $1 in
  cols) python3 rx.py "click $2 340" "shiftclick $3 340" >/dev/null; shift 3;;
  rows) python3 rx.py "click 20 $2" "shiftclick 20 $3" >/dev/null; shift 3;;
  range) python3 rx.py "select $2" >/dev/null; shift 2;;
  *) echo "no selection kind $1"; exit 1;;
esac
python3 rx.py 'move 3700 1900' ready "shot $name-before" >/dev/null
sq=$(./hd.sh $name-before $1 $2 $3 $4 | head -1); echo "handle: $sq"
read x0 y0 x1 y1 <<<"$(echo $sq | sed -E 's/square ([0-9]+),([0-9]+) to ([0-9]+),([0-9]+)/\1 \2 \3 \4/')"
[ -z "$x0" ] && { echo "no handle found; nothing dragged"; exit 1; }
cx=$(( (x0 + x1) / 2 )); cy=$(( (y0 + y1) / 2 ))
python3 rx.py "drag $cx $cy $5 $6" 'move 3700 1900' ready "shot $name-after" | /usr/bin/grep -v '^ok shot\|^ok move'
