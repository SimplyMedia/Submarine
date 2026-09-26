/**
 * Central navigation registry. The rail, the mobile tab bar and the More
 * sheet all render from this list; placeholder titles resolve through it too.
 */
export interface NavChild {
	label: string
	to: string
}

export interface NavItem {
	id: string
	label: string
	to: string
	icon: string
	children?: NavChild[]
}

export const navigation: NavItem[] = [
	{ id: 'series', label: 'Series', to: '/series', icon: 'lucide:tv' },
	{ id: 'movies', label: 'Movies', to: '/movies', icon: 'lucide:film' },
	{ id: 'calendar', label: 'Calendar', to: '/calendar', icon: 'lucide:calendar' },
	{
		id: 'activity',
		label: 'Activity',
		to: '/activity',
		icon: 'lucide:activity',
		children: [
			{ label: 'Queue', to: '/activity/queue' },
			{ label: 'History', to: '/activity/history' },
			{ label: 'Blocklist', to: '/activity/blocklist' },
			{ label: 'Import', to: '/activity/import' },
		],
	},
	{
		id: 'wanted',
		label: 'Wanted',
		to: '/wanted',
		icon: 'lucide:flag',
		children: [
			{ label: 'Missing', to: '/wanted/missing' },
			{ label: 'Cut off', to: '/wanted/cutoff' },
		],
	},
	{
		id: 'indexers',
		label: 'Indexers',
		to: '/indexers',
		icon: 'lucide:rss',
		children: [
			{ label: 'Indexers', to: '/indexers' },
			{ label: 'Search', to: '/indexers/search' },
			{ label: 'Stats', to: '/indexers/stats' },
			{ label: 'Proxies', to: '/indexers/proxies' },
			{ label: 'Definitions', to: '/indexers/definitions' },
		],
	},
	{
		id: 'settings',
		label: 'Settings',
		to: '/settings',
		icon: 'lucide:settings',
		children: [
			{ label: 'Media management', to: '/settings/media-management' },
			{ label: 'Profiles', to: '/settings/profiles' },
			{ label: 'Quality', to: '/settings/quality' },
			{ label: 'Custom formats', to: '/settings/custom-formats' },
			{ label: 'Indexers', to: '/settings/indexers' },
			{ label: 'Download clients', to: '/settings/download-clients' },
			{ label: 'Import lists', to: '/settings/import-lists' },
			{ label: 'Connect', to: '/settings/connect' },
			{ label: 'Metadata', to: '/settings/metadata' },
			{ label: 'Metadata consumers', to: '/settings/metadata-consumers' },
			{ label: 'Tags', to: '/settings/tags' },
			{ label: 'General', to: '/settings/general' },
			{ label: 'UI', to: '/settings/ui' },
		],
	},
	{
		id: 'system',
		label: 'System',
		to: '/system',
		icon: 'lucide:cpu',
		children: [
			{ label: 'Status', to: '/system/status' },
			{ label: 'Tasks', to: '/system/tasks' },
			{ label: 'Backups', to: '/system/backups' },
			{ label: 'Logs', to: '/system/logs' },
			{ label: 'Updates', to: '/system/updates' },
		],
	},
]

/** Sub-nav items for a top-level nav id; shared by every SubNav call site. */
export function navChildren(id: string): NavChild[] {
	return navigation.find(item => item.id === id)?.children ?? []
}
