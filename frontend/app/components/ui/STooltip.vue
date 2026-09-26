<template>
	<TooltipProvider :delay-duration="300">
		<TooltipRoot :open="text ? undefined : false">
			<TooltipTrigger as-child>
				<slot />
			</TooltipTrigger>
			<TooltipContent
				:side="side"
				:side-offset="6"
				class="s-tooltip"
			>
				{{ text }}
			</TooltipContent>
		</TooltipRoot>
	</TooltipProvider>
</template>

<script setup lang="ts">
import { TooltipContent, TooltipProvider, TooltipRoot, TooltipTrigger } from 'reka-ui'

withDefaults(defineProps<{
	text?: string
	side?: 'top' | 'right' | 'bottom' | 'left'
}>(), {
	text: undefined,
	side: 'top',
})
</script>
