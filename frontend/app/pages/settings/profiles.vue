<script setup lang="ts">
import { useI18n } from 'vue-i18n'

definePageMeta({ layout: 'default' })
const { t } = useI18n()
useHead({ title: t('pages.settings.profiles.title') })

const tab = ref('quality')
const tabs = [
	{ value: 'quality', label: t('pages.settings.profiles.quality') },
	{ value: 'language', label: t('pages.settings.profiles.language') },
	{ value: 'delay', label: t('pages.settings.profiles.delay') },
	{ value: 'release', label: t('pages.settings.profiles.release') },
]
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.settings.profiles.title')" />
		<STabs
			v-model="tab"
			:tabs="tabs"
			:label="t('pages.settings.profiles.profileTypes')"
		>
			<template #panel-quality>
				<QualityProfilesPanel />
			</template>
			<template #panel-language>
				<LanguageProfilesPanel />
			</template>
			<template #panel-delay>
				<DelayProfilesPanel />
			</template>
			<template #panel-release>
				<ReleaseProfilesPanel />
			</template>
		</STabs>
	</div>
</template>
