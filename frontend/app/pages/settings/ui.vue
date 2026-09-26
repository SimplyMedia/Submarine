<script setup lang="ts">
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import { useReferenceStore } from '~/stores/reference'
import { applyUiFormat } from '~/composables/useFormat'
import { themeOptions } from '~/utils/settings-labels'
import type { components } from '~/types/api'

type UiConfig = components['schemas']['UiConfig']

definePageMeta({ layout: 'default' })
useHead({ title: 'Interface' })

const api = useApi()
const { toast } = useToast()
const colorMode = useColorMode()
const reference = useReferenceStore()

const firstDayOfWeekOptions = [
	{ value: '0', label: 'Sunday' },
	{ value: '1', label: 'Monday' },
	{ value: '2', label: 'Tuesday' },
	{ value: '3', label: 'Wednesday' },
	{ value: '4', label: 'Thursday' },
	{ value: '5', label: 'Friday' },
	{ value: '6', label: 'Saturday' },
]

const loading = ref(true)
const saving = ref(false)
const draft = ref<UiConfig>({})
const { isDirty, markSaved, revert } = useDirtyForm(draft)

const firstDayOfWeekValue = computed({
	get: () => String(draft.value.firstDayOfWeek ?? 0),
	set: (value: string) => { draft.value.firstDayOfWeek = Number(value) },
})

function applyTheme(theme: UiConfig['theme']) {
	colorMode.preference = theme === 'LIGHT' ? 'light' : theme === 'DARK' ? 'dark' : 'system'
}

async function load() {
	loading.value = true
	const result = await api.GET('/api/v1/config/ui')
	if (result.data) {
		markSaved(result.data)
		applyTheme(result.data.theme)
		applyUiFormat(result.data)
	}
	loading.value = false
}
void load()

async function save() {
	saving.value = true
	try {
		const result = await api.PUT('/api/v1/config/ui', { body: draft.value })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		markSaved(result.data)
		applyTheme(result.data.theme)
		applyUiFormat(result.data)
		reference.uiConfig = result.data
		toast({ title: 'Saved', tone: 'ok' })
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: 'Could not save', tone: 'danger', description: apiError.message })
	}
	finally {
		saving.value = false
	}
}
</script>

<template>
	<div>
		<SPageHeader title="Interface" />

		<SSection title="Appearance">
			<SSpinner v-if="loading" />
			<div
				v-else
				class="settings-form"
			>
				<SField
					label="Theme"
					control-id="ui-theme"
				>
					<SSelect
						v-model="draft.theme"
						control-id="ui-theme"
						:options="themeOptions"
					/>
				</SField>
				<SField
					label="First day of week"
					control-id="ui-first-day"
				>
					<SSelect
						v-model="firstDayOfWeekValue"
						control-id="ui-first-day"
						:options="firstDayOfWeekOptions"
					/>
				</SField>
				<SField
					label="Short date format"
					hint="Moment.js-style tokens, e.g. MMM D, YYYY"
					control-id="ui-short-date"
				>
					<SInput
						id="ui-short-date"
						v-model="draft.shortDateFormat"
					/>
				</SField>
				<SField
					label="Long date format"
					hint="Moment.js-style tokens, e.g. dddd, MMMM D, YYYY"
					control-id="ui-long-date"
				>
					<SInput
						id="ui-long-date"
						v-model="draft.longDateFormat"
					/>
				</SField>
				<SField
					label="Time format"
					hint="Moment.js-style tokens, e.g. h:mm A"
					control-id="ui-time-format"
				>
					<SInput
						id="ui-time-format"
						v-model="draft.timeFormat"
					/>
				</SField>
				<SField label="Show relative dates">
					<SCheckbox
						v-model="draft.showRelativeDates"
						label="Show dates like &quot;in 2 hours&quot; instead of exact timestamps"
					/>
				</SField>
			</div>
		</SSection>

		<SettingsSaveBar
			:dirty="isDirty"
			:saving="saving"
			@save="save"
			@discard="revert"
		/>
	</div>
</template>

<style scoped>
.settings-form {
	display: grid;
	gap: 16px;
	max-width: 480px;
}
</style>
