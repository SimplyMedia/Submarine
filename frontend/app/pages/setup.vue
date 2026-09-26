<script setup lang="ts">
import { ApiError } from '~/composables/useApi'
import { useI18n } from 'vue-i18n'

definePageMeta({
	layout: 'auth',
	public: true,
})

const { t } = useI18n()

useHead({ title: t('pages.setup.title') })

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
		errors.username = [t('pages.setup.usernameRequired')]
	}
	if (password.value.length === 0) {
		errors.password = [t('pages.setup.passwordRequired')]
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
			formError.value = t('pages.setup.accountCreationFailed')
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
			{{ t('pages.setup.title') }}
		</h1>
		<form
			class="auth-form"
			novalidate
			@submit.prevent="submit"
		>
			<SField
				:label="t('pages.setup.username')"
				:hint="t('pages.setup.usernameHint')"
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
				:label="t('pages.setup.password')"
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
				{{ t('pages.setup.createAccount') }}
			</SButton>
		</form>
	</div>
</template>
