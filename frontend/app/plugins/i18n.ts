import { availableLocales, i18n } from '~/i18n'

export default defineNuxtPlugin(async (nuxtApp) => {
	nuxtApp.vueApp.use(i18n)

	try {
		const api = useApi()
		const { data } = await api.GET('/api/v1/config/ui')
		i18n.global.locale.value = availableLocales.find(locale => locale.code === data?.language?.toLowerCase())?.code as typeof i18n.global.locale.value ?? 'en'
	}
	catch {
		i18n.global.locale.value = 'en'
	}

	watch(i18n.global.locale, (locale) => {
		if (import.meta.client) {
			document.documentElement.lang = locale
		}
	}, { immediate: true })
})
