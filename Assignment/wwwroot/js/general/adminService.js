$(document).on('click', '.activate-btn', function (e) {
    e.preventDefault();

    const serviceId = $(this).data('id'); // Get the category ID

    $.ajax({
        type: 'POST',
        url: baseURL + '/Service/GetRelatedPackages/',
        data: {
            serviceId: serviceId
        },
        success: function (response) {
            if (response.length > 0) { // If services are found
                $('#packageList').empty(); // Clear existing services

                // Add each service to the modal's service list using Spectre CSS
                response.forEach(function (package) {
                    $('#packageList').append(`
                        <div class="form-group">
                            <label class="form-checkbox">
                                <input type="checkbox" value="${package.id}" id="package-${package.id}">
                                <i class="form-icon"></i> <div class="text-break"> ${package.name}</div>
                            </label>
                        </div>
                    `);
                });

                // Store the category ID in the confirm button's data attribute
                $('#confirmPackageSelection').data('service-id', serviceId);

                // Add the 'active' class to show the modal
                $('#modal-id').addClass('active');
            } else {
                // No services found, prompt user
                Swal.fire({
                    title: "No Package Found",
                    text: 'No inactive package(s) found that contain this service. Do you want to activate the service anyway?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: "Yes, Activate"
                }).then((result) => {
                    if (result.isConfirmed) {
                        activateService(serviceId); // Activate without services
                    }
                });
            }
        },
        error: function () {
            Swal.fire({
                title: "Error",
                text: "Failed to retrieve packages. Please try again.",
                icon: "error"
            });
        }
    });
});



// Ensure close modal works
$(document).on('click', '.btn-clear', function (e) {
    e.preventDefault(); // Prevent the default anchor behavior
    $('#modal-id').removeClass('active'); // Remove the 'active' class to hide the modal
});

// Function to update the confirm button with the count of selected services
function updateConfirmButton() {
    const checkedCount = $('#packageList input[type="checkbox"]:checked').length;
    const buttonText = checkedCount > 0
        ? `Activate ${checkedCount} Package(s) `
        : 'Activate Service Only';
    $('#confirmPackageSelection').text(buttonText);
}

// Select/Deselect all services and update the confirm button
$(document).on('change', '#selectAllPackages', function () {
    const isChecked = $(this).is(':checked');
    $('#packageList input[type="checkbox"]').prop('checked', isChecked);
    updateConfirmButton();
});

// Update "Select All" checkbox and the confirm button based on individual checkbox states
$(document).on('change', '#packageList input[type="checkbox"]', function () {
    const totalCheckboxes = $('#packageList input[type="checkbox"]').length;
    const checkedCheckboxes = $('#packageList input[type="checkbox"]:checked').length;

    // Update "Select All" checkbox
    $('#selectAllPackages').prop('checked', totalCheckboxes === checkedCheckboxes);

    // Update confirm button
    updateConfirmButton();
});

// Confirm service selection and proceed with activation
$(document).on('click', '#confirmPackageSelection', function () {
    const selectedPackages = getSelectedPackages();
    const serviceId = $(this).data('service-id'); // Retrieve the category ID

    Swal.fire({
        title: "Activate Confirmation",
        html: `Confirm to activate the service together with ${selectedPackages.length} package(s)?`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: "Yes, Activate"
    }).then((result) => {
        if (result.isConfirmed) {
            if (selectedPackages.length > 0) {
                activateService(serviceId, selectedPackages); // Activate with selected services
            } else {
                activateService(serviceId); // Activate without services
            }

            $('#modal-id').removeClass('active');
        }
    });
});

// Function to activate category with or without services
function activateService(serviceId, selectedPackages = []) {
    const form = $('<form>')
        .attr('method', 'post')
        .attr('action', `/Service/Activate/${serviceId}`);

    // Add service IDs to form
    selectedPackages.forEach(packageId => {
        $('<input>').attr({
            type: 'hidden',
            name: 'packageIds[]',
            value: packageId
        }).appendTo(form);
    });

    form.appendTo(document.body).submit(); // Submit form
}

// Function to get selected services
function getSelectedPackages() {
    const selectedPackages = [];
    $('#packageList input[type="checkbox"]:checked').each(function () {
        selectedPackages.push($(this).val());
    });
    return selectedPackages;
}


//Deactivate
$(document).on('click', '.deactivate-btn', function (e) {
    e.preventDefault();

    const id = $(this).data('id');

    Swal.fire({
        title: "Deactivate Service",
        text: 'Do you confirm to deactivate this service? This will deactivate all related packages.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: "Yes"
    }).then((result) => {
        if (result.isConfirmed) {
            const url = '/Service/Deactivate/' + id;
            $('<form>').appendTo(document.body).attr({ method: 'post', action: url }).submit();
        }
    });
});