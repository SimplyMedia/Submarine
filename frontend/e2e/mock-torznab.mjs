// Stand-in Torznab indexer so the indexers/search/RSS/grab flows have real
// data to exercise end-to-end, without depending on a real tracker.
// Usage: node e2e/mock-torznab.mjs   (TORZNAB_PORT=5300)
import http from 'node:http'

const port = Number(process.env.TORZNAB_PORT ?? 5300)

const CAPS = `<?xml version="1.0" encoding="UTF-8"?>
<caps>
	<server version="1.0" title="Stub Torznab" strapline="Test tracker" url="http://localhost:${port}/" />
	<limits max="100" default="50" />
	<searching>
		<search available="yes" supportedParams="q" />
		<tv-search available="yes" supportedParams="q,season,ep,tvdbid" />
		<movie-search available="yes" supportedParams="q,imdbid,tmdbid" />
	</searching>
	<categories>
		<category id="5000" name="TV">
			<subcat id="5040" name="TV/HD" />
		</category>
		<category id="2000" name="Movies">
			<subcat id="2040" name="Movies/HD" />
		</category>
	</categories>
</caps>`

/** One release: title, size (bytes), seeders, peers, categories, an hour offset for pubDate. */
const RELEASES = [
	{ title: 'Harbour.Lights.S02E06.1080p.WEB.H264-GROUP', size: 2_100_000_000, seeders: 42, peers: 48, categories: [5000, 5040], hoursAgo: 2 },
	{ title: 'Harbour.Lights.S02.1080p.BluRay.x264-GROUP', size: 28_000_000_000, seeders: 18, peers: 22, categories: [5000, 5040], hoursAgo: 30 },
	{ title: 'Harbour.Lights.S02E06.720p.HDTV.x264-OTHER', size: 1_100_000_000, seeders: 6, peers: 9, categories: [5000], hoursAgo: 3 },
	{ title: 'Harbour.Lights.S02E06.1080p.BluRay.AVC.DTS-HD.MA.5.1-DISC', size: 44_000_000_000, seeders: 3, peers: 4, categories: [5000, 5040], hoursAgo: 50 },
	{ title: 'Harbour.Lights.S02E06.2160p.UHD.BluRay.REMUX.HDR.DTS-HD.MA.5.1-GROUP', size: 62_000_000_000, seeders: 9, peers: 11, categories: [5000, 5040], hoursAgo: 48 },
	{ title: 'Harbour.Lights.S02E06.2160p.WEB-DL.H265-GROUP', size: 4_500_000_000, seeders: 27, peers: 30, categories: [5000, 5040], hoursAgo: 1 },
	{ title: 'Harbour.Lights.S02E06.1080p.WEB.H264-SMALLSEED', size: 2_050_000_000, seeders: 1, peers: 1, categories: [5000, 5040], hoursAgo: 5 },
	{ title: 'Harbour.Lights.S02E06.XviD-SD', size: 400_000_000, seeders: 12, peers: 14, categories: [5000], hoursAgo: 60 },
]

function infoHash(title) {
	let hash = 0
	for (let i = 0; i < title.length; i++) {
		hash = (hash * 31 + title.charCodeAt(i)) >>> 0
	}
	return hash.toString(16).padStart(40, '0').slice(0, 40)
}

function feed() {
	const items = RELEASES.map((release) => {
		const pubDate = new Date(Date.now() - release.hoursAgo * 3_600_000).toUTCString()
		const hash = infoHash(release.title)
		const categoryAttrs = release.categories.map(id => `<torznab:attr name="category" value="${id}" />`).join('')
		return `<item>
			<title>${release.title}</title>
			<guid isPermaLink="false">stub-${hash}</guid>
			<link>http://localhost:${port}/download/${hash}.torrent</link>
			<comments>http://localhost:${port}/details/${hash}</comments>
			<pubDate>${pubDate}</pubDate>
			<enclosure url="http://localhost:${port}/download/${hash}.torrent" length="${release.size}" type="application/x-bittorrent" />
			<torznab:attr name="size" value="${release.size}" />
			<torznab:attr name="seeders" value="${release.seeders}" />
			<torznab:attr name="peers" value="${release.peers}" />
			<torznab:attr name="infohash" value="${hash}" />
			${categoryAttrs}
		</item>`
	}).join('\n')

	return `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed" xmlns:atom="http://www.w3.org/2005/Atom">
	<channel>
		<title>Stub Torznab</title>
		<description>Test tracker</description>
		${items}
	</channel>
</rss>`
}

function send(res, status, body, type = 'application/xml') {
	res.writeHead(status, { 'content-type': type, 'access-control-allow-origin': '*' })
	res.end(body)
}

const server = http.createServer((req, res) => {
	const url = new URL(req.url, `http://localhost:${port}`)
	if (url.pathname === '/_status/healthz') {
		return send(res, 200, JSON.stringify({ status: 'ok' }), 'application/json')
	}
	const type = url.searchParams.get('t')
	if (type === 'caps') {
		return send(res, 200, CAPS)
	}
	if (type === 'tvsearch' || type === 'search' || type === 'movie') {
		return send(res, 200, feed())
	}
	if (url.pathname.startsWith('/download/')) {
		return send(res, 200, 'fake torrent bytes', 'application/x-bittorrent')
	}
	return send(res, 404, '<error code="900" description="unknown request" />')
})

server.listen(port, () => console.log(`mock torznab on http://localhost:${port}`))
