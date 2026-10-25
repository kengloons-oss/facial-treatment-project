$(document).ready(function () {

    // Variables to track form errors
    var nameErr = true,
        emailErr = true,
        currentPassErr = false,
        passErr = false,
        confirmPassErr = false,
        telErr = false,
        genderErr = true,
        addressErr = true;

    // Form validation
    $('input, textarea').on('blur input', function () {

        if ($(this).hasClass('name')) {

            const inputElement = $(this);
            var input = $(this).val();
            
            var err = validateText(input, true, "Name");

            if (err != null) {
                showError(inputElement, err);
                nameErr = true;
            }
            else {
                clearError(inputElement);
                nameErr = false;
            }
        }

        // Email
        if ($(this).hasClass('email')) {
            const inputElement = $(this);
            const email = inputElement.val();

            // Hide the verify button initially
            $('#verify-email-btn').hide();

            // Validate the email format
            const validationError = validateEmail(email);

            if (validationError) {
                showError(inputElement, validationError);
                emailErr = true;
                return;
            }

            // Check if the email is different from the original
            if (email && email !== originalEmail) {
                checkEmailExist(email, function (exists) {
                    if (exists) {
                        showError(inputElement, 'Email already exists');
                        emailErr = true;
                    } else {
                        clearError(inputElement);
                        emailErr = false;

                        // Show the verify button after successful validation
                        $('#verify-email-btn').show();
                    }
                });
            } else {
                // Clear any error for unchanged emails
                clearError(inputElement);
                emailErr = false;
            }
        }

        if ($(this).hasClass('tel_no')) {
            var input = $(this).val()
            var inputElement = $(this);

            const err = validateTel_No(input, true);

            if (err != null) {
                showError(inputElement, err);
                telErr = true;
            }
            else {
                clearError(inputElement);
                telErr = false;
            }
        }

        if ($(this).hasClass('address')) {
            var input = $(this).val()
            var inputElement = $(this);

            const err = validateText(input, true, "Address", false);

            if (err) {
                showError(inputElement, err);
                addressErr = true;
            }
            else {
                clearError(inputElement);
                addressErr = false;
            }
        }

        if ($(this).hasClass('currentPass')) {
            const input = $(this).val();
            const inputElement = $(this);

            validateOldPassword(input, false, function (err) {
                if (err) {
                    showError(inputElement, err);
                    currentPassErr = true;

                    // Disable the New Password field
                    $('.newPass').prop('disabled', true);
                } else {
                    clearError(inputElement);
                    currentPassErr = false;

                    if (input.length > 0) {
                        // Enable the New Password field
                        $('.newPass').prop('disabled', false);
                    } else {
                        // Disable and hide the fields when current password is empty
                        $('.newPass, .confirmPass').val('');
                        $('.newPass, .confirmPass').prop('disabled', true);
                        
                        clearError($('.newPass'));
                        clearError($('.confirmPass'));
                    }
                }
            });
        }

        if ($(this).hasClass('newPass')) {
            var input = $(this).val()
            var inputElement = $(this);

            const err = validatePasswordFormat(input);

            if (err != null) {
                showError(inputElement, err);
                passErr = true;
            }
            else if (input.length > 0) {
                clearError(inputElement);
                passErr = false;

                $('.confirmPass').prop('disabled', false);
                $('.confirmPass').trigger('blur');
            }
        }

        if ($(this).hasClass('confirmPass')) {
            var input = $(this).val();
            var newPassword = $('.newPass').val();

            var inputElement = $(this);

            let err = validatePasswordFormat(input); // Use `let` instead of `const`

            if (err != null) {
                showError(inputElement, err);
                confirmPassErr = true;
            } else if (input.length > 0) {
                err = validateNewPassword(newPassword, input); // Now reassignment is allowed

                if (err != null) {
                    showError(inputElement, err);
                    confirmPassErr = true;
                    return;
                }

                clearError(inputElement);
                confirmPassErr = false;
            }
        }

    });

    $('input[name="Person.Gender"]').on('change', function () {
        const gender = $('input[name="Person.Gender"]:checked').val(); // Get selected value
        const err = validateGender(gender); // Call helper function for validation

        if (err) {
            showError($('input[name="Person.Gender"]').first(), err); // Show error message
            genderErr = true;
        } else {
            clearError($('input[name="Person.Gender"]').first()); // Clear error
            genderErr = false;
        }
    });

    // Form submit
    $('form').submit(function (event) {
        event.preventDefault();

        // Trigger blur event to check and display
        $('.name, .email, .currentPass, .tel_no, .address').trigger('blur');
        $('input[name="Person.Gender"]').trigger('change');

        // Add a short delay to make sure the blur event is processed
        setTimeout(() => {
            // Check for form errors before submission
            if (!nameErr && !emailErr && !currentPassErr && !passErr && !confirmPassErr && !telErr && !genderErr && !addressErr) {

                // Submit the form when all field is valid
                HTMLFormElement.prototype.submit.call(this);
            }
            //else {
            //    console.log("NameErr: " + nameErr);
            //    console.log("EmailErr: " + emailErr);
            //    console.log("currentPassErr: " + currentPassErr);
            //    console.log("passErr: " + passErr);
            //    console.log("confirmPassErr: " + confirmPassErr);
            //    console.log("telErr: " + telErr);
            //    console.log("genderErr: " + genderErr);
            //    console.log("addressErr: " + addressErr);
            //}
        }, 100); // 100ms delay to ensure blur event has time to process
    });
});
