<template>
	<SDialog
		v-model="open"
		:title="title"
		:description="description"
	>
		<template #footer>
			<SButton
				variant="secondary"
				:disabled="busy"
				@click="open = false"
			>
				{{ $t('components.system.ConfirmDialog.cancel') }}
			</SButton>
			<SButton
				:variant="danger ? 'danger' : 'primary'"
				:loading="busy"
				@click="$emit('confirm')"
			>
				{{ confirmLabel }}
			</SButton>
		</template>
	</SDialog>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
	title: string
	description?: string
	confirmLabel: string
	danger?: boolean
	busy?: boolean
}>(), {
	description: undefined,
	danger: false,
	busy: false,
})

defineEmits<{
	confirm: []
}>()

const open = defineModel<boolean>({ default: false })
</script>
