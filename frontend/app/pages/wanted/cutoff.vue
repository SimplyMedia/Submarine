<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatDate } from '~/composables/useFormat'
import { useWantedTable } from '~/composables/useWantedTable'
import type { WantedItem } from '~/composables/useWantedTable'
import { qualityLabel } from '~/utils/library-labels'
import { navChildren } from '~/navigation'

const { t } = useI18n()
useHead({ title: t('pages.wanted.cutoff.title') })

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
	{ key: 'title', label: t('pages.wanted.cutoff.titleColumn') },
	{ key: 'episode', label: t('pages.wanted.cutoff.episode') },
	{ key: 'airDate', label: t('pages.wanted.cutoff.airDate') },
	{ key: 'quality', label: t('pages.wanted.cutoff.quality') },
	{ key: 'monitored', label: t('pages.wanted.cutoff.monitored'), align: 'right' as const },
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
		<SPageHeader :title="t('pages.wanted.cutoff.title')">
			<template #actions>
				<SButton
					variant="secondary"
					:loading="searchingAll"
					@click="searchAll"
				>
					{{ t('pages.wanted.cutoff.searchAll') }}
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			:label="t('pages.wanted.cutoff.wantedNav')"
			:items="navChildren('wanted')"
		/>

		<div class="wanted-toolbar">
			<SCheckbox
				:model-value="allSelected"
				:label="t('pages.wanted.cutoff.selectAll')"
				@update:model-value="toggleAll"
			/>
			<SSwitch
				v-model="monitoredOnly"
				:label="t('pages.wanted.cutoff.monitoredOnly')"
			/>
			<SButton
				size="sm"
				:disabled="selected.length === 0"
				:loading="searching"
				@click="searchSelected"
			>
				{{ t('pages.wanted.cutoff.searchSelected') }}
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
					:message="t('pages.wanted.cutoff.emptyState')"
					icon="lucide:circle-check"
				/>
			</template>
			<template #cell-select="{ row }">
				<SCheckbox
					:model-value="isSelected(row)"
					:aria-label="t('pages.wanted.cutoff.selectItem', { title: row.title })"
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
				{{ t(qualityLabel(row.quality)) }}
			</template>
			<template #cell-monitored="{ row }">
				<SBadge :tone="row.monitored ? 'ok' : 'neutral'">
					{{ t(row.monitored ? 'pages.wanted.cutoff.monitored' : 'pages.wanted.cutoff.unmonitored') }}
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
				{{ t('pages.wanted.cutoff.previous') }}
			</SButton>
			<span>{{ t('pages.wanted.cutoff.page', { page, totalPages, totalCount }) }}</span>
			<SButton
				variant="secondary"
				size="sm"
				:disabled="page >= totalPages"
				@click="setPage(page + 1)"
			>
				{{ t('pages.wanted.cutoff.next') }}
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
