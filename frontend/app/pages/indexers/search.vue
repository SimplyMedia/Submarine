<script setup lang="ts">
import { navChildren } from '~/navigation'
import { toApiError, useApi } from '~/composables/useApi'
import { useIndexersStore } from '~/stores/indexers'
import { filterAndSortReleases } from '~/composables/useReleaseFilters'
import { protocolOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type ReleaseResource = components['schemas']['ReleaseResource']
type SeriesListItemDto = components['schemas']['SeriesListItemDto']
type MovieListItemDto = components['schemas']['MovieListItemDto']
type SeasonDto = components['schemas']['SeasonDto']
type EpisodeDto = components['schemas']['EpisodeDto']
type Protocol = components['schemas']['Protocol']

definePageMeta({ layout: 'default' })
useHead({ title: 'Search' })

const api = useApi()
const indexersStore = useIndexersStore()

const mode = ref<'term' | 'library'>('term')
const term = ref('')

// Library item picker
const librarySearch = ref('')
const librarySeriesResults = ref<SeriesListItemDto[]>([])
const libraryMovieResults = ref<MovieListItemDto[]>([])
const searchingLibrary = ref(false)
const selectedSeries = ref<SeriesListItemDto | null>(null)
const selectedMovie = ref<MovieListItemDto | null>(null)
const seasons = ref<SeasonDto[]>([])
const selectedSeasonNumber = ref<number | null>(null)
const episodes = ref<EpisodeDto[]>([])
const selectedEpisodeId = ref<number | null>(null)
const versionLabels = ref<Record<number, string>>({})

let librarySearchTimer: ReturnType<typeof setTimeout> | null = null

watch(librarySearch, (value) => {
	if (librarySearchTimer) {
		clearTimeout(librarySearchTimer)
	}
	const trimmed = value.trim()
	if (trimmed.length < 2) {
		librarySeriesResults.value = []
		libraryMovieResults.value = []
		return
	}
	librarySearchTimer = setTimeout(() => void searchLibrary(trimmed), 250)
})

async function searchLibrary(value: string) {
	searchingLibrary.value = true
	try {
		const [seriesResult, movieResult] = await Promise.all([
			api.GET('/api/v1/series', { params: { query: { term: value, PageSize: 10 } } }),
			api.GET('/api/v1/movies', { params: { query: { term: value, PageSize: 10 } } }),
		])
		librarySeriesResults.value = seriesResult.data?.items ?? []
		libraryMovieResults.value = movieResult.data?.items ?? []
	}
	finally {
		searchingLibrary.value = false
	}
}

async function pickSeries(series: SeriesListItemDto) {
	selectedSeries.value = series
	selectedMovie.value = null
	selectedSeasonNumber.value = null
	selectedEpisodeId.value = null
	episodes.value = []
	librarySearch.value = ''
	librarySeriesResults.value = []
	libraryMovieResults.value = []
	versionLabels.value = Object.fromEntries(series.versions.map(v => [v.id, v.name]))
	const result = await api.GET('/api/v1/series/{id}', { params: { path: { id: series.id } } })
	seasons.value = result.data?.seasons ?? []
}

async function pickMovie(movie: MovieListItemDto) {
	selectedMovie.value = movie
	selectedSeries.value = null
	seasons.value = []
	episodes.value = []
	selectedSeasonNumber.value = null
	selectedEpisodeId.value = null
	librarySearch.value = ''
	librarySeriesResults.value = []
	libraryMovieResults.value = []
	versionLabels.value = Object.fromEntries(movie.versions.map(v => [v.id, v.name]))
}

watch(selectedSeasonNumber, async (seasonNumber) => {
	selectedEpisodeId.value = null
	episodes.value = []
	if (!selectedSeries.value || seasonNumber == null) {
		return
	}
	const result = await api.GET('/api/v1/episodes', { params: { query: { seriesId: selectedSeries.value.id, seasonNumber } } })
	episodes.value = result.data ?? []
})

function clearLibrarySelection() {
	selectedSeries.value = null
	selectedMovie.value = null
	seasons.value = []
	episodes.value = []
	selectedSeasonNumber.value = null
	selectedEpisodeId.value = null
	versionLabels.value = {}
}

function switchMode(value: 'term' | 'library') {
	mode.value = value
	if (value === 'term') {
		clearLibrarySelection()
	}
}

// Search execution
const releases = ref<ReleaseResource[]>([])
const searching = ref(false)
const searchError = ref('')
const hasSearched = ref(false)

async function runSearch() {
	searching.value = true
	searchError.value = ''
	try {
		const result = await api.GET('/api/v1/search', {
			params: {
				query: {
					term: mode.value === 'term' ? (term.value || undefined) : undefined,
					seriesId: selectedSeries.value?.id,
					seasonNumber: selectedSeasonNumber.value ?? undefined,
					episodeId: selectedEpisodeId.value ?? undefined,
					movieId: selectedMovie.value?.id,
				},
			},
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		releases.value = result.data
		hasSearched.value = true
	}
	catch (error) {
		searchError.value = toApiError(error).message
	}
	finally {
		searching.value = false
	}
}

// Filters & sort
const filterProtocol = ref<Protocol | 'ALL'>('ALL')
const filterIndexerId = ref<number | 'ALL'>('ALL')
const filterMinSeeders = ref(0)
const sortKey = ref<'age' | 'size' | 'seeders' | 'score'>('age')

const indexerFilterOptions = computed(() => [
	{ value: 'ALL', label: 'All indexers' },
	...indexersStore.indexers.map(indexer => ({ value: String(indexer.id), label: indexer.name })),
])

const filteredReleases = computed(() => filterAndSortReleases(releases.value, {
	protocol: filterProtocol.value,
	indexerId: filterIndexerId.value,
	minSeeders: filterMinSeeders.value,
	sortKey: sortKey.value,
}))

onMounted(() => {
	void indexersStore.load()
})
</script>

<template>
	<div>
		<SPageHeader title="Search" />
		<SubNav
			label="Indexers"
			:items="navChildren('indexers')"
		/>

		<SSection title="Search for releases">
			<STabs
				:model-value="mode"
				:tabs="[{ value: 'term', label: 'Search by title' }, { value: 'library', label: 'Search a library item' }]"
				@update:model-value="switchMode($event as 'term' | 'library')"
			>
				<template #panel-term>
					<div class="search-term-row">
						<SInput
							v-model="term"
							type="search"
							placeholder="Release title, e.g. Harbour Lights S02E06"
							@keydown.enter="runSearch"
						/>
						<SButton
							variant="primary"
							:loading="searching"
							@click="runSearch"
						>
							Search
						</SButton>
					</div>
				</template>
				<template #panel-library>
					<div
						v-if="!selectedSeries && !selectedMovie"
						class="library-picker"
					>
						<SInput
							v-model="librarySearch"
							type="search"
							placeholder="Search your series and movies"
						/>
						<ul
							v-if="librarySeriesResults.length > 0 || libraryMovieResults.length > 0"
							class="library-results"
						>
							<li
								v-for="series in librarySeriesResults"
								:key="`series-${series.id}`"
								class="library-result-row"
								@click="pickSeries(series)"
							>
								<SBadge tone="info">
									Series
								</SBadge>
								<span>{{ series.title }}<span v-if="series.year"> ({{ series.year }})</span></span>
							</li>
							<li
								v-for="movie in libraryMovieResults"
								:key="`movie-${movie.id}`"
								class="library-result-row"
								@click="pickMovie(movie)"
							>
								<SBadge tone="ok">
									Movie
								</SBadge>
								<span>{{ movie.title }}<span v-if="movie.year"> ({{ movie.year }})</span></span>
							</li>
						</ul>
					</div>
					<div
						v-else
						class="library-selection"
					>
						<div class="library-selection-row">
							<span class="library-selection-title">{{ selectedSeries?.title ?? selectedMovie?.title }}</span>
							<SButton
								variant="ghost"
								size="sm"
								@click="clearLibrarySelection"
							>
								Change
							</SButton>
						</div>
						<div
							v-if="selectedSeries"
							class="field-grid"
						>
							<SField label="Season">
								<SSelect
									:model-value="selectedSeasonNumber == null ? 'all' : String(selectedSeasonNumber)"
									:options="[{ value: 'all', label: 'Whole series' }, ...seasons.map(s => ({ value: String(s.seasonNumber), label: s.seasonNumber === 0 ? 'Specials' : `Season ${s.seasonNumber}` }))]"
									@update:model-value="selectedSeasonNumber = $event === 'all' ? null : Number($event)"
								/>
							</SField>
							<SField
								v-if="selectedSeasonNumber != null"
								label="Episode"
							>
								<SSelect
									:model-value="selectedEpisodeId == null ? 'all' : String(selectedEpisodeId)"
									:options="[{ value: 'all', label: 'Whole season' }, ...episodes.map(e => ({ value: String(e.id), label: `${e.episodeNumber}. ${e.title ?? ''}` }))]"
									@update:model-value="selectedEpisodeId = $event === 'all' ? null : Number($event)"
								/>
							</SField>
						</div>
						<SButton
							variant="primary"
							:loading="searching"
							@click="runSearch"
						>
							Search
						</SButton>
					</div>
				</template>
			</STabs>
		</SSection>

		<SSection
			v-if="hasSearched"
			title="Results"
		>
			<div class="search-filters">
				<SField
					label="Protocol"
					control-id="protocol-filter"
				>
					<SSelect
						:model-value="filterProtocol"
						control-id="protocol-filter"
						:options="[{ value: 'ALL', label: 'All protocols' }, ...protocolOptions]"
						@update:model-value="filterProtocol = $event as Protocol | 'ALL'"
					/>
				</SField>
				<SField
					label="Indexer"
					control-id="indexer-filter"
				>
					<SSelect
						:model-value="String(filterIndexerId)"
						control-id="indexer-filter"
						:options="indexerFilterOptions"
						@update:model-value="filterIndexerId = $event === 'ALL' ? 'ALL' : Number($event)"
					/>
				</SField>
				<SField
					label="Min seeders"
					control-id="min-seeders-filter"
				>
					<SInput
						id="min-seeders-filter"
						type="number"
						:model-value="String(filterMinSeeders)"
						@update:model-value="filterMinSeeders = Number($event) || 0"
					/>
				</SField>
				<SField
					label="Sort by"
					control-id="sort-filter"
				>
					<SSelect
						:model-value="sortKey"
						control-id="sort-filter"
						:options="[{ value: 'age', label: 'Age' }, { value: 'size', label: 'Size' }, { value: 'seeders', label: 'Seeders' }, { value: 'score', label: 'Score' }]"
						@update:model-value="sortKey = $event as 'age' | 'size' | 'seeders' | 'score'"
					/>
				</SField>
			</div>

			<SSpinner v-if="searching" />
			<p
				v-else-if="searchError"
				class="s-field-error"
				role="alert"
			>
				{{ searchError }}
			</p>
			<ReleaseTable
				v-else
				:releases="filteredReleases"
				:version-labels="versionLabels"
			/>
		</SSection>
	</div>
</template>

<style scoped>
.search-term-row {
	display: flex;
	gap: 12px;
}

.search-term-row .s-input,
.search-term-row > :first-child {
	flex: 1;
}

.library-picker {
	position: relative;
}

.library-results {
	margin-top: 8px;
	display: flex;
	flex-direction: column;
	gap: 4px;
	max-height: 280px;
	overflow-y: auto;
}

.library-result-row {
	display: flex;
	align-items: center;
	gap: 8px;
	padding: 8px 12px;
	border: 1px solid var(--line);
	border-radius: var(--r-control);
	cursor: pointer;
}

.library-result-row:hover {
	background: var(--surface-2);
}

.library-selection {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.library-selection-row {
	display: flex;
	align-items: center;
	gap: 12px;
}

.library-selection-title {
	font-weight: 500;
}

.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
	gap: 16px;
}

.search-filters {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(160px, 200px));
	gap: 12px;
	margin-bottom: 16px;
}
</style>
