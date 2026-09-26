<template>
	<div class="min-h-dvh">
		<NuxtLayout>
			<NuxtPage />
		</NuxtLayout>
		<SToast />
	</div>
</template>

<script setup lang="ts">
useHead({
	titleTemplate: (title?: string) => (title && title !== 'Submarine' ? `${title} - Submarine` : 'Submarine'),
})
</script>
