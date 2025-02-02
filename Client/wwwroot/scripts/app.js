window.dashboardElementResized = new Event('dashboardElementResized', {});

window.ff = {

    log: function (level, parameters) {

        if (!parameters || parameters.length == 0)
            return;
        let message = parameters[0]
        parameters.splice(0, 1);

        if (level === 1) parameters.length > 0 ? console.error(message, parameters) : console.error(message);
        else if (level === 2) parameters.length > 0 ? console.warn(message, parameters) : console.warn(message);
        else if (level === 3) parameters.length > 0 ? console.info(message, parameters) : console.info(message);
        else parameters.length > 0 ? console.error(message, parameters) : console.log(message);
    },
    deviceDimensions: function () {

        return { width: screen.width, height: screen.height };
    },
    disableMovementKeys: function (element) {
        if (typeof (element) === 'string')
            element = document.getElementById(element);
        if (!element)
            return;
        const blocked = ['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'Enter', 'PageUp', 'PageDown', 'Home', 'End'];
        element.addEventListener('keydown', e => {
            if (e.target.getAttribute('data-disable-movement') != 1)
                return;

            if (blocked.indexOf(e.code) >= 0) {
                e.preventDefault();
                return false;
            }
        })
    },
    loadJS: function (url, callback) {
        if (!url)
            return;
        let tag = document.createElement('script');
        tag.src = url;
        tag.onload = () => {
            if (callback)
                callback();
        };
        document.body.appendChild(tag);
    },
    downloadFile: function (url, filename) {
        const anchorElement = document.createElement('a');
        anchorElement.href = url;
        anchorElement.download = filename ? filename : 'File';
        anchorElement.click();
        anchorElement.remove();
    },
    copyToClipboard: function (text) {
        if (window.clipboardData && window.clipboardData.setData) {
            // Internet Explorer-specific code path to prevent textarea being shown while dialog is visible.
            return window.clipboardData.setData("Text", text);

        }
        else if (document.queryCommandSupported && document.queryCommandSupported("copy")) {
            var textarea = document.createElement("textarea");
            textarea.textContent = text;
            textarea.style.position = "fixed";  // Prevent scrolling to bottom of page in Microsoft Edge.
            document.body.appendChild(textarea);
            textarea.select();
            try {
                return document.execCommand("copy");  // Security exception may be thrown by some browsers.
            }
            catch (ex) {
                console.warn("Copy to clipboard failed.", ex);
                return prompt("Copy to clipboard: Ctrl+C, Enter", text);
            }
            finally {
                document.body.removeChild(textarea);
            }
        }
    },
    scrollTableToTop: function(){
        document.querySelector('.flowtable-body').scrollTop = 0;
    },
    
    
    dashboard: function(uid) {
        let dashboardData = localStorage.getItem('dashboard-' + uid);
        
        if(dashboardData)
        {
            try {
                dashboardData = JSON.parse(dashboardData);
                for (let item of dashboardData) {
                    let ele = document.getElementById(item.id);
                    if (!ele) {
                        console.log('element not found', item, item.id);
                        continue;
                    }
                    ele.setAttribute('gs-x', item.x);
                    ele.setAttribute('gs-y', item.y);
                    ele.setAttribute('gs-w', item.w);
                    ele.setAttribute('gs-h', item.h);
                }
            }catch(err){
                // can throw if the saved data is corrupt, silent fail to defaults
            }
        }

        var grid = GridStack.init({
            cellHeight:170,
            handle: '.draghandle'
        });

        saveGrid = () => {
            let data = [];
            for(let ele of document.querySelectorAll('.grid-stack-item')){
                let id = ele.id;
                let x = parseInt(ele.getAttribute('gs-x'), 10);
                let y = parseInt(ele.getAttribute('gs-y'), 10);
                let w = parseInt(ele.getAttribute('gs-w'), 10);
                let h = parseInt(ele.getAttribute('gs-h'), 10);
                data.push({
                    id:id, x: x, y:y, w:w, h:h
                });                
            }
            localStorage.setItem('dashboard-' + uid, JSON.stringify(data));
        }
        
        grid.on('resizestop', (e, el) => {
            window.dashboardElementResized.args = e;
            el.dispatchEvent(window.dashboardElementResized);
            saveGrid();
        });
    }
};