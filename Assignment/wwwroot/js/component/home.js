//Home Navigation Bar:
$(document).ready(function () {
    var $header = $('header');
    var $sectionNav = $('#sectionnav');
    var $navLinks = $('#sectionnav a');
    var $activeLine = $('.active-line');
    var scrolling = false;

    // Handle scroll event
    $(window).on('scroll', function () {
        var headerHeight = $header.outerHeight();
        var scrollTop = $(window).scrollTop();

        if (scrollTop > headerHeight) {
            $sectionNav.css({
                'position': 'fixed',
                'top': '0'
            });
        } else {
            $sectionNav.css({
                'position': 'absolute',
                'top': '-20%' // Adjust according to your original style
            });
        }

        // Handle active link and line update during scrolling
        if (!scrolling) {
            scrolling = true;
            requestAnimationFrame(function () {
                var activeNavLinkId = null;

                $navLinks.each(function () {
                    var $navLink = $(this);
                    var targetId = $navLink.attr('href').substring(1);
                    var $targetSection = $('#' + targetId);
                    var sectionTop = $targetSection.offset().top;
                    var sectionBottom = sectionTop + $targetSection.outerHeight();

                    if (scrollTop >= sectionTop - 70 && scrollTop < sectionBottom - 70) {
                        activeNavLinkId = targetId;
                    }
                });

                if (activeNavLinkId) {
                    var $activeNavLink = $('nav a[href="#' + activeNavLinkId + '"]');
                    if ($activeNavLink.length) {
                        updateActiveLink($activeNavLink);
                        updateActiveLine($activeNavLink);
                    }
                }

                scrolling = false;
            });
        }
    });

    // Set the first navigation item and line to be active when the page loads
    var $firstNavLink = $navLinks.first();
    $firstNavLink.addClass('active');
    updateActiveLine($firstNavLink);

    // Event handler when a navigation link is clicked
    $navLinks.on('click', function (event) {
        event.preventDefault();
        var $navLink = $(this);
        var targetId = $navLink.attr('href').substring(1);
        var offset = $('#' + targetId).offset().top - 20;

        $('html, body').animate({
            scrollTop: offset
        }, 400);

        // Update active link and line on click
        updateActiveLink($navLink);
        updateActiveLine($navLink);
    });

    // Update the style of active navigation items
    function updateActiveLink($activeNavLink) {
        $navLinks.removeClass('active');
        $activeNavLink.addClass('active');
    }

    // Update the position and width of the line
    function updateActiveLine($activeNavLink) {
        if ($activeNavLink.offset() !== undefined && $sectionNav.offset() !== undefined) {
            var linkLeft = $activeNavLink.offset().left - $sectionNav.offset().left; // Use offset to ensure correct positioning
            var linkWidth = $activeNavLink.outerWidth();
            $activeLine.css({
                'width': linkWidth + 'px',
                'transform': 'translateX(' + linkLeft + 'px)'
            });
        }
    }
});

//----------------------------------------------------------------------

//Show product:
$(document).ready(function () {
    // Click event delegate
    $('#carousel').on('click', 'div', function () {
        moveToSelected($(this));
    });

    // Forward button click event
    $('#prev').click(function () {
        moveToSelected('prev');
    });

    // Back button click event
    $('#next').click(function () {
        moveToSelected('next');
    });

    //Keyboard events
    $(document).keydown(function (e) {
        switch (e.which) {
            case 12: // left arrow
                moveToSelected('prev');
                break;
            case 18: //right arrow
                moveToSelected('next');
                break;
            default:
                return;
        }
        e.preventDefault();
    });
});


function moveToSelected(element) {

    if (element == "next") {
        var selected = $(".selected").next();
    } else if (element == "prev") {
        var selected = $(".selected").prev();
    } else {
        var selected = element;
    }

    var next = $(selected).next();
    var prev = $(selected).prev();
    var prevSecond = $(prev).prev();
    var nextSecond = $(next).next();

    $(selected).removeClass().addClass("selected");

    $(prev).removeClass().addClass("prev");
    $(next).removeClass().addClass("next");

    $(nextSecond).removeClass().addClass("nextRightSecond");
    $(prevSecond).removeClass().addClass("prevLeftSecond");

    $(nextSecond).nextAll().removeClass().addClass('hideRight');
    $(prevSecond).prevAll().removeClass().addClass('hideLeft');

}

$(document).keydown(function (e) {
    switch (e.which) {
        case 12: // left
            moveToSelected('prev');
            break;

        case 18: // right
            moveToSelected('next');
            break;

        default: return;
    }
    e.preventDefault();
});

$('#carousel div').click(function () {
    moveToSelected($(this));
});

$('#prev').click(function () {
    moveToSelected('prev');
});

$('#next').click(function () {
    moveToSelected('next');
});


