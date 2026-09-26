<script setup lang="ts">
definePageMeta({ layout: 'default' })
useHead({ title: 'Profiles' })

const tab = ref('quality')
const tabs = [
	{ value: 'quality', label: 'Quality' },
	{ value: 'language', label: 'Language' },
	{ value: 'delay', label: 'Delay' },
	{ value: 'release', label: 'Release' },
]
</script>

<template>
	<div>
		<SPageHeader title="Profiles" />
		<STabs
			v-model="tab"
			:tabs="tabs"
			label="Profile types"
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
