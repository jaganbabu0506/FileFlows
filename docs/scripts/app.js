document.addEventListener("DOMContentLoaded", function()
{
    var checkboxes = document.querySelectorAll('.side-bar input[type=checkbox]');
    for(let chk of checkboxes){
        checkToggle(chk);
    }
});

function checkToggle(chk)
{
    let id = chk.id;
    console.log('toggle:', id);
    console.log('collapse: ', localStorage.getItem('collapse_' + id));
    if(localStorage.getItem('collapse_' + id)){
        chk.checked = true;

        let parent = chk.closest('input[type=checkbox]');
        if(parent)
            checkToggle(parent);
    }
}

function toggleCollapse(event){
    let id = event.target.id;
    console.log('event.target.id: ', event.target.id);
    console.log('event.target.checked: ', event.target.checked);
    localStorage.setItem('collapse_' + id, event.target.checked ? 1 : 0);

}