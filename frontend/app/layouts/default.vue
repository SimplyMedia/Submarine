<template>
	<div class="shell">
		<a
			class="skip-link"
			href="#content"
		>{{ t('utils.layout.skipToContent', 'Skip to content') }}</a>

		<aside class="rail">
			<div class="rail-brand">
				<NuxtLink
					to="/series"
					class="rail-wordmark"
				>Submarine</NuxtLink>
				<span
					class="rail-state"
					:class="`tone-${hubTone}`"
					:title="t('utils.layout.hubStatus', { status: t(hubLabel) }, 'Hub {status}')"
				>
					<span
						class="state-dot"
						aria-hidden="true"
					/>
					<span
						v-if="railExpanded"
						class="rail-state-text"
					>{{ t(hubLabel) }}</span>
				</span>

				<nav
					class="rail-nav"
					:aria-label="t('utils.layout.primaryNavigation', 'Primary')"
				>
					<div
						v-for="item in navigation"
						:key="item.id"
						class="rail-group"
					>
						<STooltip
							:text="railExpanded ? undefined : t(item.key)"
							side="right"
						>
							<NuxtLink
								:to="item.to"
								class="rail-item"
								:class="{ 'rail-item-active': isActive(item) }"
								:aria-current="isActive(item) ? 'page' : undefined"
							>
								<Icon
									:name="item.icon"
									class="rail-item-icon"
									aria-hidden="true"
								/>
								<span
									v-if="railExpanded"
									class="rail-item-label"
								>{{ t(item.key) }}</span>
							</NuxtLink>
						</STooltip>
						<div
							v-if="railExpanded && isActive(item) && item.children"
							class="rail-children"
						>
							<NuxtLink
								v-for="child in item.children"
								:key="child.to"
								:to="child.to"
								class="rail-child"
								:class="{ 'rail-child-active': route.path === child.to }"
							>
								{{ t(child.key) }}
							</NuxtLink>
						</div>
					</div>
				</nav>

				<div class="rail-foot">
					<TaskIndicator
						:icon-only="!railExpanded"
						:trigger-class="railExpanded ? 'rail-item' : ''"
					/>
					<ThemeMenu
						:icon-only="!railExpanded"
						:trigger-class="railExpanded ? 'rail-item' : ''"
					/>
					<SDropdownMenu :items="userItems">
						<template #trigger>
							<button
								v-if="railExpanded"
								type="button"
								class="rail-item"
							>
								<span
									class="rail-avatar"
									aria-hidden="true"
								>{{ userInitials }}</span>
								<span class="rail-item-label">{{ displayName }}</span>
							</button>
							<SIconButton
								v-else
								:label="t('utils.layout.signedInAs', { name: displayName }, 'Signed in as {name}')"
							>
								<span
									class="rail-avatar"
									aria-hidden="true"
								>{{ userInitials }}</span>
							</SIconButton>
						</template>
					</SDropdownMenu>
					<div
						class="rail-health"
						:class="`tone-${healthTone}`"
					>
						<span
							class="state-dot"
							aria-hidden="true"
						/>
						<span
							v-if="railExpanded"
							class="rail-health-text"
						>{{ healthText }}</span>
					</div>
				</div>
			</div>
		</aside>

		<div class="shell-main">
			<main
				id="content"
				class="content"
			>
				<slot />
			</main>
		</div>

		<nav
			class="tabbar"
			:aria-label="t('utils.layout.primaryNavigation', 'Primary')"
		>
			<NuxtLink
				v-for="item in primaryNav"
				:key="item.id"
				:to="item.to"
				class="tabbar-item"
				:class="{ 'tabbar-item-active': isActive(item) }"
			>
				<Icon
					:name="item.icon"
					class="tabbar-icon"
					aria-hidden="true"
				/>
				<span>{{ t(item.key) }}</span>
			</NuxtLink>
			<button
				type="button"
				class="tabbar-item"
				@click="moreOpen = true"
			>
				<Icon
					name="lucide:menu"
					class="tabbar-icon"
					aria-hidden="true"
				/>
				<span>{{ t('utils.layout.more', 'More') }}</span>
			</button>
		</nav>

		<SDialog
			v-model="moreOpen"
			:title="t('utils.layout.more', 'More')"
		>
			<nav
				class="more-nav"
				:aria-label="t('utils.layout.allPages', 'All pages')"
			>
				<div
					v-for="item in navigation"
					:key="item.id"
					class="more-group"
				>
					<NuxtLink
						:to="item.to"
						class="more-item more-item-parent"
						@click="moreOpen = false"
					>
						<Icon
							:name="item.icon"
							class="more-icon"
							aria-hidden="true"
						/>
						{{ t(item.key) }}
					</NuxtLink>
					<NuxtLink
						v-for="child in item.children ?? []"
						:key="child.to"
						:to="child.to"
						class="more-item more-item-child"
						@click="moreOpen = false"
					>
						{{ t(child.key) }}
					</NuxtLink>
				</div>
			</nav>
			<template #footer>
				<ThemeMenu />
				<SButton
					variant="secondary"
					@click="signOut"
				>
					{{ t('utils.layout.signOut', 'Sign out') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { navigation } from '~/navigation'
import type { NavItem } from '~/navigation'
import type { MenuEntry } from '~/types/ui'
import type { components } from '~/types/api'
import { useReferenceStore } from '~/stores/reference'
import { applyUiFormat } from '~/composables/useFormat'

const { t } = useI18n()
const route = useRoute()
const auth = useAuthStore()
const system = useSystemStore()
const reference = useReferenceStore()
const events = useEvents()
const colorMode = useColorMode()

const railExpanded = useMediaQuery('(min-width: 1280px)')
const moreOpen = ref(false)

const primaryNav = computed(() => navigation.slice(0, 4))

const hubLabel = computed(() => {
	switch (events.state.value) {
		case 'connected':
			return 'utils.layout.live'
		case 'reconnecting':
			return 'utils.layout.reconnecting'
		case 'connecting':
			return 'utils.layout.connecting'
		default:
			return 'utils.layout.offline'
	}
})

const hubTone = computed(() => (events.state.value === 'connected' ? 'ok' : events.state.value === 'reconnecting' ? 'warn' : 'danger'))

const healthTone = computed(() => {
	if (system.healthIssues.some(issue => issue.type === 'ERROR')) {
		return 'danger'
	}
	if (system.healthIssues.some(issue => issue.type === 'WARNING')) {
		return 'warn'
	}
	return 'ok'
})

const healthText = computed(() => {
	const count = system.healthIssues.length
	return count === 0
		? t('utils.layout.allSystemsNormal', 'All systems normal')
		: t('utils.layout.healthIssueCount', {
				count,
				countLabel: t(count === 1 ? 'utils.layout.issue' : 'utils.layout.issues', count === 1 ? 'issue' : 'issues'),
			}, '{count} health {countLabel}')
})

const displayName = computed(() => auth.user?.username ?? t('utils.layout.anonymous', 'Anonymous'))
const userInitials = computed(() => displayName.value.slice(0, 2).toUpperCase())

const userItems = computed<MenuEntry[]>(() => (
	auth.authMethodNone ? [] : [{ label: t('utils.layout.signOut', 'Sign out'), icon: 'lucide:log-out', danger: true, onSelect: signOut }]
))

function isActive(item: NavItem) {
	if (route.path === item.to || route.path.startsWith(`${item.to}/`)) {
		return true
	}
	return item.children?.some(child => route.path === child.to || route.path.startsWith(`${child.to}/`)) ?? false
}

async function signOut() {
	moreOpen.value = false
	await auth.logout()
	await navigateTo('/login', { replace: true })
}

function applyTheme(theme: components['schemas']['UiConfig']['theme']) {
	colorMode.preference = theme === 'LIGHT' ? 'light' : theme === 'DARK' ? 'dark' : 'system'
}

onMounted(() => {
	void system.loadStatus()
	void system.loadHealth()
	void reference.load().then(() => {
		if (reference.uiConfig) {
			applyTheme(reference.uiConfig.theme)
			applyUiFormat(reference.uiConfig)
		}
	})
})
</script>

<style scoped>
.shell {
	--rail-w: 232px;
	min-height: 100dvh;
}

@media (max-width: 1279px) {
	.shell {
		--rail-w: 64px;
	}
}

/* Rail ------------------------------------------------------------------- */

.rail {
	position: fixed;
	inset-block: 0;
	left: 0;
	z-index: 40;
	display: flex;
	flex-direction: column;
	gap: 16px;
	width: var(--rail-w);
	padding: 16px 12px;
	border-right: 1px solid var(--line);
	background: var(--bg);
}

@media (max-width: 1279px) {
	.rail {
		align-items: center;
		padding: 16px 8px;
	}
}

@media (max-width: 767px) {
	.rail {
		display: none;
	}
}

.rail-brand {
	display: flex;
	flex-direction: column;
	gap: 4px;
	padding: 0 8px;
}

@media (max-width: 1279px) {
	.rail-brand {
		align-items: center;
		padding: 0;
	}
}

.rail-wordmark {
	font-size: 1rem;
	font-weight: 600;
	letter-spacing: -0.01em;
	color: var(--fg);
	text-decoration: none;
}

@media (max-width: 1279px) {
	.rail-wordmark {
		display: none;
	}
}

.rail-state {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	font-size: 0.75rem;
	color: var(--fg-muted);
}

.state-dot {
	width: 8px;
	height: 8px;
	border-radius: 999px;
	background: var(--fg-faint);
	flex: none;
}

.tone-ok .state-dot {
	background: var(--ok);
}

.tone-warn .state-dot {
	background: var(--warn);
}

.tone-danger .state-dot {
	background: var(--danger);
}

.rail-nav {
	display: flex;
	flex-direction: column;
	gap: 2px;
	flex: 1;
	overflow-y: auto;
	min-height: 0;
}

.rail-group {
	display: flex;
	flex-direction: column;
}

.rail-children {
	display: flex;
	flex-direction: column;
	gap: 2px;
	margin: 2px 0 6px;
	padding-left: 22px;
}

.rail-child {
	height: 30px;
	display: flex;
	align-items: center;
	padding: 0 12px;
	border-radius: var(--r-control);
	color: var(--fg-muted);
	font-size: 0.8125rem;
	text-decoration: none;
}

.rail-child:hover {
	background: var(--surface-2);
	color: var(--fg);
}

.rail-child-active {
	color: var(--fg);
	font-weight: 500;
}

.rail-child-active:hover {
	background: transparent;
}

.rail-foot {
	display: flex;
	flex-direction: column;
	gap: 4px;
	margin-top: auto;
}

@media (max-width: 1279px) {
	.rail-foot {
		align-items: center;
	}
}

.rail-foot :deep(.rail-item) {
	width: 100%;
}

@media (max-width: 1279px) {
	.rail-foot :deep(.rail-item) {
		width: 40px;
		padding: 0;
		justify-content: center;
	}
}

.rail-avatar {
	display: grid;
	place-items: center;
	width: 22px;
	height: 22px;
	border-radius: 999px;
	background: var(--accent-soft);
	color: var(--accent);
	font-size: 0.625rem;
	font-weight: 600;
	flex: none;
}

.rail-health {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	height: 28px;
	padding: 0 8px;
	font-size: 0.75rem;
	color: var(--fg-muted);
	white-space: nowrap;
}

.rail-health-text {
	overflow: hidden;
	text-overflow: ellipsis;
}

/* Main column ------------------------------------------------------------ */

.shell-main {
	margin-left: var(--rail-w);
	min-height: 100dvh;
}

@media (max-width: 767px) {
	.shell-main {
		margin-left: 0;
	}
}

.content {
	max-width: 1920px;
	margin: 0 auto;
	padding: 24px;
	padding-bottom: 48px;
}

@media (min-width: 2560px) {
	.content {
		max-width: 1920px;
	}
}

@media (max-width: 767px) {
	.content {
		padding: 16px;
		padding-bottom: 96px;
	}
}

/* Mobile tab bar ---------------------------------------------------------- */

.tabbar {
	position: fixed;
	inset-inline: 0;
	bottom: 0;
	z-index: 40;
	display: none;
	height: 56px;
	padding-bottom: env(safe-area-inset-bottom);
	border-top: 1px solid var(--line);
	background: var(--surface);
}

@media (max-width: 767px) {
	.tabbar {
		display: flex;
	}
}

.tabbar-item {
	flex: 1;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 2px;
	border: 0;
	background: transparent;
	color: var(--fg-muted);
	font: inherit;
	font-size: 0.6875rem;
	text-decoration: none;
	cursor: pointer;
}

.tabbar-item-active {
	color: var(--accent);
}

.tabbar-icon {
	width: 20px;
	height: 20px;
}

/* More sheet -------------------------------------------------------------- */

.more-nav {
	display: grid;
	gap: 4px;
}

.more-group {
	display: grid;
	gap: 2px;
	padding-bottom: 8px;
	border-bottom: 1px solid var(--line);
	margin-bottom: 4px;
}

.more-group:last-child {
	border-bottom: 0;
}

.more-item {
	display: flex;
	align-items: center;
	gap: 10px;
	height: 40px;
	padding: 0 12px;
	border-radius: var(--r-control);
	color: var(--fg);
	text-decoration: none;
}

.more-item:hover {
	background: var(--surface-2);
}

.more-item-parent {
	font-weight: 500;
}

.more-item-child {
	padding-left: 40px;
	color: var(--fg-muted);
	font-size: 0.875rem;
}

.more-icon {
	width: 18px;
	height: 18px;
	color: var(--fg-muted);
}
</style>
