<script setup lang="ts">
import { useI18n } from 'vue-i18n'
/**
 * Text input for an absolute path plus a folder browser backed by
 * GET /api/v1/filesystem. The path may not exist yet (recycle bin, a new
 * root folder), so typing is always allowed regardless of browse results.
 */
const props = withDefaults(defineProps<{
	controlId?: string
	placeholder?: string
}>(), {
	controlId: undefined,
	placeholder: undefined,
})

const { t } = useI18n()

const path = defineModel<string>({ default: '' })

const api = useApi()
const browsePath = ref('')
const directories = ref<Array<{ name: string, path: string }>>([])
const parent = ref<string | null>(null)
const loading = ref(false)
const error = ref('')

async function browse(target: string) {
	loading.value = true
	error.value = ''
	try {
		const result = await api.GET('/api/v1/filesystem', {
			params: { query: { path: target || undefined, includeFiles: false } },
		})
		if (result.data) {
			directories.value = result.data.directories
			parent.value = result.data.parent
			browsePath.value = target
		}
		else {
			error.value = t('components.shared.PathPicker.readError')
		}
	}
	catch {
		error.value = t('components.shared.PathPicker.readError')
	}
	finally {
		loading.value = false
	}
}

function onOpen() {
	void browse(path.value)
}

function useCurrent() {
	path.value = browsePath.value
}
</script>

<template>
	<div class="path-picker">
		<input
			:id="controlId"
			v-model="path"
			type="text"
			class="s-input"
			:placeholder="props.placeholder ?? t('components.shared.PathPicker.placeholder')"
		>
		<SPopover>
			<template #trigger>
				<SIconButton
					:label="t('components.shared.PathPicker.browseFolders')"
					@click="onOpen"
				>
					<Icon
						name="lucide:folder-open"
						aria-hidden="true"
					/>
				</SIconButton>
			</template>
			<div class="path-picker-browser">
				<p class="path-picker-current">
					{{ browsePath || '/' }}
				</p>
				<SSpinner v-if="loading" />
				<p
					v-else-if="error"
					class="s-field-error"
				>
					{{ error }}
				</p>
				<ul
					v-else
					class="path-picker-list"
				>
					<li v-if="parent !== null">
						<button
							type="button"
							class="path-picker-entry"
							@click="browse(parent)"
						>
							<Icon
								name="lucide:corner-left-up"
								aria-hidden="true"
							/> ..
						</button>
					</li>
					<li
						v-for="dir in directories"
						:key="dir.path"
					>
						<button
							type="button"
							class="path-picker-entry"
							@click="browse(dir.path)"
						>
							<Icon
								name="lucide:folder"
								aria-hidden="true"
							/> {{ dir.name }}
						</button>
					</li>
					<li v-if="directories.length === 0 && parent === null">
						<p class="path-picker-empty">
							{{ $t('components.shared.PathPicker.noSubfolders') }}
						</p>
					</li>
				</ul>
				<SButton
					variant="primary"
					size="sm"
					@click="useCurrent"
				>
					{{ $t('components.shared.PathPicker.useThisFolder') }}
				</SButton>
			</div>
		</SPopover>
	</div>
</template>

<style scoped>
.path-picker {
	display: flex;
	gap: 8px;
	align-items: center;
}

.path-picker > .s-input {
	flex: 1;
}

.path-picker-browser {
	display: flex;
	flex-direction: column;
	gap: 8px;
	width: 320px;
}

.path-picker-current {
	font-size: var(--text-xs);
	color: var(--fg-muted);
	word-break: break-all;
}

.path-picker-list {
	max-height: 240px;
	overflow-y: auto;
	display: flex;
	flex-direction: column;
	gap: 2px;
}

.path-picker-entry {
	display: flex;
	align-items: center;
	gap: 8px;
	width: 100%;
	padding: 6px 8px;
	border: none;
	border-radius: var(--r-control);
	background: transparent;
	color: var(--fg);
	text-align: left;
	cursor: pointer;
	font-size: var(--text-base);
}

.path-picker-entry:hover {
	background: var(--surface-2);
}

.path-picker-empty {
	color: var(--fg-faint);
	font-size: var(--text-sm);
	padding: 6px 8px;
}
</style>
