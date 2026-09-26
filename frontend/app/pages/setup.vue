<script setup lang="ts">
import { ApiError } from '~/composables/useApi'

definePageMeta({
	layout: 'auth',
	public: true,
})

useHead({ title: 'Set up Submarine' })

const auth = useAuthStore()

const username = ref('')
const password = ref('')
const busy = ref(false)
const fieldErrors = ref<Record<string, string[]>>({})
const formError = ref('')

async function submit() {
	fieldErrors.value = {}
	formError.value = ''

	const errors: Record<string, string[]> = {}
	if (username.value.trim().length === 0) {
		errors.username = ['Choose a username']
	}
	if (password.value.length === 0) {
		errors.password = ['Choose a password']
	}
	fieldErrors.value = errors
	if (Object.keys(errors).length > 0) {
		return
	}

	busy.value = true
	try {
		await auth.setup(username.value.trim(), password.value)
		await navigateTo('/series', { replace: true })
	}
	catch (error) {
		if (error instanceof ApiError) {
			formError.value = error.message
			fieldErrors.value = error.fieldErrors
		}
		else {
			formError.value = 'Could not create the account. Check your connection and try again.'
		}
	}
	finally {
		busy.value = false
	}
}
</script>

<template>
	<div>
		<h1 class="auth-title">
			Set up Submarine
		</h1>
		<form
			class="auth-form"
			novalidate
			@submit.prevent="submit"
		>
			<SField
				label="Username"
				hint="This is the account you use to sign in."
				control-id="setup-username"
				:error="fieldErrors.username?.[0]"
			>
				<SInput
					id="setup-username"
					v-model="username"
					autocomplete="username"
					autofocus
					:invalid="fieldErrors.username !== undefined"
				/>
			</SField>
			<SField
				label="Password"
				control-id="setup-password"
				:error="fieldErrors.password?.[0]"
			>
				<SInput
					id="setup-password"
					v-model="password"
					type="password"
					autocomplete="new-password"
					:invalid="fieldErrors.password !== undefined"
				/>
			</SField>
			<p
				v-if="formError"
				class="auth-error"
				role="alert"
			>
				{{ formError }}
			</p>
			<SButton
				type="submit"
				variant="primary"
				class="auth-submit"
				:loading="busy"
			>
				Create account
			</SButton>
		</form>
	</div>
</template>
