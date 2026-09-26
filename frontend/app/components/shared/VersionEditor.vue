<script setup lang="ts">
import { useI18n } from 'vue-i18n'
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
const { t } = useI18n()

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
			name: t('components.shared.VersionEditor.versionName', { number: props.modelValue.length + 1 }),
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
			<SField :label="index === 0 ? t('components.shared.VersionEditor.name') : undefined">
				<SInput
					:model-value="version.name"
					:placeholder="t('components.shared.VersionEditor.mainPlaceholder')"
					@update:model-value="patch(index, { name: $event })"
				/>
			</SField>
			<SField :label="index === 0 ? t('components.shared.VersionEditor.qualityProfile') : undefined">
				<SSelect
					:model-value="version.qualityProfileId != null ? String(version.qualityProfileId) : undefined"
					:options="qualityOptions"
					:placeholder="t('components.shared.VersionEditor.chooseProfile')"
					@update:model-value="patch(index, { qualityProfileId: Number($event) })"
				/>
			</SField>
			<SField :label="index === 0 ? t('components.shared.VersionEditor.languageProfile') : undefined">
				<SSelect
					:model-value="version.languageProfileId != null ? String(version.languageProfileId) : undefined"
					:options="languageOptions"
					:placeholder="t('components.shared.VersionEditor.chooseProfile')"
					@update:model-value="patch(index, { languageProfileId: Number($event) })"
				/>
			</SField>
			<SField :label="index === 0 ? t('components.shared.VersionEditor.rootFolder') : undefined">
				<SSelect
					:model-value="version.rootFolderId != null ? String(version.rootFolderId) : undefined"
					:options="rootFolderOptions"
					:placeholder="t('components.shared.VersionEditor.useDefault')"
					@update:model-value="patch(index, { rootFolderId: $event ? Number($event) : null })"
				/>
			</SField>
			<SIconButton
				:label="t('components.shared.VersionEditor.removeVersion')"
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
			{{ t('components.shared.VersionEditor.addVersion') }}
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
