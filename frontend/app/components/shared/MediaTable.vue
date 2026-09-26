<script setup lang="ts" generic="T extends { id: number, to: string, posterUrl: string | null, title: string, subtitle?: string }">
/**
 * Table view of a library list: a fixed poster+title leading column, an
 * optional selection column, sortable headers, and slot-driven data cells
 * (same `cell-<key>` convention as STable).
 */
import type { MediaTableColumn } from '~/types/ui'

const props = withDefaults(defineProps<{
	columns: MediaTableColumn[]
	rows: T[]
	selectable?: boolean
	selected?: number[]
	sortKey?: string
	sortDirection?: 'asc' | 'desc'
}>(), {
	selectable: false,
	selected: () => [],
	sortKey: undefined,
	sortDirection: 'asc',
})

const emit = defineEmits<{
	'update:selected': [ids: number[]]
	'sort': [key: string]
}>()

const selectedSet = computed(() => new Set(props.selected))
const allSelected = computed(() => props.rows.length > 0 && props.rows.every(row => selectedSet.value.has(row.id)))

function toggleAll() {
	emit('update:selected', allSelected.value ? [] : props.rows.map(row => row.id))
}

function toggleRow(id: number) {
	const next = new Set(props.selected)
	if (next.has(id)) {
		next.delete(id)
	}
	else {
		next.add(id)
	}
	emit('update:selected', [...next])
}
</script>

<template>
	<div
		v-if="rows.length > 0"
		class="s-table-wrap"
	>
		<table class="s-table media-table">
			<thead>
				<tr>
					<th
						v-if="selectable"
						class="media-table-check"
						scope="col"
					>
						<SCheckbox
							:model-value="allSelected"
							:aria-label="$t('components.shared.MediaTable.selectAll')"
							@update:model-value="toggleAll"
						/>
					</th>
					<th scope="col">
						{{ $t('components.shared.MediaTable.title') }}
					</th>
					<th
						v-for="column in columns"
						:key="column.key"
						scope="col"
						:class="{ 's-th-right': column.align === 'right', 'media-table-sortable': column.sortKey }"
						@click="column.sortKey && emit('sort', column.sortKey)"
					>
						{{ column.label }}
						<Icon
							v-if="column.sortKey && sortKey === column.sortKey"
							:name="sortDirection === 'asc' ? 'lucide:chevron-up' : 'lucide:chevron-down'"
							aria-hidden="true"
						/>
					</th>
				</tr>
			</thead>
			<tbody>
				<tr
					v-for="row in rows"
					:key="row.id"
				>
					<td
						v-if="selectable"
						data-label=""
					>
						<SCheckbox
							:model-value="selectedSet.has(row.id)"
							:aria-label="$t('components.shared.MediaTable.selectItem', { title: row.title })"
							@update:model-value="toggleRow(row.id)"
						/>
					</td>
					<td :data-label="$t('components.shared.MediaTable.title')">
						<NuxtLink
							:to="row.to"
							class="media-table-title"
						>
							<SPosterImage
								:src="row.posterUrl"
								:alt="row.title"
								class="media-table-poster"
							/>
							<span class="media-table-title-col">
								<span class="media-table-title-text">{{ row.title }}</span>
								<span
									v-if="row.subtitle"
									class="media-table-subtitle"
								>{{ row.subtitle }}</span>
							</span>
						</NuxtLink>
					</td>
					<td
						v-for="column in columns"
						:key="column.key"
						:data-label="column.label"
						:class="{ 's-td-right': column.align === 'right' }"
					>
						<slot
							:name="`cell-${column.key}`"
							:row="row"
						/>
					</td>
				</tr>
			</tbody>
		</table>
	</div>
	<slot
		v-else
		name="empty"
	>
		<SEmptyState :message="$t('components.shared.MediaTable.nothingHereYet')" />
	</slot>
</template>

<style scoped>
.media-table-check {
	width: 40px;
}

.media-table-sortable {
	cursor: pointer;
	user-select: none;
}

.media-table-title {
	display: flex;
	align-items: center;
	gap: 12px;
	color: inherit;
	text-decoration: none;
}

.media-table-poster {
	width: 32px;
	height: 48px;
	flex: none;
	border-radius: 4px;
	overflow: hidden;
}

.media-table-title-col {
	display: flex;
	flex-direction: column;
	min-width: 0;
}

.media-table-title-text {
	font-weight: 500;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.media-table-subtitle {
	font-size: var(--text-xs);
	color: var(--fg-muted);
}
</style>
