<template>
	<div class="min-h-dvh">
		<NuxtLayout>
			<NuxtPage />
		</NuxtLayout>
		<SToast />
	</div>
</template>

<script setup lang="ts">
const system = useSystemStore()

useHead(() => {
	const instanceName = system.status?.instanceName || 'Submarine'
	return {
		titleTemplate: (title?: string) =>
			title && title !== 'Submarine' ? `${title} - ${instanceName}` : instanceName,
	}
})
</script>
