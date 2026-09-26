<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatDate } from '~/composables/useFormat'
import { pendingReleaseReasonLabel } from '~/utils/activity-labels'
import { useActivityStore } from '~/stores/activity'
import { navChildren } from '~/navigation'
import type { PendingRelease } from '~/stores/activity'

const { t } = useI18n()
useHead({ title: t('pages.activity.queue.title') })

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
		toast({ title: t('pages.activity.queue.removeSelectedFailed'), description: (error as Error).message, tone: 'danger' })
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
		toast({ title: t('pages.activity.queue.removePendingFailed'), description: (error as Error).message, tone: 'danger' })
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
	{ key: 'title', label: t('pages.activity.queue.titleColumn') },
	{ key: 'reason', label: t('pages.activity.queue.reason') },
	{ key: 'added', label: t('pages.activity.queue.added') },
	{ key: 'actions', label: '', align: 'right' as const },
]
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.activity.queue.title')">
			<template #actions>
				<SButton
					variant="danger"
					size="sm"
					:disabled="selectedIds.length === 0"
					@click="bulkDialogOpen = true"
				>
					{{ t('pages.activity.queue.removeSelectedCount', { count: selectedIds.length }) }}
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			:label="t('pages.activity.queue.activityNav')"
			:items="navChildren('activity')"
		/>

		<div
			v-if="activity.status"
			class="queue-summary"
		>
			<SBadge tone="neutral">
				{{ t('pages.activity.queue.total', { count: activity.status.total }) }}
			</SBadge>
			<SBadge
				v-if="activity.status.errors > 0"
				tone="danger"
			>
				{{ t('pages.activity.queue.errors', { count: activity.status.errors }) }}
			</SBadge>
			<SBadge
				v-if="activity.status.warnings > 0"
				tone="warn"
			>
				{{ t('pages.activity.queue.warnings', { count: activity.status.warnings }) }}
			</SBadge>
			<SBadge
				v-if="activity.status.unknown > 0"
				tone="info"
			>
				{{ t('pages.activity.queue.unmatched', { count: activity.status.unknown }) }}
			</SBadge>
		</div>

		<SEmptyState
			v-if="activity.loadError"
			:message="activity.loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="activity.load()">
					{{ t('pages.activity.queue.retry') }}
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
			:title="t('pages.activity.queue.pendingReleases')"
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
					{{ t(pendingReleaseReasonLabel(row.reason)) }}
				</template>
				<template #cell-added="{ row }">
					{{ formatDate(row.added) }}
				</template>
				<template #cell-actions="{ row }">
					<SIconButton
						:label="t('pages.activity.queue.removePendingRelease')"
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
			:title="t('pages.activity.queue.removeSelectedTitle')"
		>
			<p>{{ t('pages.activity.queue.removeSelectedQuestion', { count: selectedIds.length }) }}</p>
			<div class="queue-bulk-options">
				<SCheckbox
					v-model="bulkRemoveFromClient"
					:label="t('pages.activity.queue.removeFromDownloadClient')"
				/>
				<SCheckbox
					v-model="bulkBlocklist"
					:label="t('pages.activity.queue.addToBlocklist')"
				/>
				<SCheckbox
					v-model="bulkSkipRedownload"
					:label="t('pages.activity.queue.skipRedownload')"
				/>
			</div>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="bulkRemoving"
					@click="bulkDialogOpen = false"
				>
					{{ t('pages.activity.queue.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="bulkRemoving"
					@click="confirmBulkRemove"
				>
					{{ t('pages.activity.queue.remove') }}
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
