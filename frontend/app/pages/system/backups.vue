<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { backupKindLabel } from '~/utils/system-labels'
import { formatBytes, formatDateTime } from '~/composables/useFormat'
import { toApiError } from '~/composables/useApi'
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

const { t } = useI18n()
useHead({ title: t('pages.system.backups.title') })

type BackupEntry = components['schemas']['BackupEntry']

const { toast } = useToast()
const commandsStore = useCommandsStore()

const backups = ref<BackupEntry[]>([])
const loading = ref(true)
const loadError = ref('')
const creating = ref(false)
const backupCommandId = ref<number | null>(null)

const deleteTarget = ref<BackupEntry | null>(null)
const deleting = ref(false)

const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteTarget.value = null
		}
	},
})

const restoreTarget = ref<BackupEntry | null>(null)
const restoring = ref(false)
const restartingAfterRestore = ref(false)

const restoreTargetOpen = computed({
	get: () => restoreTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			restoreTarget.value = null
		}
	},
})

const uploadFile = ref<File | null>(null)
const uploadOpen = ref(false)
const uploading = ref(false)
const uploadError = ref('')

const backupCommand = computed(() =>
	backupCommandId.value ? commandsStore.commands.find(command => command.id === backupCommandId.value) ?? null : null,
)

const columns = [
	{ key: 'name', label: t('pages.system.backups.name') },
	{ key: 'kind', label: t('pages.system.backups.type') },
	{ key: 'size', label: t('pages.system.backups.size'), align: 'right' as const },
	{ key: 'createdAt', label: t('pages.system.backups.created') },
	{ key: 'actions', label: '' },
]

async function loadBackups() {
	loading.value = true
	loadError.value = ''
	const api = useApi()
	const result = await api.GET('/api/v1/backups')
	if (!result.data) {
		loadError.value = t('pages.system.backups.loadFailed')
	}
	backups.value = result.data ?? []
	loading.value = false
}

async function createBackup() {
	creating.value = true
	backupCommandId.value = null
	try {
		const api = useApi()
		const result = await api.POST('/api/v1/backups')
		if (result.data) {
			backupCommandId.value = result.data.commandId
			await commandsStore.fetchById(result.data.commandId)
		}
	}
	catch {
		toast({ title: t('pages.system.backups.createFailed'), tone: 'danger' })
	}
	finally {
		creating.value = false
	}
}

watch(backupCommand, (command) => {
	if (command && (command.status === 'COMPLETED' || command.status === 'FAILED')) {
		if (command.status === 'COMPLETED') {
			toast({ title: t('pages.system.backups.created'), tone: 'ok' })
			void loadBackups()
		}
		else {
			toast({ title: t('pages.system.backups.failed'), description: command.message ?? undefined, tone: 'danger' })
		}
		backupCommandId.value = null
	}
})

async function confirmDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	try {
		const api = useApi()
		const result = await api.DELETE('/api/v1/backups/{name}', { params: { path: { name: deleteTarget.value.name } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		backups.value = backups.value.filter(backup => backup.name !== deleteTarget.value?.name)
		deleteTarget.value = null
	}
	catch (error) {
		toast({ title: t('pages.system.backups.deleteFailed'), description: error instanceof Error ? error.message : undefined, tone: 'danger' })
	}
	finally {
		deleting.value = false
	}
}

async function confirmRestore() {
	if (!restoreTarget.value) {
		return
	}
	restoring.value = true
	try {
		const api = useApi()
		const result = await api.POST('/api/v1/backups/{name}/restore', { params: { path: { name: restoreTarget.value.name } } })
		if (!result.response.ok) {
			throw toApiError(result.error, result.response)
		}
		restoreTarget.value = null
		restartingAfterRestore.value = true
		const ready = await pollReady()
		if (ready) {
			window.location.reload()
		}
		else {
			toast({ title: t('pages.system.backups.restartSlow'), tone: 'danger' })
			restartingAfterRestore.value = false
		}
	}
	catch (error) {
		toast({ title: t('pages.system.backups.restoreFailed'), description: error instanceof Error ? error.message : undefined, tone: 'danger' })
		restoring.value = false
	}
}

function onFileChange(event: Event) {
	const input = event.target as HTMLInputElement
	uploadFile.value = input.files?.[0] ?? null
	uploadError.value = ''
}

async function uploadAndRestore() {
	if (!uploadFile.value) {
		uploadError.value = t('pages.system.backups.chooseFile')
		return
	}
	uploading.value = true
	uploadError.value = ''
	try {
		const form = new FormData()
		form.append('file', uploadFile.value)
		// POST /api/v1/backups/restore takes multipart/form-data; openapi-fetch
		// has no ergonomic support for file uploads, so this call bypasses it.
		const response = await fetch(`${baseUrl()}/api/v1/backups/restore`, {
			method: 'POST',
			credentials: 'include',
			body: form,
		})
		if (!response.ok) {
			const problem = await response.json().catch(() => null) as { title?: string, detail?: string } | null
			throw new Error(problem?.detail || problem?.title || t('pages.system.backups.restoreErrorFallback'))
		}
		uploadOpen.value = false
		uploadFile.value = null
		restartingAfterRestore.value = true
		const ready = await pollReady()
		if (ready) {
			window.location.reload()
		}
		else {
			toast({ title: t('pages.system.backups.restartSlow'), tone: 'danger' })
			restartingAfterRestore.value = false
		}
	}
	catch (error) {
		uploadError.value = error instanceof Error ? error.message : t('pages.system.backups.restoreErrorFallback')
	}
	finally {
		uploading.value = false
	}
}

onMounted(() => {
	void loadBackups()
})
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.system.backups.title')">
			<template #actions>
				<SButton
					variant="secondary"
					@click="uploadOpen = true"
				>
					{{ t('pages.system.backups.restoreFromFile') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="creating"
					@click="createBackup"
				>
					{{ t('pages.system.backups.backUpNow') }}
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			:label="t('pages.system.backups.systemNav')"
			:items="navChildren('system')"
		/>

		<div
			v-if="restartingAfterRestore"
			class="backups-restarting"
		>
			<SSpinner :size="16" />
			<span>{{ t('pages.system.backups.restarting') }}</span>
		</div>

		<template v-else>
			<SSection v-if="backupCommand">
				<CommandProgress :command="backupCommand" />
			</SSection>

			<SSection :title="t('pages.system.backups.archives')">
				<SEmptyState
					v-if="loadError"
					:message="loadError"
					icon="lucide:alert-triangle"
				>
					<template #action>
						<SButton @click="loadBackups">
							{{ t('pages.system.backups.retry') }}
						</SButton>
					</template>
				</SEmptyState>
				<SSkeleton
					v-else-if="loading"
					height="200px"
				/>
				<STable
					v-else
					:columns="columns"
					:rows="backups"
					:row-key="row => row.name"
				>
					<template #empty>
						<SEmptyState
							:message="t('pages.system.backups.emptyState')"
							icon="lucide:archive"
						>
							<template #action>
								<SButton
									variant="primary"
									:loading="creating"
									@click="createBackup"
								>
									{{ t('pages.system.backups.backUpNow') }}
								</SButton>
							</template>
						</SEmptyState>
					</template>
					<template #cell-kind="{ row }">
						{{ t(backupKindLabel(row.name)) }}
					</template>
					<template #cell-size="{ row }">
						{{ formatBytes(row.size) }}
					</template>
					<template #cell-createdAt="{ row }">
						{{ formatDateTime(row.createdAt) }}
					</template>
					<template #cell-actions="{ row }">
						<div class="backup-actions">
							<a
								:href="`${baseUrl()}/api/v1/backups/${row.name}/download`"
								download
							>
								<SIconButton :label="t('pages.system.backups.downloadBackup')">
									<Icon
										name="lucide:download"
										aria-hidden="true"
									/>
								</SIconButton>
							</a>
							<SIconButton
								:label="t('pages.system.backups.restoreThisBackup')"
								@click="restoreTarget = row"
							>
								<Icon
									name="lucide:rotate-ccw"
									aria-hidden="true"
								/>
							</SIconButton>
							<SIconButton
								:label="t('pages.system.backups.deleteThisBackup')"
								variant="danger"
								@click="deleteTarget = row"
							>
								<Icon
									name="lucide:trash-2"
									aria-hidden="true"
								/>
							</SIconButton>
						</div>
					</template>
				</STable>
			</SSection>
		</template>

		<ConfirmDialog
			v-model="deleteTargetOpen"
			:title="t('pages.system.backups.deleteConfirmTitle')"
			:description="deleteTarget ? t('pages.system.backups.deleteConfirmDescription', { name: deleteTarget.name }) : undefined"
			:confirm-label="t('pages.system.backups.delete')"
			danger
			:busy="deleting"
			@confirm="confirmDelete"
		/>
		<ConfirmDialog
			v-model="restoreTargetOpen"
			:title="t('pages.system.backups.restoreConfirmTitle')"
			:description="restoreTarget ? t('pages.system.backups.restoreConfirmDescription', { name: restoreTarget.name }) : undefined"
			:confirm-label="t('pages.system.backups.restore')"
			danger
			:busy="restoring"
			@confirm="confirmRestore"
		/>

		<SDialog
			v-model="uploadOpen"
			:title="t('pages.system.backups.restoreFromFile')"
			:description="t('pages.system.backups.uploadDescription')"
		>
			<SField
				:label="t('pages.system.backups.backupFile')"
				control-id="backup-upload-file"
				:error="uploadError"
			>
				<input
					id="backup-upload-file"
					type="file"
					accept=".zip"
					class="s-input backup-file-input"
					@change="onFileChange"
				>
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="uploading"
					@click="uploadOpen = false"
				>
					{{ t('pages.system.backups.cancel') }}
				</SButton>
				<SButton
					variant="danger"
					:loading="uploading"
					@click="uploadAndRestore"
				>
					{{ t('pages.system.backups.restore') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.backup-actions {
	display: flex;
	align-items: center;
	gap: 4px;
	justify-content: flex-end;
}

.backup-file-input {
	padding: 6px 12px;
}

.backups-restarting {
	display: flex;
	align-items: center;
	gap: 12px;
	padding: 48px 0;
	justify-content: center;
	color: var(--fg-muted);
}
</style>
