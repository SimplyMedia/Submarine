<script setup lang="ts">
/**
 * Re-search the metadata provider for a library import row's proposed match.
 * Debounced search against /api/v1/series/lookup or /api/v1/movies/lookup,
 * depending on the root folder's media kind.
 */
import type { components } from '~/types/api'

type SearchResultResource = components['schemas']['SearchResultResource']

const props = defineProps<{
	kind: 'series' | 'movie'
}>()

const model = defineModel<SearchResultResource | null>({ default: null })

const api = useApi()
const term = ref('')
const results = ref<SearchResultResource[]>([])
const searching = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | undefined

watch(term, (value) => {
	clearTimeout(debounceTimer)
	if (value.trim().length < 2) {
		results.value = []
		return
	}
	debounceTimer = setTimeout(() => void runSearch(value.trim()), 350)
})

async function runSearch(value: string) {
	searching.value = true
	try {
		const result = props.kind === 'series'
			? await api.GET('/api/v1/series/lookup', { params: { query: { term: value } } })
			: await api.GET('/api/v1/movies/lookup', { params: { query: { term: value } } })
		results.value = result.data ?? []
	}
	finally {
		searching.value = false
	}
}

function pick(result: SearchResultResource) {
	model.value = result
	term.value = ''
	results.value = []
}
</script>

<template>
	<SPopover>
		<template #trigger>
			<button
				type="button"
				class="match-picker-trigger"
			>
				<SPosterImage
					:src="model?.posterUrl"
					:alt="model?.title ?? 'No match'"
				/>
				<span class="match-picker-title">
					<template v-if="model">{{ model.title }}<span v-if="model.year"> ({{ model.year }})</span></template>
					<span
						v-else
						class="s-cell-muted"
					>No match — search</span>
				</span>
				<Icon
					name="lucide:chevron-down"
					aria-hidden="true"
				/>
			</button>
		</template>
		<div class="match-picker-content">
			<SInput
				v-model="term"
				type="search"
				placeholder="Search by title"
			/>
			<SSpinner v-if="searching" />
			<ul
				v-else
				class="match-picker-list"
			>
				<li
					v-for="result in results"
					:key="`${result.tvdbId ?? ''}-${result.tmdbId ?? ''}-${result.title}`"
				>
					<button
						type="button"
						class="match-picker-option"
						@click="pick(result)"
					>
						<SPosterImage
							:src="result.posterUrl"
							:alt="result.title"
						/>
						<span>{{ result.title }}<span v-if="result.year"> ({{ result.year }})</span></span>
					</button>
				</li>
				<li
					v-if="term.trim().length >= 2 && results.length === 0"
					class="s-cell-muted match-picker-empty"
				>
					No matches
				</li>
			</ul>
		</div>
	</SPopover>
</template>

<style scoped>
.match-picker-trigger {
	display: flex;
	align-items: center;
	gap: 8px;
	width: 260px;
	padding: 6px 10px;
	border: 1px solid var(--line-strong);
	border-radius: var(--r-control);
	background: var(--surface);
	cursor: pointer;
	text-align: left;
}

.match-picker-trigger :deep(.s-poster) {
	width: 32px;
	height: 48px;
	flex: none;
}

.match-picker-title {
	flex: 1;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
	font-size: 0.875rem;
}

.match-picker-content {
	display: flex;
	flex-direction: column;
	gap: 8px;
	width: 280px;
}

.match-picker-list {
	display: flex;
	flex-direction: column;
	gap: 2px;
	max-height: 280px;
	overflow-y: auto;
}

.match-picker-option {
	display: flex;
	align-items: center;
	gap: 8px;
	width: 100%;
	text-align: left;
	padding: 6px 8px;
	border: 0;
	border-radius: var(--r-control);
	background: transparent;
	font-size: 0.875rem;
	color: var(--fg);
	cursor: pointer;
}

.match-picker-option:hover {
	background: var(--surface-2);
}

.match-picker-option :deep(.s-poster) {
	width: 28px;
	height: 42px;
	flex: none;
}

.match-picker-empty {
	padding: 6px 8px;
	font-size: 0.8125rem;
}
</style>
