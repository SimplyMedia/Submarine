<script setup lang="ts">
definePageMeta({
	middleware: [
		() => navigateTo('/system/status', { replace: true }),
	],
})
</script>

<template>
	<div />
</template>
