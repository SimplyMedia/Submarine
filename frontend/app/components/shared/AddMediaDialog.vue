<script setup lang="ts">
/**
 * Two-step add flow shared by series and movies: search the metadata
 * provider, pick a hit, then hand off to the caller's kind-specific form via
 * the #details slot. The caller owns the actual POST and its own busy state.
 */
import type { MediaLookupResult } from '~/types/ui'

const props = withDefaults(defineProps<{
	title: string
	searchPlaceholder?: string
	search: (term: string) => Promise<MediaLookupResult[]>
}>(), {
	searchPlaceholder: 'Search by title',
})

const emit = defineEmits<{
	pick: [result: MediaLookupResult]
	closed: []
}>()

const open = defineModel<boolean>('open', { default: false })

const term = ref('')
const results = ref<MediaLookupResult[]>([])
const searching = ref(false)
const searchError = ref('')
const picked = ref<MediaLookupResult | null>(null)
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
	searchError.value = ''
	try {
		results.value = await props.search(value)
	}
	catch {
		searchError.value = 'Search failed. Check your connection and try again.'
	}
	finally {
		searching.value = false
	}
}

function pick(result: MediaLookupResult) {
	if (result.existingId != null) {
		return
	}
	picked.value = result
	emit('pick', result)
}

function back() {
	picked.value = null
}

watch(open, (value) => {
	if (!value) {
		term.value = ''
		results.value = []
		picked.value = null
		emit('closed')
	}
})

defineExpose({ back })
</script>

<template>
	<SDialog
		v-model="open"
		:title="picked ? picked.title : title"
		wide
	>
		<template v-if="!picked">
			<SField label="Search">
				<SInput
					v-model="term"
					type="search"
					:placeholder="searchPlaceholder"
				/>
			</SField>
			<p
				v-if="searchError"
				class="s-field-error"
				role="alert"
			>
				{{ searchError }}
			</p>
			<div class="add-media-results">
				<SSpinner v-if="searching" />
				<template v-else-if="results.length > 0">
					<button
						v-for="result in results"
						:key="result.key"
						type="button"
						class="add-media-result"
						:disabled="result.existingId != null"
						@click="pick(result)"
					>
						<SPosterImage
							:src="result.posterUrl"
							:alt="result.title"
						/>
						<div class="add-media-result-body">
							<p class="add-media-result-title">
								{{ result.title }}<span v-if="result.year"> ({{ result.year }})</span>
							</p>
							<p
								v-if="result.overview"
								class="add-media-result-overview"
							>
								{{ result.overview }}
							</p>
							<SBadge
								v-if="result.existingId != null"
								tone="info"
							>
								Already in library
							</SBadge>
						</div>
					</button>
				</template>
				<SEmptyState
					v-else-if="term.trim().length >= 2 && !searching"
					message="No matches. Try a different title."
				/>
			</div>
		</template>
		<template v-else>
			<button
				type="button"
				class="add-media-back"
				@click="back"
			>
				<Icon
					name="lucide:arrow-left"
					aria-hidden="true"
				/> Back to search
			</button>
			<slot
				name="details"
				:result="picked"
			/>
		</template>
		<template
			v-if="picked"
			#footer
		>
			<slot
				name="footer"
				:result="picked"
			/>
		</template>
	</SDialog>
</template>

<style scoped>
.add-media-results {
	display: flex;
	flex-direction: column;
	gap: 4px;
	max-height: 420px;
	overflow-y: auto;
	margin-top: 12px;
}

.add-media-result {
	display: flex;
	gap: 12px;
	text-align: left;
	padding: 8px;
	border: none;
	border-radius: var(--r-control);
	background: transparent;
	cursor: pointer;
}

.add-media-result :deep(.s-poster) {
	width: 48px;
	flex: none;
	border-radius: 4px;
}

.add-media-result:hover:not(:disabled) {
	background: var(--surface-2);
}

.add-media-result:disabled {
	cursor: default;
	opacity: 0.6;
}

.add-media-result-body {
	display: flex;
	flex-direction: column;
	gap: 4px;
	min-width: 0;
}

.add-media-result-title {
	font-weight: 500;
}

.add-media-result-overview {
	font-size: var(--text-sm);
	color: var(--fg-muted);
	overflow: hidden;
	text-overflow: ellipsis;
	display: -webkit-box;
	-webkit-line-clamp: 2;
	-webkit-box-orient: vertical;
}

.add-media-back {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	border: none;
	background: transparent;
	color: var(--fg-muted);
	cursor: pointer;
	font-size: var(--text-sm);
	padding: 0 0 12px;
}

.add-media-back:hover {
	color: var(--fg);
}
</style>
