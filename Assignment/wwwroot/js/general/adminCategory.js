$(document).on('click', '.activate-btn', function (e) {
    e.preventDefault();

    const categoryId = $(this).data('id'); // Get the category ID

    $.ajax({
        type: 'POST',
        url: '/Category/GetRelatedServices/',
        data: {
            categoryId: categoryId
        },
        success: function (response) {
            if (response.length > 0) { // If services are found
                $('#serviceList').empty(); // Clear existing services

                // Add each service to the modal's service list using Spectre CSS
                response.forEach(function (service) {
                    $('#serviceList').append(`
                        <div class="form-group">
                            <label class="form-checkbox">
                                <input type="checkbox" value="${service.id}" id="service-${service.id}">
                                <i class="form-icon"></i> <div class="text-break"> ${service.name}</div>
                            </label>
                        </div>
                    `);
                });

                // Store the category ID in the confirm button's data attribute
                $('#confirmServiceSelection').data('category-id', categoryId);

                // Add the 'active' class to show the modal
                $('#modal-id').addClass('active');
            } else {
                // No services found, prompt user
                Swal.fire({
                    title: "No Services Found",
                    text: 'No inactive services found for this category. Do you want to activate the category anyway?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: "Yes, Activate"
                }).then((result) => {
                    if (result.isConfirmed) {
                        activateCategory(categoryId); // Activate without services
                    }
                });
            }
        },
        error: function () {
            Swal.fire({
                title: "Error",
                text: "Failed to retrieve services. Please try again.",
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
    const checkedCount = $('#serviceList input[type="checkbox"]:checked').length;
    const buttonText = checkedCount > 0
        ? `Activate ${checkedCount} Service(s) `
        : 'Activate Category Only';
    $('#confirmServiceSelection').text(buttonText);
}

// Select/Deselect all services and update the confirm button
$(document).on('change', '#selectAllServices', function () {
    const isChecked = $(this).is(':checked');
    $('#serviceList input[type="checkbox"]').prop('checked', isChecked);
    updateConfirmButton();
});

// Update "Select All" checkbox and the confirm button based on individual checkbox states
$(document).on('change', '#serviceList input[type="checkbox"]', function () {
    const totalCheckboxes = $('#serviceList input[type="checkbox"]').length;
    const checkedCheckboxes = $('#serviceList input[type="checkbox"]:checked').length;

    // Update "Select All" checkbox
    $('#selectAllServices').prop('checked', totalCheckboxes === checkedCheckboxes);

    // Update confirm button
    updateConfirmButton();
});

// Confirm service selection and proceed with activation
$(document).on('click', '#confirmServiceSelection', function () {
    const selectedServices = getSelectedServices();
    const categoryId = $(this).data('category-id'); // Retrieve the category ID

    console.log("Category ID:", categoryId);
    console.log("Selected Services:", selectedServices);

    Swal.fire({
        title: "Activate Confirmation",
        html: `Confirm to activate the category together with ${selectedServices.length} service(s)? <br><br><small>*Package(s) contains the services will not be activate, you can activate it on <strong>Package List.</strong></small>`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: "Yes, Activate"
    }).then((result) => {
        if (result.isConfirmed) {
            if (selectedServices.length > 0) {
                activateCategory(categoryId, selectedServices); // Activate with selected services
            } else {
                activateCategory(categoryId); // Activate without services
            }

            $('#modal-id').removeClass('active');
        }
    });
});

// Function to activate category with or without services
function activateCategory(categoryId, selectedServices = []) {
    const form = $('<form>')
        .attr('method', 'post')
        .attr('action', `/Category/Activate/${categoryId}`);

    // Add service IDs to form
    selectedServices.forEach(serviceId => {
        $('<input>').attr({
            type: 'hidden',
            name: 'serviceIds[]',
            value: serviceId
        }).appendTo(form);
    });

    form.appendTo(document.body).submit(); // Submit form
}

// Function to get selected services
function getSelectedServices() {
    const selectedServices = [];
    $('#serviceList input[type="checkbox"]:checked').each(function () {
        selectedServices.push($(this).val());
    });
    return selectedServices;
}




// Deactivate
$(document).on('click', '.deactivate-btn', function (e) {
    e.preventDefault();

    const id = $(this).data('id');

    Swal.fire({
        title: "Deactivate Category",
        html: 'Do you confirm to deactivate this category? <br><br><small>* All the <strong>services</strong> under this category will also be deactivate. <br>*All the <strong>packages</strong> contains the service will also be deactivated.</small>',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: "Yes"
    }).then((result) => {
        if (result.isConfirmed) {
            const url = '/Category/ToggleStatus/' + id;
            $('<form>').appendTo(document.body).attr({ method: 'post', action: url }).submit();
        }
    });
});