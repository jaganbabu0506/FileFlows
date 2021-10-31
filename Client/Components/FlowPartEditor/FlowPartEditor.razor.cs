namespace FileFlow.Client.Components
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using FileFlow.Shared;
    using FileFlow.Shared.Models;
    using ffElement = FileFlow.Shared.Models.FlowElement;
    using System.Collections;
    using System.Collections.Generic;
    using System.Dynamic;

    public partial class FlowPartEditor : ComponentBase
    {

        public bool Visible { get; set; }

        private FlowPart Part { get; set; }
        private ffElement Element { get; set; }

        private string Icon { get; set; }
        private bool IsSaving { get; set; }

        private string lblSave, lblSaving, lblCancel;

        private ExpandoObject Model { get; set; }

        TaskCompletionSource<ExpandoObject> OpenTask;

        protected override void OnInitialized()
        {
            lblSave = Translater.Instant("Labels.Save");
            lblSaving = Translater.Instant("Labels.Saving");
            lblCancel = Translater.Instant("Labels.Cancel");
        }

        internal Task<ExpandoObject> Open(FlowPart part, ffElement element)
        {
            OpenTask = new TaskCompletionSource<ExpandoObject>();
            Logger.Instance.DLog("Part: ", part);
            this.Part = part;
            this.Element = element;
            this.Icon = Helpers.FlowHelper.GetFlowPartIcon(part.Type);
            this.Visible = true;
            this.Model = part.Model ?? element.Model ?? new ExpandoObject();
            this.StateHasChanged();
            return OpenTask.Task;
        }

        private void Save()
        {
            OpenTask.TrySetResult(this.Model);
            this.Visible = false;
            this.Part = null;
        }

        private void Cancel()
        {
            OpenTask.TrySetCanceled();
            this.Visible = false;
            this.Part = null;

        }

        private void UpdateValue(string field, object value)
        {
            var dict = (IDictionary<string, object>)Model;
            if (dict.ContainsKey(field))
                dict[field] = value;
            else
                dict.Add(field, value);
        }

        private T GetValue<T>(string field, T @default = default(T))
        {
            var dict = (IDictionary<string, object>)Model;
            Logger.Instance.DLog("Getting value for: " + field);
            if (dict.ContainsKey(field) == false)
            {
                Logger.Instance.DLog("Not in model");
                return @default;
            }
            object value = dict[field];
            if (value == null)
            {
                Logger.Instance.DLog("value is null");
                return @default;
            }
            if (value is T)
            {
                return (T)value;
            }
            var valueType = value.GetType();
            try
            {
                return (T)FileFlow.Shared.Converter.ConvertObject(typeof(T), value);
            }
            catch (Exception ex)
            {
                Logger.Instance.DLog("Not of type: " + value.GetType());
                Logger.Instance.WLog("error: " + ex.Message + "\n" + ex.StackTrace);
                return @default;

            }
        }
    }
}