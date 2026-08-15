// Call the dataTables jQuery plugin
$(document).ready(function () {
    var table = $('#dataTable').DataTable({
        "order": [[0, 'desc']]
    });

    // Source filter buttons (Application log view only)
    $('#sourceFilter button').on('click', function () {
        $('#sourceFilter button').removeClass('active');
        $(this).addClass('active');

        var source = $(this).data('source');
        // Exact match on the Source column (index 3); empty = show all
        table.column(3).search(source ? '^' + source + '$' : '', true, false).draw();
    });
});
