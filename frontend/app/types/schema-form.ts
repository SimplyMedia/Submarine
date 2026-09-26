/**
 * Backend field descriptor shape shared by notification, import list and
 * download client settings schemas ({@link https://…NotificationFieldDescriptor},
 * ImportListField, and the download client schema normalized to match).
 */
export interface SchemaField {
	name: string
	label: string
	type: 'text' | 'password' | 'number' | 'select' | 'checkbox' | 'tags' | 'url' | 'info'
	options?: readonly string[] | null
	required?: boolean
	helpText?: string | null
	default?: unknown
}
