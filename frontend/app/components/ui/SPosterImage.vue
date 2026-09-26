<template>
	<span class="s-poster">
		<img
			v-if="src && !failed"
			:src="src"
			:alt="alt"
			loading="lazy"
			class="s-poster-img"
			@error="failed = true"
		>
		<span
			v-else
			class="s-poster-fallback"
			aria-hidden="true"
		>{{ initials }}</span>
	</span>
</template>

<script setup lang="ts">
const props = defineProps<{
	src?: string | null
	alt: string
}>()

const failed = ref(false)

watch(() => props.src, () => {
	failed.value = false
})

const initials = computed(() => {
	const words = props.alt.split(/\s+/).filter(word => /[a-z0-9]/i.test(word))
	if (words.length === 0) {
		return '?'
	}
	return words.slice(0, 2).map(word => word[0]!.toUpperCase()).join('')
})
</script>
