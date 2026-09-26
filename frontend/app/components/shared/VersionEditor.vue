<script setup lang="ts">
/**
 * Draft version list editor for the add-series/add-movie forms (before the
 * item exists, so there is nothing to persist per row yet). At least one
 * version is required; rootFolderId null means "use the item's root folder".
 */
import type { DraftVersion, SelectOption } from '~/types/ui'

const props = defineProps<{
	modelValue: DraftVersion[]
	rootFolderOptions: SelectOption[]
}>()

const emit = defineEmits<{
	'update:modelValue': [value: DraftVersion[]]
}>()

const reference = useReferenceStore()

const qualityOptions = computed<SelectOption[]>(() =>
	reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name })),
)
const languageOptions = computed<SelectOption[]>(() =>
	reference.languageProfiles.map(profile => ({ value: String(profile.id), label: profile.name })),
)

function patch(index: number, changes: Partial<DraftVersion>) {
	const next = props.modelValue.map((version, i) => (i === index ? { ...version, ...changes } : version))
	emit('update:modelValue', next)
}

function addVersion() {
	const template = props.modelValue[0]
	emit('update:modelValue', [
		...props.modelValue,
		{
			name: `Version ${props.modelValue.length + 1}`,
			qualityProfileId: template?.qualityProfileId ?? reference.qualityProfiles[0]?.id ?? null,
			languageProfileId: template?.languageProfileId ?? reference.languageProfiles[0]?.id ?? null,
			rootFolderId: null,
		},
	])
}

function removeVersion(index: number) {
	if (props.modelValue.length <= 1) {
		return
	}
	emit('update:modelValue', props.modelValue.filter((_, i) => i !== index))
}
</script>

<template>
	<div class="version-editor">
		<div
			v-for="(version, index) in modelValue"
			:key="index"
			class="version-editor-row"
		>
			<SField :label="index === 0 ? 'Name' : undefined">
				<SInput
					:model-value="version.name"
					placeholder="Main"
					@update:model-value="patch(index, { name: $event })"
				/>
			</SField>
			<SField :label="index === 0 ? 'Quality profile' : undefined">
				<SSelect
					:model-value="version.qualityProfileId != null ? String(version.qualityProfileId) : undefined"
					:options="qualityOptions"
					placeholder="Choose a profile"
					@update:model-value="patch(index, { qualityProfileId: Number($event) })"
				/>
			</SField>
			<SField :label="index === 0 ? 'Language profile' : undefined">
				<SSelect
					:model-value="version.languageProfileId != null ? String(version.languageProfileId) : undefined"
					:options="languageOptions"
					placeholder="Choose a profile"
					@update:model-value="patch(index, { languageProfileId: Number($event) })"
				/>
			</SField>
			<SField :label="index === 0 ? 'Root folder' : undefined">
				<SSelect
					:model-value="version.rootFolderId != null ? String(version.rootFolderId) : undefined"
					:options="rootFolderOptions"
					placeholder="Use the default"
					@update:model-value="patch(index, { rootFolderId: $event ? Number($event) : null })"
				/>
			</SField>
			<SIconButton
				label="Remove version"
				:disabled="modelValue.length <= 1"
				@click="removeVersion(index)"
			>
				<Icon
					name="lucide:trash-2"
					aria-hidden="true"
				/>
			</SIconButton>
		</div>
		<SButton
			size="sm"
			@click="addVersion"
		>
			Add version
		</SButton>
	</div>
</template>

<style scoped>
.version-editor {
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.version-editor-row {
	display: grid;
	grid-template-columns: 1fr 1fr 1fr 1fr auto;
	gap: 8px;
	align-items: end;
}

@media (max-width: 767px) {
	.version-editor-row {
		grid-template-columns: 1fr;
	}
}
</style>
