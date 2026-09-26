<template>
	<div
		v-if="rows.length > 0"
		class="s-table-wrap"
	>
		<table class="s-table">
			<thead>
				<tr>
					<th
						v-for="column in columns"
						:key="column.key"
						:class="{ 's-th-right': column.align === 'right' }"
						scope="col"
					>
						{{ column.label }}
					</th>
				</tr>
			</thead>
			<tbody>
				<tr
					v-for="(row, index) in rows"
					:key="rowKey ? rowKey(row) : index"
				>
					<td
						v-for="column in columns"
						:key="column.key"
						:data-label="column.label"
						:class="{ 's-td-right': column.align === 'right' }"
					>
						<slot
							:name="`cell-${column.key}`"
							:row="row"
						>
							{{ row[column.key] }}
						</slot>
					</td>
				</tr>
			</tbody>
		</table>
	</div>
	<slot
		v-else
		name="empty"
	>
		<SEmptyState :message="$t('components.ui.STable.nothingHereYet')" />
	</slot>
</template>

<script setup lang="ts" generic="T extends Record<string, unknown>">
withDefaults(defineProps<{
	columns: Array<{ key: string, label: string, align?: 'left' | 'right' }>
	rows: T[]
	/** Stable key for rows; falls back to index. */
	rowKey?: (row: T) => string | number
}>(), {
	rowKey: undefined,
})
</script>
