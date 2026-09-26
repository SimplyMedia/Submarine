<template>
	<div
		v-if="dirty"
		class="save-bar"
		role="region"
		:aria-label="$t('components.shared.SettingsSaveBar.unsavedChanges')"
	>
		<span class="save-bar-text">{{ $t('components.shared.SettingsSaveBar.unsavedChangesMessage') }}</span>
		<div class="save-bar-actions">
			<SButton
				variant="ghost"
				:disabled="saving"
				@click="emit('discard')"
			>
				{{ $t('components.shared.SettingsSaveBar.discard') }}
			</SButton>
			<SButton
				variant="primary"
				:loading="saving"
				@click="emit('save')"
			>
				{{ $t('components.shared.SettingsSaveBar.saveChanges') }}
			</SButton>
		</div>
	</div>
</template>

<script setup lang="ts">
defineProps<{
	dirty: boolean
	saving?: boolean
}>()

const emit = defineEmits<{ save: [], discard: [] }>()
</script>

<style scoped>
.save-bar {
	position: sticky;
	bottom: 16px;
	z-index: 10;
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 16px;
	margin-top: 24px;
	padding: 12px 16px;
	border: 1px solid var(--line-strong);
	border-radius: var(--r-panel);
	background: var(--surface);
	box-shadow: var(--shadow-pop);
}

.save-bar-text {
	font-size: 0.875rem;
	color: var(--fg-muted);
}

.save-bar-actions {
	display: flex;
	gap: 8px;
	flex: none;
}
</style>
