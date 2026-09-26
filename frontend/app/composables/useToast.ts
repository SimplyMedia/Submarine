import type { Ref } from 'vue'

export interface ToastMessage {
	id: number
	title: string
	description?: string
	tone: 'ok' | 'danger' | 'info'
}

let toastState: Ref<ToastMessage[]> | null = null
let nextToastId = 1

/**
 * App-wide toast queue rendered by the SToast host in app.vue.
 *
 *   const { toast } = useToast()
 *   toast({ title: 'Series added', tone: 'ok' })
 */
export function useToast() {
	toastState ??= useState<ToastMessage[]>('toasts', () => [])

	function dismiss(id: number) {
		toastState!.value = toastState!.value.filter(t => t.id !== id)
	}

	function toast(message: Omit<ToastMessage, 'id'>) {
		const id = nextToastId++
		toastState!.value = [...toastState!.value, { ...message, id }]
	}

	return { toasts: toastState, toast, dismiss }
}
