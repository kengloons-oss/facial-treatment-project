$(document).ready(function(){
    $('#main-search-button').click(function(){
      $('.main-search-container').addClass('active');
      $('#main-search-input').focus();
      $('#main-search-button i').removeClass('fa-search').addClass('fa-times');
    });
    
    $('#main-search-input').focus(function(){
      $('.suggest-container').removeClass('hide').addClass('showflex');
    });
  
    $('#main-search-input').keyup(function(){
      var query = $(this).val();
      if(query !== ''){
        // searchProduct(query);
        $('.suggest-container').addClass('showflex').removeClass('hide');
        $('#main-search-button i').removeClass('fa-search').addClass('fa-times');
      } else {
        $('.suggest-container').addClass('hide').removeClass('showflex');
        $('#main-search-button i').removeClass('fa-times').addClass('fa-search');
      }
    });
  
    $('#main-search-button').click(function(){
      if ($('#main-search-button i').hasClass('fa-times')) {
        $('#main-search-input').val('');
        $('.main-search-container').removeClass('active');
        $('#main-search-button i').removeClass('fa-times').addClass('fa-search');
        $('.suggest-container').addClass('hide').removeClass('showflex');
      }
    });
  
    $('#main-search-input').blur(function(){
      if ($(this).val() === '') {
        $('.suggest-container').addClass('hide').removeClass('showflex');
        $('.main-search-container').removeClass('active');
        $('#main-search-button i').removeClass('fa-times').addClass('fa-search');
      }
    });
    
      
          $('#main-search-button').click(function () {
              $('#main-search-Input').focus();
          });
  
          $('#main-search-Input').keyup(function () {          
                  $('#main-search-button i').removeClass('fa-search').addClass('fa-times');
          });
  
          $('#main-search-button').click(function () {
              if ($('#main-search-button i').hasClass('fa-times')) {
                  $('#main-search-Input').val('');
                  $('#main-search-button i').removeClass('fa-times').addClass('fa-search');
              }
          });
  
          $('#main-search-Input').blur(function () {
              if ($(this).val() === '') {
                  $('#main-search-button i').removeClass('fa-times').addClass('fa-search');
              }
          });
    
});
  
//   /**
//    * AJAX function
//    * 
//    * @param {*} searchStr Product name contain with this String
//    * @returns {void}
//    */
//   function searchProduct(searchStr) {
//     $.ajax({
//       url: baseURL + "/user-Product",
//       type: "GET",
//       data: {
//           action: "search",
//           searchInput: searchStr
//       },
//       success: function (response) {
//           return productDOMElement(response.productList);
//       }
//     });
//   }
  
//   /**
//    * DOM Element convert
//    * 
//    * @param {*} response.productList Product name contain with this String
//    * @param {String} productList.productid 
//    * @param {String} productList.image Base64 format, possible in null
//    * @param {String} productList.name 
//    * @param {String} productList.price Able to parse to float data type
//    * @returns {void} 
//    */
//   function productDOMElement(data){
//     let DOMContent = "";
    
//     if(data.length == 0) return DOMContent;
  
//     data.forEach((product) => {
//       const tempImg = product.image === 'null'
//       ? baseURL + "/includes/img/default_product.jpg"
//       : "data:image/jpeg;base64," + product.image;
//       DOMContent += `
//           <a href="${baseURL + "/user-Product?action=view&id=" + product.productid}" class="suggestion-item link" style="text-decoration: none;">
//           <div class="product-container">
//           <img src="${tempImg}" alt="Image 1">
//           <div class="product-info">
//           <h4>${product.name}</h4>
//           <p>RM ${parseFloat(product.price).toFixed(2)}</p>
//           </div>
//           </div>
//           </a>
//       `
//     })
//     $('.suggestcol-2').html(DOMContent);
//     return DOMContent;
//   }