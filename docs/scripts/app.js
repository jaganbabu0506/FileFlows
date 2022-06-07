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
    if(localStorage.getItem('collapse_' + id) == true){
        chk.checked = true;
        console.log('toggle3:', id);
        console.log('collapse3: ', localStorage.getItem('collapse_' + id));

        let parent = chk.parentNode.closest('input[type=checkbox]');
        if(parent)
            checkToggle(parent);
    }
}

function toggleCollapse(event){
    let id = event.target.id;
    let chk = event.target;
    console.log('chk.id: ', chk.id);
    console.log('chk.checked: ', chk.checked);
    localStorage.setItem('collapse_' + id, chk.checked ? 1 : 0);
    
    // close any below this one if checked
    if(event.target.checked == false){
        for(let sub of chk.querySelectorAll('input[type=checkbox]')){
            if(sub.checked)
                toggleCollapse(sub);
        }
    }
}