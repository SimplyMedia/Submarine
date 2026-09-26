<script setup lang="ts">
/**
 * Multi-select tag input: chips for selected tags, type-ahead suggestions
 * from the reference store, and inline "create tag" when nothing matches.
 */
withDefaults(defineProps<{
	controlId?: string
	placeholder?: string
}>(), {
	controlId: undefined,
	placeholder: 'Add a tag',
})

const tagIds = defineModel<number[]>('tagIds', { default: () => [] })

const reference = useReferenceStore()
const query = ref('')
const open = ref(false)
const activeIndex = ref(0)
const creating = ref(false)
const inputRef = ref<HTMLInputElement>()

const selectedTags = computed(() =>
	tagIds.value
		.map(id => reference.tags.find(tag => tag.id === id))
		.filter((tag): tag is NonNullable<typeof tag> => tag != null),
)

const suggestions = computed(() => {
	const term = query.value.trim().toLowerCase()
	const pool = reference.tags.filter(tag => !tagIds.value.includes(tag.id))
	if (term.length === 0) {
		return pool.slice(0, 8)
	}
	return pool.filter(tag => tag.label.toLowerCase().includes(term)).slice(0, 8)
})

const canCreate = computed(() => {
	const term = query.value.trim()
	return term.length > 0 && !reference.tags.some(tag => tag.label.toLowerCase() === term.toLowerCase())
})

function addTag(id: number) {
	if (!tagIds.value.includes(id)) {
		tagIds.value = [...tagIds.value, id]
	}
	query.value = ''
	activeIndex.value = 0
	inputRef.value?.focus()
}

function removeTag(id: number) {
	tagIds.value = tagIds.value.filter(existing => existing !== id)
}

async function createAndAdd() {
	const label = query.value.trim()
	if (label.length === 0 || creating.value) {
		return
	}
	creating.value = true
	try {
		const tag = await reference.createTag(label)
		addTag(tag.id)
	}
	catch {
		// Leaves the query in place so the user can see the failed text and retry.
	}
	finally {
		creating.value = false
	}
}

function onKeydown(event: KeyboardEvent) {
	const optionCount = suggestions.value.length + (canCreate.value ? 1 : 0)
	if (event.key === 'ArrowDown') {
		event.preventDefault()
		open.value = true
		activeIndex.value = Math.min(activeIndex.value + 1, optionCount - 1)
	}
	else if (event.key === 'ArrowUp') {
		event.preventDefault()
		activeIndex.value = Math.max(activeIndex.value - 1, 0)
	}
	else if (event.key === 'Enter') {
		event.preventDefault()
		if (activeIndex.value < suggestions.value.length) {
			addTag(suggestions.value[activeIndex.value]!.id)
		}
		else if (canCreate.value) {
			void createAndAdd()
		}
	}
	else if (event.key === 'Escape') {
		open.value = false
	}
	else if (event.key === 'Backspace' && query.value.length === 0 && selectedTags.value.length > 0) {
		removeTag(selectedTags.value[selectedTags.value.length - 1]!.id)
	}
}

watch(query, () => {
	activeIndex.value = 0
	open.value = true
})
</script>

<template>
	<div class="tag-picker">
		<ul class="tag-picker-chips">
			<li
				v-for="tag in selectedTags"
				:key="tag.id"
				class="tag-picker-chip"
			>
				{{ tag.label }}
				<button
					type="button"
					:aria-label="`Remove ${tag.label}`"
					@click="removeTag(tag.id)"
				>
					<Icon
						name="lucide:x"
						aria-hidden="true"
					/>
				</button>
			</li>
			<li class="tag-picker-input-item">
				<input
					:id="controlId"
					ref="inputRef"
					v-model="query"
					type="text"
					class="tag-picker-input"
					:placeholder="selectedTags.length === 0 ? placeholder : ''"
					role="combobox"
					:aria-expanded="open"
					aria-autocomplete="list"
					@focus="open = true"
					@blur="open = false"
					@keydown="onKeydown"
				>
			</li>
		</ul>
		<ul
			v-if="open && (suggestions.length > 0 || canCreate)"
			class="tag-picker-menu"
			role="listbox"
		>
			<li
				v-for="(tag, index) in suggestions"
				:key="tag.id"
				role="option"
				:aria-selected="index === activeIndex"
				class="tag-picker-option"
				:class="{ 'tag-picker-option-active': index === activeIndex }"
				@mousedown.prevent="addTag(tag.id)"
			>
				{{ tag.label }}
			</li>
			<li
				v-if="canCreate"
				role="option"
				:aria-selected="activeIndex === suggestions.length"
				class="tag-picker-option"
				:class="{ 'tag-picker-option-active': activeIndex === suggestions.length }"
				@mousedown.prevent="createAndAdd"
			>
				Create "{{ query.trim() }}"
			</li>
		</ul>
	</div>
</template>

<style scoped>
.tag-picker {
	position: relative;
}

.tag-picker-chips {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 6px;
	min-height: var(--control-h);
	padding: 4px 8px;
	border: 1px solid var(--line);
	border-radius: var(--r-control);
	background: var(--surface);
}

.tag-picker-chips:focus-within {
	border-color: var(--accent);
}

.tag-picker-chip {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	padding: 2px 4px 2px 8px;
	border-radius: 999px;
	background: var(--surface-2);
	color: var(--fg);
	font-size: var(--text-sm);
}

.tag-picker-chip button {
	display: inline-flex;
	border: none;
	background: transparent;
	color: var(--fg-faint);
	cursor: pointer;
	padding: 2px;
}

.tag-picker-chip button:hover {
	color: var(--danger);
}

.tag-picker-input-item {
	flex: 1;
	min-width: 96px;
}

.tag-picker-input {
	width: 100%;
	border: none;
	background: transparent;
	color: var(--fg);
	font-size: var(--text-base);
	font-family: inherit;
}

.tag-picker-input:focus {
	outline: none;
}

.tag-picker-menu {
	position: absolute;
	z-index: 20;
	top: calc(100% + 4px);
	left: 0;
	right: 0;
	max-height: 220px;
	overflow-y: auto;
	background: var(--surface);
	border: 1px solid var(--line);
	border-radius: var(--r-control);
	box-shadow: var(--shadow-pop);
	padding: 4px;
}

.tag-picker-option {
	padding: 6px 8px;
	border-radius: var(--r-control);
	cursor: pointer;
	font-size: var(--text-base);
}

.tag-picker-option-active {
	background: var(--surface-2);
}
</style>
