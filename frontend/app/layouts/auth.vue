<template>
	<div class="auth">
		<main class="auth-panel">
			<NuxtLink
				to="/"
				class="auth-wordmark"
			>Submarine</NuxtLink>
			<slot />
		</main>
	</div>
</template>

<style scoped>
.auth {
	min-height: 100dvh;
	display: grid;
	place-items: center;
	padding: 24px;
}

.auth-panel {
	width: 100%;
	max-width: 400px;
	display: grid;
	gap: 24px;
	padding: 32px 24px;
	background: var(--surface);
	border: 1px solid var(--line);
	border-radius: var(--r-panel);
	box-shadow: var(--shadow-pop);
}

.auth-wordmark {
	justify-self: center;
	font-size: 1.25rem;
	font-weight: 600;
	letter-spacing: -0.01em;
	color: var(--fg);
	text-decoration: none;
}
</style>
