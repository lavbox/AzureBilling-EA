/// <reference path="templates/subscription-details.html" />
/// <reference path="templates/subscription-details.html" />
var app = angular.module('app', ['ngRoute','ngCookies']);
app.config(['$routeProvider',function ($routeProvider) {

    //================================================
    // Routes
    //================================================

    $routeProvider.when('/dash/:monthId?', {
        templateUrl: '/angular/templates/dashboard.html',
        controller: 'dashboardCtrl'
    });
    $routeProvider.when('/byAccount/:monthId?', {
        templateUrl: '/angular/templates/byAccount.html',
        controller: 'accountsCtrl'
    });
$routeProvider.when('/byService/:monthId?', {
        templateUrl: '/angular/templates/byServices.html',
        controller: 'servicesCtrl'
    });
    $routeProvider.otherwise({
        redirectTo: '/dash'
    });
}]);

app.run();