<template>
	<nav
		class="s-tabs sub-nav"
		:aria-label="label"
	>
		<NuxtLink
			v-for="item in items"
			:key="item.to"
			:to="item.to"
			class="s-tab"
			:data-state="route.path === item.to ? 'active' : undefined"
			:aria-current="route.path === item.to ? 'page' : undefined"
		>
			{{ t(item.key) }}
		</NuxtLink>
	</nav>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { NavChild } from '~/navigation'

const { t } = useI18n()
defineProps<{
	label: string
	items: NavChild[]
}>()

const route = useRoute()
</script>

<style scoped>
.sub-nav {
	margin-bottom: 24px;
	overflow-x: auto;
}

.sub-nav .s-tab {
	display: inline-flex;
	align-items: center;
	text-decoration: none;
	flex: none;
}
</style>
