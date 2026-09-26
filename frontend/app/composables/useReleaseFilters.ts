import type { components } from '~/types/api'

type ReleaseResource = components['schemas']['ReleaseResource']
type Protocol = components['schemas']['Protocol']

export interface ReleaseFilters {
	protocol: Protocol | 'ALL'
	indexerId: number | 'ALL'
	minSeeders: number
	sortKey: 'age' | 'size' | 'seeders' | 'score'
}

/** Best score among a release's approved decisions, falling back to all decisions when none are approved. */
export function bestReleaseScore(release: ReleaseResource): number {
	const approved = release.decisions.filter(decision => decision.approved)
	const pool = approved.length > 0 ? approved : release.decisions
	return pool.reduce((max, decision) => Math.max(max, decision.score), Number.NEGATIVE_INFINITY)
}

/** Filters by protocol/indexer/minimum seeders, then sorts by the chosen key, newest/largest/most first. */
export function filterAndSortReleases(releases: ReleaseResource[], filters: ReleaseFilters): ReleaseResource[] {
	let list = releases
	if (filters.protocol !== 'ALL') {
		list = list.filter(release => release.protocol === filters.protocol)
	}
	if (filters.indexerId !== 'ALL') {
		list = list.filter(release => release.indexerId === filters.indexerId)
	}
	if (filters.minSeeders > 0) {
		list = list.filter(release => release.protocol !== 'BITTORRENT' || (release.seeders ?? 0) >= filters.minSeeders)
	}

	const sorted = [...list]
	switch (filters.sortKey) {
		case 'age':
			sorted.sort((a, b) => (b.publishDate ?? '').localeCompare(a.publishDate ?? ''))
			break
		case 'size':
			sorted.sort((a, b) => (b.size ?? 0) - (a.size ?? 0))
			break
		case 'seeders':
			sorted.sort((a, b) => (b.seeders ?? 0) - (a.seeders ?? 0))
			break
		case 'score':
			sorted.sort((a, b) => bestReleaseScore(b) - bestReleaseScore(a))
			break
	}
	return sorted
}
