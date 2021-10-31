namespace FileFlow.Client
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;
    using FileFlow.Client.Helpers;
    using FileFlow.Shared;

    public partial class App : ComponentBase
    {
        private App Instance { get; set; }

        [Inject]
        public HttpClient Client { get; set; }
        [Inject]
        public IJSRuntime jsRuntime { get; set; }
        public bool LanguageLoaded { get; set; } = false;

        private async Task LoadLanguage()
        {
            string langFile = await LoadLanguageFile("i18n/en.json");
            string pluginLang = await LoadLanguageFile("/api/plugin/language/en.json");
            Translater.Init(langFile, pluginLang);
        }

        private async Task<string> LoadLanguageFile(string url)
        {
            return (await HttpHelper.Get<string>(url)).Data ?? "";
        }

        protected override async Task OnInitializedAsync()
        {
            Instance = this;
            Logger.jsRuntime = jsRuntime;
            Translater.Logger = Logger.Instance;
            FileFlow.Shared.Logger.Instance = Logger.Instance;
            HttpHelper.Client = Client;
            await Task.Run(async () =>
            {
                await LoadLanguage();
                LanguageLoaded = true;
                this.StateHasChanged();
            });
        }
    }
}