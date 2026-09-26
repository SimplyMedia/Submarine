import { defineStore } from 'pinia'
import { toApiError, useApi } from '~/composables/useApi'
import type { components } from '~/types/api'

export type ReferenceTag = components['schemas']['TagDto']
export type ReferenceQualityProfile = components['schemas']['QualityProfileResource']
export type ReferenceLanguageProfile = components['schemas']['LanguageProfileResource']
export type ReferenceRootFolder = components['schemas']['RootFolderDto']
export type ReferenceUiConfig = components['schemas']['UiConfig']

/**
 * Cached cross-area lookups: tags, quality profiles, language profiles, root
 * folders and UI preferences. Every area reads this instead of refetching;
 * settings pages call `load(true)` after editing profiles/folders/tags.
 */
export const useReferenceStore = defineStore('reference', () => {
	const tags = ref<ReferenceTag[]>([])
	const qualityProfiles = ref<ReferenceQualityProfile[]>([])
	const languageProfiles = ref<ReferenceLanguageProfile[]>([])
	const rootFolders = ref<ReferenceRootFolder[]>([])
	const uiConfig = ref<ReferenceUiConfig | null>(null)
	const loaded = ref(false)
	const loadError = ref('')
	let inFlight: Promise<void> | null = null

	async function load(force = false) {
		if (loaded.value && !force) {
			return
		}
		if (inFlight) {
			return inFlight
		}
		const api = useApi()
		inFlight = (async () => {
			const [tagsResult, qualityResult, languageResult, rootFolderResult, uiResult] = await Promise.all([
				api.GET('/api/v1/tags'),
				api.GET('/api/v1/quality-profiles', { params: { query: { PageSize: 250 } } }),
				api.GET('/api/v1/language-profiles', { params: { query: { PageSize: 250 } } }),
				api.GET('/api/v1/root-folders'),
				api.GET('/api/v1/config/ui'),
			])
			loadError.value = [tagsResult, qualityResult, languageResult, rootFolderResult, uiResult].some(result => !result.data)
				? 'Could not load reference data. Check your connection and try again.'
				: ''
			if (tagsResult.data) {
				tags.value = tagsResult.data
			}
			if (qualityResult.data) {
				qualityProfiles.value = qualityResult.data.items
			}
			if (languageResult.data) {
				languageProfiles.value = languageResult.data.items
			}
			if (rootFolderResult.data) {
				rootFolders.value = rootFolderResult.data
			}
			if (uiResult.data) {
				uiConfig.value = uiResult.data
			}
			loaded.value = loadError.value === ''
		})()
		try {
			await inFlight
		}
		finally {
			inFlight = null
		}
	}

	async function createTag(label: string): Promise<ReferenceTag> {
		const api = useApi()
		const result = await api.POST('/api/v1/tags', { body: { label } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		tags.value = [...tags.value, result.data].sort((a, b) => a.label.localeCompare(b.label))
		return result.data
	}

	function tagLabel(id: number): string {
		return tags.value.find(tag => tag.id === id)?.label ?? `Tag ${id}`
	}

	function rootFolderPath(id: number | null | undefined): string {
		if (id == null) {
			return 'None'
		}
		return rootFolders.value.find(folder => folder.id === id)?.path ?? `Folder ${id}`
	}

	function qualityProfileName(id: number | null | undefined): string {
		if (id == null) {
			return 'None'
		}
		return qualityProfiles.value.find(profile => profile.id === id)?.name ?? `Profile ${id}`
	}

	function languageProfileName(id: number | null | undefined): string {
		if (id == null) {
			return 'None'
		}
		return languageProfiles.value.find(profile => profile.id === id)?.name ?? `Profile ${id}`
	}

	return {
		tags,
		qualityProfiles,
		languageProfiles,
		rootFolders,
		uiConfig,
		loaded,
		loadError,
		load,
		createTag,
		tagLabel,
		rootFolderPath,
		qualityProfileName,
		languageProfileName,
	}
})
