<script setup lang="ts">
import { navChildren } from '~/navigation'
import { ApiError, toApiError, useApi } from '~/composables/useApi'
import { useReferenceStore } from '~/stores/reference'
import { indexerProxyTypeLabel, indexerProxyTypeOptions } from '~/utils/indexer-labels'
import type { components } from '~/types/api'

type IndexerProxyDto = components['schemas']['IndexerProxyDto']
type IndexerProxyType = components['schemas']['IndexerProxyType']

definePageMeta({ layout: 'default' })
useHead({ title: 'Proxies' })

const api = useApi()
const reference = useReferenceStore()
const { toast } = useToast()

const loading = ref(true)
const loadError = ref('')
const proxies = ref<IndexerProxyDto[]>([])

async function loadProxies() {
	const result = await api.GET('/api/v1/indexer-proxies', { params: { query: { PageSize: 200 } } })
	if (!result.data) {
		loadError.value = 'Could not load proxies. Check your connection and try again.'
	}
	proxies.value = result.data?.items ?? []
}

async function load() {
	loading.value = true
	loadError.value = ''
	await Promise.all([loadProxies(), reference.load()])
	loading.value = false
}

onMounted(load)

const columns = [
	{ key: 'name', label: 'Name' },
	{ key: 'type', label: 'Type' },
	{ key: 'host', label: 'Host' },
	{ key: 'tags', label: 'Tags' },
	{ key: 'actions', label: '', align: 'right' as const },
]

function proxyTags(proxy: IndexerProxyDto): string {
	return proxy.tagIds.map(id => reference.tagLabel(id)).join(', ')
}

// --- Add/edit dialog -----------------------------------------------------
const dialogOpen = ref(false)
const editingId = ref<number | null>(null)
const name = ref('')
const type = ref<IndexerProxyType>('HTTP')
const host = ref('')
const port = ref(8080)
const username = ref('')
const password = ref('')
const requestTimeoutSeconds = ref(60)
const tagIds = ref<number[]>([])
const nameError = ref('')
const saving = ref(false)
const testing = ref(false)

const showUsername = computed(() => type.value === 'SOCKS4' || type.value === 'SOCKS5')
const showPassword = computed(() => type.value === 'SOCKS5')

function resetForm() {
	editingId.value = null
	name.value = ''
	type.value = 'HTTP'
	host.value = ''
	port.value = 8080
	username.value = ''
	password.value = ''
	requestTimeoutSeconds.value = 60
	tagIds.value = []
	nameError.value = ''
}

function openCreate() {
	resetForm()
	dialogOpen.value = true
}

function openEdit(proxy: IndexerProxyDto) {
	resetForm()
	editingId.value = proxy.id
	name.value = proxy.name
	type.value = proxy.type
	host.value = proxy.host
	port.value = proxy.port
	username.value = proxy.username ?? ''
	requestTimeoutSeconds.value = proxy.requestTimeoutSeconds
	tagIds.value = [...proxy.tagIds]
	dialogOpen.value = true
}

async function save() {
	nameError.value = ''
	saving.value = true
	try {
		const body = {
			name: name.value,
			type: type.value,
			host: host.value,
			port: port.value,
			username: showUsername.value ? (username.value || null) : null,
			password: showPassword.value ? (password.value || null) : null,
			requestTimeoutSeconds: requestTimeoutSeconds.value,
			tagIds: tagIds.value,
		}
		const result = editingId.value === null
			? await api.POST('/api/v1/indexer-proxies', { body })
			: await api.PUT('/api/v1/indexer-proxies/{id}', { params: { path: { id: editingId.value } }, body })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: editingId.value === null ? 'Proxy added' : 'Proxy saved', tone: 'ok' })
		dialogOpen.value = false
		await loadProxies()
	}
	catch (error) {
		const apiError = error instanceof ApiError ? error : toApiError(error)
		if (apiError.fieldErrors.name || apiError.fieldErrors.Name) {
			nameError.value = (apiError.fieldErrors.name ?? apiError.fieldErrors.Name)![0] ?? ''
		}
		else {
			toast({ title: 'Could not save proxy', description: apiError.message, tone: 'danger' })
		}
	}
	finally {
		saving.value = false
	}
}

async function testProxy(proxy: IndexerProxyDto) {
	testing.value = true
	try {
		const result = await api.POST('/api/v1/indexer-proxies/{id}/test', { params: { path: { id: proxy.id } } })
		if (!result.data) {
			throw toApiError(result.error, result.response)
		}
		toast({ title: result.data.isValid ? 'Connection successful' : 'Connection failed', description: result.data.message ?? undefined, tone: result.data.isValid ? 'ok' : 'danger' })
	}
	catch (error) {
		toast({ title: 'Connection failed', description: toApiError(error).message, tone: 'danger' })
	}
	finally {
		testing.value = false
	}
}

// --- Delete ----------------------------------------------------------------
const deleteTarget = ref<IndexerProxyDto | null>(null)
const deleting = ref(false)
const deleteError = ref('')

const deleteTargetOpen = computed({
	get: () => deleteTarget.value !== null,
	set: (value: boolean) => {
		if (!value) {
			deleteTarget.value = null
		}
	},
})

async function doDelete() {
	if (!deleteTarget.value) {
		return
	}
	deleting.value = true
	deleteError.value = ''
	const result = await api.DELETE('/api/v1/indexer-proxies/{id}', { params: { path: { id: deleteTarget.value.id } } })
	if (!result.response.ok) {
		deleteError.value = toApiError(result.error, result.response).message
		deleting.value = false
		return
	}
	toast({ title: 'Proxy deleted', tone: 'ok' })
	deleteTarget.value = null
	deleting.value = false
	await loadProxies()
}
</script>

<template>
	<div>
		<SPageHeader title="Proxies">
			<template #actions>
				<SButton
					variant="primary"
					@click="openCreate"
				>
					Add proxy
				</SButton>
			</template>
		</SPageHeader>
		<SubNav
			label="Indexers"
			:items="navChildren('indexers')"
		/>

		<SEmptyState
			v-if="loadError"
			:message="loadError"
			icon="lucide:alert-triangle"
		>
			<template #action>
				<SButton @click="load">
					Retry
				</SButton>
			</template>
		</SEmptyState>
		<SSpinner v-else-if="loading" />
		<STable
			v-else
			:columns="columns"
			:rows="proxies"
			:row-key="(row) => row.id"
		>
			<template #empty>
				<SEmptyState message="Add a proxy to route indexer requests through it.">
					<template #action>
						<SButton
							variant="primary"
							@click="openCreate"
						>
							Add proxy
						</SButton>
					</template>
				</SEmptyState>
			</template>
			<template #cell-type="{ row }">
				{{ indexerProxyTypeLabel(row.type) }}
			</template>
			<template #cell-host="{ row }">
				{{ row.host }}:{{ row.port }}
			</template>
			<template #cell-tags="{ row }">
				<span v-if="proxyTags(row)">{{ proxyTags(row) }}</span>
				<span
					v-else
					class="s-cell-muted"
				>None</span>
			</template>
			<template #cell-actions="{ row }">
				<SDropdownMenu
					:items="[
						{ label: 'Edit', icon: 'lucide:pencil', onSelect: () => openEdit(row) },
						{ label: 'Test', icon: 'lucide:plug-zap', onSelect: () => testProxy(row) },
						{ label: 'Delete', icon: 'lucide:trash-2', danger: true, onSelect: () => (deleteTarget = row) },
					]"
				>
					<template #trigger>
						<SIconButton
							label="Proxy actions"
							:disabled="testing"
						>
							<Icon
								name="lucide:more-horizontal"
								aria-hidden="true"
							/>
						</SIconButton>
					</template>
				</SDropdownMenu>
			</template>
		</STable>

		<SDialog
			v-model="dialogOpen"
			:title="editingId === null ? 'Add proxy' : 'Edit proxy'"
		>
			<SField
				label="Name"
				:error="nameError"
				control-id="proxy-name"
			>
				<SInput
					id="proxy-name"
					v-model="name"
					:invalid="!!nameError"
				/>
			</SField>
			<SField label="Type">
				<SSelect
					:model-value="type"
					:options="indexerProxyTypeOptions"
					@update:model-value="type = $event as IndexerProxyType"
				/>
			</SField>
			<div class="field-grid">
				<SField
					:label="type === 'FLARESOLVERR' ? 'FlareSolverr host' : 'Host'"
					control-id="proxy-host"
				>
					<SInput
						id="proxy-host"
						v-model="host"
					/>
				</SField>
				<SField
					label="Port"
					control-id="proxy-port"
				>
					<SInput
						id="proxy-port"
						type="number"
						:model-value="String(port)"
						@update:model-value="port = Number($event) || 0"
					/>
				</SField>
			</div>
			<div class="field-grid">
				<SField
					v-if="showUsername"
					label="Username"
					control-id="proxy-username"
				>
					<SInput
						id="proxy-username"
						v-model="username"
					/>
				</SField>
				<SField
					v-if="showPassword"
					label="Password"
					control-id="proxy-password"
				>
					<SInput
						id="proxy-password"
						v-model="password"
						type="password"
					/>
				</SField>
				<SField
					label="Request timeout (seconds)"
					:hint="type === 'FLARESOLVERR' ? 'Used for the FlareSolverr challenge solve' : undefined"
					control-id="proxy-timeout"
				>
					<SInput
						id="proxy-timeout"
						type="number"
						:model-value="String(requestTimeoutSeconds)"
						@update:model-value="requestTimeoutSeconds = Number($event) || 1"
					/>
				</SField>
			</div>
			<SField label="Tags">
				<TagPicker v-model:tag-ids="tagIds" />
			</SField>
			<template #footer>
				<SButton
					variant="secondary"
					@click="dialogOpen = false"
				>
					Cancel
				</SButton>
				<SButton
					variant="primary"
					:loading="saving"
					@click="save"
				>
					{{ editingId === null ? 'Add proxy' : 'Save changes' }}
				</SButton>
			</template>
		</SDialog>

		<SDialog
			v-model="deleteTargetOpen"
			title="Delete proxy"
		>
			<p v-if="deleteTarget">
				Delete "{{ deleteTarget.name }}"? This cannot be undone.
			</p>
			<p
				v-if="deleteError"
				class="s-field-error"
				role="alert"
			>
				{{ deleteError }}
			</p>
			<template #footer>
				<SButton
					variant="secondary"
					:disabled="deleting"
					@click="deleteTarget = null"
				>
					Cancel
				</SButton>
				<SButton
					variant="danger"
					:loading="deleting"
					@click="doDelete"
				>
					Delete proxy
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.field-grid {
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
	gap: 16px;
	margin-bottom: 16px;
}
</style>
