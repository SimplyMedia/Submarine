import { defineStore } from 'pinia'
import { useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

export type QualitySchemaGroup = components['schemas']['QualitySchemaGroup']
export type LanguageSchemaItem = components['schemas']['LanguageSchemaItem']
export type DownloadClientSchema = components['schemas']['DownloadClientSchema']
export type ImportListTypeSchema = components['schemas']['ImportListTypeSchema']
export type NotificationSchemaDto = components['schemas']['NotificationSchemaDto']
export type CustomFormatResource = components['schemas']['CustomFormatResource']

/**
 * Static schema and reference caches specific to the settings area: the
 * quality/language building blocks used by profile editors, the per-type
 * field schemas that drive SchemaForm dialogs, and the custom format list
 * referenced by the quality profile format-score table. Each loads once.
 *
 * Tags, quality/language profiles (as saved records) and root folders live
 * in app/stores/reference.ts; use that store for those.
 */
export const useSettingsStore = defineStore('settings', () => {
	const qualitySchema = ref<QualitySchemaGroup[]>([])
	const languageSchema = ref<LanguageSchemaItem[]>([])
	const downloadClientSchemas = ref<DownloadClientSchema[]>([])
	const importListSchemas = ref<ImportListTypeSchema[]>([])
	const notificationSchemas = ref<NotificationSchemaDto[]>([])
	const customFormats = ref<CustomFormatResource[]>([])
	const customFormatsLoadError = ref('')

	const loaded = {
		qualitySchema: false,
		languageSchema: false,
		downloadClientSchemas: false,
		importListSchemas: false,
		notificationSchemas: false,
		customFormats: false,
	}

	async function refreshCustomFormats() {
		const api = useApi()
		const { data } = await api.GET('/api/v1/custom-formats', { params: { query: { PageSize: 500 } } })
		if (!data) {
			customFormatsLoadError.value = 'Could not load custom formats. Check your connection and try again.'
			return
		}
		customFormatsLoadError.value = ''
		customFormats.value = data.items
		loaded.customFormats = true
	}

	async function ensureQualitySchema() {
		if (loaded.qualitySchema) {
			return
		}
		const api = useApi()
		const { data } = await api.GET('/api/v1/quality-profiles/schema')
		qualitySchema.value = data ?? []
		loaded.qualitySchema = true
	}

	async function ensureLanguageSchema() {
		if (loaded.languageSchema) {
			return
		}
		const api = useApi()
		const { data } = await api.GET('/api/v1/language-profiles/schema')
		languageSchema.value = data ?? []
		loaded.languageSchema = true
	}

	async function ensureDownloadClientSchemas() {
		if (loaded.downloadClientSchemas) {
			return
		}
		const api = useApi()
		const { data } = await api.GET('/api/v1/download-clients/schema')
		downloadClientSchemas.value = data ?? []
		loaded.downloadClientSchemas = true
	}

	async function ensureImportListSchemas() {
		if (loaded.importListSchemas) {
			return
		}
		const api = useApi()
		const { data } = await api.GET('/api/v1/import-lists/schema')
		importListSchemas.value = data ?? []
		loaded.importListSchemas = true
	}

	async function ensureNotificationSchemas() {
		if (loaded.notificationSchemas) {
			return
		}
		const api = useApi()
		const { data } = await api.GET('/api/v1/notifications/schema')
		notificationSchemas.value = data ?? []
		loaded.notificationSchemas = true
	}

	async function ensureCustomFormats() {
		if (!loaded.customFormats) {
			await refreshCustomFormats()
		}
	}

	return {
		qualitySchema,
		languageSchema,
		downloadClientSchemas,
		importListSchemas,
		notificationSchemas,
		customFormats,
		customFormatsLoadError,
		ensureQualitySchema,
		ensureLanguageSchema,
		ensureDownloadClientSchemas,
		ensureImportListSchemas,
		ensureNotificationSchemas,
		ensureCustomFormats,
		refreshCustomFormats,
	}
})
