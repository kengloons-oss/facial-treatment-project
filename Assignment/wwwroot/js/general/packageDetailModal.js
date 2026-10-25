$(document).on('click', '.detail-btn', function (e) {
	e.preventDefault();

	const packageId = $(this).data('id'); // Get the package Id
	console.log("Package ID:", packageId); // Debugging

	$.ajax({
		type: 'POST',
		url: '/Package/GetPackageDetail/',
		data: {
			packageId: packageId
		},
		success: function (response) {
			if (response) { // Check if a valid package detail is returned
				$('#package-content').empty();

				$('#package-name').empty();
				$('#package-name').append(`<span id='package-name-detail'>${response.code} - ${response.name} - RM ${response.price}</span>`);

				response.packageTreatments.forEach(function (packageTreatmentDetail, i) {
					// Append the treatment to the list

					$('#package-content').append(`
								<li>Treatment ${i + 1} <small>Duration: ${packageTreatmentDetail.duration} hour(s)</small></li>
								<ul id="treatment-${i}-services">
								</ul>
							`);

					// Sort services by priority
					const orderedServices = packageTreatmentDetail.packageTreatmentServices.sort((a, b) => a.priority - b.priority);

					// Add services under the current treatment
					orderedServices.forEach(function (service, j) {
						console.log("Service object:", service);

						$(`#treatment-${i}-services`).append(`
									<li data-index="${j}" class="service-name">
										${service.service.name} <small>Duration: ${service.service.duration * 60} minute(s)</small>
									</li>
									<ul class="service-description">
										<li class="description-item">${service.service.description}</li>
									</ul>
								`);
					});
				});

				// Add the 'active' class to show the modal
				$('#package-modal').addClass('active');
			} else {
				Swal.fire({
					title: "No Package Found",
					text: "No package detail found for this package.",
					icon: "error",
					confirmButtonText: "OK"
				});
			}
		},
		error: function () {
			Swal.fire({
				title: "Error",
				text: "Failed to retrieve package detail. Please try again.",
				icon: "error"
			});
		}
	});
});


// Ensure close modal works
$(document).on('click', '.btn-clear, .btn-close', function (e) {
	e.preventDefault(); // Prevent the default anchor behavior
	$('#package-modal').removeClass('active'); // Remove the 'active' class to hide the modal
});