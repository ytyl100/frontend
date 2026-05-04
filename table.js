(function ($) {
    "use strict";

    function initBasicDatatable() {
        var $table = $("#basic-datatable");

        if (!$table.length || !$.fn.DataTable) {
            return;
        }

        $table.DataTable({
            language: {
                paginate: {
                    previous: "&#8249;",
                    next: "&#8250;"
                }
            },
            drawCallback: function () {
                $(".dataTables_paginate > .pagination").addClass("pagination-rounded");
            }
        });
    }

    $(initBasicDatatable);
})(jQuery);