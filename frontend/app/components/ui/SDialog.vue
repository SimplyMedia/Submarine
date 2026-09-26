<template>
	<DialogRoot
		:open="open"
		@update:open="open = $event"
	>
		<DialogOverlay class="s-overlay" />
		<DialogContent
			class="s-dialog"
			:class="{ 's-dialog-wide': wide }"
			:aria-describedby="description ? undefined : ''"
		>
			<header class="s-dialog-head">
				<DialogTitle class="s-dialog-title">
					{{ title }}
				</DialogTitle>
				<DialogClose as-child>
					<SIconButton :label="$t('components.ui.SDialog.close')">
						<Icon
							name="lucide:x"
							aria-hidden="true"
						/>
					</SIconButton>
				</DialogClose>
			</header>
			<DialogDescription
				v-if="description"
				class="s-dialog-desc"
			>
				{{ description }}
			</DialogDescription>
			<div class="s-dialog-body">
				<slot />
			</div>
			<footer
				v-if="$slots.footer"
				class="s-dialog-foot"
			>
				<slot name="footer" />
			</footer>
		</DialogContent>
	</DialogRoot>
</template>

<script setup lang="ts">
import { DialogClose, DialogContent, DialogDescription, DialogOverlay, DialogRoot, DialogTitle } from 'reka-ui'

withDefaults(defineProps<{
	title: string
	description?: string
	/** Wide dialogs (960px) for interactive search; forms stay 560px. */
	wide?: boolean
}>(), {
	description: undefined,
	wide: false,
})

const open = defineModel<boolean>({ default: false })
</script>
