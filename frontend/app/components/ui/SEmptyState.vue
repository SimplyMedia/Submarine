<template>
	<div class="s-empty">
		<Icon
			:name="icon"
			class="s-empty-icon"
			aria-hidden="true"
		/>
		<p>{{ message }}</p>
		<div v-if="$slots.action">
			<slot name="action" />
		</div>
	</div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	message: string
	icon?: string
}>(), {
	icon: 'lucide:inbox',
})
</script>
