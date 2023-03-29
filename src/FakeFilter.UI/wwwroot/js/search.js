$(() => {
	$('#search-input').on('keydown', (event) => {
		if (event.code === "Enter") {
			$('#search-button').focus();
			$('#search-button').trigger('click');
		}
	});

	$('#search-button').on('click', async (event) => {
		event.preventDefault();

		$('#out-request').text('');
		$('#out-isFake').text('');
		$('#out-providers').text('');
		$('#out-error').text('');

		const queryString = $('#search-input').val().trim();
		if (queryString === '')
			return;

		let form = new FormData();
		form.append(FakeFilter.token.name, FakeFilter.token.value);
		form.append('query', queryString);

		const response = await fetch(FakeFilter.searchUrl, {
			method: 'POST',
			body: form
		});

		if (!response.ok) {
			$('#out-error').text(await response.text());
			return;
		}

		const result = await response.json();
		$('#out-request').text(result.request);

		if (!result.isSuccess) {
			$('#out-error').text(result.errorMessage);
			return;
		}

		if (result.isFakeDomain) {
			$('#out-isFake').html('<span class="text-danger">YES <span class="fa-solid fa-fw fa-face-angry"></span></span>');
			$('#out-providers').text(result.details.providers.join(', '));
		}
		else {
			$('#out-isFake').html('<span class="text-success">NO <span class="fa-solid fa-fw fa-face-smile"></span></span>');
		}
	});
});
