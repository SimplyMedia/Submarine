/**
 * Central navigation registry. The rail, the mobile tab bar and the More
 * sheet all render from this list; placeholder titles resolve through it too.
 */
export interface NavChild {
	key: string
	to: string
}

export interface NavItem {
	id: string
	key: string
	to: string
	icon: string
	children?: NavChild[]
}

export const navigation: NavItem[] = [
	{ id: 'series', key: 'utils.navigation.series', to: '/series', icon: 'lucide:tv' },
	{ id: 'movies', key: 'utils.navigation.movies', to: '/movies', icon: 'lucide:film' },
	{ id: 'calendar', key: 'utils.navigation.calendar', to: '/calendar', icon: 'lucide:calendar' },
	{
		id: 'activity',
		key: 'utils.navigation.activity',
		to: '/activity',
		icon: 'lucide:activity',
		children: [
			{ key: 'utils.navigation.queue', to: '/activity/queue' },
			{ key: 'utils.navigation.history', to: '/activity/history' },
			{ key: 'utils.navigation.blocklist', to: '/activity/blocklist' },
			{ key: 'utils.navigation.import', to: '/activity/import' },
		],
	},
	{
		id: 'wanted',
		key: 'utils.navigation.wanted',
		to: '/wanted',
		icon: 'lucide:flag',
		children: [
			{ key: 'utils.navigation.missing', to: '/wanted/missing' },
			{ key: 'utils.navigation.cutoff', to: '/wanted/cutoff' },
		],
	},
	{
		id: 'indexers',
		key: 'utils.navigation.indexers',
		to: '/indexers',
		icon: 'lucide:rss',
		children: [
			{ key: 'utils.navigation.indexers', to: '/indexers' },
			{ key: 'utils.navigation.search', to: '/indexers/search' },
			{ key: 'utils.navigation.stats', to: '/indexers/stats' },
			{ key: 'utils.navigation.proxies', to: '/indexers/proxies' },
			{ key: 'utils.navigation.definitions', to: '/indexers/definitions' },
		],
	},
	{
		id: 'settings',
		key: 'utils.navigation.settings',
		to: '/settings',
		icon: 'lucide:settings',
		children: [
			{ key: 'utils.navigation.mediaManagement', to: '/settings/media-management' },
			{ key: 'utils.navigation.profiles', to: '/settings/profiles' },
			{ key: 'utils.navigation.quality', to: '/settings/quality' },
			{ key: 'utils.navigation.customFormats', to: '/settings/custom-formats' },
			{ key: 'utils.navigation.indexers', to: '/settings/indexers' },
			{ key: 'utils.navigation.downloadClients', to: '/settings/download-clients' },
			{ key: 'utils.navigation.importLists', to: '/settings/import-lists' },
			{ key: 'utils.navigation.connect', to: '/settings/connect' },
			{ key: 'utils.navigation.metadata', to: '/settings/metadata' },
			{ key: 'utils.navigation.metadataConsumers', to: '/settings/metadata-consumers' },
			{ key: 'utils.navigation.tags', to: '/settings/tags' },
			{ key: 'utils.navigation.general', to: '/settings/general' },
			{ key: 'utils.navigation.ui', to: '/settings/ui' },
		],
	},
	{
		id: 'system',
		key: 'utils.navigation.system',
		to: '/system',
		icon: 'lucide:cpu',
		children: [
			{ key: 'utils.navigation.status', to: '/system/status' },
			{ key: 'utils.navigation.tasks', to: '/system/tasks' },
			{ key: 'utils.navigation.backups', to: '/system/backups' },
			{ key: 'utils.navigation.logs', to: '/system/logs' },
			{ key: 'utils.navigation.updates', to: '/system/updates' },
		],
	},
]

/** Sub-nav items for a top-level nav id; shared by every SubNav call site. */
export function navChildren(id: string): NavChild[] {
	return navigation.find(item => item.id === id)?.children ?? []
}
