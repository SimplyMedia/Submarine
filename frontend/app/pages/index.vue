<script setup lang="ts">
import type { components } from '~/types/api'
import type { PosterCardItem } from '~/types/ui'
import { historyEventTypeLabel } from '~/utils/library-labels'
import { useActivityStore } from '~/stores/activity'

type CalendarEvent = components['schemas']['CalendarEventDto']
type HistoryEvent = components['schemas']['HistoryEventDto']

useHead({ title: 'Submarine' })

const api = useApi()
const system = useSystemStore()
const activity = useActivityStore()

const loading = ref(true)

const posterMap = new Map<string, string | null>()

interface RecentItem {
	kind: 'series' | 'movie'
	id: number
	title: string
	posterUrl: string | null
	added: string
}

const recentlyAdded = ref<RecentItem[]>([])
const calendarEvents = ref<CalendarEvent[]>([])
const recentHistory = ref<HistoryEvent[]>([])

const days = computed(() => {
	const now = new Date()
	const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
	return Array.from({ length: 7 }, (_, index) => {
		const dayStart = todayStart + (index - 1) * 86_400_000
		return {
			date: new Date(dayStart),
			isToday: index === 1,
		}
	})
})

const scheduleStart = computed(() => days.value[0]!.date.toISOString())
const scheduleEnd = computed(() => new Date(days.value[6]!.date.getTime() + 86_400_000).toISOString())

const eventsByDay = computed(() => {
	const map = new Map<string, CalendarEvent[]>()
	for (const day of days.value) {
		map.set(day.date.toDateString(), [])
	}
	for (const event of calendarEvents.value) {
		const key = new Date(event.date).toDateString()
		map.get(key)?.push(event)
	}
	return map
})

function eventKey(event: CalendarEvent) {
	return `${event.type}-${event.id}`
}

function posterKey(event: CalendarEvent) {
	return `${event.type}-${event.seriesOrMovieId}`
}

function eventTo(event: CalendarEvent) {
	return event.type === 'movie' ? `/movies/${event.seriesOrMovieId}` : `/series/${event.seriesOrMovieId}`
}

function eventLabel(event: CalendarEvent) {
	if (event.type === 'episode' && event.seasonNumber != null && event.episodeNumber != null) {
		return `S${String(event.seasonNumber).padStart(2, '0')}E${String(event.episodeNumber).padStart(2, '0')}`
	}
	return event.kind === 'inCinemas' ? 'In cinemas' : event.kind === 'digital' ? 'Digital' : event.kind === 'physical' ? 'Physical' : 'Movie'
}

const dayFormatter = new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short' })

const healthTone = computed(() => {
	if (system.healthIssues.some(issue => issue.type === 'ERROR')) {
		return 'danger' as const
	}
	if (system.healthIssues.some(issue => issue.type === 'WARNING')) {
		return 'warn' as const
	}
	return 'ok' as const
})

const healthText = computed(() => {
	const count = system.healthIssues.length
	return count === 0 ? 'All systems normal' : `${count} health ${count === 1 ? 'issue' : 'issues'}`
})

const recentCards = computed<PosterCardItem[]>(() => recentlyAdded.value.map(item => ({
	id: item.kind === 'movie' ? item.id + 100_000 : item.id,
	to: item.kind === 'series' ? `/series/${item.id}` : `/movies/${item.id}`,
	posterUrl: item.posterUrl,
	title: item.title,
	meta: item.kind === 'series' ? 'Series' : 'Movie',
})))

let stopWatchQueue: (() => void) | null = null

onMounted(async () => {
	loading.value = true
	stopWatchQueue = activity.watchQueue()
	const [seriesResult, moviesResult, recentSeriesResult, recentMoviesResult, calendarResult, historyResult] = await Promise.all([
		api.GET('/api/v1/series', { params: { query: { PageSize: 200 } } }),
		api.GET('/api/v1/movies', { params: { query: { PageSize: 200 } } }),
		api.GET('/api/v1/series', { params: { query: { PageSize: 12, SortKey: 'CreatedAt', SortDirection: 'descending' } } }),
		api.GET('/api/v1/movies', { params: { query: { PageSize: 12, SortKey: 'CreatedAt', SortDirection: 'descending' } } }),
		api.GET('/api/v1/calendar', { params: { query: { start: scheduleStart.value, end: scheduleEnd.value } } }),
		api.GET('/api/v1/history', { params: { query: { PageSize: 5 } } }),
		activity.load(5),
		system.loadStatus(),
	])

	if (seriesResult.data) {
		for (const series of seriesResult.data.items) {
			posterMap.set(`episode-${series.id}`, series.posterUrl)
		}
	}
	if (moviesResult.data) {
		for (const movie of moviesResult.data.items) {
			posterMap.set(`movie-${movie.id}`, movie.posterUrl)
		}
	}

	const combined: RecentItem[] = []
	if (recentSeriesResult.data) {
		for (const series of recentSeriesResult.data.items) {
			combined.push({ kind: 'series', id: series.id, title: series.title, posterUrl: series.posterUrl, added: series.added })
		}
	}
	if (recentMoviesResult.data) {
		for (const movie of recentMoviesResult.data.items) {
			combined.push({ kind: 'movie', id: movie.id, title: movie.title, posterUrl: movie.posterUrl, added: movie.added })
		}
	}
	combined.sort((a, b) => b.added.localeCompare(a.added))
	recentlyAdded.value = combined.slice(0, 12)

	if (calendarResult.data) {
		calendarEvents.value = calendarResult.data
	}
	if (historyResult.data) {
		recentHistory.value = historyResult.data.items
	}
	loading.value = false
})

onBeforeUnmount(() => {
	stopWatchQueue?.()
})
</script>

<template>
	<div>
		<SPageHeader title="Dashboard" />

		<SSpinner v-if="loading" />
		<template v-else>
			<SSection title="Schedule">
				<div class="schedule-strip">
					<div
						v-for="day in days"
						:key="day.date.toISOString()"
						class="schedule-day"
						:class="{ 'schedule-day-today': day.isToday }"
					>
						<p class="schedule-day-header">
							{{ dayFormatter.format(day.date) }}
						</p>
						<div
							v-if="(eventsByDay.get(day.date.toDateString()) ?? []).length > 0"
							class="schedule-day-items"
						>
							<NuxtLink
								v-for="event in eventsByDay.get(day.date.toDateString())"
								:key="eventKey(event)"
								:to="eventTo(event)"
								class="schedule-item"
								:class="{ 'schedule-item-dim': !event.monitored }"
							>
								<SPosterImage
									:src="posterMap.get(posterKey(event)) ?? null"
									:alt="event.title"
									class="schedule-item-poster"
								/>
								<span class="schedule-item-text">
									<span class="schedule-item-title">{{ event.title }}</span>
									<span class="schedule-item-sub">{{ eventLabel(event) }}</span>
								</span>
							</NuxtLink>
						</div>
						<p
							v-else
							class="schedule-day-empty"
						>
							Nothing scheduled
						</p>
					</div>
				</div>
			</SSection>

			<SSection title="Activity">
				<div class="activity-grid">
					<div class="activity-panel">
						<p class="activity-panel-title">
							Queue
						</p>
						<p
							v-if="activity.status"
							class="activity-summary"
						>
							{{ activity.status.total }} downloading<span v-if="activity.status.errors">, {{ activity.status.errors }} failed</span>
						</p>
						<SEmptyState
							v-if="activity.loadError"
							:message="activity.loadError"
							icon="lucide:alert-triangle"
						>
							<template #action>
								<SButton @click="activity.load(5)">
									Retry
								</SButton>
							</template>
						</SEmptyState>
						<QueueTable
							v-else
							:items="activity.items"
							compact
						/>
					</div>
					<div class="activity-panel">
						<p class="activity-panel-title">
							Recent activity
						</p>
						<ul
							v-if="recentHistory.length > 0"
							class="activity-list"
						>
							<li
								v-for="event in recentHistory"
								:key="event.id"
							>
								<span class="activity-list-title">{{ event.seriesTitle ?? event.movieTitle ?? event.sourceTitle }}</span>
								<span class="activity-list-sub">{{ historyEventTypeLabel(event.type as never) }}</span>
							</li>
						</ul>
						<SEmptyState
							v-else
							message="No activity yet"
						/>
					</div>
					<div class="activity-panel">
						<p class="activity-panel-title">
							Health
						</p>
						<p class="activity-summary">
							<SBadge :tone="healthTone">
								{{ healthText }}
							</SBadge>
						</p>
						<ul
							v-if="system.healthIssues.length > 0"
							class="activity-list"
						>
							<li
								v-for="issue in system.healthIssues"
								:key="issue.id"
							>
								<span class="activity-list-title">{{ issue.message }}</span>
							</li>
						</ul>
					</div>
				</div>
			</SSection>

			<SSection title="Recently added">
				<MediaPosterGrid :items="recentCards">
					<template #empty>
						<SEmptyState message="Your library is empty. Add a series or a movie to get started.">
							<template #action>
								<div class="dashboard-empty-actions">
									<SButton
										variant="primary"
										@click="navigateTo('/series?add=1')"
									>
										Add series
									</SButton>
									<SButton @click="navigateTo('/movies?add=1')">
										Add movie
									</SButton>
								</div>
							</template>
						</SEmptyState>
					</template>
				</MediaPosterGrid>
			</SSection>
		</template>
	</div>
</template>

<style scoped>
.schedule-strip {
	display: grid;
	grid-template-columns: repeat(7, 1fr);
	gap: 16px;
}

.schedule-day {
	min-width: 0;
	padding-left: 12px;
	border-left: 2px solid transparent;
}

.schedule-day-today {
	border-left-color: var(--accent);
}

.schedule-day-header {
	font-weight: 500;
	font-size: var(--text-sm);
	margin-bottom: 12px;
}

.schedule-day-items {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.schedule-item {
	display: flex;
	gap: 8px;
	color: inherit;
	text-decoration: none;
	border-radius: var(--r-control);
	padding: 4px;
	margin: -4px;
}

.schedule-item:hover {
	background: var(--surface-2);
}

.schedule-item-dim {
	opacity: 0.6;
}

.schedule-item-poster {
	width: 32px;
	flex: none;
	border-radius: 4px;
}

.schedule-item-text {
	display: flex;
	flex-direction: column;
	min-width: 0;
}

.schedule-item-title {
	font-size: var(--text-sm);
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.schedule-item-sub {
	font-size: var(--text-xs);
	color: var(--fg-muted);
}

.schedule-day-empty {
	font-size: var(--text-sm);
	color: var(--fg-faint);
}

.activity-grid {
	display: grid;
	grid-template-columns: repeat(3, 1fr);
	gap: 24px;
}

.activity-panel-title {
	font-weight: 500;
	margin-bottom: 8px;
}

.activity-summary {
	margin-bottom: 12px;
	color: var(--fg-muted);
	font-size: var(--text-sm);
}

.activity-list {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.activity-list-title {
	display: block;
	font-size: var(--text-sm);
}

.activity-list-sub {
	font-size: var(--text-xs);
	color: var(--fg-muted);
}

.dashboard-empty-actions {
	display: flex;
	gap: 8px;
}

@media (max-width: 1023px) {
	.schedule-strip {
		grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
	}

	.activity-grid {
		grid-template-columns: 1fr;
	}
}

@media (max-width: 767px) {
	.schedule-strip {
		grid-template-columns: 1fr 1fr;
	}
}
</style>
