import { type Ref, ref, watch } from 'vue'

/**
 * Tracks a draft object against its last saved snapshot for the sticky
 * "Save changes" bar pattern used by every settings page.
 *
 *   const draft = ref(cloneConfig())
 *   const { isDirty, markSaved, revert } = useDirtyForm(draft)
 *   // after a successful PUT:
 *   markSaved(response.data)
 */
export function useDirtyForm<T>(draft: Ref<T>) {
	const snapshot = ref(JSON.stringify(draft.value)) as Ref<string>
	const isDirty = ref(false)

	watch(draft, (value) => {
		isDirty.value = JSON.stringify(value) !== snapshot.value
	}, { deep: true })

	function markSaved(value: T = draft.value) {
		draft.value = value
		snapshot.value = JSON.stringify(value)
		isDirty.value = false
	}
	function revert() {
		draft.value = JSON.parse(snapshot.value)
		isDirty.value = false
	}

	/**
	 * Merge a server-driven partial (e.g. a regenerated API key) into both the
	 * draft and the saved snapshot, without marking unrelated unsaved edits as
	 * saved.
	 */
	function patchSnapshot(partial: Partial<T>) {
		const saved = { ...JSON.parse(snapshot.value), ...partial }
		snapshot.value = JSON.stringify(saved)
		draft.value = { ...draft.value, ...partial }
		isDirty.value = JSON.stringify(draft.value) !== snapshot.value
	}

	return { isDirty, markSaved, revert, patchSnapshot }
}
