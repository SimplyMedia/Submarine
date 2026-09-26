<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { components } from '~/types/api'
import { minimumAvailabilityOptions } from '~/utils/library-labels'

const { t } = useI18n()

type CollectionDto = components['schemas']['CollectionDto']

useHead({ title: t('pages.movies.collections.title') })

const api = useApi()
const reference = useReferenceStore()
const toast = useToast()

const collections = ref<CollectionDto[]>([])
const loading = ref(true)
const loadError = ref('')

async function load() {
	loading.value = true
	loadError.value = ''
	const result = await api.GET('/api/v1/collections')
	if (result.data) {
		collections.value = result.data
	}
	else {
		loadError.value = t('pages.movies.collections.loadError')
	}
	loading.value = false
}

onMounted(async () => {
	await reference.load()
	await load()
})

const columns = [
	{ key: 'title', label: t('pages.movies.collections.collection') },
	{ key: 'movies', label: t('pages.movies.collections.movies'), align: 'right' as const },
	{ key: 'missing', label: t('pages.movies.collections.missing'), align: 'right' as const },
	{ key: 'monitored', label: t('pages.movies.collections.monitored'), align: 'right' as const },
	{ key: 'actions', label: '', align: 'right' as const },
]

const rootFolderOptions = computed(() =>
	reference.rootFolders.filter(folder => folder.mediaKind === 'MOVIES').map(folder => ({ value: String(folder.id), label: folder.path })),
)
const qualityOptions = computed(() => reference.qualityProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))
const languageOptions = computed(() => reference.languageProfiles.map(profile => ({ value: String(profile.id), label: profile.name })))

async function toggleMonitored(collection: CollectionDto, monitored: boolean) {
	if (collection.id == null) {
		return
	}
	const previous = collection.monitored
	collection.monitored = monitored
	const result = await api.PUT('/api/v1/collections/{id}', {
		params: { path: { id: collection.id } },
		body: { monitored, rootFolderId: null, qualityProfileId: null, languageProfileId: null, minimumAvailability: null, searchOnAdd: null },
	})
	if (!result.data) {
		collection.monitored = previous
		toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
	}
}

const editTarget = ref<CollectionDto | null>(null)
const editOpen = computed({
	get: () => editTarget.value != null,
	set: (value: boolean) => { if (!value) editTarget.value = null },
})
const editForm = reactive({
	rootFolderId: '',
	qualityProfileId: '',
	languageProfileId: '',
	minimumAvailability: 'RELEASED' as NonNullable<components['schemas']['MinimumAvailability']>,
	searchOnAdd: false,
})
const savingEdit = ref(false)

function openEdit(collection: CollectionDto) {
	editTarget.value = collection
	editForm.rootFolderId = collection.rootFolderId != null ? String(collection.rootFolderId) : ''
	editForm.qualityProfileId = collection.qualityProfileId != null ? String(collection.qualityProfileId) : ''
	editForm.languageProfileId = collection.languageProfileId != null ? String(collection.languageProfileId) : ''
	editForm.minimumAvailability = collection.minimumAvailability
	editForm.searchOnAdd = collection.searchOnAdd
}

async function saveEdit() {
	if (!editTarget.value?.id) {
		return
	}
	savingEdit.value = true
	try {
		const result = await api.PUT('/api/v1/collections/{id}', {
			params: { path: { id: editTarget.value.id } },
			body: {
				monitored: null,
				rootFolderId: editForm.rootFolderId ? Number(editForm.rootFolderId) : null,
				qualityProfileId: editForm.qualityProfileId ? Number(editForm.qualityProfileId) : null,
				languageProfileId: editForm.languageProfileId ? Number(editForm.languageProfileId) : null,
				minimumAvailability: editForm.minimumAvailability,
				searchOnAdd: editForm.searchOnAdd,
			},
		})
		if (result.data) {
			toast.toast({ title: t('pages.movies.collections.updated'), tone: 'ok' })
			editTarget.value = null
			await load()
		}
		else {
			toast.toast({ title: toApiError(result.error, result.response).message, tone: 'danger' })
		}
	}
	finally {
		savingEdit.value = false
	}
}
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.movies.collections.title')" />

		<SSpinner v-if="loading && collections.length === 0" />
		<p
			v-else-if="loadError"
			class="s-field-error"
			role="alert"
		>
			{{ loadError }}
		</p>
		<SSection v-else>
			<STable
				:columns="columns"
				:rows="collections"
				:row-key="(row) => row.tmdbCollectionId"
			>
				<template #cell-title="{ row }">
					<NuxtLink
						:to="`/movies/collections/${row.tmdbCollectionId}`"
						class="collection-title-cell"
					>
						<SPosterImage
							:src="row.posterUrl"
							:alt="row.title"
							class="collection-poster"
						/>
						<span>{{ row.title }}</span>
					</NuxtLink>
				</template>
				<template #cell-movies="{ row }">
					{{ row.movieCount }}
				</template>
				<template #cell-missing="{ row }">
					<SBadge
						v-if="row.missingCount > 0"
						tone="warn"
					>
						{{ row.missingCount }}
					</SBadge>
					<span v-else>0</span>
				</template>
				<template #cell-monitored="{ row }">
					<STooltip :text="row.id == null ? t('pages.movies.collections.configureMonitoring') : undefined">
						<MonitorToggle
							:model-value="row.monitored"
							:label="t('pages.movies.collections.toggleMonitored', { title: row.title })"
							:disabled="row.id == null"
							@update:model-value="toggleMonitored(row, $event)"
						/>
					</STooltip>
				</template>
				<template #cell-actions="{ row }">
					<STooltip :text="row.id == null ? t('pages.movies.collections.noSavedSettings') : undefined">
						<SButton
							size="sm"
							:disabled="row.id == null"
							@click="openEdit(row)"
						>
							{{ t('pages.movies.collections.editDefaults') }}
						</SButton>
					</STooltip>
				</template>
				<template #empty>
					<SEmptyState :message="t('pages.movies.collections.empty')" />
				</template>
			</STable>
		</SSection>

		<SDialog
			v-model="editOpen"
			:title="t('pages.movies.collections.defaultsTitle')"
			:description="editTarget ? t('pages.movies.collections.defaultsDescription', { title: editTarget.title }) : undefined"
		>
			<div class="add-form">
				<SField :label="t('pages.movies.collections.rootFolder')">
					<SSelect
						v-model="editForm.rootFolderId"
						:options="rootFolderOptions"
						:placeholder="t('pages.movies.collections.chooseRootFolder')"
					/>
				</SField>
				<SField :label="t('pages.movies.collections.qualityProfile')">
					<SSelect
						v-model="editForm.qualityProfileId"
						:options="qualityOptions"
						:placeholder="t('pages.movies.collections.chooseProfile')"
					/>
				</SField>
				<SField :label="t('pages.movies.collections.languageProfile')">
					<SSelect
						v-model="editForm.languageProfileId"
						:options="languageOptions"
						:placeholder="t('pages.movies.collections.chooseProfile')"
					/>
				</SField>
				<SField :label="t('pages.movies.collections.minimumAvailability')">
					<SSelect
						v-model="editForm.minimumAvailability"
						:options="minimumAvailabilityOptions.map(option => ({ ...option, label: t(option.label) }))"
					/>
				</SField>
				<SCheckbox
					v-model="editForm.searchOnAdd"
					:label="t('pages.movies.collections.searchMissingOnAdd')"
				/>
			</div>
			<template #footer>
				<SButton @click="editTarget = null">
					{{ t('pages.movies.collections.cancel') }}
				</SButton>
				<SButton
					variant="primary"
					:loading="savingEdit"
					@click="saveEdit"
				>
					{{ t('pages.movies.collections.saveChanges') }}
				</SButton>
			</template>
		</SDialog>
	</div>
</template>

<style scoped>
.collection-title-cell {
	display: flex;
	align-items: center;
	gap: 12px;
}

.collection-poster {
	width: 32px;
	flex: none;
	border-radius: var(--r-control);
}

.add-form {
	display: flex;
	flex-direction: column;
	gap: 12px;
}
</style>
