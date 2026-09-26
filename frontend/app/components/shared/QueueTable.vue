<script setup lang="ts">
/**
 * The download queue, in two shapes: `compact` (dashboard panel — top items,
 * title and progress only) and full (Activity > Queue — status, protocol,
 * indexer, client, size, progress and row actions). Owns remove/import/retry
 * against the activity store so both callers get the same behaviour for free.
 */
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
const { toast } = useToast()

const columns = [
	{ key: 'select', label: '' },
	{ key: 'title', label: 'Title' },
	{ key: 'status', label: 'Status' },
	{ key: 'protocol', label: 'Protocol' },
	{ key: 'indexer', label: 'Indexer' },
	{ key: 'client', label: 'Client' },
	{ key: 'size', label: 'Size', align: 'right' as const },
	{ key: 'progress', label: 'Progress' },
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
		toast({ title: 'Could not remove queue item', description: (error as Error).message, tone: 'danger' })
	}
	finally {
		removing.value = false
	}
}

async function importNow(item: QueueItem) {
	actionBusyId.value = item.id
	try {
		await activity.importNow(item.id)
		toast({ title: `Importing "${item.title}"`, tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Could not start import', description: (error as Error).message, tone: 'danger' })
	}
	finally {
		actionBusyId.value = null
	}
}

async function retry(item: QueueItem) {
	actionBusyId.value = item.id
	try {
		await activity.retry(item.id)
		toast({ title: `Retrying "${item.title}"`, tone: 'ok' })
	}
	catch (error) {
		toast({ title: 'Could not retry', description: (error as Error).message, tone: 'danger' })
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
			message="Nothing downloading right now"
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
					:label="`${item.title} download progress`"
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
					message="Nothing in the queue right now."
					icon="lucide:download"
				/>
			</template>
			<template #cell-select="{ row }">
				<SCheckbox
					:model-value="selectedIds.includes(row.id)"
					:aria-label="`Select ${row.title}`"
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
								{{ trackedDownloadStateLabel(row.trackedDownloadState) }}
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
					{{ trackedDownloadStateLabel(row.trackedDownloadState) }}
				</SBadge>
			</template>
			<template #cell-protocol="{ row }">
				{{ protocolLabel(row.protocol) }}
			</template>
			<template #cell-indexer="{ row }">
				<span v-if="row.indexer">{{ row.indexer }}</span>
				<span
					v-else
					class="s-cell-muted"
				>None</span>
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
					:label="`${row.title} download progress`"
				/>
				<span class="queue-progress-remaining">{{ formatBytes(row.sizeLeft) }} left</span>
			</template>
			<template #cell-actions="{ row }">
				<SDropdownMenu
					:items="[
						{ label: 'Import now', icon: 'lucide:folder-input', onSelect: () => importNow(row) },
						...(canImportManually(row) ? [{ label: 'Import manually', icon: 'lucide:folder-open', onSelect: () => importManually(row) }] : []),
						...(isRetryable(row) ? [{ label: 'Retry', icon: 'lucide:refresh-cw', onSelect: () => retry(row) }] : []),
						{ label: 'Remove', icon: 'lucide:trash-2', danger: true, onSelect: () => openRemove(row) },
					]"
				>
					<template #trigger>
						<SIconButton
							label="Queue item actions"
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
			title="Remove from queue"
		>
			<p v-if="removeTarget">
				Remove "{{ removeTarget.title }}" from the queue?
			</p>
			<div class="queue-remove-options">
				<SCheckbox
					v-model="removeFromClient"
					label="Remove from the download client"
				/>
				<SCheckbox
					v-model="blocklist"
					label="Add to blocklist"
				/>
				<SCheckbox
					v-model="skipRedownload"
					label="Skip redownload"
				/>
			</div>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="removing"
					@click="removeTarget = null"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="removing"
					@click="confirmRemove"
				>
					Remove
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
