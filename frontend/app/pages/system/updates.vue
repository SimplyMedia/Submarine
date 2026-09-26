<script setup lang="ts">
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import { formatDate, formatDateTime } from '~/composables/useFormat'
import type { components } from '~/types/api'

useHead({ title: 'Updates' })

type UpdateInfo = components['schemas']['UpdateDto']
type ReleaseInfo = components['schemas']['ReleaseInfo']

const { toast } = useToast()
const commandsStore = useCommandsStore()

const update = ref<UpdateInfo | null>(null)
const releases = ref<ReleaseInfo[]>([])
const loading = ref(true)
const loadError = ref(false)
const checkCommandId = ref<number | null>(null)

const checkCommand = computed(() =>
	checkCommandId.value ? commandsStore.commands.find(command => command.id === checkCommandId.value) ?? null : null,
)
const checking = computed(() => checkCommand.value?.status === 'QUEUED' || checkCommand.value?.status === 'RUNNING')

async function loadUpdate() {
	loading.value = true
	loadError.value = false
	try {
		const api = useApi()
		const [updateResult, releasesResult] = await Promise.all([
			api.GET('/api/v1/updates'),
			api.GET('/api/v1/updates/releases'),
		])
		if (updateResult.data) {
			update.value = updateResult.data
		}
		else {
			loadError.value = true
		}
		releases.value = releasesResult.data ?? []
	}
	catch {
		loadError.value = true
	}
	finally {
		loading.value = false
	}
}

async function checkNow() {
	try {
		const command = await commandsStore.enqueue('CheckForUpdate')
		checkCommandId.value = command.id ?? null
	}
	catch {
		toast({ title: 'Could not start the update check', tone: 'danger' })
	}
}

watch(checkCommand, (command) => {
	if (command && (command.status === 'COMPLETED' || command.status === 'FAILED')) {
		checkCommandId.value = null
		void loadUpdate()
	}
})

onMounted(() => {
	void loadUpdate()
})
</script>

<template>
	<div>
		<SPageHeader title="Updates">
			<template #actions>
				<SButton
					variant="primary"
					:loading="checking"
					@click="checkNow"
				>
					Check now
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			label="System"
			:items="navChildren('system')"
		/>

		<SSection>
			<SSkeleton
				v-if="loading"
				height="120px"
			/>
			<SEmptyState
				v-else-if="loadError"
				message="Could not check for updates. Check the application's internet connection and try again."
				icon="lucide:cloud-off"
			/>
			<template v-else-if="update">
				<dl class="fact-grid">
					<div class="fact-row">
						<dt>Current version</dt>
						<dd>{{ update.current }}</dd>
					</div>
					<div
						v-if="update.latest"
						class="fact-row"
					>
						<dt>Latest version</dt>
						<dd>{{ update.latest }}</dd>
					</div>
				</dl>

				<div class="updates-status">
					<template v-if="update.checkFailed">
						<SBadge tone="warn">
							Unknown
						</SBadge>
						<span>Could not reach the update server. Check the application's internet connection.</span>
					</template>
					<template v-else-if="update.updateAvailable">
						<SBadge tone="info">
							Update available
						</SBadge>
						<a
							v-if="update.releaseNotesUrl"
							:href="update.releaseNotesUrl"
							target="_blank"
							rel="noopener"
						>
							View release notes
						</a>
					</template>
					<template v-else-if="!update.latest">
						<SBadge tone="ok">
							No releases yet
						</SBadge>
						<span>No published releases were found for this branch.</span>
					</template>
					<template v-else>
						<SBadge tone="ok">
							Up to date
						</SBadge>
						<span>You're running the latest version.</span>
					</template>
				</div>

				<p
					v-if="update.updateAvailable && update.isDocker"
					class="updates-docker-note"
				>
					Pull the new image to update.
				</p>
			</template>
		</SSection>

		<SSection
			v-if="!loading && !loadError"
			title="Release history"
		>
			<SEmptyState
				v-if="releases.length === 0"
				message="No releases have been published yet."
				icon="lucide:package"
			/>
			<ul
				v-else
				class="release-list"
			>
				<li
					v-for="release in releases"
					:key="release.version"
					class="release-row"
				>
					<div class="release-header">
						<span class="release-version">{{ release.version }}</span>
						<SBadge v-if="release.installed" tone="ok">
							Currently installed
						</SBadge>
						<SBadge v-if="release.prerelease" tone="info">
							Prerelease
						</SBadge>
						<span
							class="release-date"
							:title="formatDateTime(release.publishedAt)"
						>
							{{ formatDate(release.publishedAt) }}
						</span>
						<a
							:href="release.htmlUrl"
							target="_blank"
							rel="noopener"
							class="release-link"
						>
							View on GitHub
						</a>
					</div>
					<pre
						v-if="release.notes"
						class="release-notes"
					>{{ release.notes }}</pre>
				</li>
			</ul>
		</SSection>
	</div>
</template>

<style scoped>
.fact-grid {
	display: flex;
	flex-direction: column;
}

.fact-row {
	display: flex;
	justify-content: space-between;
	gap: 24px;
	padding: 10px 0;
	border-bottom: 1px solid var(--line);
}

.fact-row dt {
	color: var(--fg-muted);
}

.fact-row dd {
	font-weight: 500;
}

.updates-status {
	display: flex;
	align-items: center;
	gap: 12px;
	padding-top: 16px;
	color: var(--fg-muted);
}

.updates-docker-note {
	margin-top: 12px;
	color: var(--fg-muted);
	font-size: 0.8125rem;
}

.release-list {
	display: flex;
	flex-direction: column;
	gap: 16px;
}

.release-row {
	padding-bottom: 16px;
	border-bottom: 1px solid var(--line);
}

.release-row:last-child {
	padding-bottom: 0;
	border-bottom: none;
}

.release-header {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 8px;
}

.release-version {
	font-weight: 600;
}

.release-date {
	color: var(--fg-muted);
	font-size: 0.8125rem;
}

.release-link {
	margin-left: auto;
	font-size: 0.8125rem;
}

.release-notes {
	margin-top: 8px;
	white-space: pre-wrap;
	word-break: break-word;
	font-family: inherit;
	font-size: 0.875rem;
	color: var(--fg-muted);
}
</style>
