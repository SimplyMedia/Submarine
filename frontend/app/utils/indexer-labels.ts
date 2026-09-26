/**
 * Sentence-case labels for indexer enums, plus adapters that turn the two
 * backend settings-field descriptor shapes (implementation schema fields and
 * Cardigann definition fields) into the shared `SchemaField` shape SchemaForm
 * renders. Backend enum member names are serialization contracts.
 */
import { humanizeFieldName } from '~/utils/settings-labels'
import { toOptions } from '~/utils/library-labels'
import type { SBadgeTone } from '~/utils/system-labels'
import type { SchemaField } from '~/types/schema-form'
import type { components } from '~/types/api'

type IndexerImplementation = components['schemas']['IndexerImplementation']
type IndexerProxyType = components['schemas']['IndexerProxyType']
type IndexerHistoryEventType = components['schemas']['IndexerHistoryEventType']
type SettingsFieldSchema = components['schemas']['SettingsFieldSchema']
type IndexerSettingField = components['schemas']['IndexerSettingField']

const IMPLEMENTATION: Record<IndexerImplementation, string> = {
	TORZNAB: 'utils.indexerLabels.implementation.torznab',
	NEWZNAB: 'utils.indexerLabels.implementation.newznab',
	CARDIGANN: 'utils.indexerLabels.implementation.cardigann',
}
const IMPLEMENTATION_ICON: Record<IndexerImplementation, string> = {
	TORZNAB: 'lucide:magnet',
	NEWZNAB: 'lucide:newspaper',
	CARDIGANN: 'lucide:list-checks',
}

export function indexerImplementationLabel(value: IndexerImplementation): string {
	return IMPLEMENTATION[value] ?? value
}

export function indexerImplementationIcon(value: IndexerImplementation): string {
	return IMPLEMENTATION_ICON[value] ?? 'lucide:rss'
}

export const indexerImplementationOptions = toOptions(IMPLEMENTATION)

const PROXY_TYPE: Record<IndexerProxyType, string> = {
	HTTP: 'utils.indexerLabels.proxyType.http',
	SOCKS4: 'utils.indexerLabels.proxyType.socks4',
	SOCKS5: 'utils.indexerLabels.proxyType.socks5',
	FLARESOLVERR: 'utils.indexerLabels.proxyType.flareSolverr',
}

export function indexerProxyTypeLabel(value: IndexerProxyType): string {
	return PROXY_TYPE[value] ?? value
}

export const indexerProxyTypeOptions = toOptions(PROXY_TYPE)

const HISTORY_EVENT: Record<IndexerHistoryEventType, string> = {
	QUERY: 'utils.indexerLabels.historyEvent.query',
	RSS: 'utils.indexerLabels.historyEvent.rss',
	GRAB: 'utils.indexerLabels.historyEvent.grab',
	AUTH: 'utils.indexerLabels.historyEvent.auth',
	FAILED: 'utils.indexerLabels.historyEvent.failed',
}

const HISTORY_EVENT_TONE: Record<IndexerHistoryEventType, SBadgeTone> = {
	QUERY: 'neutral',
	RSS: 'info',
	GRAB: 'ok',
	AUTH: 'neutral',
	FAILED: 'danger',
}

export function indexerHistoryEventTypeLabel(value: IndexerHistoryEventType): string {
	return HISTORY_EVENT[value] ?? value
}

export function indexerHistoryEventTone(value: IndexerHistoryEventType): SBadgeTone {
	return HISTORY_EVENT_TONE[value] ?? 'neutral'
}

/** Turns an implementation settings field (Torznab/Newznab/download-client shaped) into a SchemaForm field. */
export function settingsFieldToSchemaField(field: SettingsFieldSchema): SchemaField {
	let type: SchemaField['type']
	if (field.clrType === 'bool') {
		type = 'checkbox'
	}
	else if (field.clrType === 'enum') {
		type = 'select'
	}
	else if (field.clrType === 'int' || field.clrType === 'double') {
		type = 'number'
	}
	else {
		type = /password|secret|token|apikey|api_key/i.test(field.name) ? 'password' : 'text'
	}
	return {
		name: field.name,
		label: humanizeFieldName(field.name),
		type,
		options: field.enumValues,
		required: field.required,
		default: field.default,
	}
}

const CARDIGANN_TYPE: Record<components['schemas']['IndexerSettingType'], SchemaField['type']> = {
	TEXT: 'text',
	PASSWORD: 'password',
	CHECKBOX: 'checkbox',
	SELECT: 'select',
	INFO: 'info',
	CARDIGANNCAPTCHA: 'info',
}

/**
 * Turns a Cardigann definition field into a SchemaForm field plus its option
 * label lookup (Cardigann select options are a value→label map, unlike the
 * plain value lists other schemas use).
 */
export function cardigannFieldToSchemaField(field: IndexerSettingField): { field: SchemaField, optionLabels?: Record<string, string> } {
	const isCaptcha = field.type === 'CARDIGANNCAPTCHA'
	return {
		field: {
			name: field.name,
			label: field.label ?? humanizeFieldName(field.name),
			type: CARDIGANN_TYPE[field.type] ?? 'text',
			options: field.options ? Object.keys(field.options) : undefined,
			helpText: isCaptcha ? 'utils.indexerLabels.captchaHelp' : (field.type === 'INFO' ? field.label : undefined),
			default: field.default,
		},
		optionLabels: field.options ?? undefined,
	}
}
