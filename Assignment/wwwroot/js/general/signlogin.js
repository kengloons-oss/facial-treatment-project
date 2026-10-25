$(document).ready(function () {

    'use strict';

    // Variables to track form errors
    var usernameError = true,
        emailError = true,
        passwordError = true,
        passConfirm = true,
        telError = false,
        genderError = true;

    function validatePasswordFormat(password) {
        const passwordFormat = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$/;

        if (password.length === 0) {

            return 'Password is required';
        }
        else if (password.length < 6) {

            return 'Please type at least 6 characters';

        }
        else if (!passwordFormat.test(password)) {

            return 'MUST CONTAIN a-z AND A-Z AND 1-9';

        }
        else return null;
    }

    // Gender
    function validateGender() {
        var input = $('input[name="Gender"]:checked').val();

        if (input == undefined) {
            console.log(input);
            $('.form-group').has('input[name="Gender"]').find('.error').text('Please select your gender').fadeIn().parent('.form-group').addClass('hasError');
            genderError = true;
        } else {
            $('.form-group').has('input[name="Gender"]').find('.error').text('').fadeOut().parent('.form-group').removeClass('hasError');
            genderError = false;
        }
    }

    // Detect browser for CSS purposes
    if (navigator.userAgent.toLowerCase().indexOf('firefox') > -1) {
        $('.form form label').addClass('fontSwitch');
    }

    // Label effect
    $('input').focus(function () {
        $(this).siblings('label').addClass('active');
    });

    // Form validation
    $('input').on('blur input', function () {

        // User Name
        if ($(this).hasClass('name')) {
            if ($(this).val().length === 0) {
                $(this).siblings('span.error').text('Please type your full name').fadeIn().parent('.form-group').addClass('hasError');
                usernameError = true;
            } else if ($(this).val().length > 1 && $(this).val().length <= 6) {
                $(this).siblings('span.error').text('Please type at least 6 characters').fadeIn().parent('.form-group').addClass('hasError');
                usernameError = true;
            } else {
                $(this).siblings('.error').text('').fadeOut().parent('.form-group').removeClass('hasError');
                usernameError = false;
            }
        }

        // Email
        if ($(this).hasClass('email')) {
            const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/; // Basic email regex pattern
            const email = $(this).val();

            if ($(this).val().length == '') {
                $(this).siblings('span.error').text('Please type your email address').fadeIn().parent('.form-group').addClass('hasError');
                emailError = true;
            }
            else if (!emailPattern.test($(this).val())) {
                $(this).siblings('span.error').text('Invalid email format. (Eg. example@email.com)').fadeIn().parent('.form-group').addClass('hasError');
                emailError = true;
            }
            else {
                if ($(this).is('#regEmail')) {
                    var input = $(this); // Save the context of 'this'

                    $.ajax({
                        url: '/Login/CheckEmailExist',
                        type: 'GET',
                        data: {
                            email: email // Use the saved context
                        },
                        success: function (data) {
                            if (data.success) {
                                console.log('email does not exist');

                                input.siblings('.error').text('').fadeOut().parent('.form-group').removeClass('hasError');
                                emailError = false;
                            } else {

                                input.siblings('span.error').text(data.message).fadeIn().parent('.form-group').addClass('hasError');
                                emailError = true;
                            }
                        },
                        error: function (xhr, status, error) {
                            console.log('Error:', error);
                            console.log(xhr.responseText);
                        }
                    });
                }
                else {
                    $(this).siblings('span.error').text('').fadeIn().parent('.form-group').addClass('hasError');
                    emailError = false;
                }

            }
        }

        // Tel_no
        if ($(this).hasClass('tel_no')) {
            const telPattern = /^01\d{1}(-?\d{7,8})$/;

            if ($(this).val().length != '' && !telPattern.test($(this).val())) {
                $(this).siblings('span.error').text('Invalid tel_no format. (Eg. 01X-XXXXXXX or 01XXXXXXXX)').fadeIn().parent('.form-group').addClass('hasError');
                telError = true;
            }
            else {
                $(this).siblings('.error').text('').fadeOut().parent('.form-group').removeClass('hasError');
                emailError = false;
            }
        }

        // Password
        if ($(this).hasClass('pass')) {

            const password = $(this).val();
            const errMsg = validatePasswordFormat(password);

            if (errMsg != null) {
                $(this).siblings('span.error').text(errMsg).fadeIn().parent('.form-group').addClass('hasError');
                passwordError = true;
            }
            else {
                if (password !== $('.passConfirm').val() && $('.passConfirm').val().length != '') {
                    $('.passConfirm').siblings('.error').text('Passwords don\'t match').fadeIn().parent('.form-group').addClass('hasError');
                    passConfirm = true;
                }

                $(this).siblings('span.error').text('').fadeOut().parent('.form-group').removeClass('hasError');
                passwordError = false;
                $('.passConfirm').trigger('blur');
            }
        }

        if ($(this).hasClass('passConfirm')) {
            // Password confirmation
            const password = $(this).val();
            const passwordFormat = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$/;

            const errMsg = validatePasswordFormat(password);


            if (errMsg != null) {
                $(this).siblings('span.error').text(errMsg).fadeIn().parent('.form-group').addClass('hasError');
                passConfirm = true;
            }
            else {

                if ($('#regPass').val() !== $('.passConfirm').val()) {
                    $('.passConfirm').siblings('.error').text('Passwords don\'t match').fadeIn().parent('.form-group').addClass('hasError');
                    passConfirm = true;
                }
                else {
                    $(this).siblings('span.error').text('').fadeOut().parent('.form-group').removeClass('hasError');
                    passConfirm = false;
                }
            }
        }

        // Label effect
        if ($(this).val().length > 0) {
            $(this).siblings('label').addClass('active');
        } else {
            $(this).siblings('label').removeClass('active');
        }
    });

    // Listen for changes on the gender radio buttons
    $('input[name="Gender"]').on('change', function () {
        validateGender();
    });


    // Form switch
    $('a.switch').click(function (e) {
        $(this).toggleClass('active');
        e.preventDefault();

        if ($('a.switch').hasClass('active')) {
            $(this).parents('.form-peice').addClass('switched').siblings('.form-peice').removeClass('switched');
        } else {
            $(this).parents('.form-peice').removeClass('switched').siblings('.form-peice').addClass('switched');
        }
    });

    // Register Form submit 
    $('form.signup-form').submit(function (event) {
        event.preventDefault();

        // Trigger blur event to check and display
        $('.name, .email, .pass, .passConfirm, .tel_no').trigger('blur');
        validateGender();

        setTimeout(() => {
        // Check for form errors before submission
            if (!usernameError && !emailError && !passwordError && !passConfirm && !telError && !genderError) {

                // Show loading spinner
                loadingAlert();

                HTMLFormElement.prototype.submit.call(this);

                // Hide forms and show success message
                $('.signup, .login').addClass('switched');
                setTimeout(function () { $('.signup, .login').hide(); }, 700);
                setTimeout(function () { $('.brand').addClass('active'); }, 300);
                setTimeout(function () { $('.heading').addClass('active'); }, 600);
                setTimeout(function () { $('.success-msg p').addClass('active'); }, 900);
                setTimeout(function () { $('.success-msg a').addClass('active'); }, 1050);
                setTimeout(function () { $('.form').hide(); }, 700);

            }
            else {
                Swal.close();
            }
        }, 100);
    });

    // Reset Password Form submit 
    $('form.reset-form').submit(function (event) {
        event.preventDefault(); // Prevent default submission

        // Trigger blur event to check and display
        $('.pass, .passConfirm').trigger('blur');

        // Check for form errors before submission
        if (!passwordError && !passConfirm) {
            HTMLFormElement.prototype.submit.call(this);
        }
    });

    // Reset Password Form submit 
    $('form.sendEmail-form').submit(function (event) {
        event.preventDefault(); // Prevent default submission

        // Trigger validation (if applicable)
        $('.email').trigger('blur');

        // Check for form errors
        if (!emailError) {

            // Show loading spinner
            loadingAlert();

            // Submit the form programmatically
            HTMLFormElement.prototype.submit.call(event.target);
        }
        else {
            // Close the swal fire
            Swal.close();
        }

    });


    // Reload page
    $('a.profile').on('click', function () {
        location.reload(true);
    });

});

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
