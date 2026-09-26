<script setup lang="ts">
/**
 * The download queue, in two shapes: `compact` (dashboard panel — top items,
 * title and progress only) and full (Activity > Queue — status, protocol,
 * indexer, client, size, progress and row actions). Owns remove/import/retry
 * against the activity store so both callers get the same behaviour for free.
 */
import { useI18n } from 'vue-i18n'
import { formatBytes } from '~/composables/useFormat'
import { protocolLabel } from '~/utils/settings-labels'
import { trackedDownloadStateLabel, trackedDownloadStateTone } from '~/utils/activity-labels'
import { useActivityStore } from '~/stores/activity'
import type { QueueItem } from '~/stores/activity'

withDefaults(defineProps<{
	items: QueueItem[]
	compact?: boolean
	selectable?: boolean
}>(), {
	compact: false,
	selectable: false,
})

const selectedIds = defineModel<number[]>('selectedIds', { default: () => [] })

const activity = useActivityStore()
const { t } = useI18n()
const { toast } = useToast()

const columns = [
	{ key: 'select', label: '' },
	{ key: 'title', label: t('components.shared.QueueTable.title') },
	{ key: 'status', label: t('components.shared.QueueTable.status') },
	{ key: 'protocol', label: t('components.shared.QueueTable.protocol') },
	{ key: 'indexer', label: t('components.shared.QueueTable.indexer') },
	{ key: 'client', label: t('components.shared.QueueTable.client') },
	{ key: 'size', label: t('components.shared.QueueTable.size'), align: 'right' as const },
	{ key: 'progress', label: t('components.shared.QueueTable.progress') },
	{ key: 'actions', label: '', align: 'right' as const },
]

function rowKey(row: QueueItem) {
	return row.id
}

function mediaTo(item: QueueItem): string | null {
	if (item.seriesId != null) {
		return `/series/${item.seriesId}`
	}
	if (item.movieId != null) {
		return `/movies/${item.movieId}`
	}
	return null
}

function progressPercent(item: QueueItem): number {
	return item.size > 0 ? ((item.size - item.sizeLeft) / item.size) * 100 : 0
}

function isRetryable(item: QueueItem): boolean {
	return item.trackedDownloadState === 'FAILED' || item.trackedDownloadState === 'FAILED_PENDING' || item.status === 'FAILED'
}

function canImportManually(item: QueueItem): boolean {
	return (item.trackedDownloadState === 'IMPORT_PENDING' || item.trackedDownloadState === 'FAILED_PENDING') && item.outputPath != null
}

function importManually(item: QueueItem) {
	void navigateTo({
		path: '/activity/import',
		query: {
			downloadId: item.downloadId,
			folder: item.outputPath ?? undefined,
			seriesId: item.seriesId ?? undefined,
			movieId: item.movieId ?? undefined,
		},
	})
}

function toggleRow(id: number, value: boolean) {
	selectedIds.value = value ? [...selectedIds.value, id] : selectedIds.value.filter(existing => existing !== id)
}

const removeTarget = ref<QueueItem | null>(null)

const removeTargetOpen = computed({
	get: () => removeTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			removeTarget.value = null
		}
	},
})
const removeFromClient = ref(true)
const blocklist = ref(false)
const skipRedownload = ref(false)
const removing = ref(false)
const actionBusyId = ref<number | null>(null)

function openRemove(item: QueueItem) {
	removeTarget.value = item
	removeFromClient.value = true
	blocklist.value = false
	skipRedownload.value = false
}

async function confirmRemove() {
	if (!removeTarget.value) {
		return
	}
	removing.value = true
	try {
		await activity.removeItem(removeTarget.value.id, {
			removeFromClient: removeFromClient.value,
			blocklist: blocklist.value,
			skipRedownload: skipRedownload.value,
		})
		removeTarget.value = null
	}
	catch (error) {
		toast({ title: t('components.shared.QueueTable.couldNotRemove'), description: (error as Error).message, tone: 'danger' })
	}
	finally {
		removing.value = false
	}
}

async function importNow(item: QueueItem) {
	actionBusyId.value = item.id
	try {
		await activity.importNow(item.id)
		toast({ title: t('components.shared.QueueTable.importing', { title: item.title }), tone: 'ok' })
	}
	catch (error) {
		toast({ title: t('components.shared.QueueTable.couldNotStartImport'), description: (error as Error).message, tone: 'danger' })
	}
	finally {
		actionBusyId.value = null
	}
}

async function retry(item: QueueItem) {
	actionBusyId.value = item.id
	try {
		await activity.retry(item.id)
		toast({ title: t('components.shared.QueueTable.retrying', { title: item.title }), tone: 'ok' })
	}
	catch (error) {
		toast({ title: t('components.shared.QueueTable.couldNotRetry'), description: (error as Error).message, tone: 'danger' })
	}
	finally {
		actionBusyId.value = null
	}
}
</script>

<template>
	<div
		v-if="compact"
		class="queue-compact"
	>
		<SEmptyState
			v-if="items.length === 0"
			:message="t('components.shared.QueueTable.nothingDownloading')"
		/>
		<ul
			v-else
			class="queue-compact-list"
		>
			<li
				v-for="item in items"
				:key="item.id"
			>
				<NuxtLink
					v-if="mediaTo(item)"
					:to="mediaTo(item)!"
					class="queue-compact-title"
				>
					{{ item.seriesTitle ?? item.movieTitle ?? item.title }}
				</NuxtLink>
				<span
					v-else
					class="queue-compact-title"
				>{{ item.title }}</span>
				<SProgress
					:value="progressPercent(item)"
					:label="t('components.shared.QueueTable.downloadProgress', { title: item.title })"
				/>
			</li>
		</ul>
	</div>

	<template v-else>
		<STable
			:columns="selectable ? columns : columns.filter(c => c.key !== 'select')"
			:rows="items"
			:row-key="rowKey"
		>
			<template #empty>
				<SEmptyState
					:message="$t('components.shared.QueueTable.nothingInQueue')"
					icon="lucide:download"
				/>
			</template>
			<template #cell-select="{ row }">
				<SCheckbox
					:model-value="selectedIds.includes(row.id)"
					:aria-label="t('components.shared.QueueTable.selectItem', { title: row.title })"
					@update:model-value="value => toggleRow(row.id, value)"
				/>
			</template>
			<template #cell-title="{ row }">
				<div class="queue-title-cell">
					<NuxtLink
						v-if="mediaTo(row)"
						:to="mediaTo(row)!"
						class="queue-title"
					>
						{{ row.seriesTitle ?? row.movieTitle ?? row.title }}
					</NuxtLink>
					<span
						v-else
						class="queue-title"
					>{{ row.title }}</span>
					<span
						v-if="mediaTo(row)"
						class="queue-subtitle"
					>{{ row.title }}</span>
				</div>
			</template>
			<template #cell-status="{ row }">
				<SPopover v-if="row.statusMessages.length > 0">
					<template #trigger>
						<button
							type="button"
							class="queue-status-trigger"
						>
							<SBadge :tone="trackedDownloadStateTone(row.trackedDownloadState)">
								{{ t(trackedDownloadStateLabel(row.trackedDownloadState)) }}
							</SBadge>
							<Icon
								name="lucide:info"
								aria-hidden="true"
							/>
						</button>
					</template>
					<ul class="queue-status-messages">
						<li
							v-for="(message, index) in row.statusMessages"
							:key="index"
						>
							{{ message }}
						</li>
					</ul>
				</SPopover>
				<SBadge
					v-else
					:tone="trackedDownloadStateTone(row.trackedDownloadState)"
				>
					{{ t(trackedDownloadStateLabel(row.trackedDownloadState)) }}
				</SBadge>
			</template>
			<template #cell-protocol="{ row }">
				{{ t(protocolLabel(row.protocol)) }}
			</template>
			<template #cell-indexer="{ row }">
				<span v-if="row.indexer">{{ row.indexer }}</span>
				<span
					v-else
					class="s-cell-muted"
				>{{ $t('components.shared.QueueTable.none') }}</span>
			</template>
			<template #cell-client="{ row }">
				{{ row.downloadClient }}
			</template>
			<template #cell-size="{ row }">
				{{ formatBytes(row.size) }}
			</template>
			<template #cell-progress="{ row }">
				<SProgress
					:value="progressPercent(row)"
					:label="t('components.shared.QueueTable.downloadProgress', { title: row.title })"
				/>
				<span class="queue-progress-remaining">{{ formatBytes(row.sizeLeft) }} {{ $t('components.shared.QueueTable.left') }}</span>
			</template>
			<template #cell-actions="{ row }">
				<SDropdownMenu
					:items="[
						{ label: t('components.shared.QueueTable.importNow'), icon: 'lucide:folder-input', onSelect: () => importNow(row) },
						...(canImportManually(row) ? [{ label: t('components.shared.QueueTable.importManually'), icon: 'lucide:folder-open', onSelect: () => importManually(row) }] : []),
						...(isRetryable(row) ? [{ label: t('components.shared.QueueTable.retry'), icon: 'lucide:refresh-cw', onSelect: () => retry(row) }] : []),
						{ label: t('components.shared.QueueTable.remove'), icon: 'lucide:trash-2', danger: true, onSelect: () => openRemove(row) },
					]"
				>
					<template #trigger>
						<SIconButton
							:label="$t('components.shared.QueueTable.itemActions')"
							:disabled="actionBusyId === row.id"
						>
							<Icon
								name="lucide:more-horizontal"
								aria-hidden="true"
							/>
						</SIconButton>
					</template>
				</SDropdownMenu>
			</template>
		</STable>

		<SDialog
			v-model="removeTargetOpen"
			:title="$t('components.shared.QueueTable.removeFromQueue')"
		>
			<p v-if="removeTarget">
				{{ $t('components.shared.QueueTable.confirmRemove', { title: removeTarget.title }) }}
			</p>
			<div class="queue-remove-options">
				<SCheckbox
					v-model="removeFromClient"
					:label="$t('components.shared.QueueTable.removeFromDownloadClient')"
				/>
				<SCheckbox
					v-model="blocklist"
					:label="$t('components.shared.QueueTable.addToBlocklist')"
				/>
				<SCheckbox
					v-model="skipRedownload"
					:label="$t('components.shared.QueueTable.skipRedownload')"
				/>
			</div>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="removing"
					@click="removeTarget = null"
				>
					{{ $t('components.shared.QueueTable.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="removing"
					@click="confirmRemove"
				>
					{{ $t('components.shared.QueueTable.remove') }}
				</SButton>
			</template>
		</SDialog>
	</template>
</template>

<style scoped>
.queue-compact-list {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.queue-compact-title {
	display: block;
	font-size: 0.875rem;
	margin-bottom: 4px;
	color: var(--fg);
	text-decoration: none;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.queue-title-cell {
	display: flex;
	flex-direction: column;
	gap: 2px;
}

.queue-title {
	font-weight: 500;
	color: var(--fg);
	text-decoration: none;
}

.queue-subtitle {
	font-size: 0.75rem;
	color: var(--fg-muted);
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
	max-width: 320px;
}

.queue-status-trigger {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	border: none;
	background: transparent;
	cursor: pointer;
	color: var(--fg-muted);
}

.queue-status-messages {
	display: flex;
	flex-direction: column;
	gap: 6px;
	font-size: 0.8125rem;
	max-width: 320px;
}

.queue-progress-remaining {
	display: block;
	font-size: 0.75rem;
	color: var(--fg-muted);
	margin-top: 2px;
}

.queue-remove-options {
	display: flex;
	flex-direction: column;
	gap: 8px;
	margin-top: 12px;
}
</style>
