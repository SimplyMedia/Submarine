<script setup lang="ts">
import { formatDate } from '~/composables/useFormat'
import { pendingReleaseReasonLabel } from '~/utils/activity-labels'
import { useActivityStore } from '~/stores/activity'
import { navChildren } from '~/navigation'
import type { PendingRelease } from '~/stores/activity'

useHead({ title: 'Queue' })

const activity = useActivityStore()
const { toast } = useToast()

const selectedIds = ref<number[]>([])
const bulkDialogOpen = ref(false)
const bulkRemoving = ref(false)
const bulkRemoveFromClient = ref(true)
const bulkBlocklist = ref(false)
const bulkSkipRedownload = ref(false)

let stopWatch: (() => void) | null = null

onMounted(async () => {
	stopWatch = activity.watchQueue()
	await Promise.all([activity.load(), activity.loadPendingReleases()])
})

onBeforeUnmount(() => {
	stopWatch?.()
})

async function confirmBulkRemove() {
	bulkRemoving.value = true
	try {
		await activity.bulkRemove(selectedIds.value, {
			removeFromClient: bulkRemoveFromClient.value,
			blocklist: bulkBlocklist.value,
			skipRedownload: bulkSkipRedownload.value,
		})
		selectedIds.value = []
		bulkDialogOpen.value = false
	}
	catch (error) {
		toast({ title: 'Could not remove selected items', description: (error as Error).message, tone: 'danger' })
	}
	finally {
		bulkRemoving.value = false
	}
}

async function removePendingRelease(id: number) {
	try {
		await activity.removePendingRelease(id)
	}
	catch (error) {
		toast({ title: 'Could not remove pending release', description: (error as Error).message, tone: 'danger' })
	}
}

function pendingReleaseTo(row: PendingRelease): string | null {
	if (row.seriesId != null) {
		return `/series/${row.seriesId}`
	}
	if (row.movieId != null) {
		return `/movies/${row.movieId}`
	}
	return null
}

const pendingColumns = [
	{ key: 'title', label: 'Title' },
	{ key: 'reason', label: 'Reason' },
	{ key: 'added', label: 'Added' },
	{ key: 'actions', label: '', align: 'right' as const },
]
</script>

<template>
	<div>
		<SPageHeader title="Queue">
			<template #actions>
				<SButton
					variant="danger"
					size="sm"
					:disabled="selectedIds.length === 0"
					@click="bulkDialogOpen = true"
				>
					Remove selected ({{ selectedIds.length }})
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			label="Activity"
			:items="navChildren('activity')"
		/>

		<div
			v-if="activity.status"
			class="queue-summary"
		>
			<SBadge tone="neutral">
				{{ activity.status.total }} total
			</SBadge>
			<SBadge
				v-if="activity.status.errors > 0"
				tone="danger"
			>
				{{ activity.status.errors }} errors
			</SBadge>
			<SBadge
				v-if="activity.status.warnings > 0"
				tone="warn"
			>
				{{ activity.status.warnings }} warnings
			</SBadge>
			<SBadge
				v-if="activity.status.unknown > 0"
				tone="info"
			>
				{{ activity.status.unknown }} unmatched
			</SBadge>
		</div>

		<SEmptyState
			v-if="activity.loadError"
			:message="activity.loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="activity.load()">
					Retry
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="activity.loading && activity.items.length === 0" />
		<QueueTable
			v-else
			v-model:selected-ids="selectedIds"
			:items="activity.items"
			selectable
		/>

		<SSection
			v-if="activity.pendingReleases.length > 0"
			title="Pending releases"
		>
			<STable
				:columns="pendingColumns"
				:rows="activity.pendingReleases"
				:row-key="(row) => row.id"
			>
				<template #cell-title="{ row }">
					<NuxtLink
						v-if="pendingReleaseTo(row)"
						:to="pendingReleaseTo(row)!"
					>
						{{ row.title }}
					</NuxtLink>
					<span v-else>{{ row.title }}</span>
				</template>
				<template #cell-reason="{ row }">
					{{ pendingReleaseReasonLabel(row.reason) }}
				</template>
				<template #cell-added="{ row }">
					{{ formatDate(row.added) }}
				</template>
				<template #cell-actions="{ row }">
					<SIconButton
						label="Remove pending release"
						@click="removePendingRelease(row.id)"
					>
						<Icon
							name="lucide:x"
							aria-hidden="true"
						/>
					</SIconButton>
				</template>
			</STable>
		</SSection>

		<SDialog
			v-model="bulkDialogOpen"
			title="Remove selected"
		>
			<p>Remove {{ selectedIds.length }} item(s) from the queue?</p>
			<div class="queue-bulk-options">
				<SCheckbox
					v-model="bulkRemoveFromClient"
					label="Remove from the download client"
				/>
				<SCheckbox
					v-model="bulkBlocklist"
					label="Add to blocklist"
				/>
				<SCheckbox
					v-model="bulkSkipRedownload"
					label="Skip redownload"
				/>
			</div>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="bulkRemoving"
					@click="bulkDialogOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="bulkRemoving"
					@click="confirmBulkRemove"
				>
					Remove
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.queue-summary {
	display: flex;
	gap: 8px;
	margin-bottom: 16px;
}

.queue-bulk-options {
	display: flex;
	flex-direction: column;
	gap: 8px;
	margin-top: 12px;
}
</style>
