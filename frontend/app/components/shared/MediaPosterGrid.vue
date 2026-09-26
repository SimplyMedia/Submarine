<script setup lang="ts">
/**
 * Poster grid for series/movies libraries. In select mode, clicking a card
 * toggles selection instead of navigating (used by the mass editor).
 */
import type { PosterCardItem } from '~/types/ui'

const props = withDefaults(defineProps<{
	items: PosterCardItem[]
	selectMode?: boolean
	selected?: number[]
}>(), {
	selectMode: false,
	selected: () => [],
})

const emit = defineEmits<{
	'update:selected': [ids: number[]]
}>()

const selectedSet = computed(() => new Set(props.selected))

function toggle(id: number) {
	const next = new Set(props.selected)
	if (next.has(id)) {
		next.delete(id)
	}
	else {
		next.add(id)
	}
	emit('update:selected', [...next])
}

function onClick(event: MouseEvent, id: number) {
	if (!props.selectMode) {
		return
	}
	event.preventDefault()
	toggle(id)
}
</script>

<template>
	<div
		v-if="items.length > 0"
		class="media-poster-grid"
	>
		<NuxtLink
			v-for="item in items"
			:key="item.id"
			:to="item.to"
			class="poster-card"
			:class="{ 'poster-card-selected': selectMode && selectedSet.has(item.id) }"
			@click="onClick($event, item.id)"
		>
			<span
				v-if="selectMode"
				class="poster-card-check"
				:class="{ 'poster-card-check-on': selectedSet.has(item.id) }"
				aria-hidden="true"
			>
				<Icon
					v-if="selectedSet.has(item.id)"
					name="lucide:check"
				/>
			</span>
			<SPosterImage
				:src="item.posterUrl"
				:alt="item.title"
			/>
			<div
				v-if="item.badges?.length"
				class="poster-card-badges"
			>
				<SBadge
					v-for="badge in item.badges"
					:key="badge.label"
					:tone="badge.tone"
				>
					{{ badge.label }}
				</SBadge>
			</div>
			<SProgress
				v-if="item.progress != null"
				:value="item.progress"
				:label="item.progressLabel ?? `${item.title} episodes on disk`"
			/>
			<div class="poster-card-meta">
				<p class="poster-card-title">
					{{ item.title }}
				</p>
				<p
					v-if="item.meta"
					class="poster-card-sub"
				>
					{{ item.meta }}
				</p>
				<p
					v-if="item.metaSecondary"
					class="poster-card-sub"
				>
					{{ item.metaSecondary }}
				</p>
			</div>
		</NuxtLink>
	</div>
	<slot
		v-else
		name="empty"
	>
		<SEmptyState message="Nothing here yet" />
	</slot>
</template>

<style scoped>
.media-poster-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
	gap: 24px;
}

.poster-card {
	position: relative;
	display: block;
	color: inherit;
	text-decoration: none;
	border-radius: var(--r-panel);
}

.poster-card :deep(.s-poster) {
	border-radius: var(--r-panel);
	outline: 1px solid var(--line);
	outline-offset: -1px;
	transition: outline-color 120ms ease;
}

.poster-card:hover :deep(.s-poster) {
	outline-color: var(--line-strong);
}

.poster-card-selected :deep(.s-poster) {
	outline: 2px solid var(--accent);
	outline-offset: -2px;
}

.poster-card-check {
	position: absolute;
	top: 8px;
	right: 8px;
	z-index: 1;
	width: 22px;
	height: 22px;
	border-radius: 999px;
	background: var(--surface);
	border: 1px solid var(--line-strong);
	display: flex;
	align-items: center;
	justify-content: center;
	color: transparent;
}

.poster-card-check-on {
	background: var(--accent);
	border-color: var(--accent);
	color: var(--accent-fg);
}

.poster-card-badges {
	position: absolute;
	top: 8px;
	left: 8px;
	display: flex;
	flex-direction: column;
	gap: 4px;
	align-items: flex-start;
}

.poster-card-meta {
	padding-top: 8px;
}

.poster-card-title {
	font-size: var(--text-sm);
	font-weight: 500;
	line-height: 1.3;
	overflow: hidden;
	text-overflow: ellipsis;
	display: -webkit-box;
	-webkit-line-clamp: 2;
	-webkit-box-orient: vertical;
}

.poster-card-sub {
	font-size: var(--text-xs);
	color: var(--fg-muted);
	margin-top: 2px;
}
</style>
