<script setup lang="ts">
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

useHead({ title: 'UI reference' })

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
	{ key: 'title', label: 'Title' },
	{ key: 'quality', label: 'Quality' },
	{ key: 'size', label: 'Size', align: 'right' as const },
]

const tableRows = [
	{ title: 'North of North Island, S01E03', quality: 'WEBDL-1080p', size: '1.4 GB' },
	{ title: 'Harbour Lights, S02E09', quality: 'Bluray-2160p', size: '12.9 GB' },
	{ title: 'The Trawler, S01E01', quality: 'HDTV-720p', size: '0.6 GB' },
]

const tabs = [
	{ value: 'queue', label: 'Queue' },
	{ value: 'history', label: 'History' },
	{ value: 'blocklist', label: 'Blocklist' },
]

const selectOptions = [
	{ value: 'any', label: 'Any' },
	{ value: 'series', label: 'Series' },
	{ value: 'movies', label: 'Movies' },
]
</script>

<template>
	<div>
		<SPageHeader title="UI reference">
			<template #actions>
				<ThemeMenu trigger-class="s-btn s-btn-secondary" />
			</template>
		</SPageHeader>

		<SSection title="Buttons">
			<div class="ui-row">
				<SButton variant="primary">
					Primary
				</SButton>
				<SButton variant="secondary">
					Secondary
				</SButton>
				<SButton variant="ghost">
					Ghost
				</SButton>
				<SButton variant="danger">
					Danger
				</SButton>
				<SButton
					variant="primary"
					loading
				>
					Loading
				</SButton>
				<SButton
					variant="secondary"
					disabled
				>
					Disabled
				</SButton>
				<SButton
					variant="secondary"
					size="sm"
				>
					Small
				</SButton>
				<SIconButton label="Search">
					<Icon name="lucide:search" />
				</SIconButton>
			</div>
		</SSection>

		<SSection title="Inputs">
			<div class="ui-grid">
				<SField
					label="Title"
					hint="Used for sorting and search."
				>
					<SInput
						v-model="textValue"
						placeholder="Release title"
					/>
				</SField>
				<SField
					label="With error"
					error="Enter a path that exists."
				>
					<SInput
						invalid
						placeholder="/downloads/series"
					/>
				</SField>
				<SField label="Quality profile">
					<SSelect
						v-model="selectValue"
						:options="selectOptions"
					/>
				</SField>
				<SField label="Notes">
					<STextarea
						v-model="areaValue"
						placeholder="Anything worth remembering"
					/>
				</SField>
				<SCheckbox
					v-model="checkValue"
					label="Monitor new items"
				/>
				<SSwitch
					v-model="switchValue"
					label="Season folder"
				/>
			</div>
		</SSection>

		<SSection title="Tabs and badges">
			<STabs
				v-model="activeTab"
				:tabs="tabs"
				label="Activity views"
			>
				<template #panel-queue>
					<p class="ui-note">
						Queue panel content.
					</p>
				</template>
				<template #panel-history>
					<p class="ui-note">
						History panel content.
					</p>
				</template>
				<template #panel-blocklist>
					<p class="ui-note">
						Blocklist panel content.
					</p>
				</template>
			</STabs>
			<div class="ui-row ui-row-top">
				<SBadge tone="ok">
					Downloaded
				</SBadge>
				<SBadge tone="warn">
					Retrying
				</SBadge>
				<SBadge tone="danger">
					Failed
				</SBadge>
				<SBadge tone="info">
					Queued
				</SBadge>
				<SBadge tone="neutral">
					Unmonitored
				</SBadge>
			</div>
		</SSection>

		<SSection title="Progress and loading">
			<div class="ui-stack">
				<SProgress
					:value="35"
					label="Download progress"
				/>
				<SProgress
					:value="80"
					label="Import progress"
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

		<SSection title="Data">
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
				<SPosterImage alt="Long Strange Trip" />
				<SPosterImage
					src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='200' height='300'%3E%3Crect width='200' height='300' fill='%23163D3A'/%3E%3C/svg%3E"
					alt="Deep water documentary"
				/>
				<SEmptyState message="No results. Try a different search.">
					<template #action>
						<SButton variant="primary">
							Search again
						</SButton>
					</template>
				</SEmptyState>
			</div>
		</SSection>

		<SSection title="Overlays">
			<div class="ui-row">
				<SButton
					variant="primary"
					@click="dialogOpen = true"
				>
					Open dialog
				</SButton>
				<SButton
					variant="secondary"
					@click="wideDialogOpen = true"
				>
					Open wide dialog
				</SButton>
				<SDropdownMenu
					:items="[
						{ label: 'Refresh', icon: 'lucide:refresh-cw' },
						{ label: 'Preview rename', icon: 'lucide:eye' },
						{ separator: true },
						{ label: 'Delete', icon: 'lucide:trash-2', danger: true },
					]"
				>
					<template #trigger>
						<SButton variant="secondary">
							Menu
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
							Popover
						</SButton>
					</template>
					<p class="ui-note">
						Secondary metadata lives here on hover.
					</p>
				</SPopover>
				<STooltip text="Shows what changed">
					<SIconButton label="Info">
						<Icon name="lucide:info" />
					</SIconButton>
				</STooltip>
				<SButton
					variant="secondary"
					@click="toast({ title: 'Series added', description: 'North of North Island', tone: 'ok' })"
				>
					Success toast
				</SButton>
				<SButton
					variant="secondary"
					@click="toast({ title: 'Download failed', description: 'No indexers responded', tone: 'danger' })"
				>
					Error toast
				</SButton>
			</div>

			<SDialog
				v-model="dialogOpen"
				title="Add series"
				description="Search Tvdb by title, then pick the folder and profile."
			>
				<SField label="Path">
					<SInput placeholder="/data/series" />
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
						@click="dialogOpen = false"
					>
						Add series
					</SButton>
				</template>
			</SDialog>

			<SDialog
				v-model="wideDialogOpen"
				title="Interactive search"
				wide
			>
				<p class="ui-note">
					Release results would fill this wide dialog.
				</p>
				<template #footer>
					<SButton
						variant="secondary"
						@click="wideDialogOpen = false"
					>
						Close
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
