<template>
	<form
		class="schema-form"
		@submit.prevent
	>
		<template
			v-for="field in fields"
			:key="field.name"
		>
			<p
				v-if="field.type === 'info'"
				class="schema-form-info"
			>
				{{ helpText(field) }}
			</p>
			<div
				v-else-if="field.type === 'checkbox'"
				class="schema-form-checkbox"
			>
				<SCheckbox
					:label="label(field)"
					:model-value="boolValue(field)"
					@update:model-value="setValue(field, $event)"
				/>
				<p
					v-if="helpText(field)"
					class="s-field-hint"
				>
					{{ helpText(field) }}
				</p>
				<p
					v-if="fieldErrors?.[field.name]?.[0]"
					class="s-field-error"
					role="alert"
				>
					{{ fieldErrors[field.name]?.[0] }}
				</p>
			</div>
			<SField
				v-else
				:label="label(field)"
				:hint="helpText(field) ?? undefined"
				:error="fieldErrors?.[field.name]?.[0]"
				:control-id="`schema-${field.name}`"
			>
				<SInput
					v-if="field.type === 'text' || field.type === 'password' || field.type === 'url'"
					:id="`schema-${field.name}`"
					:type="field.type"
					:model-value="stringValue(field)"
					:invalid="hasError(field)"
					@update:model-value="setValue(field, $event)"
				/>
				<SInput
					v-else-if="field.type === 'number'"
					:id="`schema-${field.name}`"
					type="number"
					:model-value="stringValue(field)"
					:invalid="hasError(field)"
					@update:model-value="setValue(field, $event === '' ? null : Number($event))"
				/>
				<SSelect
					v-else-if="field.type === 'select'"
					:control-id="`schema-${field.name}`"
					:model-value="stringValue(field)"
					:options="selectOptions(field)"
					:invalid="hasError(field)"
					@update:model-value="setValue(field, $event)"
				/>
				<div
					v-else-if="field.type === 'tags'"
					class="schema-form-tags"
				>
					<span
						v-for="(tag, index) in tagsValue(field)"
						:key="`${field.name}-${index}`"
						class="schema-form-tag"
					>
						{{ tag }}
						<button
							type="button"
							:aria-label="t('components.shared.SchemaForm.removeTag', { tag })"
							@click="removeTag(field, index)"
						>
							<Icon
								name="lucide:x"
								aria-hidden="true"
							/>
						</button>
					</span>
					<input
						:id="`schema-${field.name}`"
						type="text"
						class="schema-form-tag-input"
						:placeholder="tagsValue(field).length === 0 ? t('components.shared.SchemaForm.tagsPlaceholder') : ''"
						@keydown.enter.prevent="addTag(field, $event)"
						@keydown="onTagKeydown(field, $event)"
					>
				</div>
			</SField>
		</template>
	</form>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { humanizeEnumValue, humanizeFieldName } from '~/utils/settings-labels'
import type { SchemaField } from '~/types/schema-form'

const { t } = useI18n()

const props = withDefaults(defineProps<{
	fields: SchemaField[]
	modelValue: Record<string, unknown>
	providerType: string
	fieldErrors?: Record<string, string[]>
	/** Overrides the default humanized label for a select option value. */
	optionLabel?: (field: SchemaField, value: string) => string
}>(), {
	fieldErrors: undefined,
	optionLabel: undefined,
})

const emit = defineEmits<{ 'update:modelValue': [Record<string, unknown>] }>()

function label(field: SchemaField): string {
	const defaultLabel = field.label === 'Url' ? humanizeFieldName(field.name) : field.label
	return t(`schema.${props.providerType}.${field.name}.label`, defaultLabel)
}

function helpText(field: SchemaField): string | null {
	return field.helpText
		? t(`schema.${props.providerType}.${field.name}.helpText`, field.helpText)
		: null
}

function rawValue(field: SchemaField): unknown {
	const value = props.modelValue[field.name]
	return value === undefined ? field.default : value
}

function stringValue(field: SchemaField): string {
	const value = rawValue(field)
	return value === null || value === undefined ? '' : String(value)
}

function boolValue(field: SchemaField): boolean {
	return rawValue(field) === true
}

function tagsValue(field: SchemaField): string[] {
	const value = rawValue(field)
	return Array.isArray(value) ? value as string[] : []
}

function hasError(field: SchemaField): boolean {
	return (props.fieldErrors?.[field.name]?.length ?? 0) > 0
}

function selectOptions(field: SchemaField) {
	return (field.options ?? []).map(value => ({
		value,
		label: props.optionLabel?.(field, value) ?? humanizeEnumValue(value),
	}))
}

function setValue(field: SchemaField, value: unknown) {
	emit('update:modelValue', { ...props.modelValue, [field.name]: value })
}

function addTag(field: SchemaField, event: Event) {
	const input = event.target as HTMLInputElement
	const value = input.value.trim().replace(/,$/, '')
	if (value.length === 0) {
		return
	}
	setValue(field, [...tagsValue(field), value])
	input.value = ''
}

function onTagKeydown(field: SchemaField, event: KeyboardEvent) {
	if (event.key === ',') {
		event.preventDefault()
		addTag(field, event)
	}
}

function removeTag(field: SchemaField, index: number) {
	setValue(field, tagsValue(field).filter((_, i) => i !== index))
}
</script>

<style scoped>
.schema-form {
	display: grid;
	gap: 16px;
}

.schema-form-info {
	font-size: 0.8125rem;
	color: var(--fg-muted);
}

.schema-form-tags {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 6px;
	min-height: var(--control-h);
	padding: 6px 8px;
	border: 1px solid var(--line-strong);
	border-radius: var(--r-control);
	background: var(--surface);
}

.schema-form-tag {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	height: 24px;
	padding: 0 6px 0 10px;
	border-radius: 999px;
	background: var(--surface-2);
	font-size: 0.8125rem;
}

.schema-form-tag-remove {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 16px;
	height: 16px;
	border-radius: 999px;
	color: var(--fg-muted);
	cursor: pointer;
}

.schema-form-tag-remove:hover {
	background: var(--line);
	color: var(--fg);
}

.schema-form-tag-input {
	flex: 1;
	min-width: 120px;
	height: 24px;
	border: none;
	background: transparent;
	color: var(--fg);
	font: inherit;
	font-size: 0.875rem;
}

.schema-form-tag-input:focus {
	outline: none;
}
</style>
