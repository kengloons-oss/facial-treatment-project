$(document).ready(function(){
    $('#search-button').click(function(){
      $('.search-container').addClass('active');
      $('.search-input').focus();
      $('#search-button i').removeClass('fa-search').addClass('fa-times');
    });
    
    $('.search-input').focus(function(){
    //   $('.suggest-container').removeClass('hide').addClass('showflex');
    });
  
    $('.search-input').keyup(function(){
      var query = $(this).val();
      if(query !== ''){
        // $('.suggest-container').addClass('showflex').removeClass('hide');
        $('#search-button i').removeClass('fa-search').addClass('fa-times');
      } else {
        // $('.suggest-container').addClass('hide').removeClass('showflex');
        $('#search-button i').removeClass('fa-times').addClass('fa-search');
      }
    });
  
    $('#search-button').click(function(){
      if ($('#search-button i').hasClass('fa-times')) {
        $('.search-input').val('');
        $('.search-container').removeClass('active');
        $('#search-button i').removeClass('fa-times').addClass('fa-search');
        // $('.suggest-container').addClass('hide').removeClass('showflex');
      }
    });
  
    $('.search-input').blur(function(){
      if ($(this).val() === '') {
        // $('.suggest-container').addClass('hide').removeClass('showflex');
        $('.search-container').removeClass('active');
        $('#search-button i').removeClass('fa-times').addClass('fa-search');
      }
    });
    
      
          $('#searchButton').click(function () {
              $('#searchInput').focus();
          });
  
          $('#searchInput').keyup(function () {          
                  $('#searchButton i').removeClass('fa-search').addClass('fa-times');
          });
  
          $('#searchButton').click(function () {
              if ($('#searchButton i').hasClass('fa-times')) {
                  $('#searchInput').val('');
                  $('#searchButton i').removeClass('fa-times').addClass('fa-search');
              }
          });
  
          $('#searchInput').blur(function () {
              if ($(this).val() === '') {
                  $('#searchButton i').removeClass('fa-times').addClass('fa-search');
              }
          });
    
  });