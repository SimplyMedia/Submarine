<script setup lang="ts">
import { useI18n } from 'vue-i18n'

definePageMeta({
	layout: 'default',
	middleware: [
		() => {
			if (!import.meta.dev) {
				return abortNavigation(createError({ statusCode: 404 }))
			}
		},
	],
})

const { t } = useI18n()
useHead({ title: t('pages.dev.ui.title', 'UI reference') })

const { toast } = useToast()

const dialogOpen = ref(false)
const wideDialogOpen = ref(false)
const activeTab = ref('queue')
const selectValue = ref('any')
const checkValue = ref(true)
const switchValue = ref(false)
const textValue = ref('')
const areaValue = ref('')

const tableColumns = [
	{ key: 'title', label: t('pages.dev.ui.columnTitle', 'Title') },
	{ key: 'quality', label: t('pages.dev.ui.columnQuality', 'Quality') },
	{ key: 'size', label: t('pages.dev.ui.columnSize', 'Size'), align: 'right' as const },
]

const tableRows = [
	{ title: 'North of North Island, S01E03', quality: 'WEBDL-1080p', size: '1.4 GB' },
	{ title: 'Harbour Lights, S02E09', quality: 'Bluray-2160p', size: '12.9 GB' },
	{ title: 'The Trawler, S01E01', quality: 'HDTV-720p', size: '0.6 GB' },
]

const tabs = [
	{ value: 'queue', label: t('pages.dev.ui.tabQueue', 'Queue') },
	{ value: 'history', label: t('pages.dev.ui.tabHistory', 'History') },
	{ value: 'blocklist', label: t('pages.dev.ui.tabBlocklist', 'Blocklist') },
]

const selectOptions = [
	{ value: 'any', label: t('pages.dev.ui.optionAny', 'Any') },
	{ value: 'series', label: t('pages.dev.ui.optionSeries', 'Series') },
	{ value: 'movies', label: t('pages.dev.ui.optionMovies', 'Movies') },
]
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.dev.ui.title', 'UI reference')">
			<template #actions>
				<ThemeMenu trigger-class="s-btn s-btn-secondary" />
			</template>
		</SPageHeader>

		<SSection :title="t('pages.dev.ui.buttons', 'Buttons')">
			<div class="ui-row">
				<SButton variant="primary">
					{{ t('pages.dev.ui.primary', 'Primary') }}
				</SButton>
				<SButton variant="secondary">
					{{ t('pages.dev.ui.secondary', 'Secondary') }}
				</SButton>
				<SButton variant="ghost">
					{{ t('pages.dev.ui.ghost', 'Ghost') }}
				</SButton>
				<SButton variant="danger">
					{{ t('pages.dev.ui.danger', 'Danger') }}
				</SButton>
				<SButton
					variant="primary"
					loading
				>
					{{ t('pages.dev.ui.loading', 'Loading') }}
				</SButton>
				<SButton
					variant="secondary"
					disabled
				>
					{{ t('pages.dev.ui.disabled', 'Disabled') }}
				</SButton>
				<SButton
					variant="secondary"
					size="sm"
				>
					{{ t('pages.dev.ui.small', 'Small') }}
				</SButton>
				<SIconButton :label="t('pages.dev.ui.search', 'Search')">
					<Icon name="lucide:search" />
				</SIconButton>
			</div>
		</SSection>

		<SSection :title="t('pages.dev.ui.inputs', 'Inputs')">
			<div class="ui-grid">
				<SField
					:label="t('pages.dev.ui.titleField', 'Title')"
					:hint="t('pages.dev.ui.titleHint', 'Used for sorting and search.')"
				>
					<SInput
						v-model="textValue"
						:placeholder="t('pages.dev.ui.releaseTitle', 'Release title')"
					/>
				</SField>
				<SField
					:label="t('pages.dev.ui.withError', 'With error')"
					:error="t('pages.dev.ui.pathError', 'Enter a path that exists.')"
				>
					<SInput
						invalid
						:placeholder="t('pages.dev.ui.downloadsPathPlaceholder', '/downloads/series')"
					/>
				</SField>
				<SField :label="t('pages.dev.ui.qualityProfile', 'Quality profile')">
					<SSelect
						v-model="selectValue"
						:options="selectOptions"
					/>
				</SField>
				<SField :label="t('pages.dev.ui.notes', 'Notes')">
					<STextarea
						v-model="areaValue"
						:placeholder="t('pages.dev.ui.notesPlaceholder', 'Anything worth remembering')"
					/>
				</SField>
				<SCheckbox
					v-model="checkValue"
					:label="t('pages.dev.ui.monitorNewItems', 'Monitor new items')"
				/>
				<SSwitch
					v-model="switchValue"
					:label="t('pages.dev.ui.seasonFolder', 'Season folder')"
				/>
			</div>
		</SSection>

		<SSection :title="t('pages.dev.ui.tabsAndBadges', 'Tabs and badges')">
			<STabs
				v-model="activeTab"
				:tabs="tabs"
				:label="t('pages.dev.ui.activityViews', 'Activity views')"
			>
				<template #panel-queue>
					<p class="ui-note">
						{{ t('pages.dev.ui.queuePanel', 'Queue panel content.') }}
					</p>
				</template>
				<template #panel-history>
					<p class="ui-note">
						{{ t('pages.dev.ui.historyPanel', 'History panel content.') }}
					</p>
				</template>
				<template #panel-blocklist>
					<p class="ui-note">
						{{ t('pages.dev.ui.blocklistPanel', 'Blocklist panel content.') }}
					</p>
				</template>
			</STabs>
			<div class="ui-row ui-row-top">
				<SBadge tone="ok">
					{{ t('pages.dev.ui.downloaded', 'Downloaded') }}
				</SBadge>
				<SBadge tone="warn">
					{{ t('pages.dev.ui.retrying', 'Retrying') }}
				</SBadge>
				<SBadge tone="danger">
					{{ t('pages.dev.ui.failed', 'Failed') }}
				</SBadge>
				<SBadge tone="info">
					{{ t('pages.dev.ui.queued', 'Queued') }}
				</SBadge>
				<SBadge tone="neutral">
					{{ t('pages.dev.ui.unmonitored', 'Unmonitored') }}
				</SBadge>
			</div>
		</SSection>

		<SSection :title="t('pages.dev.ui.progressAndLoading', 'Progress and loading')">
			<div class="ui-stack">
				<SProgress
					:value="35"
					:label="t('pages.dev.ui.downloadProgress', 'Download progress')"
				/>
				<SProgress
					:value="80"
					:label="t('pages.dev.ui.importProgress', 'Import progress')"
				/>
				<div class="ui-row ui-row-top">
					<SSpinner />
					<SSkeleton width="180px" />
					<SSkeleton
						width="120px"
						height="36px"
					/>
				</div>
			</div>
		</SSection>

		<SSection :title="t('pages.dev.ui.data', 'Data')">
			<STable
				:columns="tableColumns"
				:rows="tableRows"
				:row-key="row => row.title"
			>
				<template #cell-quality="{ row }">
					<SBadge tone="neutral">
						{{ row.quality }}
					</SBadge>
				</template>
			</STable>
			<div class="ui-row ui-row-top">
				<SPosterImage :alt="t('pages.dev.ui.posterAltA', 'Long Strange Trip')" />
				<SPosterImage
					src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='300'%3E%3Crect width='200' height='300' fill='%23163D3A'/%3E%3C/svg%3E"
					:alt="t('pages.dev.ui.posterAltB', 'Deep water documentary')"
				/>
				<SEmptyState :message="t('pages.dev.ui.noResults', 'No results. Try a different search.')">
					<template #action>
						<SButton variant="primary">
							{{ t('pages.dev.ui.searchAgain', 'Search again') }}
						</SButton>
					</template>
				</SEmptyState>
			</div>
		</SSection>

		<SSection :title="t('pages.dev.ui.overlays', 'Overlays')">
			<div class="ui-row">
				<SButton
					variant="primary"
					@click="dialogOpen = true"
				>
					{{ t('pages.dev.ui.openDialog', 'Open dialog') }}
				</SButton>
				<SButton
					variant="secondary"
					@click="wideDialogOpen = true"
				>
					{{ t('pages.dev.ui.openWideDialog', 'Open wide dialog') }}
				</SButton>
				<SDropdownMenu
					:items="[
						{ label: t('pages.dev.ui.refresh', 'Refresh'), icon: 'lucide:refresh-cw' },
						{ label: t('pages.dev.ui.previewRename', 'Preview rename'), icon: 'lucide:eye' },
						{ separator: true },
						{ label: t('pages.dev.ui.delete', 'Delete'), icon: 'lucide:trash-2', danger: true },
					]"
				>
					<template #trigger>
						<SButton variant="secondary">
							{{ t('pages.dev.ui.menu', 'Menu') }}
							<Icon
								name="lucide:chevron-down"
								aria-hidden="true"
							/>
						</SButton>
					</template>
				</SDropdownMenu>
				<SPopover>
					<template #trigger>
						<SButton variant="secondary">
							{{ t('pages.dev.ui.popover', 'Popover') }}
						</SButton>
					</template>
					<p class="ui-note">
						{{ t('pages.dev.ui.popoverContent', 'Secondary metadata lives here on hover.') }}
					</p>
				</SPopover>
				<STooltip :text="t('pages.dev.ui.tooltipText', 'Shows what changed')">
					<SIconButton :label="t('pages.dev.ui.info', 'Info')">
						<Icon name="lucide:info" />
					</SIconButton>
				</STooltip>
				<SButton
					variant="secondary"
					@click="toast({ title: t('pages.dev.ui.seriesAdded', 'Series added'), description: t('pages.dev.ui.seriesAddedDescription', 'North of North Island'), tone: 'ok' })"
				>
					{{ t('pages.dev.ui.successToast', 'Success toast') }}
				</SButton>
				<SButton
					variant="secondary"
					@click="toast({ title: t('pages.dev.ui.downloadFailed', 'Download failed'), description: t('pages.dev.ui.noIndexersResponded', 'No indexers responded'), tone: 'danger' })"
				>
					{{ t('pages.dev.ui.errorToast', 'Error toast') }}
				</SButton>
			</div>

			<SDialog
				v-model="dialogOpen"
				:title="t('pages.dev.ui.addSeries', 'Add series')"
				:description="t('pages.dev.ui.addSeriesDescription', 'Search Tvdb by title, then pick the folder and profile.')"
			>
				<SField :label="t('pages.dev.ui.path', 'Path')">
					<SInput :placeholder="t('pages.dev.ui.dataSeriesPlaceholder', '/data/series')" />
				</SField>
				<template #footer>
					<SButton
						variant="secondary"
						@click="dialogOpen = false"
					>
						{{ t('pages.dev.ui.cancel', 'Cancel') }}
					</SButton>
					<SButton
						variant="primary"
						@click="dialogOpen = false"
					>
						{{ t('pages.dev.ui.addSeries', 'Add series') }}
					</SButton>
				</template>
			</SDialog>

			<SDialog
				v-model="wideDialogOpen"
				:title="t('pages.dev.ui.interactiveSearch', 'Interactive search')"
				wide
			>
				<p class="ui-note">
					{{ t('pages.dev.ui.wideDialogContent', 'Release results would fill this wide dialog.') }}
				</p>
				<template #footer>
					<SButton
						variant="secondary"
						@click="wideDialogOpen = false"
					>
						{{ t('pages.dev.ui.close', 'Close') }}
					</SButton>
				</template>
			</SDialog>
		</SSection>
	</div>
</template>

<style scoped>
.ui-row {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
}

.ui-row-top {
	align-items: flex-start;
}

.ui-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
	gap: 16px;
	align-items: start;
	max-width: 960px;
}

.ui-stack {
	display: grid;
	gap: 16px;
	max-width: 480px;
}

.ui-note {
	color: var(--fg-muted);
}

.ui-row .s-poster {
	width: 120px;
}
</style>
