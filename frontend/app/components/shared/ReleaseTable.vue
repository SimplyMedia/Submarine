<script setup lang="ts">
import { useI18n } from 'vue-i18n'
/**
 * Table of releases returned by a search (manual search page and the
 * interactive search dialog both use this). Owns the grab flow: releases
 * with exactly one media version decision grab immediately, releases with
 * several open a version chooser dialog, and releases with no library match
 * grab is disabled with a hint. `versionLabels` lets a caller that already
 * has the series/movie's media versions loaded show real names instead of
 * "Version {id}".
 */
import { toApiError, useApi } from '~/composables/useApi'
import { formatBytes, formatRelative } from '~/composables/useFormat'
import { languageLabel, protocolLabel } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type ReleaseResource = components['schemas']['ReleaseResource']
type ReleaseVersionDecisionResource = components['schemas']['ReleaseVersionDecisionResource']
type GrabReleaseRequest = components['schemas']['GrabReleaseRequest']

const props = withDefaults(defineProps<{
	releases: ReleaseResource[]
	loading?: boolean
	versionLabels?: Record<number, string>
	selectable?: boolean
	selectedGuids?: string[]
}>(), {
	loading: false,
	versionLabels: () => ({}),
	selectable: false,
	selectedGuids: () => [],
})

const emit = defineEmits<{
	'grabbed': [ReleaseResource]
	'update:selectedGuids': [string[]]
}>()

const { toast } = useToast()
const { t } = useI18n()

const tableColumns = computed(() => props.selectable
	? [{ key: 'select', label: '' }, ...columns]
	: columns)
const columns = [
	{ key: 'title', label: t('components.shared.ReleaseTable.title') },
	{ key: 'indexer', label: t('components.shared.ReleaseTable.indexer') },
	{ key: 'protocol', label: t('components.shared.ReleaseTable.protocol') },
	{ key: 'age', label: t('components.shared.ReleaseTable.age') },
	{ key: 'size', label: t('components.shared.ReleaseTable.size'), align: 'right' as const },
	{ key: 'peers', label: t('components.shared.ReleaseTable.seedersLeechers'), align: 'right' as const },
	{ key: 'quality', label: t('components.shared.ReleaseTable.quality') },
	{ key: 'score', label: t('components.shared.ReleaseTable.score'), align: 'right' as const },
	{ key: 'grab', label: '', align: 'right' as const },
]

function rowKey(row: ReleaseResource) {
	return row.guid
}

function hasMatch(release: ReleaseResource): boolean {
	return release.mappedSeriesId != null || release.mappedMovieId != null
}

function bestDecision(release: ReleaseResource): ReleaseVersionDecisionResource | undefined {
	const approved = release.decisions.filter(decision => decision.approved)
	const pool = approved.length > 0 ? approved : release.decisions
	return [...pool].sort((a, b) => b.score - a.score)[0]
}

function versionLabel(id: number): string {
	return props.versionLabels[id] ?? t('components.shared.ReleaseTable.versionNumber', { number: id })
}

function grabDisabledHint(release: ReleaseResource): string | null {
	if (!hasMatch(release)) {
		return t('components.shared.ReleaseTable.noLibraryMatch')
	}
	if (release.indexerId == null) {
		return t('components.shared.ReleaseTable.noIndexer')
	}
	if (release.decisions.length === 0) {
		return t('components.shared.ReleaseTable.noCompatibleVersion')
	}
	return null
}

const chooserRelease = ref<ReleaseResource | null>(null)
const chooserOpen = computed({
	get: () => chooserRelease.value !== null,
	set: (value: boolean) => { if (!value) chooserRelease.value = null },
})
const grabbingGuid = ref<string | null>(null)

function buildRequest(release: ReleaseResource, mediaVersionId: number): GrabReleaseRequest {
	return {
		guid: release.guid,
		indexerId: release.indexerId!,
		mediaVersionId,
		seriesId: release.mappedSeriesId,
		episodeIds: release.episodeIds.length > 0 ? release.episodeIds : null,
		movieId: release.mappedMovieId,
		qualitySource: null,
		qualityResolution: null,
		languages: null,
		override: false,
	}
}

async function grab(release: ReleaseResource, mediaVersionId: number) {
	grabbingGuid.value = release.guid
	try {
		const api = useApi()
		const result = await api.POST('/api/v1/releases/grab', { body: buildRequest(release, mediaVersionId) })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: t('components.shared.ReleaseTable.grabbed', { title: release.title }), tone: 'ok' })
		chooserRelease.value = null
		emit('grabbed', release)
	}
	catch (error) {
		toast({ title: t('components.shared.ReleaseTable.couldNotGrab'), description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		grabbingGuid.value = null
	}
}

function onGrabClick(release: ReleaseResource) {
	if (grabDisabledHint(release)) {
		return
	}
	if (release.decisions.length === 1) {
		void grab(release, release.decisions[0]!.mediaVersionId)
		return
	}
	chooserRelease.value = release
}
</script>

<template>
	<STable
		:columns="tableColumns"
		:rows="releases"
		:row-key="rowKey"
	>
		<template #empty>
			<SEmptyState
				:message="t('components.shared.ReleaseTable.noReleasesFound')"
				icon="lucide:search-x"
			/>
		</template>
		<template #cell-select="{ row }">
			<SCheckbox
				:model-value="selectedGuids.includes(row.guid)"
				:aria-label="t('components.shared.ReleaseTable.selectRelease', { title: row.title })"
				@update:model-value="value => emit('update:selectedGuids', value ? [...selectedGuids, row.guid] : selectedGuids.filter(guid => guid !== row.guid))"
			/>
		</template>
		<template #cell-title="{ row }">
			<div class="release-title-cell">
				<span class="release-title">{{ row.title }}</span>
				<span
					v-if="row.releaseGroup"
					class="release-subtitle"
				>{{ row.releaseGroup }}</span>
			</div>
		</template>
		<template #cell-indexer="{ row }">
			<span v-if="row.indexer">{{ row.indexer }}</span>
			<span
				v-else
				class="s-cell-muted"
			> {{ $t('components.shared.ReleaseTable.none') }}</span>
		</template>
		<template #cell-protocol="{ row }">
			<SBadge tone="neutral">
				{{ t(protocolLabel(row.protocol)) }}
			</SBadge>
		</template>
		<template #cell-age="{ row }">
			<span v-if="row.publishDate">{{ formatRelative(row.publishDate) }}</span>
			<span
				v-else
				class="s-cell-muted"
			> {{ $t('components.shared.ReleaseTable.none') }}</span>
		</template>
		<template #cell-size="{ row }">
			<span v-if="row.size != null">{{ formatBytes(row.size) }}</span>
			<span
				v-else
				class="s-cell-muted"
			> {{ $t('components.shared.ReleaseTable.none') }}</span>
		</template>
		<template #cell-peers="{ row }">
			<span v-if="row.protocol === 'BITTORRENT'">{{ row.seeders ?? 0 }} / {{ row.leechers ?? 0 }}</span>
			<span
				v-else
				class="s-cell-muted"
			> {{ $t('components.shared.ReleaseTable.none') }}</span>
		</template>
		<template #cell-quality="{ row }">
			<div class="release-quality-cell">
				<SBadge tone="info">
					{{ row.qualityName || $t('components.shared.ReleaseTable.unknown') }}
				</SBadge>
				<span
					v-if="row.languages.length > 0"
					class="release-languages"
				>{{ row.languages.map(language => t(languageLabel(language))).join(', ') }}</span>
			</div>
		</template>
		<template #cell-score="{ row }">
			<STooltip
				v-if="bestDecision(row)?.rejections.length"
				:text="bestDecision(row)!.rejections.join('; ')"
			>
				<span class="release-score release-score-rejected">{{ bestDecision(row)?.score ?? t('components.shared.ReleaseTable.none') }}</span>
			</STooltip>
			<span v-else>{{ bestDecision(row)?.score ?? t('components.shared.ReleaseTable.none') }}</span>
		</template>
		<template #cell-grab="{ row }">
			<STooltip :text="grabDisabledHint(row) ?? undefined">
				<SButton
					size="sm"
					variant="primary"
					:disabled="!!grabDisabledHint(row)"
					:loading="grabbingGuid === row.guid"
					@click="onGrabClick(row)"
				>
					{{ $t('components.shared.ReleaseTable.grab') }}
				</SButton>
			</STooltip>
		</template>
	</STable>

	<SDialog
		v-model="chooserOpen"
		:title="$t('components.shared.ReleaseTable.chooseVersion')"
		:description="chooserRelease?.title"
	>
		<ul
			v-if="chooserRelease"
			class="version-chooser-list"
		>
			<li
				v-for="decision in chooserRelease.decisions"
				:key="decision.mediaVersionId"
				class="version-chooser-row"
			>
				<div class="version-chooser-info">
					<span class="version-chooser-name">{{ versionLabel(decision.mediaVersionId) }}</span>
					<SBadge :tone="decision.approved ? 'ok' : 'warn'">
						{{ decision.approved ? $t('components.shared.ReleaseTable.approved') : $t('components.shared.ReleaseTable.rejected') }}
					</SBadge>
					<span class="version-chooser-score">{{ $t('components.shared.ReleaseTable.scoreValue', { score: decision.score }) }}</span>
				</div>
				<p
					v-if="decision.rejections.length > 0"
					class="version-chooser-rejections"
				>
					{{ decision.rejections.join('; ') }}
				</p>
				<SButton
					size="sm"
					:loading="grabbingGuid === chooserRelease.guid"
					@click="grab(chooserRelease, decision.mediaVersionId)"
				>
					{{ $t('components.shared.ReleaseTable.grabThisVersion') }}
				</SButton>
			</li>
		</ul>
	</SDialog>
</template>

<style scoped>
.release-title-cell,
.release-quality-cell {
	display: flex;
	flex-direction: column;
	gap: 2px;
}

.release-title {
	font-weight: 500;
}

.release-subtitle,
.release-languages {
	font-size: 0.75rem;
	color: var(--fg-muted);
}

.release-score-rejected {
	text-decoration: underline dotted var(--warn);
	text-underline-offset: 3px;
}

.version-chooser-list {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.version-chooser-row {
	display: flex;
	flex-direction: column;
	gap: 6px;
	padding: 12px;
	border: 1px solid var(--line);
	border-radius: var(--r-control);
}

.version-chooser-info {
	display: flex;
	align-items: center;
	gap: 8px;
}

.version-chooser-name {
	font-weight: 500;
}

.version-chooser-score {
	font-size: 0.8125rem;
	color: var(--fg-muted);
}

.version-chooser-rejections {
	font-size: 0.8125rem;
	color: var(--warn);
}
</style>
