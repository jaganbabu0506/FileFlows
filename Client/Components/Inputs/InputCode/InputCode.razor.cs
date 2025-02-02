namespace FileFlows.Client.Components.Inputs
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using BlazorMonaco;
    using Microsoft.JSInterop;
    using System.Collections.Generic;
    using FileFlows.Plugin;
    using Microsoft.AspNetCore.Components.Web;

    public partial class InputCode : Input<string>
    {

        const string API_URL = "/api/code-eval";
        private bool Updating = false;
        private string InitialValue;

        private MonacoEditor CodeEditor { get; set; }

        private Dictionary<string, object> _Variables = new Dictionary<string, object>();
        [Parameter]
        public Dictionary<string, object> Variables
        {
            get => _Variables;
            set { _Variables = value ?? new Dictionary<string, object>(); }
        }

        private StandaloneEditorConstructionOptions EditorConstructionOptions(MonacoEditor editor)
        {
            return new StandaloneEditorConstructionOptions
            {
                AutomaticLayout = true,
                Minimap = new EditorMinimapOptions { Enabled = false },
                Theme = "vs-dark",
                Language = "javascript",
                Value = this.Value?.Trim() ?? ""
            };
        }

        private void OnEditorInit(MonacoEditorBase e)
        {
            _ = jsRuntime.InvokeVoidAsync("ffCode.initModel", Variables);
            InitialValue = this.Value;
        }

        protected override void OnInitialized()
        {
            this.InitialValue = Value;
            this.Editor.OnCancel += Editor_OnCancel;
            this.Editor.OnClosed += Editor_Closed;
            base.OnInitialized();
        }

        private Task Editor_Closed()
        {
            this.Editor.OnCancel -= Editor_OnCancel;
            this.Editor.OnClosed -= Editor_Closed;
            return Task.CompletedTask;
        }

        private async Task<bool> Editor_OnCancel()
        {
            this.Updating = true;
            this.Value = await CodeEditor.GetValue();
            this.Updating = false;
            if (this.InitialValue != this.Value)
            {
                bool cancel = await Dialogs.Confirm.Show(Translater.Instant("Labels.Confirm"), Translater.Instant("Labels.CancelMessage"));
                if (cancel == false)
                    return false;
            }
            return true;
        }

        private void OnBlur()
        {
            _ = Task.Run(async () =>
            {
                this.Updating = true;
                this.Value = await CodeEditor.GetValue();
                this.Updating = false;
            });
        }

        protected override void ValueUpdated()
        {
            if (this.Updating)
                return;
            if (string.IsNullOrEmpty(this.Value) || CodeEditor == null)
                return;
            CodeEditor.SetValue(this.Value.Trim());
        }
        private async Task OnKeyDown(KeyboardEventArgs e)
        {
            if (e.Code == "Enter" && e.ShiftKey)
            {
                // for code the shortcut to submit is shift enter

                // need to get value in code block
                this.Updating = true;
                this.Value = await CodeEditor.GetValue();
                this.Updating = false;

                await OnSubmit.InvokeAsync();
            }
            else if (e.Code == "Escape")
            {
                await OnClose.InvokeAsync();
            }
        }
    }
}