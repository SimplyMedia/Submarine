<script setup lang="ts">
import { backupKindLabel } from '~/utils/system-labels'
import { formatBytes, formatDateTime } from '~/composables/useFormat'
import { toApiError } from '~/composables/useApi'
import { useCommandsStore } from '~/stores/commands'
import { navChildren } from '~/navigation'
import type { components } from '~/types/api'

useHead({ title: 'Backups' })

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
	{ key: 'name', label: 'Name' },
	{ key: 'kind', label: 'Type' },
	{ key: 'size', label: 'Size', align: 'right' as const },
	{ key: 'createdAt', label: 'Created' },
	{ key: 'actions', label: '' },
]

async function loadBackups() {
	loading.value = true
	loadError.value = ''
	const api = useApi()
	const result = await api.GET('/api/v1/backups')
	if (!result.data) {
		loadError.value = 'Could not load backups. Check your connection and try again.'
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
		toast({ title: 'Could not start the backup', tone: 'danger' })
	}
	finally {
		creating.value = false
	}
}

watch(backupCommand, (command) => {
	if (command && (command.status === 'COMPLETED' || command.status === 'FAILED')) {
		if (command.status === 'COMPLETED') {
			toast({ title: 'Backup created', tone: 'ok' })
			void loadBackups()
		}
		else {
			toast({ title: 'Backup failed', description: command.message ?? undefined, tone: 'danger' })
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
		toast({ title: 'Could not delete the backup', description: error instanceof Error ? error.message : undefined, tone: 'danger' })
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
			toast({ title: 'Restart is taking longer than expected', tone: 'danger' })
			restartingAfterRestore.value = false
		}
	}
	catch (error) {
		toast({ title: 'Could not restore the backup', description: error instanceof Error ? error.message : undefined, tone: 'danger' })
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
		uploadError.value = 'Choose a backup file'
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
			throw new Error(problem?.detail || problem?.title || 'Restore failed')
		}
		uploadOpen.value = false
		uploadFile.value = null
		restartingAfterRestore.value = true
		const ready = await pollReady()
		if (ready) {
			window.location.reload()
		}
		else {
			toast({ title: 'Restart is taking longer than expected', tone: 'danger' })
			restartingAfterRestore.value = false
		}
	}
	catch (error) {
		uploadError.value = error instanceof Error ? error.message : 'Restore failed'
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
		<SPageHeader title="Backups">
			<template #actions>
				<SButton
					variant="secondary"
					@click="uploadOpen = true"
				>
					Restore from file
				</SButton>
				<SButton
					variant="primary"
					:loading="creating"
					@click="createBackup"
				>
					Back up now
				</SButton>
			</template>
		</SPageHeader>

		<SubNav
			label="System"
			:items="navChildren('system')"
		/>

		<div
			v-if="restartingAfterRestore"
			class="backups-restarting"
		>
			<SSpinner :size="16" />
			<span>Restoring and restarting, waiting for the application to come back…</span>
		</div>

		<template v-else>
			<SSection v-if="backupCommand">
				<CommandProgress :command="backupCommand" />
			</SSection>

			<SSection title="Archives">
				<SEmptyState
					v-if="loadError"
					:message="loadError"
					icon="lucide:alert-triangle"
				>
					<template #action>
						<SButton @click="loadBackups">
							Retry
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
							message="No backups yet. Back up your library to protect against data loss."
							icon="lucide:archive"
						>
							<template #action>
								<SButton
									variant="primary"
									:loading="creating"
									@click="createBackup"
								>
									Back up now
								</SButton>
							</template>
						</SEmptyState>
					</template>
					<template #cell-kind="{ row }">
						{{ backupKindLabel(row.name) }}
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
								<SIconButton label="Download backup">
									<Icon
										name="lucide:download"
										aria-hidden="true"
									/>
								</SIconButton>
							</a>
							<SIconButton
								label="Restore this backup"
								@click="restoreTarget = row"
							>
								<Icon
									name="lucide:rotate-ccw"
									aria-hidden="true"
								/>
							</SIconButton>
							<SIconButton
								label="Delete this backup"
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
			title="Delete backup?"
			:description="deleteTarget ? `${deleteTarget.name} will be permanently deleted.` : undefined"
			confirm-label="Delete"
			danger
			:busy="deleting"
			@confirm="confirmDelete"
		/>
		<ConfirmDialog
			v-model="restoreTargetOpen"
			title="Restore backup?"
			:description="restoreTarget ? `${restoreTarget.name} will replace the current library data. The application will restart.` : undefined"
			confirm-label="Restore"
			danger
			:busy="restoring"
			@confirm="confirmRestore"
		/>

		<SDialog
			v-model="uploadOpen"
			title="Restore from file"
			description="Upload a Submarine backup archive. The application will restart once the restore finishes."
		>
			<SField
				label="Backup file"
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
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="uploading"
					@click="uploadAndRestore"
				>
					Restore
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
