#!/bin/sh
# Drops privileges to PUID:PGID (default 911:911) when started as root.
set -e

PUID="${PUID:-911}"
PGID="${PGID:-911}"

if [ "$(id -u)" = "0" ]; then
	addgroup -g "$PGID" app 2>/dev/null || true
	adduser -D -H -u "$PUID" -G app app 2>/dev/null || true
	mkdir -p /config
	chown -R "$PUID:$PGID" /config || true
	exec su-exec "$PUID:$PGID" "$@"
fi

exec "$@"
