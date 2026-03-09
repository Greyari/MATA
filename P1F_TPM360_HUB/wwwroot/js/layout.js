$(function () {

const body = $("body");
const sidebar = $(".main-sidebar");
const img = $("#sidebar-brand-img");

function updateSidebarLogo() {

if (body.hasClass("sidebar-collapse")) {

img.attr("src", "/assets/logos.png");

} else {

img.attr("src", "/assets/logo.png");

}

}

updateSidebarLogo();

$(document).on("click", '[data-widget="pushmenu"]', function () {

setTimeout(updateSidebarLogo, 300);

});

});

function showSessionExpirationModal() {

Swal.fire({

title: 'Session Expired',

text: 'Your session will expire soon',

icon: 'warning',

showCancelButton: true,

confirmButtonText: 'Stay Logged In',

cancelButtonText: 'Logout'

}).then((result)=>{

if(result.isConfirmed){

$.post("/Login/RefreshSession",function(){

location.reload()

})

}else{

window.location="/Home/Logout"

}

})

}

setTimeout(showSessionExpirationModal,15*60*1000);