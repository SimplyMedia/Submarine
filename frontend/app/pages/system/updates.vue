<script setup lang="ts">
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

useHead({ title: 'Updates' })

type UpdateInfo = components['schemas']['UpdateDto']

const { toast } = useToast()
const commandsStore = useCommandsStore()

const update = ref<UpdateInfo | null>(null)
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
		const result = await api.GET('/api/v1/updates')
		if (result.data) {
			update.value = result.data
		}
		else {
			loadError.value = true
		}
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
					<template v-if="!update.latest">
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
</style>
