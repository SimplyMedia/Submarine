<template>
	<button
		type="button"
		class="provider-card"
		:class="{ 'provider-card-selected': selected }"
		:aria-pressed="selected"
	>
		<Icon
			:name="icon"
			class="provider-card-icon"
			aria-hidden="true"
		/>
		<span class="provider-card-label">{{ label }}</span>
	</button>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	icon: string
	label: string
	selected?: boolean
}>(), {
	selected: false,
})
</script>

<style scoped>
.provider-card {
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 8px;
	height: 88px;
	padding: 12px;
	border: 1px solid var(--line);
	border-radius: var(--r-panel);
	background: var(--surface);
	color: var(--fg);
	cursor: pointer;
	transition: border-color 120ms ease, background-color 120ms ease;
}

.provider-card:hover {
	border-color: var(--line-strong);
	background: var(--surface-2);
}

.provider-card-selected {
	border-color: var(--accent);
	background: var(--accent-soft);
}

.provider-card-icon {
	width: 22px;
	height: 22px;
	color: var(--fg-muted);
}

.provider-card-selected .provider-card-icon {
	color: var(--accent);
}

.provider-card-label {
	font-size: 0.8125rem;
	font-weight: 500;
	text-align: center;
}
</style>
