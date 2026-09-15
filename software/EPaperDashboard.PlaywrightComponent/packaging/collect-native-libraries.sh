#!/usr/bin/env bash
set -euo pipefail

component_root="${1:?component root is required}"
native_root="$component_root/native"
library_root="$native_root/lib"
license_root="$native_root/licenses"

mkdir -p "$library_root" "$license_root" "$native_root/share/glib-2.0" "$native_root/fontconfig/fonts"

# Collect the complete transitive ELF dependency set used by the downloaded browser. Flattening
# by soname lets LD_LIBRARY_PATH provide an isolated runtime independent of the base container.
find "$component_root/browsers" "$component_root/worker/.playwright/node" -type f -print0 |
  while IFS= read -r -d '' candidate; do
    if file "$candidate" | grep -q 'ELF'; then
      ldd "$candidate" 2>/dev/null | awk '/=> \/|^\// { for (i = 1; i <= NF; i++) if ($i ~ /^\//) print $i }' || true
    fi
  done | sort -u > /tmp/playwright-native-libraries.txt

while IFS= read -r library; do
  case "$library" in
    "$component_root"/*) continue ;;
  esac
  cp -L --preserve=mode,timestamps "$library" "$library_root/$(basename "$library")"

  package_name="$(dpkg-query -S "$library" 2>/dev/null | head -n 1 | cut -d: -f1 || true)"
  if [ -n "$package_name" ] && [ -f "/usr/share/doc/$package_name/copyright" ]; then
    cp "/usr/share/doc/$package_name/copyright" "$license_root/$package_name.txt"
  fi
done < /tmp/playwright-native-libraries.txt

# Chromium loads GTK/GLib data and fonts at runtime rather than through ELF linkage.
if [ -d /usr/share/glib-2.0/schemas ]; then
  cp -a /usr/share/glib-2.0/schemas "$native_root/share/glib-2.0/"
fi
if [ -d /usr/share/fonts/truetype/liberation ]; then
  cp -a /usr/share/fonts/truetype/liberation/. "$native_root/fontconfig/fonts/"
fi
if [ -d /usr/share/fonts/truetype/liberation2 ]; then
  cp -a /usr/share/fonts/truetype/liberation2/. "$native_root/fontconfig/fonts/"
fi

for package_name in fonts-liberation libglib2.0-0t64; do
  if [ -f "/usr/share/doc/$package_name/copyright" ]; then
    cp "/usr/share/doc/$package_name/copyright" "$license_root/$package_name.txt"
  fi
done

cp /src/packaging/fonts.conf "$native_root/fontconfig/fonts.conf"
