<template>
	<ul class="reorder-list">
		<li
			v-for="(item, index) in modelValue"
			:key="itemKey ? itemKey(item, index) : index"
			class="reorder-row"
		>
			<div class="reorder-content">
				<slot
					:item="item"
					:index="index"
				/>
			</div>
			<div class="reorder-actions">
				<SIconButton
					label="Move up"
					size="sm"
					:disabled="index === 0"
					@click="move(index, -1)"
				>
					<Icon
						name="lucide:chevron-up"
						aria-hidden="true"
					/>
				</SIconButton>
				<SIconButton
					label="Move down"
					size="sm"
					:disabled="index === modelValue.length - 1"
					@click="move(index, 1)"
				>
					<Icon
						name="lucide:chevron-down"
						aria-hidden="true"
					/>
				</SIconButton>
				<SIconButton
					v-if="removable"
					label="Remove"
					size="sm"
					@click="remove(index)"
				>
					<Icon
						name="lucide:x"
						aria-hidden="true"
					/>
				</SIconButton>
			</div>
		</li>
	</ul>
</template>

<script setup lang="ts" generic="T">
const props = withDefaults(defineProps<{
	modelValue: T[]
	itemKey?: (item: T, index: number) => string | number
	removable?: boolean
}>(), {
	itemKey: undefined,
	removable: false,
})

const emit = defineEmits<{
	'update:modelValue': [T[]]
	'remove': [number]
}>()

function move(index: number, delta: number) {
	const target = index + delta
	if (target < 0 || target >= props.modelValue.length) {
		return
	}
	const next = [...props.modelValue]
	;[next[index], next[target]] = [next[target]!, next[index]!]
	emit('update:modelValue', next)
}

function remove(index: number) {
	const next = [...props.modelValue]
	next.splice(index, 1)
	emit('update:modelValue', next)
	emit('remove', index)
}
</script>

<style scoped>
.reorder-list {
	display: flex;
	flex-direction: column;
	gap: 4px;
}

.reorder-row {
	display: flex;
	align-items: center;
	gap: 12px;
	min-height: var(--row-h);
	padding: 4px 8px;
	border: 1px solid var(--line);
	border-radius: var(--r-control);
	background: var(--surface);
}

.reorder-content {
	flex: 1;
	min-width: 0;
}

.reorder-actions {
	display: flex;
	align-items: center;
	gap: 2px;
	flex: none;
}
</style>
