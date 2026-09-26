<template>
	<PopoverRoot>
		<PopoverTrigger as-child>
			<slot name="trigger" />
		</PopoverTrigger>
		<PopoverContent
			:side-offset="6"
			align="end"
			class="s-popover"
		>
			<slot />
		</PopoverContent>
	</PopoverRoot>
</template>

<script setup lang="ts">
import { PopoverContent, PopoverRoot, PopoverTrigger } from 'reka-ui'
</script>
