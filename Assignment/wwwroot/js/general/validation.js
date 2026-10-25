
//====================================================
// General
//====================================================

// For validate text (Eg. name, title, address)
function validateText(text, required = true, field, short = true) {
    // 'field' is for the input field to mention in the message
    // 'short' means is verify for short text (not exceed 50 characters)

    if (text.length === 0) {
        if (required) {
            return field + ' is required';
        }
    }
    else if (short && text.length > 50) {
        return field + ' must not exceed 50 characters'
    }
    else if (!short && text.length > 300) {
        return field + ' must not exceed 300 characters'
    }

    return null;
}

// For validate number with limit range (Eg. membership level. price)
function validateNumber(num, min, max, required = true, field) {

    if (!num || isNaN(num)) {

        if (required) {
            return field + ' is required';
        }
    }
    else if (num < min || num > max) {
        return field + " must between " + min + ' and ' + max + ' only';
    }

    return null;
}

//====================================================
// Person Detail (include Staff & Member)
//====================================================

// Validate Email Format
function validateEmail(email) {
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/; // Basic email regex pattern

    if (email.length === 0) {
        return 'Email address is required';
    }
    else if (!emailPattern.test(email)) {
        return 'Invalid email format (Eg. example@gmail.com)';
    }
    else return null;
}

// Validate Email Exist or Not
function checkEmailExist(email, callback) {
    $.ajax({
        url: '/Login/CheckEmailExist',
        type: 'GET',
        data: {
            email: email // Use the saved context
        },
        success: function (data) {
            if (data.success) {
                callback(false); // Email does not exist
            } else {
                callback(true); // Email exist
            }
        },
        error: function (xhr, status, error) {
            console.log('Error:', error);
            console.log(xhr.responseText);
            callback(false);
        }
    });
}

// Validate Password Format
function validatePasswordFormat(password, required = true) {
    const passwordFormat = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$/;

    if (password.length === 0 && required) {

        return 'Password is required';
    }
    else if (password.length > 0) {
        if (password.length < 6) {

            return 'Please type at least 6 characters';

        }
        else if (!passwordFormat.test(password)) {

            return 'Password must contain at least 1 uppercase letter, 1 lowercase letter, and 1 digit';

        }
    }

    return null;
}

// Validate the current password match or not 
function validateOldPassword(currentPassword, required, callback) {
    const passwordFormatError = validatePasswordFormat(currentPassword, required);

    if (passwordFormatError) {
        callback(passwordFormatError); // Immediately return the error via the callback
        return;
    }

    if (currentPassword.length > 0) {
        $.ajax({
            url: '/User/CheckCurrentPassword',
            type: 'GET',
            data: { password: currentPassword },
            success: function (data) {
                if (data.success) {
                    callback(null); // No error, current password matched
                } else {
                    callback(data.message); // Return error message
                }
            },
            error: function (xhr, status, error) {
                console.error('Error:', error);
                console.error(xhr.responseText);
                callback('An error occurred while validating the password.');
            }
        });
    } else {
        callback(null); // No password provided, but not required
    }
}

function validateNewPassword(password, confirmPassword) {
    if (password != confirmPassword) {
        return 'Confirm password must same with new password';
    }

    return null;
}

// Validate Gender
function validateGender(gender) {
    if (gender == undefined) {
        return 'Gender is required';
    }
    else if (gender != 'F' && gender != 'M') {
        return "Gender must be 'F' or 'M'";
    }

    return null;
}

// Validate Tel_No
function validateTel_No(tel_no, required = false) {
    const telPattern = /^01\d{1}(-?\d{7,8})$/;

    if (required) {
        if (tel_no.length === 0) {
            return 'Tel No. is required';
        }
        else if (!telPattern.test(tel_no)) {
            return 'Invalid Tel No. format (Eg. 01XXXXXXXX or 01X-XXXXXXX)';
        }
    }
    else {
        if (tel_no.length === 0 && !telPattern.test(tel_no)) {
            return 'Invalid Tel No. format (Eg. 01XXXXXXXX or 01X-XXXXXXX)';
        }
    }

    return null;
}

// Validate Salary in range
function validateSalary(salary) {
    if (salary <= 0 || !salary.HasValue) {
        return 'Salary is required';
    }
    else if (salary > 999999.99) {
        return 'Salary must between 0.01 and 999999.99 only';
    }

    return null;
}

// Validate role
function validateRole(role) {
    if (role == undefined) {
        return 'Role is required';
    }
    else if (role != 1 && role != 2) {
        return 'Role only can be manager or staff';
    }

    return null;
}

//====================================================
// Inbox
//====================================================

// Validate target
function validateTarget(target) {
    if (!target || target.value === '') {
        return 'Target is required';
    }
    else if (target < 1 || target > 3) {
        return 'Target must be Staff, Member or Both';
    }

    return null;
}

//====================================================
// Membership
//====================================================
function checkLevelExist(level, callback) {

    $.ajax({
        url: '/Membership/CheckLevelExist',
        type: 'GET',
        data: {
            level: level // Use the saved context
        },
        success: function (data) {
            if (data.success) {
                callback(null); // Membership does not exist
            } else {

                callback(data.message); // Email exist
            }
        },
        error: function (xhr, status, error) {
            console.log('Error:', error);
            console.log(xhr.responseText);
            callback(error)
        }
    });

}

//====================================================
// Appointment
//====================================================

// Validate Appointment Type
function validateType(type) {
    // Only when create others appointment (Not working appointment) will check

    if (!type || type.value === '') {
        return 'Type is required';
    }
    else if (type != 1 && type != 3) {
        return 'Type must be Training or Holiday';
    }

    return null;
}

function checkIfTimeSelected(start_time, end_time) {

    // Check if start time is selected
    if (!start_time) {
        return 'Start DateTime must be selected.';
    }

    // Check if end time is selected
    if (!end_time) {
        $('#End_datetime').next('.error-message').text('End DateTime must be selected.');
    }

    return null
}

//====================================================
// Referral Rewarad
//====================================================
function CheckReferralRewardExist(numOfReferral, callback) {

    $.ajax({
        url: '/ReferralReward/CheckRewardExist',
        type: 'GET',
        data: {
            numOfReferral: numOfReferral // Use the saved context
        },
        success: function (data) {
            if (data.success) {
                callback(null); // Reward with num of referral does not exist
            } else {

                callback(data.message); // Reward with num of referral exist
            }
        },
        error: function (xhr, status, error) {
            console.log('Error:', error);
            console.log(xhr.responseText);
            callback(error)
        }
    });
}

//====================================================
// Top Up Rewarad
//====================================================
function CheckTopUpRewardExist(min_topup, callback) {

    $.ajax({
        url: '/TopUpReward/CheckRewardExist',
        type: 'GET',
        data: {
            min_topup: min_topup // Use the saved context
        },
        success: function (data) {
            if (data.success) {
                callback(null); // Reward with num of referral does not exist
            } else {

                callback(data.message); // Reward with num of referral exist
            }
        },
        error: function (xhr, status, error) {
            console.log('Error:', error);
            console.log(xhr.responseText);
            callback(error)
        }
    });
}

//====================================================
// Helper Function to Show & Clear Error
//====================================================
function showError(input, message) {
    input.siblings('span.error-message')
        .text(message)
        .fadeIn()
        .parent('.form-group')
        .addClass('hasError');
}

// Helper Function to Clear Error
function clearError(input) {
    input.siblings('span.error-message')
        .text('')
        .fadeOut()
        .parent('.form-group')
        .removeClass('hasError');
}

//====================================================
// Loading alert funct
//====================================================
function loadingAlert(title = "Processing...") {
    // Show loading spinner immediately
    Swal.fire({
        title: 'Processing...',
        html: `
            <div style="display: flex; justify-content: center; align-items: center; margin:10px 0px;">
                <svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg" fill="none" stroke-width="10" style="width: 100px; height: 100px; display: flex; justify-content: center; align-items: center;">
                    <circle cx="50" cy="50" r="40" stroke="#3FC3EE" stroke-opacity="0.5" />
                    <circle cx="50" cy="50" r="40" stroke="#3FC3EE" stroke-dasharray="251.2" stroke-dashoffset="0" stroke-linecap="round">
                        <animateTransform attributeName="transform" type="rotate" repeatCount="indefinite" dur="1s" values="0 50 50;360 50 50"/>
                        <animate attributeName="stroke-dashoffset" repeatCount="indefinite" dur="2.5s" values="880;0"/>
                    </circle>
                </svg>
            </div>
        `,
        showConfirmButton: false, // Hide confirm button
        allowOutsideClick: false,
        allowEscapeKey: false,
        allowEnterKey: false
    });
}

//====================================================
// General Validation code
//====================================================
var nameErr = true,
    emailErr = true,
    currentPassErr = false,
    passErr = false,
    confirmPassErr = false,
    telErr = false,
    genderErr = true,
    addressErr = true,
    salaryErr = true,
    levelErr = true,
    minExpenseErr = true,
    discountRateErr = true,
    titleErr = true,
    messageErr = true,
    targetErr = true,
    typeErr = true,
    startDateTimeErr = true,
    endDateTimeErr = true,
    staffListErr = true;


$(document).ready(function () {


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

        if ($(this).hasClass('email-c')) {
            const inputElement = $(this);
            const email = inputElement.val();

            // Validate the email format
            const validationError = validateEmail(email);

            if (validationError) {
                showError(inputElement, validationError);
                emailErr = true;
            }
            else {
                checkEmailExist(email, function (exists) {
                    if (exists) {
                        showError(inputElement, 'Email already exists');
                        emailErr = true;
                    } else {
                        clearError(inputElement);
                        emailErr = false;
                    }
                });
            }
        }

        // In edit detail
        if ($(this).hasClass('email-d')) {
            const inputElement = $(this);
            const email = inputElement.val();

            // Validate the email format
            const validationError = validateEmail(email);

            if (validationError) {
                showError(inputElement, validationError);
                emailErr = true;
            }
            else if (originalEmail != email) {
                checkEmailExist(email, function (exists) {
                    if (exists) {
                        showError(inputElement, 'Email already exists');
                        emailErr = true;
                    } else {
                        clearError(inputElement);
                        emailErr = false;
                    }
                });
            }
            else {
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

        if ($(this).hasClass('pass')) {
            var input = $(this).val()
            var inputElement = $(this);

            const err = validatePasswordFormat(input);

            if (err != null) {
                showError(inputElement, err);
                passErr = true;
            }
            else {
                clearError(inputElement);
                passErr = false;
            }
        }

        // For detail (admin site member and staff)
        if ($(this).hasClass('pass-d')) {
            var input = $(this).val()
            var inputElement = $(this);

            const err = validatePasswordFormat(input, false);

            if (err != null) {
                showError(inputElement, err);
                passErr = true;
            }
            else {
                clearError(inputElement);
                passErr = false;
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

        if ($(this).hasClass('salary')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 0.01, 999999.99, true, "Current Salary");

            if (err != null) {
                showError(inputElement, err);
                salaryErr = true;
            }
            else {
                clearError(inputElement);
                salaryErr = false;
            }
        }


        //==========================================
        // Membership Level (CU)
        //==========================================
        if ($(this).hasClass('level')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 1, 99, true, "Membership Level"); // Use `let` instead of `const`

            if (err != null) {
                showError(inputElement, err);
                levelErr = true;
            }
            else {
                err = checkLevelExist(input, function (err) {
                    if (err != null) {
                        showError(inputElement, err);
                        levelErr = true;
                    }
                    else {
                        clearError(inputElement);
                        levelErr = false;
                    }
                }); // Now reassignment is allowed

            }
        }

        if ($(this).hasClass('minExpense')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 0, 999999.99, true, "Minimum Expense"); // Use `let` instead of `const`

            if (err != null) {
                showError(inputElement, err);
                minExpenseErr = true;
            }
            else {
                clearError(inputElement);
                minExpenseErr = false;
            }
        }

        if ($(this).hasClass('discountRate')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 0, 1, true, "Discount Rate"); // Use `let` instead of `const`

            if (err != null) {
                showError(inputElement, err);
                discountRateErr = true;
            }
            else {
                clearError(inputElement);
                discountRateErr = false;
            }
        }


        //==========================================
        // Create Other Appointment (CU)
        //==========================================

        if ($(this).hasClass('title')) {

            const inputElement = $(this);
            var input = $(this).val();

            var err = validateText(input, true, "Title");

            if (err != null) {
                showError(inputElement, err);
                titleErr = true;
            }
            else {
                clearError(inputElement);
                titleErr = false;
            }
        }

        if ($(this).hasClass('message')) {

            const inputElement = $(this);
            var input = $(this).val();

            var err = validateText(input, true, "Message", false);

            if (err != null) {
                showError(inputElement, err);
                messageErr = true;
            }
            else {
                clearError(inputElement);
                messageErr = false;
            }
        }

        //==========================================
        // Admin side Category (CU)
        //==========================================
        if ($(this).hasClass('description')) {

            const inputElement = $(this);
            var input = $(this).val();

            var err = validateText(input, true, "Description", false);

            if (err != null) {
                showError(inputElement, err);
                messageErr = true;
            }
            else {
                clearError(inputElement);
                messageErr = false;
            }
        }


        //==========================================
        // Top Up Meow Wallet / Top Up reawrd
        //==========================================
        if ($(this).hasClass('topUpAmount')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 5, 999999.99, true, "Top up amount");

            if (err != null) {
                showError(inputElement, err);
                topUpErr = true;
            }
            else {
                clearError(inputElement);
                topUpErr = false;
            }
        }

        if ($(this).hasClass('min_topup')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 5, 999999.99, true, "Minimum top up amount");

            if (err != null) {
                showError(inputElement, err);
                minTopUpErr = true;
            } else {
                CheckTopUpRewardExist(input, function (err) {
                    if (err != null) {
                        showError(inputElement, err);
                        minTopUpErr = true;
                    } else {
                        clearError(inputElement);
                        minTopUpErr = false;
                    }
                });
            }
        }

        if ($(this).hasClass('min_topup-d')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 5, 999999.99, true, "Minimum top up amount");

            if (err != null) {
                showError(inputElement, err);
                minTopUpErr = true;
            } else if (parseFloat(input) !== parseFloat(originalMinTopUp)) {

                CheckTopUpRewardExist(parseFloat(input), function (err) {
                    if (err != null) {
                        showError(inputElement, err);
                        minTopUpErr = true;
                    } else {
                        clearError(inputElement);
                        minTopUpErr = false;
                    }
                });
            } else {
                clearError(inputElement);
                minTopUpErr = false;
            }
        }

        if ($(this).hasClass('free_credit')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 0.01, 999999.99, true, "Free credit");

            if (err != null) {
                showError(inputElement, err);
                creditErr = true;
            }
            else {
                clearError(inputElement);
                creditErr = false;
            }
        }

        //==========================================
        // Referral Reward (CU)
        //==========================================
        if ($(this).hasClass('num_of_reward')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 1, 9999, true, "Number of referral (pax)");

            if (err != null) {
                showError(inputElement, err);
                numReferralErr = true;
            } else {
                CheckReferralRewardExist(input, function (err) {
                    if (err != null) {
                        showError(inputElement, err);
                        numReferralErr = true;
                    } else {
                        clearError(inputElement);
                        numReferralErr = false;
                    }
                });
            }
        }

        if ($(this).hasClass('num_of_reward-d')) {
            var input = $(this).val();
            var inputElement = $(this);

            let err = validateNumber(input, 1, 9999, true, "Number of referral (pax)");

            if (err != null) {
                showError(inputElement, err);
                numReferralErr = true;
            } else if (parseInt(input) != parseInt(originalNum)) {
                CheckReferralRewardExist(input, function (err) {
                    if (err != null) {
                        showError(inputElement, err);
                        numReferralErr = true;
                    } else {
                        clearError(inputElement);
                        numReferralErr = false;
                    }
                });
            }
            else {
                clearError(inputElement);
                numReferralErr = false;
            }
        }
    });

    $('input[name="Target"]').on('change', function () {
        const target = $('input[name="Target"]:checked').val(); // Get the selected value
        const err = validateTarget(target); // Call the helper function for validation

        if (err) {
            showError($('input[name="Target"]').first(), err); // Show error message
            targetErr = true;
        } else {
            clearError($('input[name="Target"]').first()); // Clear error message
            targetErr = false;
        }
    });

    $('input[name="Person.Gender"]').on('change', function () {
        const gender = $('input[name="Person.Gender"]:checked').val(); // Get selected value
        const err = validateGender(gender); // Call helper function for validation

        console.log('Come to trigger person.gender');

        if (err) {
            showError($('input[name="Person.Gender"]').first(), err); // Show error message
            genderErr = true;
        } else {
            clearError($('input[name="Person.Gender"]').first()); // Clear error
            genderErr = false;
        }
    });

    $('input[name="Gender"]').on('change', function () {
        const gender = $('input[name="Gender"]:checked').val(); // Get selected value
        const err = validateGender(gender); // Call helper function for validation

        if (err) {
            showError($('input[name="Gender"]').first(), err); // Show error message
            genderErr = true;
        } else {
            clearError($('input[name="Gender"]').first()); // Clear error
            genderErr = false;
        }
    });

    $('select.type').on('change', function () {
        // Check if the current element has the class 'type'
        if ($(this).hasClass('type')) {
            const selectedValue = $(this).val();
            const inputElement = $(this);

            if (selectedValue === '1' || selectedValue === '3') {
                clearError(inputElement);
                typeErr = false;
            } else {
                showError(inputElement, "Type is required and only can be 'Training' or 'Holiday'.");
                typeErr = true;
            }
        }
    });

    $('#StaffDropdown').on('change', function () {
        const selectedStaff = $(this).val();

        if ($('.AppointmentType').val() != 1) {
            clearError($(this));
            staffListErr = false;
        } else if (!selectedStaff || selectedStaff.length === 0) {
            showError($(this), "Staff is required (One or multiple)");
            staffListErr = true;
        } else if (selectedStaff.length > 5) {
            showError($(this), "Maximum select 5 staff for one training appointment creation.");
            staffListErr = true;
        } else {
            clearError($(this));
            staffListErr = false;
        }
    });

    // Handle check all and uncheck all (header checkbox click)
    $('#toggle-header-check').on('click', function () {
        const headerCheckbox = $(this);
        const checkboxes = $('input[name="ids"]');

        // If the header checkbox is checked, check all checkboxes
        if (headerCheckbox.prop('checked')) {
            checkboxes.prop('checked', true);  // Check all row checkboxes
        } else {
            checkboxes.prop('checked', false);  // Uncheck all row checkboxes
        }
    });

    // Handle individual checkbox change
    $('input[name="ids"]').on('change', function () {
        const checkboxes = $('input[name="ids"]');
        const headerCheckbox = $('#toggle-header-check');

        // Check if all checkboxes are checked
        const allChecked = checkboxes.length === checkboxes.filter(':checked').length;

        // If all checkboxes are checked, check the header checkbox, otherwise uncheck it
        if (allChecked) {
            headerCheckbox.prop('checked', true);
        } else {
            headerCheckbox.prop('checked', false);
        }
    });

});
