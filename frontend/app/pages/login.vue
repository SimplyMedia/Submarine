<script setup lang="ts">
import { useI18n } from 'vue-i18n'

definePageMeta({
	layout: 'auth',
	public: true,
})

const { t } = useI18n()

useHead({ title: t('pages.login.title') })

const route = useRoute()
const auth = useAuthStore()

const username = ref('')
const password = ref('')
const rememberMe = ref(false)
const busy = ref(false)
const fieldErrors = ref<Record<string, string[]>>({})
const formError = ref('')

function redirectTarget(): string {
	const target = route.query.redirect
	return typeof target === 'string' && target.startsWith('/') ? target : '/series'
}

async function submit() {
	fieldErrors.value = {}
	formError.value = ''

	const errors: Record<string, string[]> = {}
	if (username.value.trim().length === 0) {
		errors.username = [t('pages.login.usernameRequired')]
	}
	if (password.value.length === 0) {
		errors.password = [t('pages.login.passwordRequired')]
	}
	fieldErrors.value = errors
	if (Object.keys(errors).length > 0) {
		return
	}

	busy.value = true
	try {
		await auth.login(username.value.trim(), password.value, rememberMe.value)
		await navigateTo(redirectTarget(), { replace: true })
	}
	catch (error) {
		if (error instanceof ApiError) {
			formError.value = error.message
			fieldErrors.value = error.fieldErrors
		}
		else {
			formError.value = t('pages.login.signInFailed')
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
			{{ t('pages.login.title') }}
		</h1>
		<form
			class="auth-form"
			novalidate
			@submit.prevent="submit"
		>
			<SField
				:label="t('pages.login.username')"
				control-id="login-username"
				:error="fieldErrors.username?.[0]"
			>
				<SInput
					id="login-username"
					v-model="username"
					autocomplete="username"
					autofocus
					:invalid="fieldErrors.username !== undefined"
				/>
			</SField>
			<SField
				:label="t('pages.login.password')"
				control-id="login-password"
				:error="fieldErrors.password?.[0]"
			>
				<SInput
					id="login-password"
					v-model="password"
					type="password"
					autocomplete="current-password"
					:invalid="fieldErrors.password !== undefined"
				/>
			</SField>
			<SCheckbox
				v-model="rememberMe"
				:label="t('pages.login.rememberMe')"
			/>
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
				{{ t('pages.login.signIn') }}
			</SButton>
		</form>
	</div>
</template>
