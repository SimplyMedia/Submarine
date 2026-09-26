<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useDirtyForm } from '~/composables/useDirtyForm'
import { useReferenceStore } from '~/stores/reference'
import { applyUiFormat } from '~/composables/useFormat'
import { themeOptions as themeOptionKeys } from '~/utils/settings-labels'
import { availableLocales } from '~/i18n'
import type { components } from '~/types/api'

type UiConfig = components['schemas']['UiConfig']

definePageMeta({ layout: 'default' })
const { t, locale } = useI18n()
useHead(() => ({ title: t('settings.ui.title') }))

const api = useApi()
const { toast } = useToast()
const colorMode = useColorMode()
const reference = useReferenceStore()

const firstDayOfWeekOptions = computed(() => [
	{ value: '0', label: t('settings.ui.days.sunday') },
	{ value: '1', label: t('settings.ui.days.monday') },
	{ value: '2', label: t('settings.ui.days.tuesday') },
	{ value: '3', label: t('settings.ui.days.wednesday') },
	{ value: '4', label: t('settings.ui.days.thursday') },
	{ value: '5', label: t('settings.ui.days.friday') },
	{ value: '6', label: t('settings.ui.days.saturday') },
])

const languageOptions = availableLocales.map(language => ({
	value: language.code,
	label: t(language.nameKey),
}))
const themeOptions = computed(() => themeOptionKeys.map(option => ({
	...option,
	label: t(option.label),
})))

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

function applyLanguage(language: string | undefined) {
	locale.value = availableLocales.find(item => item.code === language?.toLowerCase())?.code ?? 'en'
}

watch(() => draft.value.language, applyLanguage)

async function load() {
	loading.value = true
	const result = await api.GET('/api/v1/config/ui')
	if (result.data) {
		markSaved(result.data)
		applyTheme(result.data.theme)
		applyLanguage(result.data.language)
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
		applyLanguage(result.data.language)
		applyUiFormat(result.data)
		reference.uiConfig = result.data
		toast({ title: t('common.saved'), tone: 'ok' })
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		toast({ title: t('common.couldNotSave'), tone: 'danger', description: apiError.message })
	}
	finally {
		saving.value = false
	}
}
</script>

<template>
	<div>
		<SPageHeader :title="t('settings.ui.title')" />

		<SSection :title="t('settings.ui.appearance')">
			<SSpinner v-if="loading" />
			<div
				v-else
				class="settings-form"
			>
				<SField
					:label="t('settings.ui.language')"
					:hint="t('settings.ui.languageDescription')"
					control-id="ui-language"
				>
					<SSelect
						v-model="draft.language"
						control-id="ui-language"
						:options="languageOptions"
					/>
				</SField>
				<SField
					:label="t('settings.ui.theme')"
					control-id="ui-theme"
				>
					<SSelect
						v-model="draft.theme"
						control-id="ui-theme"
						:options="themeOptions"
					/>
				</SField>
				<SField
					:label="t('settings.ui.firstDayOfWeek')"
					control-id="ui-first-day"
				>
					<SSelect
						v-model="firstDayOfWeekValue"
						control-id="ui-first-day"
						:options="firstDayOfWeekOptions"
					/>
				</SField>
				<SField
					:label="t('settings.ui.shortDateFormat')"
					:hint="t('settings.ui.shortDateFormatHint')"
					control-id="ui-short-date"
				>
					<SInput
						id="ui-short-date"
						v-model="draft.shortDateFormat"
					/>
				</SField>
				<SField
					:label="t('settings.ui.longDateFormat')"
					:hint="t('settings.ui.longDateFormatHint')"
					control-id="ui-long-date"
				>
					<SInput
						id="ui-long-date"
						v-model="draft.longDateFormat"
					/>
				</SField>
				<SField
					:label="t('settings.ui.timeFormat')"
					:hint="t('settings.ui.timeFormatHint')"
					control-id="ui-time-format"
				>
					<SInput
						id="ui-time-format"
						v-model="draft.timeFormat"
					/>
				</SField>
				<SField :label="t('settings.ui.showRelativeDates')">
					<SCheckbox
						v-model="draft.showRelativeDates"
						:label="t('settings.ui.relativeDatesDescription')"
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
