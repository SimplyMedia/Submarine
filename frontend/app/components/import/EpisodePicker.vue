<script setup lang="ts">
/**
 * Multi-select of a series' episodes, grouped by season, for a manual import
 * row. Fetches the series' episodes whenever `seriesId` changes.
 */
import { useI18n } from 'vue-i18n'
import type { components } from '~/types/api'

type EpisodeDto = components['schemas']['EpisodeDto']

const props = defineProps<{
	seriesId: number | null
}>()

const selected = defineModel<number[]>({ default: () => [] })

const api = useApi()
const { t } = useI18n()
const episodes = ref<EpisodeDto[]>([])
const loading = ref(false)

watch(() => props.seriesId, async (seriesId) => {
	episodes.value = []
	if (seriesId == null) {
		return
	}
	loading.value = true
	try {
		const result = await api.GET('/api/v1/episodes', { params: { query: { seriesId } } })
		episodes.value = result.data ?? []
	}
	finally {
		loading.value = false
	}
}, { immediate: true })

const seasons = computed(() => {
	const bySeason = new Map<number, EpisodeDto[]>()
	for (const episode of episodes.value) {
		const list = bySeason.get(episode.seasonNumber) ?? []
		list.push(episode)
		bySeason.set(episode.seasonNumber, list)
	}
	return [...bySeason.entries()].sort(([a], [b]) => a - b)
})

function toggle(id: number, value: boolean) {
	selected.value = value ? [...selected.value, id] : selected.value.filter(existing => existing !== id)
}

const summary = computed(() => {
	if (props.seriesId == null) {
		return t('components.import.EpisodePicker.pickSeriesFirst')
	}
	if (selected.value.length === 0) {
		return t('components.import.EpisodePicker.noEpisodes')
	}
	if (selected.value.length === 1) {
		const episode = episodes.value.find(x => x.id === selected.value[0])
		return episode ? `S${String(episode.seasonNumber).padStart(2, '0')}E${String(episode.episodeNumber).padStart(2, '0')}` : t('components.import.EpisodePicker.oneEpisode')
	}
	return t('components.import.EpisodePicker.episodes', { count: selected.value.length })
})
</script>

<template>
	<SPopover>
		<template #trigger>
			<button
				type="button"
				class="s-input episode-picker-trigger"
				:disabled="seriesId == null"
			>
				<span :class="{ 's-cell-muted': selected.length === 0 }">{{ summary }}</span>
				<Icon
					name="lucide:chevron-down"
					aria-hidden="true"
				/>
			</button>
		</template>
		<div class="episode-picker-content">
			<SSpinner v-if="loading" />
			<SEmptyState
				v-else-if="episodes.length === 0"
				:message="$t('components.import.EpisodePicker.noEpisodesFound')"
			/>
			<div
				v-else
				class="episode-picker-seasons"
			>
				<div
					v-for="[seasonNumber, seasonEpisodes] in seasons"
					:key="seasonNumber"
					class="episode-picker-season"
				>
					<p class="episode-picker-season-label">
						{{ seasonNumber === 0 ? $t('components.import.EpisodePicker.specials') : $t('components.import.EpisodePicker.seasonNumber', { number: seasonNumber }) }}
					</p>
					<SCheckbox
						v-for="episode in seasonEpisodes"
						:key="episode.id"
						:model-value="selected.includes(episode.id)"
						:label="`${String(episode.episodeNumber).padStart(2, '0')} — ${episode.title ?? $t('components.import.EpisodePicker.tba')}`"
						@update:model-value="value => toggle(episode.id, value)"
					/>
				</div>
			</div>
		</div>
	</SPopover>
</template>

<style scoped>
.episode-picker-trigger {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 8px;
	width: 220px;
	cursor: pointer;
}

.episode-picker-trigger:disabled {
	cursor: default;
}

.episode-picker-content {
	width: 260px;
	max-height: 320px;
	overflow-y: auto;
}

.episode-picker-seasons {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.episode-picker-season {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.episode-picker-season-label {
	font-size: 0.75rem;
	font-weight: 600;
	color: var(--fg-muted);
	text-transform: uppercase;
	letter-spacing: 0.02em;
}
</style>
