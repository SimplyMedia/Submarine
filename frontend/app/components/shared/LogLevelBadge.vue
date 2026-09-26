<template>
	<SBadge :tone="logLevelTone(level)">
		{{ level }}
	</SBadge>
</template>

<script setup lang="ts">
import { logLevelTone } from '~/utils/system-labels'

defineProps<{
	/** Serilog level name: Verbose, Debug, Information, Warning, Error or Fatal. */
	level: string
}>()
</script>
