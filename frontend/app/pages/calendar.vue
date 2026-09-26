<script setup lang="ts">
import type { components } from '~/types/api'

type CalendarEvent = components['schemas']['CalendarEventDto']
type ViewMode = 'month' | 'week' | 'day' | 'agenda'

interface CalendarDay {
	key: string
	date: Date
	dayNumber: number
	inCurrentMonth: boolean
	isToday: boolean
	events: CalendarEvent[]
}

useHead({ title: 'Calendar' })

const reference = useReferenceStore()
const { toast } = useToast()

const view = ref<ViewMode>('month')
const anchor = ref(localMidnight(new Date()))
const events = ref<CalendarEvent[]>([])
const loading = ref(true)
const showUnmonitored = ref(false)
const selectedTagIds = ref<number[]>([])
const feedToken = ref('')

const viewOptions: Array<{ value: ViewMode, label: string }> = [
	{ value: 'month', label: 'Month' },
	{ value: 'week', label: 'Week' },
	{ value: 'day', label: 'Day' },
	{ value: 'agenda', label: 'Agenda' },
]

const firstDayOfWeek = computed(() => reference.uiConfig?.firstDayOfWeek ?? 1)

function localMidnight(date: Date): Date {
	return new Date(date.getFullYear(), date.getMonth(), date.getDate())
}

function addDays(date: Date, days: number): Date {
	const next = new Date(date)
	next.setDate(next.getDate() + days)
	return next
}

function startOfWeek(date: Date, weekStart: number): Date {
	const diff = (date.getDay() - weekStart + 7) % 7
	return addDays(date, -diff)
}

function dayKey(date: Date): string {
	return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

const range = computed(() => {
	const weekStart = firstDayOfWeek.value
	if (view.value === 'day') {
		return { start: anchor.value, end: addDays(anchor.value, 1) }
	}
	if (view.value === 'week') {
		const start = startOfWeek(anchor.value, weekStart)
		return { start, end: addDays(start, 7) }
	}
	if (view.value === 'agenda') {
		return { start: anchor.value, end: addDays(anchor.value, 14) }
	}
	const monthStart = new Date(anchor.value.getFullYear(), anchor.value.getMonth(), 1)
	const gridStart = startOfWeek(monthStart, weekStart)
	return { start: gridStart, end: addDays(gridStart, 42) }
})

const eventsByDay = computed(() => {
	const map = new Map<string, CalendarEvent[]>()
	for (const event of events.value) {
		const key = dayKey(new Date(event.date))
		const list = map.get(key)
		if (list) {
			list.push(event)
		}
		else {
			map.set(key, [event])
		}
	}
	for (const list of map.values()) {
		list.sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
	}
	return map
})

function buildDay(date: Date, inCurrentMonth: boolean, todayKey: string): CalendarDay {
	const key = dayKey(date)
	return {
		key,
		date,
		dayNumber: date.getDate(),
		inCurrentMonth,
		isToday: key === todayKey,
		events: eventsByDay.value.get(key) ?? [],
	}
}

const weekdayLabels = computed(() => {
	const formatter = new Intl.DateTimeFormat('en-GB', { weekday: 'short' })
	const base = startOfWeek(localMidnight(new Date()), firstDayOfWeek.value)
	return Array.from({ length: 7 }, (_, i) => formatter.format(addDays(base, i)))
})

const gridDays = computed<CalendarDay[]>(() => {
	const todayKey = dayKey(localMidnight(new Date()))
	if (view.value === 'week') {
		const start = startOfWeek(anchor.value, firstDayOfWeek.value)
		return Array.from({ length: 7 }, (_, i) => buildDay(addDays(start, i), true, todayKey))
	}
	const monthStart = new Date(anchor.value.getFullYear(), anchor.value.getMonth(), 1)
	const gridStart = startOfWeek(monthStart, firstDayOfWeek.value)
	return Array.from({ length: 42 }, (_, i) => {
		const date = addDays(gridStart, i)
		return buildDay(date, date.getMonth() === anchor.value.getMonth(), todayKey)
	})
})

const dayViewEvents = computed(() => eventsByDay.value.get(dayKey(anchor.value)) ?? [])

const agendaDays = computed<CalendarDay[]>(() => {
	const todayKey = dayKey(localMidnight(new Date()))
	return Array.from({ length: 14 }, (_, i) => buildDay(addDays(anchor.value, i), true, todayKey))
})

const rangeLabel = computed(() => {
	if (view.value === 'month') {
		return new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric' }).format(anchor.value)
	}
	if (view.value === 'day') {
		return formatLongDate(anchor.value)
	}
	const { start, end } = range.value
	const last = addDays(end, -1)
	const startFmt = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short' }).format(start)
	const endFmt = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }).format(last)
	return `${startFmt} to ${endFmt}`
})

function agendaDayLabel(date: Date): string {
	return new Intl.DateTimeFormat('en-GB', { weekday: 'long', day: 'numeric', month: 'long' }).format(date)
}

function goPrev() {
	if (view.value === 'month') {
		anchor.value = new Date(anchor.value.getFullYear(), anchor.value.getMonth() - 1, 1)
	}
	else if (view.value === 'week') {
		anchor.value = addDays(anchor.value, -7)
	}
	else if (view.value === 'day') {
		anchor.value = addDays(anchor.value, -1)
	}
	else {
		anchor.value = addDays(anchor.value, -14)
	}
}

function goNext() {
	if (view.value === 'month') {
		anchor.value = new Date(anchor.value.getFullYear(), anchor.value.getMonth() + 1, 1)
	}
	else if (view.value === 'week') {
		anchor.value = addDays(anchor.value, 7)
	}
	else if (view.value === 'day') {
		anchor.value = addDays(anchor.value, 1)
	}
	else {
		anchor.value = addDays(anchor.value, 14)
	}
}

function goToday() {
	anchor.value = localMidnight(new Date())
}

function toggleTag(id: number) {
	selectedTagIds.value = selectedTagIds.value.includes(id)
		? selectedTagIds.value.filter(t => t !== id)
		: [...selectedTagIds.value, id]
}

function eventHref(event: CalendarEvent): string {
	return event.type === 'episode' ? `/series/${event.seriesOrMovieId}` : `/movies/${event.seriesOrMovieId}`
}

function movieKindLabel(kind: string): string {
	switch (kind) {
		case 'inCinemas':
			return 'In cinemas'
		case 'digital':
			return 'Digital release'
		case 'physical':
			return 'Physical release'
		default:
			return 'Movie'
	}
}

function eventSubLabel(event: CalendarEvent): string {
	if (event.type === 'episode') {
		const season = String(event.seasonNumber ?? 0).padStart(2, '0')
		const episode = String(event.episodeNumber ?? 0).padStart(2, '0')
		return `S${season}E${episode}`
	}
	return movieKindLabel(event.kind)
}

function eventTime(event: CalendarEvent): string {
	const date = new Date(event.date)
	return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

/** danger: aired with no file yet, ok: has a file, neutral: still upcoming or downloading. */
function eventTone(event: CalendarEvent): 'danger' | 'ok' | 'neutral' {
	if (event.hasFile) {
		return 'ok'
	}
	if (event.downloading || new Date(event.date).getTime() > Date.now()) {
		return 'neutral'
	}
	return 'danger'
}

function eventKey(event: CalendarEvent): string {
	return `${event.type}-${event.id}-${event.kind}-${event.date}`
}

const feedUrl = computed(() => {
	if (!import.meta.client || !feedToken.value) {
		return ''
	}
	return `${window.location.origin}${baseUrl()}/api/v1/calendar/feed.ics?token=${feedToken.value}`
})

async function copyFeedUrl() {
	if (!feedUrl.value) {
		return
	}
	try {
		await navigator.clipboard.writeText(feedUrl.value)
		toast({ title: 'Feed URL copied', tone: 'ok' })
	}
	catch {
		toast({ title: 'Could not copy the feed URL', tone: 'danger' })
	}
}

async function loadFeedToken() {
	const api = useApi()
	const result = await api.GET('/api/v1/config/general')
	if (result.data?.feedToken) {
		feedToken.value = result.data.feedToken
	}
}

async function loadEvents() {
	loading.value = true
	const api = useApi()
	const { start, end } = range.value
	const query: { start: string, end: string, unmonitored: boolean, tags?: string } = {
		start: start.toISOString(),
		end: end.toISOString(),
		unmonitored: showUnmonitored.value,
	}
	if (selectedTagIds.value.length > 0) {
		query.tags = selectedTagIds.value.join(',')
	}
	const result = await api.GET('/api/v1/calendar', { params: { query } })
	if (result.data) {
		events.value = result.data
	}
	else {
		toast({ title: 'Could not load the calendar', tone: 'danger' })
	}
	loading.value = false
}

watch(
	() => [view.value, anchor.value.getTime(), firstDayOfWeek.value, showUnmonitored.value, selectedTagIds.value],
	() => { void loadEvents() },
)

onMounted(async () => {
	await reference.load()
	await Promise.all([loadFeedToken(), loadEvents()])
})
</script>

<template>
	<div>
		<SPageHeader title="Calendar">
			<template #actions>
				<SPopover>
					<template #trigger>
						<SButton variant="secondary">
							iCal feed
						</SButton>
					</template>
					<div class="calendar-feed-popover">
						<p class="calendar-feed-hint">
							Subscribe to this URL from any calendar app that supports iCal feeds.
						</p>
						<div class="calendar-feed-row">
							<SInput
								:model-value="feedUrl"
								readonly
								aria-label="iCal feed URL"
							/>
							<SIconButton
								label="Copy feed URL"
								@click="copyFeedUrl"
							>
								<Icon
									name="lucide:copy"
									aria-hidden="true"
								/>
							</SIconButton>
						</div>
					</div>
				</SPopover>
			</template>
		</SPageHeader>

		<div class="calendar-toolbar">
			<div class="calendar-nav">
				<SIconButton
					label="Previous"
					@click="goPrev"
				>
					<Icon
						name="lucide:chevron-left"
						aria-hidden="true"
					/>
				</SIconButton>
				<SButton
					size="sm"
					variant="secondary"
					@click="goToday"
				>
					Today
				</SButton>
				<SIconButton
					label="Next"
					@click="goNext"
				>
					<Icon
						name="lucide:chevron-right"
						aria-hidden="true"
					/>
				</SIconButton>
				<h2 class="calendar-range-label">
					{{ rangeLabel }}
				</h2>
			</div>
			<div class="calendar-view-toggle">
				<SButton
					v-for="opt in viewOptions"
					:key="opt.value"
					size="sm"
					:variant="view === opt.value ? 'primary' : 'secondary'"
					@click="view = opt.value"
				>
					{{ opt.label }}
				</SButton>
			</div>
		</div>

		<div class="calendar-filters">
			<SSwitch
				v-model="showUnmonitored"
				label="Show unmonitored"
			/>
			<div
				v-if="reference.tags.length > 0"
				class="calendar-tag-chips"
			>
				<button
					v-for="tag in reference.tags"
					:key="tag.id"
					type="button"
					class="s-badge calendar-tag-chip"
					:class="selectedTagIds.includes(tag.id) ? 's-badge-info' : 's-badge-neutral'"
					@click="toggleTag(tag.id)"
				>
					{{ tag.label }}
				</button>
			</div>
		</div>

		<SSkeleton
			v-if="loading"
			height="520px"
		/>
		<template v-else>
			<div v-if="view === 'month' || view === 'week'">
				<div class="calendar-weekdays">
					<span
						v-for="label in weekdayLabels"
						:key="label"
					>{{ label }}</span>
				</div>
				<div
					class="calendar-grid"
					:class="{ 'calendar-grid-week': view === 'week' }"
				>
					<div
						v-for="day in gridDays"
						:key="day.key"
						class="calendar-cell"
						:class="{
							'calendar-cell-outside': !day.inCurrentMonth,
							'calendar-cell-today': day.isToday,
							'calendar-cell-week': view === 'week',
						}"
					>
						<span class="calendar-cell-date">{{ day.dayNumber }}</span>
						<div class="calendar-cell-events">
							<NuxtLink
								v-for="event in day.events"
								:key="eventKey(event)"
								:to="eventHref(event)"
								class="calendar-chip"
								:class="[`calendar-chip-${eventTone(event)}`, { 'calendar-chip-dim': !event.monitored }]"
							>
								<span class="calendar-chip-title">{{ event.title }}</span>
								<span class="calendar-chip-meta">{{ eventSubLabel(event) }}</span>
							</NuxtLink>
						</div>
					</div>
				</div>
			</div>

			<div
				v-else-if="view === 'day'"
				class="calendar-day-view"
			>
				<h3 class="calendar-day-heading">
					{{ rangeLabel }}
				</h3>
				<SEmptyState
					v-if="dayViewEvents.length === 0"
					message="Nothing scheduled for this day."
					icon="lucide:calendar"
				/>
				<div
					v-else
					class="calendar-rows"
				>
					<NuxtLink
						v-for="event in dayViewEvents"
						:key="eventKey(event)"
						:to="eventHref(event)"
						class="calendar-row"
						:class="[`calendar-row-${eventTone(event)}`, { 'calendar-row-dim': !event.monitored }]"
					>
						<span class="calendar-row-time">{{ eventTime(event) }}</span>
						<span class="calendar-row-title">{{ event.title }}</span>
						<span class="calendar-row-meta">{{ eventSubLabel(event) }}</span>
						<span
							v-if="event.type === 'episode' && event.episodeTitle"
							class="calendar-row-episode-title"
						>{{ event.episodeTitle }}</span>
						<SBadge
							v-if="!event.monitored"
							tone="neutral"
						>
							Unmonitored
						</SBadge>
					</NuxtLink>
				</div>
			</div>

			<div
				v-else
				class="calendar-agenda"
			>
				<SEmptyState
					v-if="events.length === 0"
					message="Nothing scheduled in the next 14 days."
					icon="lucide:calendar"
				/>
				<template v-else>
					<div
						v-for="day in agendaDays"
						:key="day.key"
						class="calendar-agenda-day"
					>
						<h3
							class="calendar-day-heading"
							:class="{ 'calendar-day-heading-today': day.isToday }"
						>
							{{ agendaDayLabel(day.date) }}
						</h3>
						<p
							v-if="day.events.length === 0"
							class="calendar-agenda-empty"
						>
							Nothing scheduled.
						</p>
						<div
							v-else
							class="calendar-rows"
						>
							<NuxtLink
								v-for="event in day.events"
								:key="eventKey(event)"
								:to="eventHref(event)"
								class="calendar-row"
								:class="[`calendar-row-${eventTone(event)}`, { 'calendar-row-dim': !event.monitored }]"
							>
								<span class="calendar-row-time">{{ eventTime(event) }}</span>
								<span class="calendar-row-title">{{ event.title }}</span>
								<span class="calendar-row-meta">{{ eventSubLabel(event) }}</span>
								<span
									v-if="event.type === 'episode' && event.episodeTitle"
									class="calendar-row-episode-title"
								>{{ event.episodeTitle }}</span>
								<SBadge
									v-if="!event.monitored"
									tone="neutral"
								>
									Unmonitored
								</SBadge>
							</NuxtLink>
						</div>
					</div>
				</template>
			</div>
		</template>
	</div>
</template>

<style scoped>
.calendar-toolbar {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	justify-content: space-between;
	gap: 12px;
	margin-bottom: 16px;
}

.calendar-nav {
	display: flex;
	align-items: center;
	gap: 8px;
}

.calendar-range-label {
	margin: 0 0 0 4px;
	font-size: var(--text-lg);
	font-weight: 600;
	white-space: nowrap;
}

.calendar-view-toggle {
	display: flex;
	gap: 4px;
}

.calendar-filters {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 16px;
	margin-bottom: 24px;
}

.calendar-tag-chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}

.calendar-tag-chip {
	border: 0;
	cursor: pointer;
	font: inherit;
}

.calendar-weekdays {
	display: grid;
	grid-template-columns: repeat(7, minmax(0, 1fr));
	padding: 0 2px 8px;
	color: var(--fg-muted);
	font-size: var(--text-sm);
	font-weight: 500;
}

.calendar-grid {
	display: grid;
	grid-template-columns: repeat(7, minmax(0, 1fr));
	gap: 1px;
	background: var(--line);
	border: 1px solid var(--line);
	border-radius: var(--r-panel);
	overflow: hidden;
}

.calendar-cell {
	display: flex;
	flex-direction: column;
	gap: 4px;
	min-width: 0;
	min-height: 96px;
	padding: 6px;
	background: var(--surface);
}

.calendar-cell-week {
	min-height: 220px;
}

.calendar-cell-outside {
	background: var(--surface-2);
}

.calendar-cell-outside .calendar-cell-date {
	color: var(--fg-faint);
}

.calendar-cell-today {
	box-shadow: inset 2px 0 0 0 var(--accent);
}

.calendar-cell-date {
	font-size: var(--text-sm);
	font-variant-numeric: tabular-nums;
	color: var(--fg-muted);
}

.calendar-cell-events {
	display: flex;
	flex-direction: column;
	gap: 2px;
	overflow-y: auto;
}

.calendar-chip {
	display: flex;
	flex-direction: column;
	min-width: 0;
	padding: 2px 6px;
	border-radius: var(--r-control);
	text-decoration: none;
	font-size: var(--text-xs);
	line-height: 1.3;
}

.calendar-chip-title {
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
	font-weight: 500;
}

.calendar-chip-meta {
	color: inherit;
	opacity: 0.8;
}

.calendar-chip-danger {
	background: color-mix(in srgb, var(--danger) 14%, transparent);
	color: var(--danger);
}

.calendar-chip-ok {
	background: color-mix(in srgb, var(--accent) 14%, transparent);
	color: var(--accent);
}

.calendar-chip-neutral {
	background: var(--surface-2);
	color: var(--fg-muted);
}

.calendar-chip-dim {
	opacity: 0.55;
}

.calendar-day-heading {
	margin: 0 0 12px;
	font-size: var(--text-lg);
	font-weight: 600;
}

.calendar-day-heading-today {
	color: var(--accent);
}

.calendar-rows {
	display: flex;
	flex-direction: column;
	border-top: 1px solid var(--line);
}

.calendar-row {
	display: flex;
	align-items: center;
	gap: 12px;
	height: var(--row-h);
	padding: 0 8px;
	border-bottom: 1px solid var(--line);
	text-decoration: none;
	color: var(--fg);
}

.calendar-row:hover {
	background: var(--surface-2);
}

.calendar-row-time {
	flex: none;
	width: 44px;
	font-variant-numeric: tabular-nums;
	color: var(--fg-muted);
	font-size: var(--text-sm);
}

.calendar-row-title {
	flex: none;
	font-weight: 500;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
	max-width: 40%;
}

.calendar-row-meta {
	flex: none;
	padding: 2px 8px;
	border-radius: var(--r-control);
	font-size: var(--text-xs);
}

.calendar-row-danger .calendar-row-meta {
	background: color-mix(in srgb, var(--danger) 14%, transparent);
	color: var(--danger);
}

.calendar-row-ok .calendar-row-meta {
	background: color-mix(in srgb, var(--accent) 14%, transparent);
	color: var(--accent);
}

.calendar-row-neutral .calendar-row-meta {
	background: var(--surface-2);
	color: var(--fg-muted);
}

.calendar-row-episode-title {
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
	color: var(--fg-muted);
	font-size: var(--text-sm);
}

.calendar-row-dim {
	opacity: 0.55;
}

.calendar-agenda-day {
	margin-bottom: 24px;
}

.calendar-agenda-empty {
	margin: 0 0 12px;
	color: var(--fg-faint);
	font-size: var(--text-sm);
}

.calendar-feed-popover {
	display: flex;
	flex-direction: column;
	gap: 8px;
	width: 300px;
	max-width: calc(100vw - 32px);
}

.calendar-feed-hint {
	margin: 0;
	color: var(--fg-muted);
	font-size: var(--text-sm);
}

.calendar-feed-row {
	display: flex;
	gap: 8px;
}

.calendar-feed-row .s-input {
	flex: 1;
	min-width: 0;
}

@media (max-width: 640px) {
	.calendar-cell {
		min-height: 72px;
		padding: 4px;
	}

	.calendar-row-title {
		max-width: 30%;
	}
}
</style>
