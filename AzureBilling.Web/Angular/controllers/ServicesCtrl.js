var app = angular.module('app');
app.controller('servicesCtrl', ['$scope', '$http', 'chartService', '$routeParams', function ($scope, $http, chartService,$routeParams) {

    $scope._chartService = chartService;

    var getAllCategories = function () {
        return {
            'Data Management': 'Data Management', 'Data Services': 'Data Services',
           /* 'Networking': 'Networking',*/
            'SQL Database': 'SQL Database', 'Storage': 'Storage', 'Visual Studio': 'Visual Studio'
        };
    }

     //initialize the graph
    $scope.initGraph = function () {

        $scope.monthId = $routeParams.monthId !== undefined ? $routeParams.monthId : ''

        $http({
            method: 'GET',
            url: '/data/SpendingByServiceDaily?monthId=' + $scope.monthId
        }).then(function successCallback(response) {
            var data = {};
            data.title = 'Daily Usage',
            data.categories = response.data.date;
            data.series = response.data.series;
            $scope._chartService.drawLineChart('container2', data);
        });
        //make server call to get data
        $http({
            method: 'GET',
            url: '/data/SpendingByService?monthId=' + $scope.monthId
        }).then(function successCallback(response) {
            var data1 = {};
            data1.title = 'Usage by Category';
            data1.data = response.data;
            $scope._chartService.drawPieChart('container1', data1);
        });
    };

    $scope.initGraph();
}]);