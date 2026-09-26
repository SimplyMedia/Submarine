import { describe, expect, it } from 'vitest'
import { ref } from 'vue'
import { useDirtyForm } from '~/composables/useDirtyForm'

describe('useDirtyForm', () => {
	it('starts clean and flags dirty once the draft changes from its snapshot', async () => {
		const draft = ref({ name: 'Any', upgradeAllowed: true })
		const { isDirty } = useDirtyForm(draft)
		expect(isDirty.value).toBe(false)

		draft.value.name = 'Any (changed)'
		await Promise.resolve()
		expect(isDirty.value).toBe(true)
	})

	it('markSaved replaces the draft and resets the snapshot so isDirty goes back to false', async () => {
		const draft = ref({ name: 'Any', upgradeAllowed: true })
		const { isDirty, markSaved } = useDirtyForm(draft)

		draft.value.name = 'Draft edit'
		await Promise.resolve()
		expect(isDirty.value).toBe(true)

		markSaved({ name: 'Server value', upgradeAllowed: false })
		await Promise.resolve()
		expect(isDirty.value).toBe(false)
		expect(draft.value).toEqual({ name: 'Server value', upgradeAllowed: false })
	})

	it('revert restores the last saved snapshot and clears the dirty flag', async () => {
		const draft = ref({ name: 'Any', upgradeAllowed: true })
		const { isDirty, revert } = useDirtyForm(draft)

		draft.value.name = 'Unsaved edit'
		await Promise.resolve()
		expect(isDirty.value).toBe(true)

		revert()
		await Promise.resolve()
		expect(draft.value).toEqual({ name: 'Any', upgradeAllowed: true })
		expect(isDirty.value).toBe(false)
	})

	it('works with a nullable draft populated asynchronously, without a spurious dirty flag on load', async () => {
		const draft = ref<{ name: string } | null>(null)
		const { isDirty, markSaved } = useDirtyForm(draft)
		expect(isDirty.value).toBe(false)

		// Loading real data via markSaved (not a raw assignment) must not appear dirty.
		markSaved({ name: 'Loaded' })
		await Promise.resolve()
		expect(isDirty.value).toBe(false)
	})
})
