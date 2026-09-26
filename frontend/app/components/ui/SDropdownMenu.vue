<template>
	<DropdownMenuRoot>
		<DropdownMenuTrigger as-child>
			<slot name="trigger" />
		</DropdownMenuTrigger>
		<DropdownMenuContent
			:side-offset="4"
			align="end"
			class="s-menu"
		>
			<template
				v-for="(entry, index) in items"
				:key="index"
			>
				<DropdownMenuSeparator
					v-if="'separator' in entry"
					class="s-menu-sep"
				/>
				<DropdownMenuItem
					v-else
					:disabled="entry.disabled"
					class="s-menu-item"
					:class="{ 's-menu-danger': entry.danger }"
					@select="entry.onSelect?.()"
				>
					<Icon
						v-if="entry.icon"
						:name="entry.icon"
						class="s-menu-icon"
						aria-hidden="true"
					/>
					<span>{{ entry.label }}</span>
				</DropdownMenuItem>
			</template>
		</DropdownMenuContent>
	</DropdownMenuRoot>
</template>

<script setup lang="ts">
import { DropdownMenuContent, DropdownMenuItem, DropdownMenuRoot, DropdownMenuSeparator, DropdownMenuTrigger } from 'reka-ui'
import type { MenuEntryOrSeparator } from '~/types/ui'

defineProps<{
	items: MenuEntryOrSeparator[]
}>()
</script>
