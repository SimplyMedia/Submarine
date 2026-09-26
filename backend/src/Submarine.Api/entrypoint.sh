#!/bin/sh
# Drops privileges to PUID:PGID (default 911:911) when started as root.
set -e

PUID="${PUID:-911}"
PGID="${PGID:-911}"

if [ "$(id -u)" = "0" ]; then
	addgroup -g "$PGID" submarine 2>/dev/null || true
	adduser -D -H -u "$PUID" -G submarine submarine 2>/dev/null || true
	mkdir -p /config
	# Only walk the volume when ownership is off, a recursive chown on every start gets slow.
	if [ "$(stat -c %u:%g /config)" != "$PUID:$PGID" ]; then
		chown -R "$PUID:$PGID" /config || true
	fi
	exec su-exec "$PUID:$PGID" "$@"
fi

exec "$@"
