<script setup lang="ts">
import { formatDate } from '~/composables/useFormat'
import { useWantedTable } from '~/composables/useWantedTable'
import type { WantedItem } from '~/composables/useWantedTable'
import { qualityLabel } from '~/utils/library-labels'
import { navChildren } from '~/navigation'

useHead({ title: 'Cut off' })

const {
	items,
	totalCount,
	page,
	totalPages,
	loading,
	monitoredOnly,
	selected,
	searching,
	searchingAll,
	allSelected,
	rowKey,
	isSelected,
	toggleRow,
	toggleAll,
	load,
	setPage,
	searchSelected,
	searchAll,
} = useWantedTable('cutoff')

const columns = [
	{ key: 'select', label: '' },
	{ key: 'title', label: 'Title' },
	{ key: 'episode', label: 'Episode' },
	{ key: 'airDate', label: 'Air date' },
	{ key: 'quality', label: 'Quality' },
	{ key: 'monitored', label: 'Monitored', align: 'right' as const },
]

function episodeLabel(row: WantedItem): string {
	if (row.type !== 'episode') {
		return ''
	}
	return `S${String(row.seasonNumber ?? 0).padStart(2, '0')}E${String(row.episodeNumber ?? 0).padStart(2, '0')}`
}

onMounted(() => {
	void load()
})
</script>

<template>
	<div>
		<SPageHeader title="Cut off">
			<template #actions>
				<SButton
					variant="secondary"
					:loading="searchingAll"
					@click="searchAll"
				>
					Search all
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			label="Wanted"
			:items="navChildren('wanted')"
		/>

		<div class="wanted-toolbar">
			<SCheckbox
				:model-value="allSelected"
				label="Select all on this page"
				@update:model-value="toggleAll"
			/>
			<SSwitch
				v-model="monitoredOnly"
				label="Monitored only"
			/>
			<SButton
				size="sm"
				:disabled="selected.length === 0"
				:loading="searching"
				@click="searchSelected"
			>
				Search selected
			</SButton>
		</div>

		<SSkeleton
			v-if="loading"
			height="400px"
		/>
		<STable
			v-else
			:columns="columns"
			:rows="items"
			:row-key="rowKey"
		>
			<template #empty>
				<SEmptyState
					message="Nothing is waiting for a quality upgrade right now."
					icon="lucide:circle-check"
				/>
			</template>
			<template #cell-select="{ row }">
				<SCheckbox
					:model-value="isSelected(row)"
					:aria-label="`Select ${row.title}`"
					@update:model-value="value => toggleRow(row, value)"
				/>
			</template>
			<template #cell-title="{ row }">
				<div class="wanted-title-cell">
					<span class="wanted-title">{{ row.title }}</span>
					<span
						v-if="row.subTitle"
						class="wanted-subtitle"
					>{{ row.subTitle }}</span>
				</div>
			</template>
			<template #cell-episode="{ row }">
				{{ episodeLabel(row) }}
			</template>
			<template #cell-airDate="{ row }">
				{{ row.airDateUtc ? formatDate(row.airDateUtc) : '' }}
			</template>
			<template #cell-quality="{ row }">
				{{ qualityLabel(row.quality) }}
			</template>
			<template #cell-monitored="{ row }">
				<SBadge :tone="row.monitored ? 'ok' : 'neutral'">
					{{ row.monitored ? 'Monitored' : 'Unmonitored' }}
				</SBadge>
			</template>
		</STable>

		<div
			v-if="totalPages > 1"
			class="wanted-pagination"
		>
			<SButton
				variant="secondary"
				size="sm"
				:disabled="page <= 1"
				@click="setPage(page - 1)"
			>
				Previous
			</SButton>
			<span>Page {{ page }} of {{ totalPages }} ({{ totalCount }} total)</span>
			<SButton
				variant="secondary"
				size="sm"
				:disabled="page >= totalPages"
				@click="setPage(page + 1)"
			>
				Next
			</SButton>
		</div>
	</div>
</template>

<style scoped>
.wanted-toolbar {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 16px;
	margin-bottom: 16px;
}

.wanted-title-cell {
	display: flex;
	flex-direction: column;
}

.wanted-title {
	font-weight: 500;
}

.wanted-subtitle {
	color: var(--fg-muted);
	font-size: var(--text-sm);
}

.wanted-pagination {
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 16px;
	padding-top: 24px;
	color: var(--fg-muted);
}
</style>
