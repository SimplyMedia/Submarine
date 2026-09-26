<script setup lang="ts">
/**
 * Manual search scoped to one series/season/episode or movie, opened from
 * the detail pages' "Interactive search" action. Wraps ReleaseTable, which
 * owns the grab flow; `versionLabels` should be the caller's already-loaded
 * media versions so results show real names instead of "Version {id}".
 */
import { toApiError, useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

const props = withDefaults(defineProps<{
	seriesId?: number
	seasonNumber?: number
	episodeId?: number
	movieId?: number
	versionLabels?: Record<number, string>
}>(), {
	seriesId: undefined,
	seasonNumber: undefined,
	episodeId: undefined,
	movieId: undefined,
	versionLabels: () => ({}),
})

const open = defineModel<boolean>({ default: false })

type ReleaseResource = components['schemas']['ReleaseResource']

const releases = ref<ReleaseResource[]>([])
const loading = ref(false)
const loadError = ref('')

async function search() {
	loading.value = true
	loadError.value = ''
	try {
		const api = useApi()
		const result = await api.GET('/api/v1/search', {
			params: {
				query: {
					seriesId: props.seriesId,
					seasonNumber: props.seasonNumber,
					episodeId: props.episodeId,
					movieId: props.movieId,
				},
			},
		})
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		releases.value = result.data
	}
	catch (error) {
		loadError.value = toApiError(error).message
	}
	finally {
		loading.value = false
	}
}

watch(open, (value) => {
	if (value) {
		releases.value = []
		void search()
	}
})
</script>

<template>
	<SDialog
		v-model="open"
		title="Interactive search"
		wide
	>
		<div class="interactive-search">
			<div class="interactive-search-toolbar">
				<SButton
					size="sm"
					variant="secondary"
					:loading="loading"
					@click="search"
				>
					Search again
				</SButton>
			</div>
			<SSpinner v-if="loading && releases.length === 0" />
			<p
				v-else-if="loadError"
				class="s-field-error"
				role="alert"
			>
				{{ loadError }}
			</p>
			<ReleaseTable
				v-else
				:releases="releases"
				:version-labels="versionLabels"
				@grabbed="open = false"
			/>
		</div>
	</SDialog>
</template>

<style scoped>
.interactive-search {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.interactive-search-toolbar {
	display: flex;
	justify-content: flex-end;
}
</style>
