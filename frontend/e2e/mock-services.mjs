// Stand-in for the Metadata (Skyhook alternative) and Mappings (TheXEM alternative)
// services so the API can be exercised offline in development and end-to-end tests.
// Usage: node e2e/mock-services.mjs   (METADATA_PORT=5100, MAPPINGS_PORT=5200)
import http from 'node:http'

const metadataPort = Number(process.env.METADATA_PORT ?? 5100)
const mappingsPort = Number(process.env.MAPPINGS_PORT ?? 5200)

const palette = ['#137F73', '#2F6FD1', '#B77A12', '#7A4FB3', '#C7423E', '#3B7D5C', '#8A6A3A', '#4C6B8A']

function poster(id, title) {
	const color = palette[id % palette.length]
	const words = title.split(' ')
	const lines = []
	let line = ''
	for (const word of words) {
		if ((line + ' ' + word).trim().length > 14) {
			lines.push(line.trim())
			line = word
		}
		else {
			line = `${line} ${word}`
		}
	}
	lines.push(line.trim())
	const text = lines.slice(0, 4).map((l, i) => `<text x="20" y="${300 + i * 40}" font-family="sans-serif" font-size="30" fill="#fff">${l.replace(/&/g, '&amp;')}</text>`).join('')
	return `<svg xmlns="http://www.w3.org/2000/svg" width="400" height="600"><rect width="400" height="600" fill="${color}"/><rect x="20" y="20" width="360" height="240" fill="rgba(255,255,255,.12)"/>${text}</svg>`
}

function backdrop(id) {
	const color = palette[(id + 3) % palette.length]
	return `<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="720"><defs><linearGradient id="g" x1="0" x2="1"><stop offset="0" stop-color="${color}"/><stop offset="1" stop-color="#0E1620"/></linearGradient></defs><rect width="1280" height="720" fill="url(#g)"/></svg>`
}

const today = new Date()
const iso = d => d.toISOString().slice(0, 10)
const daysFromNow = n => iso(new Date(today.getTime() + n * 86400000))

function makeSeries({ tvdbId, tmdbId, title, year, status, network, seasons, firstAiredOffset, genres, anime = false }) {
	const episodes = []
	const seasonResources = []
	let absolute = 0
	for (let s = 1; s <= seasons; s++) {
		const count = 10
		seasonResources.push({ seasonNumber: s, name: `Season ${s}`, episodeCount: count })
		for (let e = 1; e <= count; e++) {
			absolute += 1
			// Newest season airs around today so the calendar and wanted views have content.
			const offset = s === seasons ? (e - 6) * 7 : firstAiredOffset + ((s - 1) * count + e) * 7
			const numbers = [{ ordering: 'AIRED', seasonNumber: s, number: e, absoluteNumber: null }]
			if (anime) {
				numbers.push({ ordering: 'ABSOLUTE', seasonNumber: null, number: null, absoluteNumber: absolute })
			}
			episodes.push({
				tvdbId: tvdbId * 1000 + s * 100 + e,
				tmdbId: null,
				title: `Episode ${e}`,
				overview: `Season ${s}, episode ${e} of ${title}.`,
				airDate: daysFromNow(offset),
				airDateUtc: daysFromNow(offset),
				runtime: 45,
				numbers,
				imageUrl: null,
			})
		}
	}
	return {
		tvdbId,
		tmdbId,
		imdbId: `tt${String(tvdbId).padStart(7, '0')}`,
		title,
		sortTitle: title.toLowerCase().replace(/^(the|a|an) /, ''),
		overview: `${title} is a ${genres[0].toLowerCase()} series on ${network}.`,
		firstAired: daysFromNow(firstAiredOffset),
		status,
		runtime: 45,
		network,
		genres,
		certification: 'TV-14',
		seasons: seasonResources,
		episodes,
		posterUrl: `http://localhost:${metadataPort}/img/poster/${tvdbId}.svg`,
		backdropUrl: `http://localhost:${metadataPort}/img/backdrop/${tvdbId}.svg`,
		year,
		alternateTitles: [],
	}
}

const series = [
	makeSeries({ tvdbId: 81189, tmdbId: 1396, title: 'Breaking Point', year: 2019, status: 'CONTINUING', network: 'AMC', seasons: 3, firstAiredOffset: -900, genres: ['Drama', 'Crime'] }),
	makeSeries({ tvdbId: 121361, tmdbId: 1399, title: 'Harbour Lights', year: 2021, status: 'CONTINUING', network: 'HBO', seasons: 2, firstAiredOffset: -600, genres: ['Drama', 'Fantasy'] }),
	makeSeries({ tvdbId: 305288, tmdbId: 66732, title: 'North of North Island', year: 2022, status: 'ENDED', network: 'Netflix', seasons: 4, firstAiredOffset: -1400, genres: ['Mystery', 'Science Fiction'] }),
	makeSeries({ tvdbId: 289882, tmdbId: 65930, title: 'Deep Current', year: 2023, status: 'CONTINUING', network: 'Crunchyroll', seasons: 2, firstAiredOffset: -300, genres: ['Animation', 'Action'], anime: true }),
	makeSeries({ tvdbId: 73739, tmdbId: 4607, title: 'The Trawler', year: 2018, status: 'ENDED', network: 'ABC', seasons: 1, firstAiredOffset: -2000, genres: ['Adventure', 'Drama'] }),
	makeSeries({ tvdbId: 328487, tmdbId: 71712, title: 'Sonar', year: 2024, status: 'UPCOMING', network: 'Apple TV+', seasons: 1, firstAiredOffset: 20, genres: ['Thriller'] }),
]

function makeMovie({ tmdbId, imdbId, title, year, status, offsets, genres, studio, collection }) {
	return {
		tmdbId,
		imdbId,
		title,
		originalTitle: title,
		sortTitle: title.toLowerCase().replace(/^(the|a|an) /, ''),
		overview: `${title} (${year}) is a ${genres[0].toLowerCase()} film from ${studio}.`,
		inCinemasDate: offsets.cinema == null ? null : daysFromNow(offsets.cinema),
		digitalReleaseDate: offsets.digital == null ? null : daysFromNow(offsets.digital),
		physicalReleaseDate: offsets.physical == null ? null : daysFromNow(offsets.physical),
		status,
		year,
		runtime: 118,
		genres,
		studio,
		certification: 'PG-13',
		posterUrl: `http://localhost:${metadataPort}/img/poster/${tmdbId}.svg`,
		backdropUrl: `http://localhost:${metadataPort}/img/backdrop/${tmdbId}.svg`,
		youTubeTrailerId: null,
		tmdbCollectionId: collection?.id ?? null,
		collectionTitle: collection?.title ?? null,
		alternateTitles: [],
	}
}

const collectionAbyss = { id: 9001, title: 'Abyss collection' }
const movies = [
	makeMovie({ tmdbId: 27205, imdbId: 'tt1375666', title: 'Into the Abyss', year: 2020, status: 'RELEASED', offsets: { cinema: -700, digital: -600, physical: -550 }, genres: ['Science Fiction', 'Thriller'], studio: 'Periscope Pictures', collection: collectionAbyss }),
	makeMovie({ tmdbId: 27206, imdbId: 'tt1375667', title: 'Into the Abyss: Descent', year: 2023, status: 'RELEASED', offsets: { cinema: -200, digital: -100, physical: -60 }, genres: ['Science Fiction', 'Thriller'], studio: 'Periscope Pictures', collection: collectionAbyss }),
	makeMovie({ tmdbId: 27207, imdbId: 'tt1375668', title: 'Into the Abyss: Surface', year: 2026, status: 'ANNOUNCED', offsets: { cinema: 120, digital: null, physical: null }, genres: ['Science Fiction', 'Thriller'], studio: 'Periscope Pictures', collection: collectionAbyss }),
	makeMovie({ tmdbId: 603, imdbId: 'tt0133093', title: 'Ballast', year: 2022, status: 'RELEASED', offsets: { cinema: -400, digital: -300, physical: -250 }, genres: ['Drama'], studio: 'Keel Films' }),
	makeMovie({ tmdbId: 550, imdbId: 'tt0137523', title: 'Periscope Depth', year: 2025, status: 'IN_CINEMAS', offsets: { cinema: -20, digital: 40, physical: 90 }, genres: ['Action', 'Adventure'], studio: 'Keel Films' }),
	makeMovie({ tmdbId: 13, imdbId: 'tt0109830', title: 'The Long Dive', year: 2021, status: 'RELEASED', offsets: { cinema: -500, digital: -420, physical: -400 }, genres: ['Documentary'], studio: 'Blue Water' }),
	makeMovie({ tmdbId: 680, imdbId: 'tt0110912', title: 'Hull Breach', year: 2024, status: 'RELEASED', offsets: { cinema: -90, digital: -10, physical: 30 }, genres: ['Horror', 'Thriller'], studio: 'Blue Water' }),
	makeMovie({ tmdbId: 155, imdbId: 'tt0468569', title: 'Quiet Running', year: 2027, status: 'ANNOUNCED', offsets: { cinema: 300, digital: null, physical: null }, genres: ['Drama'], studio: 'Periscope Pictures' }),
]

const searchResult = (item, provider) => ({
	tvdbId: item.tvdbId ?? null,
	tmdbId: item.tmdbId ?? null,
	imdbId: item.imdbId ?? null,
	title: item.title,
	year: item.year,
	overview: item.overview,
	posterUrl: item.posterUrl,
	status: item.status,
	provider,
})

function matches(term, item) {
	const t = term.trim().toLowerCase()
	if (!t) return true
	if (t.startsWith('tvdb:')) return String(item.tvdbId) === t.slice(5)
	if (t.startsWith('tmdb:')) return String(item.tmdbId) === t.slice(5)
	if (t.startsWith('imdb:')) return item.imdbId === t.slice(5)
	return item.title.toLowerCase().includes(t)
}

function send(res, status, body, type = 'application/json') {
	res.writeHead(status, { 'content-type': type, 'access-control-allow-origin': '*' })
	res.end(typeof body === 'string' ? body : JSON.stringify(body))
}

function problem(res, status, title) {
	send(res, status, { type: 'about:blank', title, status }, 'application/problem+json')
}

const metadata = http.createServer((req, res) => {
	const url = new URL(req.url, `http://localhost:${metadataPort}`)
	const p = url.pathname
	let m
	if (p === '/_status/healthz') return send(res, 200, { status: 'ok' })
	if ((m = p.match(/^\/img\/poster\/(\d+)\.svg$/))) {
		const id = Number(m[1])
		const item = series.find(s => s.tvdbId === id || s.tmdbId === id) ?? movies.find(x => x.tmdbId === id)
		return send(res, 200, poster(id, item?.title ?? `#${id}`), 'image/svg+xml')
	}
	if ((m = p.match(/^\/img\/backdrop\/(\d+)\.svg$/))) return send(res, 200, backdrop(Number(m[1])), 'image/svg+xml')
	if (p === '/api/v1/series/search') {
		const provider = url.searchParams.get('provider') ?? 'tvdb'
		return send(res, 200, series.filter(s => matches(url.searchParams.get('term') ?? '', s)).map(s => searchResult(s, provider)))
	}
	if (p === '/api/v1/series/popular') return send(res, 200, series.map(s => searchResult(s, 'tmdb')))
	if ((m = p.match(/^\/api\/v1\/series\/tvdb\/(\d+)$/))) {
		const item = series.find(s => s.tvdbId === Number(m[1]))
		return item ? send(res, 200, item) : problem(res, 404, 'Series not found')
	}
	if ((m = p.match(/^\/api\/v1\/series\/tmdb\/(\d+)$/))) {
		const item = series.find(s => s.tmdbId === Number(m[1]))
		return item ? send(res, 200, item) : problem(res, 404, 'Series not found')
	}
	if (p === '/api/v1/movie/search') {
		const year = url.searchParams.get('year')
		return send(res, 200, movies.filter(x => matches(url.searchParams.get('term') ?? '', x) && (!year || String(x.year) === year)).map(x => searchResult(x, 'tmdb')))
	}
	if (p === '/api/v1/movie/popular') return send(res, 200, movies.map(x => searchResult(x, 'tmdb')))
	if (p.match(/^\/api\/v1\/movie\/list\/\d+$/)) return send(res, 200, movies.slice(0, 4).map(x => searchResult(x, 'tmdb')))
	if (/^\/api\/v1\/movie\/person\/\d+$/.test(p)) return send(res, 200, movies.slice(2, 6).map(x => searchResult(x, 'tmdb')))
	if ((m = p.match(/^\/api\/v1\/movie\/imdb\/(tt\d+)$/))) {
		const item = movies.find(x => x.imdbId === m[1])
		return item ? send(res, 200, item) : problem(res, 404, 'Movie not found')
	}
	if ((m = p.match(/^\/api\/v1\/movie\/(\d+)$/))) {
		const item = movies.find(x => x.tmdbId === Number(m[1]))
		return item ? send(res, 200, item) : problem(res, 404, 'Movie not found')
	}
	if ((m = p.match(/^\/api\/v1\/collection\/(\d+)$/))) {
		const id = Number(m[1])
		if (id !== collectionAbyss.id) return problem(res, 404, 'Collection not found')
		return send(res, 200, {
			tmdbCollectionId: id,
			title: collectionAbyss.title,
			overview: 'Three dives, one abyss.',
			posterUrl: `http://localhost:${metadataPort}/img/poster/27205.svg`,
			movies: movies.filter(x => x.tmdbCollectionId === id),
		})
	}
	return problem(res, 404, 'Not found')
})

const mappings = http.createServer((req, res) => {
	const url = new URL(req.url, `http://localhost:${mappingsPort}`)
	const p = url.pathname
	let m
	if (p === '/_status/healthz') return send(res, 200, { status: 'ok' })
	if ((m = p.match(/^\/api\/v1\/scene\/(\d+)$/))) return send(res, 200, { tvdbId: Number(m[1]), mappings: [], episodeMappings: [] })
	if (/^\/api\/v1\/scene\/\d+\/resolve$/.test(p)) return send(res, 200, { sceneSeason: Number(url.searchParams.get('season')), sceneEpisode: Number(url.searchParams.get('episode')) })
	if ((m = p.match(/^\/api\/v1\/scene\/(\d+)\/resolve-tvdb$/))) return send(res, 200, { tvdbId: Number(m[1]), season: Number(url.searchParams.get('sceneSeason')), episode: Number(url.searchParams.get('sceneEpisode')), absoluteEpisode: null })
	if (p.startsWith('/api/v1/scene/names/')) return send(res, 200, [])
	if (p === '/api/v1/scene/search') return send(res, 200, [])
	if (/^\/api\/v1\/tvdb\/\d+\/anilist$/.test(p)) return send(res, 200, [])
	if (p.match(/^\/api\/v1\/tvdb\/\d+\/anilist\/resolve$/)) return problem(res, 404, 'No mapping')
	if (p.match(/^\/api\/v1\/anilist\/\d+/)) return problem(res, 404, 'No mapping')
	return problem(res, 404, 'Not found')
})

metadata.listen(metadataPort, () => console.log(`mock metadata on http://localhost:${metadataPort}`))
mappings.listen(mappingsPort, () => console.log(`mock mappings on http://localhost:${mappingsPort}`))
