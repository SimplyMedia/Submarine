<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
useHead({ title: t('pages.notFound.title') })
</script>

<template>
	<div>
		<SPageHeader :title="t('pages.notFound.title')" />
		<SEmptyState :message="t('pages.notFound.message')">
			<template #action>
				<SButton
					variant="primary"
					@click="navigateTo('/series', { replace: true })"
				>
					{{ t('pages.notFound.goToSeries') }}
				</SButton>
			</template>
		</SEmptyState>
	</div>
</template>
