#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
missing=0

while IFS= read -r -d '' file; do
  if ! head -n 2 "$file" | grep -Fq "// Copyright © 2026 Hunter Luisi. All rights reserved." || \
     ! head -n 2 "$file" | grep -Fq "// Origin: RaveSystem.Osc; adapted for PenroseArt"; then
    printf 'Missing OSC header: %s\n' "${file#"$repo_root/"}" >&2
    missing=1
  fi
done < <(find "$repo_root/Assets/OSC" -type f -name '*.cs' -print0 | sort -z)

readme="$repo_root/Assets/OSC/README.md"
if [ -f "$readme" ]; then
  if ! head -n 3 "$readme" | grep -Fq "Copyright © 2026 Hunter Luisi. All rights reserved." || \
     ! head -n 4 "$readme" | grep -Fq "Origin: RaveSystem.Osc; adapted for PenroseArt"; then
    printf 'Missing OSC README header: Assets/OSC/README.md\n' >&2
    missing=1
  fi
fi

if [ "$missing" -ne 0 ]; then
  exit 1
fi

printf 'OSC headers OK.\n'
