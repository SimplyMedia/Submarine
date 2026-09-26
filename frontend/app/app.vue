<template>
	<div class="min-h-dvh">
		<NuxtLayout>
			<NuxtPage />
		</NuxtLayout>
		<SToast />
	</div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { configureFormatLocalization } from '~/composables/useFormat'

const { t, d, n, locale } = useI18n()
configureFormatLocalization(
	() => locale.value,
	(key, fallback) => t(key, fallback),
	(date, options, formatLocale) => d(date, options, formatLocale),
	(date, options, formatLocale) => d(date, { ...options, part: true }, formatLocale) as Intl.DateTimeFormatPart[],
	(value, options, formatLocale) => n(value, options, formatLocale),
)
const system = useSystemStore()

useHead(() => {
	const instanceName = system.status?.instanceName || t('utils.app.defaultInstanceName', 'Submarine')
	return {
		titleTemplate: (title?: string) =>
			title && title !== 'Submarine' ? `${title} - ${instanceName}` : instanceName,
	}
})
</script>
