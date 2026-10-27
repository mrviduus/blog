#!/usr/bin/env bash
# Records demo.gif: workers A and B side by side. Needs vhs, ffmpeg, and the skip-lab Postgres.
set -euo pipefail
cd "$(dirname "$0")"
docker exec skip-lab psql -U postgres -qc "UPDATE jobs SET status = 'queued', attempt = 0, started_at = NULL, lease_until = NULL;"
dotnet build -v q ../worker-cs/src/App > /dev/null
vhs A.tape & vhs B.tape & wait
ffmpeg -y -loglevel error -i A.mp4 -i B.mp4 -filter_complex \
  "[0]pad=iw+2:ih:0:0:color=0x555555[l];[l][1]hstack,fps=10,split[a][b];[a]palettegen=max_colors=64[p];[b][p]paletteuse" demo.gif
rm A.mp4 B.mp4
docker exec skip-lab psql -U postgres -c "SELECT id, status, attempt FROM jobs ORDER BY id;"
