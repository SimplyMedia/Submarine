<template>
	<div class="settings-shell">
		<nav
			class="settings-nav"
			aria-label="Settings sections"
		>
			<NuxtLink
				v-for="item in sections"
				:key="item.to"
				:to="item.to"
				class="settings-nav-item"
				:class="{ 'settings-nav-item-active': route.path === item.to }"
			>
				{{ item.label }}
			</NuxtLink>
		</nav>
		<div class="settings-content">
			<NuxtPage />
		</div>
	</div>
</template>

<script setup lang="ts">
import { navigation } from '~/navigation'

const route = useRoute()
const sections = navigation.find(item => item.id === 'settings')?.children ?? []
</script>

<style scoped>
.settings-shell {
	display: flex;
	align-items: flex-start;
	gap: 32px;
}

.settings-nav {
	display: flex;
	flex-direction: column;
	gap: 2px;
	flex: none;
	width: 200px;
	position: sticky;
	top: 24px;
}

.settings-nav-item {
	height: 36px;
	display: flex;
	align-items: center;
	padding: 0 12px;
	border-radius: var(--r-control);
	color: var(--fg-muted);
	font-size: 0.875rem;
	text-decoration: none;
}

.settings-nav-item:hover {
	background: var(--surface-2);
	color: var(--fg);
}

.settings-nav-item-active {
	background: var(--accent-soft);
	color: var(--accent);
	font-weight: 500;
}

.settings-content {
	flex: 1;
	min-width: 0;
}

@media (max-width: 767px) {
	.settings-shell {
		flex-direction: column;
		gap: 16px;
	}

	.settings-nav {
		position: static;
		flex-direction: row;
		width: 100%;
		gap: 4px;
		overflow-x: auto;
		padding-bottom: 4px;
	}

	.settings-nav-item {
		flex: none;
		white-space: nowrap;
	}
}
</style>
