$(document).ready(function () {
    $(".showcase-ratings").each(function () {
        const sum = $(this).data("sum");
        const average = parseFloat($(this).data("average"));
        const fullStars = Math.floor(average);
        const partialFill = Math.round((average % 1) * 100); // fix partial fill problem
        const emptyStars = 5 - fullStars - (partialFill > 0 ? 1 : 0);

        let starsHtml = "";

        // add full star
        for (let i = 0; i < fullStars; i++) {
            starsHtml += `<svg class="sc-star" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                        <path d="M12 2.5c.5 0 1 .3 1.2.8l2.6 5.2 5.7.8c.5.1.9.4 1.1.9s.1 1-.3 1.4l-4.2 4.1 1 5.6c.1.5-.1 1-.5 1.3s-.9.3-1.4 0L12 19.8l-5 2.6c-.5.3-1 .3-1.4 0s-.6-.8-.5-1.3l1-5.6-4.2-4.1c-.4-.4-.5-.9-.3-1.4s.6-.8 1.1-.9l5.7-.8 2.6-5.2c.2-.5.7-.8 1.2-.8z" fill="#f39c12"/>
                      </svg>`;
        }

        // add partial fill star
        if (partialFill > 0) {
            const gradientId = `partial-fill-${Math.random()
                .toString(36)
                .substr(2, 9)}`;
            starsHtml += `<svg class="sc-star" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                        <defs>
                          <linearGradient id="${gradientId}" x1="0%" y1="0%" x2="100%" y2="0%">
                            <stop offset="${partialFill}%" stop-color="#f39c12"/>
                            <stop offset="${partialFill}%" stop-color="#ddd"/>
                          </linearGradient>
                        </defs>
                        <path d="M12 2.5c.5 0 1 .3 1.2.8l2.6 5.2 5.7.8c.5.1.9.4 1.1.9s.1 1-.3 1.4l-4.2 4.1 1 5.6c.1.5-.1 1-.5 1.3s-.9.3-1.4 0L12 19.8l-5 2.6c-.5.3-1 .3-1.4 0s-.6-.8-.5-1.3l1-5.6-4.2-4.1c-.4-.4-.5-.9-.3-1.4s.6-.8 1.1-.9l5.7-.8 2.6-5.2c.2-.5.7-.8 1.2-.8z" fill="url(#${gradientId})"/>
                      </svg>`;
        }

        // add empty star
        for (let i = 0; i < emptyStars; i++) {
            starsHtml += `<svg class="sc-star" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                        <path d="M12 2.5c.5 0 1 .3 1.2.8l2.6 5.2 5.7.8c.5.1.9.4 1.1.9s.1 1-.3 1.4l-4.2 4.1 1 5.6c.1.5-.1 1-.5 1.3s-.9.3-1.4 0L12 19.8l-5 2.6c-.5.3-1 .3-1.4 0s-.6-.8-.5-1.3l1-5.6-4.2-4.1c-.4-.4-.5-.9-.3-1.4s.6-.8 1.1-.9l5.7-.8 2.6-5.2c.2-.5.7-.8 1.2-.8z" fill="#ddd"/>
                      </svg>`;
        }

        let detailsHtml = "";
        console.log($(this).data("show-details"));
        if ($(this).data("show-details") == true) {
            detailsHtml = `
            <div class="sc-stars-details">
              ${average.toFixed(1)} <i class="fas fa-star small-sc-star"></i>
              / ${sum} <i class="fa-solid fa-user-large"></i>
            </div>`;
        }

        $(this).empty().html(`
            <span class="sc-review-summary">
                <div class="sc-stars">${starsHtml}</div>
                ${detailsHtml}
            </span>
        `);

    //    $(this).empty().html(`
    //<span class="sc-review-summary">
    //  <div class="sc-stars">${starsHtml}</div>
    //  <div class="sc-stars-details">
    //    ${average.toFixed(1)} <i class="fas fa-star small-sc-star"></i>
    //    / ${sum} <i class="fa-solid fa-user-large"></i>
    //  </div>
    //  </span>
    //`);
    });
});

$(document).ready(function () {
    // Toggle functionality for each filter header
    $(".sc-filter-header").click(function () {
        const content = $(this).next(".sc-filter-content");
        const icon = $(this).find(".fas");

        if (content.is(":visible")) {
            content.slideUp();
            icon.removeClass("fa-chevron-up").addClass("fa-chevron-down");
        } else {
            content.slideDown();
            icon.removeClass("fa-chevron-down").addClass("fa-chevron-up");
        }
    });

    // Clear all checkboxes when "Clear All" is clicked
    $(".clear-all").click(function () {
        $(".showcase-filter-container input[type='checkbox']").prop(
            "checked",
            false
        );
    });
});

$(document).ready(function () {
    const slider = $(".pr-range-slider");
    const bar = $(".pr-range-bar");
    const fill = $(".pr-range-fill");
    const minHandle = $("#pr-min-handle");
    const maxHandle = $("#pr-max-handle");
    const minInput = $("#pr-min-value");
    const maxInput = $("#pr-max-value");
    const sliderWidth = slider.width();
    const maxRange = 1000000;

    function toLogScale(value) {
        return Math.log10(value + 1); // +1 to handle 0 gracefully
    }

    function toLinearScale(logValue) {
        return Math.pow(10, logValue) - 1;
    }

    function updateHandles() {
        const min = parseInt(minInput.val());
        const max = parseInt(maxInput.val());
        const minPercent = (toLogScale(min) / toLogScale(maxRange)) * 100;
        const maxPercent = (toLogScale(max) / toLogScale(maxRange)) * 100;

        // Update handle positions and tooltips
        minHandle.css("left", `${minPercent}%`).attr("data-tooltip", `Min: ${min}`);
        maxHandle.css("left", `${maxPercent}%`).attr("data-tooltip", `Max: ${max}`);

        // Update fill bar
        fill.css({
            left: `${minPercent}%`,
            width: `${maxPercent - minPercent}%`,
        });
    }

    function dragHandle(handle, targetInput) {
        handle.on("mousedown", function (e) {
            e.preventDefault();
            $(document).on("mousemove", function (e) {
                const sliderOffset = slider.offset().left;
                let percent = ((e.pageX - sliderOffset) / sliderWidth) * 100;
                percent = Math.min(Math.max(0, percent), 100);

                let value = toLinearScale((percent / 100) * toLogScale(maxRange));
                value = Math.round(value);

                // Prevent min from exceeding max and vice versa
                if (handle.is("#pr-min-handle") && value >= parseInt(maxInput.val())) {
                    value = parseInt(maxInput.val()) - 1;
                } else if (
                    handle.is("#pr-max-handle") &&
                    value <= parseInt(minInput.val())
                ) {
                    value = parseInt(minInput.val()) + 1;
                }

                targetInput.val(value);
                updateHandles();
            });

            $(document).on("mouseup", function () {
                $(document).off("mousemove mouseup");
            });
        });
    }

    minInput.on("input", function () {
        if (parseInt(minInput.val()) >= parseInt(maxInput.val())) {
            minInput.val(parseInt(maxInput.val()) - 1);
        }
        updateHandles();
    });

    maxInput.on("input", function () {
        if (parseInt(maxInput.val()) <= parseInt(minInput.val())) {
            maxInput.val(parseInt(minInput.val()) + 1);
        }
        updateHandles();
    });

    dragHandle(minHandle, minInput);
    dragHandle(maxHandle, maxInput);

    updateHandles();

    // Add Clear All functionality
    $(".clear-all").click(function () {
        const defaultMin = 0;
        const defaultMax = maxRange;

        minInput.val(defaultMin);
        maxInput.val(defaultMax);

        updateHandles();
    });
});



// minisearchbar
//$(document).ready(function () {
//    // Handle search button click
//    $("#sep-search-button").click(function () {
//        const icon = $("#sep-search-button i");

//        if (icon.hasClass("fa-times")) {
//            // Clear input and reset
//            $("#sep-search-input").val("");
//            $(".sep-search-container").removeClass("active");
//            icon.removeClass("fa-times").addClass("fa-search");
//        } else {
//            // Activate search bar
//            $(".sep-search-container").addClass("active");
//            $("#sep-search-input").focus();
//            icon.removeClass("fa-search").addClass("fa-times");
//        }
//    });

//    // Handle input focus
//    $("#sep-search-input").focus(function () {
//        const icon = $("#sep-search-button i");
//        icon.removeClass("fa-search").addClass("fa-times");
//    });

//    // Handle input keyup
//    $("#sep-search-input").keyup(function () {
//        const icon = $("#sep-search-button i");
//        const query = $(this).val();

//        if (query !== "") {
//            // Show clear icon if there's text in the input
//            icon.removeClass("fa-search").addClass("fa-times");
//        } else {
//            // Show search icon if the input is empty
//            icon.removeClass("fa-times").addClass("fa-search");
//        }
//    });

//    // Handle input blur
//    $("#sep-search-input").blur(function () {
//        if ($(this).val() === "") {
//            // Reset if the input is empty
//            $(".sep-search-container").removeClass("active");
//            $("#sep-search-button i").removeClass("fa-times").addClass("fa-search");
//        }
//    });
//});