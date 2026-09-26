<script setup lang="ts">
useHead({ title: 'Page not found' })
</script>

<template>
	<div>
		<SPageHeader title="Page not found" />
		<SEmptyState message="The address does not match any page. Head back to the library.">
			<template #action>
				<SButton
					variant="primary"
					@click="navigateTo('/series', { replace: true })"
				>
					Go to series
				</SButton>
			</template>
		</SEmptyState>
	</div>
</template>
