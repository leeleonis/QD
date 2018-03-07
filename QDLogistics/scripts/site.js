var Website = function () {
    var _baseUrl;
    var $workingTaskList = $("#list-task-working"), $updateTaskDate = $("#date-task-update"), $refreshTaskBtn = $("#btn-task-refresh");

    var init = function (base_url) {
        _baseUrl = base_url;

        $("span#activity").on("click", function () {
            $refreshTaskBtn.trigger("click");
        });

        $refreshTaskBtn.on("click", function () {
            var url = site_url("ajax/getWorkingTask");
            ajaxData(url, "get", {}).done(function (response) {
                $workingTaskList.html(response['data']['list']);
                $updateTaskDate.html(response['data']['date']);
                $refreshTaskBtn.button('reset');
            });
        });
    }

    var site_url = function (url) {
        if (isEmpty(url)) return _baseUrl;

        return _baseUrl + url;
    }

    function ajaxData (url, type, data) {
        return $.ajax({ url: url, type: type, data: data, dataType: "json", cache: false });
    }

    function isEmpty (value) {
        return (value == undefined) || (value == null) || (value == "");
    }

    return {
        Init: init,
        Site_url: site_url
    };
}();