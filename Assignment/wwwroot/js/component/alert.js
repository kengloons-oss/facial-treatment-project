$(document).ready(function() {
    // Define SVG icons for different alert types
    const icons = {
        success: 
            `<svg viewBox="0 0 100 100">
                <circle cx="50" cy="50" r="45" stroke="var(--success-color)" stroke-width="5" fill="none" />
                <path d="M30 50 L45 65 L70 35" stroke="var(--success-color)" stroke-width="5" fill="none" />
            </svg>`,
        error: 
            `<svg viewBox="0 0 100 100">
                <circle cx="50" cy="50" r="45" stroke="var(--error-color)" stroke-width="5" fill="none" />
                <line x1="30" y1="30" x2="70" y2="70" stroke="var(--error-color)" stroke-width="5" />
                <line x1="70" y1="30" x2="30" y2="70" stroke="var(--error-color)" stroke-width="5" />
            </svg>`,
        warning: 
            `<svg viewBox="0 0 100 100">
                <circle cx="50" cy="50" r="45" stroke="var(--warning-color)" stroke-width="5" fill="none" />
                <line x1="50" y1="30" x2="50" y2="60" stroke="var(--warning-color)" stroke-width="5" />
                <circle cx="50" cy="70" r="3" fill="var(--warning-color)" />
            </svg>`,
        loading: 
            `<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg" fill="none" stroke="var(--loading-color)" stroke-width="10">
                <circle cx="50" cy="50" r="40" stroke="var(--warning-color)" stroke-opacity="0.5" />
                <circle cx="50" cy="50" r="40" stroke="var(--warning-color)"  stroke-dasharray="251.2" stroke-dashoffset="0" stroke-linecap="round">
                    <animateTransform attributeName="transform" type="rotate" repeatCount="indefinite" dur="1s" values="0 50 50;360 50 50"/>
                    <animate attributeName="stroke-dashoffset" repeatCount="indefinite" dur="2.5s" values="880;0"/>
                </circle>
            </svg>`
        
        
    };

// Function to show the alert and handle the result
function showAlert(title, text, icon, showCancelButton = false, confirmButtonColor) {
    return new Promise((resolve) => {
        if(showCancelButton === 'close'){
            $('#alert').addClass('is-hidden');
            $('#overlay').addClass('is-hidden');
            return;
        }

        $('#alert-title').text(title);
        $('#alert-text').text(text);
        $('#alert-icon').html(icons[icon] || '');
        $('#alert-icon').toggleClass('pulse-animation', ['success', 'error', 'warning'].includes(icon));
        
        // If showCancelButton is 'boom' will hide both button
        if ( showCancelButton === '') {
            $('#buttons').addClass('is-hidden');
            $('#cancel-btn').addClass('is-hidden');
            $('#confirm-btn').addClass('is-hidden');
        } else if(showCancelButton === false) {
            $('#confirm-btn').removeClass('is-hidden');
            $('#cancel-btn').addClass('is-hidden');
        }
        else {
            $('#buttons').toggleClass('is-hidden', !showCancelButton);
            $('#cancel-btn').toggleClass('is-hidden', !showCancelButton);
            $('#confirm-btn').removeClass('is-hidden');
            $('#cancel-btn').removeClass('is-hidden');
        }
        
        $('#alert').removeClass('is-hidden');
        $('#overlay').removeClass('is-hidden');

        // Reset button color
        $('#confirm-btn').removeClass('btnred');

        // Apply color if specified
        if (confirmButtonColor === 'red') {
            $('#confirm-btn').addClass('btnred');
        }

        // Event handler for confirmation button
        $('#confirm-btn').off('click').on('click', function() {
            $('#alert').addClass('is-hidden');
            $('#overlay').addClass('is-hidden');
            resolve(true); // User confirmed
        });

        // Event handler for cancel button
        $('#cancel-btn').off('click').on('click', function() {
            $('#alert').addClass('is-hidden');
            $('#overlay').addClass('is-hidden');
            resolve(false); // User canceled
        });

        // Optionally, handle clicking outside the alert box
        // $('#overlay').off('click').on('click', function() {
        //     $('#alert').addClass('is-hidden');
        //     $('#overlay').addClass('is-hidden');
        //     resolve(false); // User canceled by clicking outside
        // });
    });
}

// Expose to global scope
window.showAlert = showAlert;
  
});
