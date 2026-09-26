import { describe, expect, it } from 'vitest'
import { bestReleaseScore, filterAndSortReleases } from '~/composables/useReleaseFilters'
import type { components } from '~/types/api'

type ReleaseResource = components['schemas']['ReleaseResource']

function release(overrides: Partial<ReleaseResource>): ReleaseResource {
	return {
		guid: 'guid-1',
		title: 'Harbour.Lights.S02E06.1080p.WEB.H264-GROUP',
		indexerId: 1,
		indexer: 'Stub Torznab',
		protocol: 'BITTORRENT',
		size: 2_000_000_000,
		seeders: 10,
		leechers: 2,
		publishDate: '2026-01-01T00:00:00Z',
		infoUrl: null,
		hasMagnet: false,
		indexerFlags: [],
		qualityName: 'WEBDL-1080p',
		languages: ['ENGLISH'],
		releaseGroup: 'GROUP',
		mappedSeriesId: 5,
		mappedMovieId: null,
		episodeIds: [12],
		decisions: [{ mediaVersionId: 1, approved: true, score: 10, rejections: [], customFormatScore: 0 }],
		...overrides,
	}
}

describe('useReleaseFilters', () => {
	it('bestReleaseScore prefers the highest approved decision, falling back to all decisions when none approved', () => {
		const approvedHigh = release({ decisions: [
			{ mediaVersionId: 1, approved: true, score: 5, rejections: [], customFormatScore: 0 },
			{ mediaVersionId: 2, approved: true, score: 20, rejections: [], customFormatScore: 0 },
		] })
		expect(bestReleaseScore(approvedHigh)).toBe(20)

		const noneApproved = release({ decisions: [
			{ mediaVersionId: 1, approved: false, score: 5, rejections: ['too small'], customFormatScore: 0 },
			{ mediaVersionId: 2, approved: false, score: 8, rejections: ['too small'], customFormatScore: 0 },
		] })
		expect(bestReleaseScore(noneApproved)).toBe(8)
	})

	it('filters by protocol', () => {
		const releases = [release({ guid: 'a', protocol: 'BITTORRENT' }), release({ guid: 'b', protocol: 'USENET' })]
		const filtered = filterAndSortReleases(releases, { protocol: 'USENET', indexerId: 'ALL', minSeeders: 0, sortKey: 'age' })
		expect(filtered.map(r => r.guid)).toEqual(['b'])
	})

	it('filters by indexer id', () => {
		const releases = [release({ guid: 'a', indexerId: 1 }), release({ guid: 'b', indexerId: 2 })]
		const filtered = filterAndSortReleases(releases, { protocol: 'ALL', indexerId: 2, minSeeders: 0, sortKey: 'age' })
		expect(filtered.map(r => r.guid)).toEqual(['b'])
	})

	it('minimum seeders only applies to torrents, leaving usenet releases untouched', () => {
		const releases = [
			release({ guid: 'low-seed', protocol: 'BITTORRENT', seeders: 1 }),
			release({ guid: 'high-seed', protocol: 'BITTORRENT', seeders: 50 }),
			release({ guid: 'usenet', protocol: 'USENET', seeders: null }),
		]
		const filtered = filterAndSortReleases(releases, { protocol: 'ALL', indexerId: 'ALL', minSeeders: 10, sortKey: 'age' })
		expect(filtered.map(r => r.guid).sort()).toEqual(['high-seed', 'usenet'])
	})

	it('sorts by size, seeders and score independently of the input order', () => {
		const releases = [
			release({ guid: 'small', size: 1000, seeders: 5, decisions: [{ mediaVersionId: 1, approved: true, score: 1, rejections: [], customFormatScore: 0 }] }),
			release({ guid: 'big', size: 5000, seeders: 50, decisions: [{ mediaVersionId: 1, approved: true, score: 99, rejections: [], customFormatScore: 0 }] }),
		]
		expect(filterAndSortReleases(releases, { protocol: 'ALL', indexerId: 'ALL', minSeeders: 0, sortKey: 'size' }).map(r => r.guid)).toEqual(['big', 'small'])
		expect(filterAndSortReleases(releases, { protocol: 'ALL', indexerId: 'ALL', minSeeders: 0, sortKey: 'seeders' }).map(r => r.guid)).toEqual(['big', 'small'])
		expect(filterAndSortReleases(releases, { protocol: 'ALL', indexerId: 'ALL', minSeeders: 0, sortKey: 'score' }).map(r => r.guid)).toEqual(['big', 'small'])
	})
})
