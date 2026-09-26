<template>
	<ToastProvider
		:duration="5000"
		swipe-direction="right"
	>
		<ToastRoot
			v-for="message in toasts"
			:key="message.id"
			force-mount
			class="s-toast"
			:class="`s-toast-${message.tone}`"
			@update:open="(isOpen: boolean) => !isOpen && dismiss(message.id)"
		>
			<ToastTitle class="s-toast-title">
				{{ message.title }}
			</ToastTitle>
			<ToastDescription
				v-if="message.description"
				class="s-toast-desc"
			>
				{{ message.description }}
			</ToastDescription>
			<ToastClose
				class="s-toast-close"
				aria-label="Dismiss"
			>
				<Icon
					name="lucide:x"
					aria-hidden="true"
				/>
			</ToastClose>
		</ToastRoot>
		<ToastViewport class="s-toast-viewport" />
	</ToastProvider>
</template>

<script setup lang="ts">
import { ToastClose, ToastDescription, ToastProvider, ToastRoot, ToastTitle, ToastViewport } from 'reka-ui'

const { toasts, dismiss } = useToast()
</script>
