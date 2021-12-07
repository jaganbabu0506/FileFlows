namespace FileFlows.Client.Pages
{
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Threading.Tasks;
    using Radzen;
    using FileFlows.Client.Components;
    using FileFlows.Shared.Helpers;
    using FileFlows.Shared;
    using FileFlows.Shared.Models;
    using FileFlows.Plugin;
    using System;
    using FileFlows.Client.Components.Inputs;

    public partial class Libraries : ListPage<Library>
    {
        public override string ApiUrl => "/api/library";

        private Library EditingItem = null;

        private async Task Add()
        {
            await Edit(new Library() { Enabled = true, ScanInterval = 60, FileSizeDetectionInterval = 5 });
        }
#if (DEMO)
        public override async Task Load(Guid? selectedUid = null)
        {
            this.Data = Enumerable.Range(1, 5).Select(x => new Library
            {
                Enabled = true,
                Flow = new ObjectReference
                {
                    Name = "Flow",
                    Uid = Guid.NewGuid()
                },
                Path = "/media/library" + x,                
                Name = "Demo Library " + x,
                Uid = Guid.NewGuid(),
                Filter = "\\.(mkv|mp4|avi|mov|divx)$"
            }).ToList();
        }
#endif

        private async Task<RequestResult<FileFlows.Shared.Models.Flow[]>> GetFlows()
        {
#if (DEMO)
            var results = Enumerable.Range(1, 5).Select(x => new FileFlows.Shared.Models.Flow { Name = "Flow " + x, Uid = System.Guid.NewGuid() }).ToArray();
            return new RequestResult<FileFlows.Shared.Models.Flow[]> { Success = true, Data = results };
#endif
            return await HttpHelper.Get<FileFlows.Shared.Models.Flow[]>("/api/flow");
        }

        public override async Task<bool> Edit(Library library)
        {
            Blocker.Show();
            var flowResult = await GetFlows();
            Blocker.Hide();
            if (flowResult.Success == false || flowResult.Data?.Any() != true)
            {
                ShowEditHttpError(flowResult, "Pages.Libraries.ErrorMessages.NoFlows");
                return false;
            }
            var flowOptions = flowResult.Data.Select(x => new ListOption { Value = new ObjectReference { Name = x.Name, Uid = x.Uid }, Label = x.Name });


            this.EditingItem = library;
            List<ElementField> fields = new List<ElementField>();
            ElementField efTemplate = null;
#if (!DEMO)
            if (library == null || library.Uid == Guid.Empty)
            {
                // adding
                Blocker.Show();
                try
                {
                    var templateResult = await HttpHelper.Get<Dictionary<string, List<Library>>>("/api/library/templates");
                    if (templateResult.Success == true || templateResult.Data?.Any() == true)
                    {
                        List<ListOption> templates = new();
                        foreach (var group in templateResult.Data)
                        {
                            if (string.IsNullOrEmpty(group.Key) == false)
                            {
                                templates.Add(new ListOption
                                {
                                    Value = Globals.LIST_OPTION_GROUP,
                                    Label = group.Key
                                });
                            }
                            templates.AddRange(group.Value.Select(x => new ListOption
                            {
                                Label = x.Name,
                                Value = x
                            }));
                        }
                        templates.Insert(0, new ListOption
                        {
                            Label = "Custom",
                            Value = null
                        });
                        efTemplate = new ElementField
                        {
                            Name = "Template",
                            InputType = FormInputType.Select,
                            Parameters = new Dictionary<string, object>
                            {
                                { nameof(InputSelect.Options), templates },
                                { nameof(InputSelect.AllowClear), false},
                                { nameof(InputSelect.ShowDescription), true }
                            }
                        };
                        efTemplate.ValueChanged += TemplateValueChanged;
                        fields.Add(efTemplate);
                        fields.Add(new ElementField
                        {
                            InputType = FormInputType.HorizontalRule
                        });
                    }
                }
                finally
                {
                    Blocker.Hide();
                }
            }
#endif
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Text,
                Name = nameof(library.Name),
                Validators = new List<FileFlows.Shared.Validators.Validator> {
                    new FileFlows.Shared.Validators.Required()
                }
            });
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Folder,
                Name = nameof(library.Path),
                Validators = new List<FileFlows.Shared.Validators.Validator> {
                    new FileFlows.Shared.Validators.Required()
                }
            });
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Text,
                Name = nameof(library.Filter)
            });
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Select,
                Name = nameof(library.Flow),
                Parameters = new Dictionary<string, object>{
                    { "Options", flowOptions.ToList() }
                }
            });
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Select,
                Name = nameof(library.Priority),
                Parameters = new Dictionary<string, object>{
                    { "AllowClear", false },
                    { "Options", new List<ListOption> {
                        new ListOption { Value = ProcessingPriority.Lowest, Label = $"Enums.{nameof(ProcessingPriority)}.{nameof(ProcessingPriority.Lowest)}" },
                        new ListOption { Value = ProcessingPriority.Low, Label = $"Enums.{nameof(ProcessingPriority)}.{nameof(ProcessingPriority.Low)}" },
                        new ListOption { Value = ProcessingPriority.Normal, Label =$"Enums.{nameof(ProcessingPriority)}.{nameof(ProcessingPriority.Normal)}" },
                        new ListOption { Value = ProcessingPriority.High, Label = $"Enums.{nameof(ProcessingPriority)}.{nameof(ProcessingPriority.High)}" },
                        new ListOption { Value = ProcessingPriority.Highest, Label = $"Enums.{nameof(ProcessingPriority)}.{nameof(ProcessingPriority.Highest)}" }
                    } }
                }
            });
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Int,
                Parameters = new Dictionary<string, object>
                {
                    { "Min", 10 },
                    { "Max", 24 * 60 * 60 }
                },
                Name = nameof(library.ScanInterval)
            });
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Int,
                Parameters = new Dictionary<string, object>
                {
                    { "Min", 0 },
                    { "Max", 300 }
                },
                Name = nameof(library.FileSizeDetectionInterval)
            });
            fields.Add(new ElementField
            {
                InputType = FileFlows.Plugin.FormInputType.Switch,
                Name = nameof(library.Enabled)
            });
            var result = await Editor.Open("Pages.Library", "Pages.Library.Title", fields, library,
              saveCallback: Save);
            if(efTemplate != null)
            {
                efTemplate.ValueChanged -= TemplateValueChanged;
            }
            return false;
        }

        private void TemplateValueChanged(object sender, object value) 
        {
            if (value == null)
                return;
            Library template = value as Library;
            if (template == null)
                return;
            Editor editor = sender as Editor;
            if (editor == null)
                return;
            if (editor.Model == null)
                editor.Model = new ExpandoObject();
            IDictionary<string, object> model = editor.Model;
            
            SetModelProperty(nameof(template.Name), template.Name);
            SetModelProperty(nameof(template.Template), template.Name);
            SetModelProperty(nameof(template.FileSizeDetectionInterval), template.FileSizeDetectionInterval);
            SetModelProperty(nameof(template.Filter), template.Filter);
            SetModelProperty(nameof(template.Path), template.Path);
            SetModelProperty(nameof(template.Priority), template.Priority);
            SetModelProperty(nameof(template.ScanInterval), template.ScanInterval);

            void SetModelProperty(string property, object value)
            {
                if(model.ContainsKey(property))
                    model[property] = value;
                else
                    model.Add(property, value);
            }
        }

        async Task<bool> Save(ExpandoObject model)
        {
#if (DEMO)
            return true;
#else
            Blocker.Show();
            this.StateHasChanged();

            try
            {
                var saveResult = await HttpHelper.Post<Library>($"{ApiUrl}", model);
                if (saveResult.Success == false)
                {
                    NotificationService.Notify(NotificationSeverity.Error, saveResult.Body?.EmptyAsNull() ?? Translater.Instant("ErrorMessages.SaveFailed"));
                    return false;
                }

                int index = this.Data.FindIndex(x => x.Uid == saveResult.Data.Uid);
                if (index < 0)
                    this.Data.Add(saveResult.Data);
                else
                    this.Data[index] = saveResult.Data;

                await this.Load(saveResult.Data.Uid);

                return true;
            }
            finally
            {
                Blocker.Hide();
                this.StateHasChanged();
            }
#endif
        }


        private string TimeSpanToString(Library lib)
        {
            if (lib.LastScanned.Year < 2001)
                return Translater.Instant("Times.Never");

            if (lib.LastScannedAgo.TotalMinutes < 1)
                return Translater.Instant("Times.SecondsAgo", new { num = (int)lib.LastScannedAgo.TotalSeconds });
            if (lib.LastScannedAgo.TotalHours < 1 && lib.LastScannedAgo.TotalMinutes < 120)
                return Translater.Instant("Times.MinutesAgo", new { num = (int)lib.LastScannedAgo.TotalMinutes });
            if (lib.LastScannedAgo.TotalDays < 1)
                return Translater.Instant("Times.HoursAgo", new { num = (int)Math.Round(lib.LastScannedAgo.TotalHours) });
            else
                return Translater.Instant("Times.DaysAgo", new { num = (int)lib.LastScannedAgo.TotalDays });
        }

        private async Task Rescan()
        {
            var uids = Table.GetSelected()?.Select(x => x.Uid)?.ToArray() ?? new System.Guid[] { };
            if (uids.Length == 0)
                return; // nothing to rescan

            Blocker.Show();
            this.StateHasChanged();

            try
            {
#if (!DEMO)
                var deleteResult = await HttpHelper.Put($"{ApiUrl}/rescan", new ReferenceModel { Uids = uids });
                if (deleteResult.Success == false)
                    return;
#endif
            }
            finally
            {
                Blocker.Hide();
                this.StateHasChanged();
            }
        }
    }

}