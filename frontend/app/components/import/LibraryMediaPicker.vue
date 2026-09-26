<script setup lang="ts">
/**
 * Picks an existing library series or movie for a manual import row, filtered
 * client side against a preloaded option list (the caller loads the full
 * series/movie list once and shares it across every row).
 */
export interface LibraryMediaOption {
	id: number
	title: string
	year: number | null
}

const props = defineProps<{
	options: LibraryMediaOption[]
	placeholder: string
}>()

const selectedId = defineModel<number | null>({ default: null })

const term = ref('')

const filtered = computed(() => {
	const value = term.value.trim().toLowerCase()
	const list = value ? props.options.filter(option => option.title.toLowerCase().includes(value)) : props.options
	return list.slice(0, 50)
})

const current = computed(() => props.options.find(option => option.id === selectedId.value) ?? null)

function pick(id: number) {
	selectedId.value = id
	term.value = ''
}

function clear() {
	selectedId.value = null
	term.value = ''
}
</script>

<template>
	<SPopover>
		<template #trigger>
			<button
				type="button"
				class="s-input media-picker-trigger"
			>
				<span
					class="media-picker-title"
					:class="{ 's-cell-muted': !current }"
				>
					<template v-if="current">{{ current.title }}<span v-if="current.year"> ({{ current.year }})</span></template>
					<template v-else>{{ placeholder }}</template>
				</span>
				<Icon
					name="lucide:chevron-down"
					aria-hidden="true"
				/>
			</button>
		</template>
		<div class="media-picker-content">
			<SInput
				v-model="term"
				type="search"
				placeholder="Filter"
			/>
			<button
				v-if="current"
				type="button"
				class="media-picker-clear"
				@click="clear"
			>
				Clear match
			</button>
			<ul class="media-picker-list">
				<li
					v-for="option in filtered"
					:key="option.id"
				>
					<button
						type="button"
						class="media-picker-option"
						@click="pick(option.id)"
					>
						{{ option.title }}<span v-if="option.year"> ({{ option.year }})</span>
					</button>
				</li>
				<li
					v-if="filtered.length === 0"
					class="s-cell-muted media-picker-empty"
				>
					No matches
				</li>
			</ul>
		</div>
	</SPopover>
</template>

<style scoped>
.media-picker-trigger {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 8px;
	width: 220px;
	cursor: pointer;
}

.media-picker-title {
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.media-picker-content {
	display: flex;
	flex-direction: column;
	gap: 8px;
	width: 260px;
}

.media-picker-clear {
	align-self: flex-start;
	border: 0;
	background: transparent;
	color: var(--danger);
	font-size: 0.8125rem;
	cursor: pointer;
}

.media-picker-list {
	display: flex;
	flex-direction: column;
	gap: 2px;
	max-height: 240px;
	overflow-y: auto;
}

.media-picker-option {
	width: 100%;
	text-align: left;
	padding: 6px 8px;
	border: 0;
	border-radius: var(--r-control);
	background: transparent;
	font-size: 0.875rem;
	color: var(--fg);
	cursor: pointer;
}

.media-picker-option:hover {
	background: var(--surface-2);
}

.media-picker-empty {
	padding: 6px 8px;
	font-size: 0.8125rem;
}
</style>
