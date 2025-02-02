namespace FileFlows.Server.Controllers;

using FileFlows.Plugin.Models;
using System;
using Microsoft.AspNetCore.Mvc;
using FileFlows.Shared.Models;
using FileFlows.Server.Helpers;
using System.Dynamic;
using FileFlows.Plugin;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FileFlows.Shared;
using System.Text.Json;

/// <summary>
/// Controller for Flows
/// </summary>
[Route("/api/flow")]
public class FlowController : ControllerStore<Flow>
{
    const int DEFAULT_XPOS = 450;
    const int DEFAULT_YPOS = 50;

    /// <summary>
    /// Get all flows in the system
    /// </summary>
    /// <returns>all flows in the system</returns>
    [HttpGet]
    public async Task<IEnumerable<Flow>> GetAll() => (await GetDataList()).OrderBy(x => x.Name.ToLower());

    /// <summary>
    /// Gets the failure flow for a particular library
    /// </summary>
    /// <param name="libraryUid">the UID of the library</param>
    /// <returns>the failure flow</returns>
    [HttpGet("failure-flow/by-library/{libraryUid}")]
    public async Task<Flow> GetFailureFlow([FromRoute] Guid libraryUid)
    {
        if (DbHelper.UseMemoryCache)
        {
            var flow = (await GetDataList())?.Where(x => x.Type == FlowType.Failure && x.Enabled)?.FirstOrDefault();
            return flow;
        }
        return await DbHelper.GetFailureFlow(libraryUid);
    }

    /// <summary>
    /// Exports a specific flow
    /// </summary>
    /// <param name="uid">The Flow UID</param>
    /// <returns>A download response of the flow</returns>
    [HttpGet("export/{uid}")]
    public async Task<IActionResult> Export([FromRoute] Guid uid)
    {
        var flow = await GetByUid(uid);
        if (flow == null)
            return NotFound();
        string json = JsonSerializer.Serialize(flow, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        byte[] data = System.Text.UTF8Encoding.UTF8.GetBytes(json);
        return File(data, "application/octet-stream", flow.Name + ".json");
    }

    /// <summary>
    /// Imports a flow
    /// </summary>
    /// <param name="json">The json data to import</param>
    /// <returns>The newly import flow</returns>
    [HttpPost("import")]
    public async Task<Flow> Import([FromBody] string json)
    {
        Flow? flow = JsonSerializer.Deserialize<Flow>(json);
        if (flow == null)
            throw new ArgumentNullException(nameof(flow));
        if (flow.Parts == null || flow.Parts.Count == 0)
            throw new ArgumentException(nameof(flow.Parts));

        // generate new UIDs for each part
        foreach (var part in flow.Parts)
        {
            Guid newGuid = Guid.NewGuid();
            json = json.Replace(part.Uid.ToString(), newGuid.ToString());
        }

        // reparse with new UIDs
        flow = JsonSerializer.Deserialize<Flow>(json);
        flow.Uid = Guid.Empty;
        flow.DateModified = DateTime.Now;
        flow.DateCreated = DateTime.Now;
        flow.Name = await GetNewUniqueName(flow.Name);
        return await Update(flow);
    }


    /// <summary>
    /// Duplicates a flow
    /// </summary>
    /// <param name="uid">The UID of the flow</param>
    /// <returns>The duplicated flow</returns>
    [HttpGet("duplicate/{uid}")]
    public async Task<Flow> Duplicate([FromRoute] Guid uid)
    { 
        var flow = await GetByUid(uid);
        if (flow == null)
            return null;
        
        string json = JsonSerializer.Serialize(flow, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return await Import(json);
    }

    /// <summary>
    /// Generates a template for of a flow
    /// </summary>
    /// <param name="uid">The Flow UID</param>
    /// <returns>A download response of the flow template</returns>
    [HttpGet("template/{uid}")]
    public async Task<IActionResult> Template([FromRoute] Guid uid)
    {
        var flow = await GetByUid(uid);
        if (flow == null)
            return NotFound();

        string json = JsonSerializer.Serialize(flow, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        int count = 1;
        foreach (var p in flow.Parts)
        {
            json = json.Replace(p.Uid.ToString(), $"00000000-0000-0000-0000-{count:000000000000}");
            ++count;
        }

        json = json.Replace("OutputConnections", "connections");
        json = json.Replace("InputNode", "node");
        json = Regex.Replace(json, "\"FlowElementUid\": \"([\\w]+\\.)*([\\w]+)\"", "\"node\": \"$2\"");
        json = Regex.Replace(json,
            "\"(Icon|Label|Inputs|Template|Type|Enabled|DateCreated|DateModified)\": [^,}]+,[\\s]*", string.Empty);

        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
        return File(data, "application/octet-stream", flow.Name + ".json");
    }

    /// <summary>
    /// Sets the enabled state of a flow
    /// </summary>
    /// <param name="uid">The flow UID</param>
    /// <param name="enable">Whether or not the flow should be enabled</param>
    /// <returns>The updated flow</returns>
    [HttpPut("state/{uid}")]
    public async Task<Flow> SetState([FromRoute] Guid uid, [FromQuery] bool? enable)
    {
        var flow = await GetByUid(uid);
        if (flow == null)
            throw new Exception("Flow not found.");
        if (enable != null)
        {
            flow.Enabled = enable.Value;
            await DbHelper.Update(flow);
        }

        return flow;
    }

    /// <summary>
    /// Delete flows from the system
    /// </summary>
    /// <param name="model">A reference model containing UIDs to delete</param>
    /// <returns>an awaited task</returns>
    [HttpDelete]
    public async Task Delete([FromBody] ReferenceModel model)
    {
        if (model == null || model.Uids?.Any() != true)
            return; // nothing to delete
        await DeleteAll(model);
    }

    /// <summary>
    /// Get a flow
    /// </summary>
    /// <param name="uid">The Flow UID</param>
    /// <returns>The flow instance</returns>
    [HttpGet("{uid}")]
    public async Task<Flow> Get(Guid uid)
    {
        if (uid != Guid.Empty)
        {

            var flow = await GetByUid(uid);
            if (flow == null)
                return flow;

            var elements = await GetElements();

            var scripts = (await new ScriptController().GetAll()).ToDictionary(x => x.Uid.ToString(), x => x.Name);
            foreach (var p in flow.Parts)
            {
                if (p.Type == FlowElementType.Script && string.IsNullOrWhiteSpace(p.Name))
                {
                    string feUid = p.FlowElementUid[7..43];
                    // set the name to the script name
                    if (scripts.ContainsKey(feUid))
                        p.Name = scripts[feUid];
                    else
                        p.Name = "Missing Script";
                }

                if (p.FlowElementUid.EndsWith("." + p.Name))
                    p.Name = string.Empty;
                string icon =
                    elements?.Where(x => x.Uid == p.FlowElementUid)?.Select(x => x.Icon)?.FirstOrDefault() ??
                    string.Empty;
                if (string.IsNullOrEmpty(icon) == false)
                    p.Icon = icon;
                p.Label = Translater.TranslateIfHasTranslation(
                    $"Flow.Parts.{p.FlowElementUid.Substring(p.FlowElementUid.LastIndexOf(".") + 1)}.Label",
                    string.Empty);
            }

            return flow;
        }
        else
        {
            // create default flow
            IEnumerable<string> flowNames = await GetNames();
            Flow flow = new Flow();
            flow.Parts = new();
            flow.Name = "New Flow";
            flow.Enabled = true;
            int count = 0;
            while (flowNames.Contains(flow.Name))
            {
                flow.Name = "New Flow " + (++count);
            }

            // try find basic node
            var elements = await GetElements();
            var info = elements.Where(x => x.Uid == "FileFlows.BasicNodes.File.InputFile").FirstOrDefault();
            if (info != null && string.IsNullOrEmpty(info.Name) == false)
            {
                flow.Parts.Add(new FlowPart
                {
                    Name = "InputFile",
                    xPos = DEFAULT_XPOS,
                    yPos = DEFAULT_YPOS,
                    Uid = Guid.NewGuid(),
                    Type = FlowElementType.Input,
                    Outputs = 1,
                    FlowElementUid = info.Name,
                    Icon = "far fa-file"
                });
            }

            return flow;
        }
    }


    /// <summary>
    /// Gets all nodes in the system
    /// </summary>
    /// <returns>Returns a list of all the nodes in the system</returns>
    [HttpGet("elements")]
    public async Task<FlowElement[]> GetElements(FlowType type = FlowType.Standard)
    {
        var plugins = await new PluginController().GetAll(includeElements: true);
        var results = plugins.Where(x => x.Elements != null).SelectMany(x => x.Elements)?.Where(x =>
        {
            if ((int)type == -1) // special case used by get variables, we want everything
                return true;
            if (type == FlowType.Failure)
            {
                if (x.FailureNode == false)
                    return false;
            }
            else if (x.Type == FlowElementType.Failure)
            {
                return false;
            }

            return true;
        })?.ToList();

        // get scripts 
        var scripts = (await new ScriptController().GetAll())?
            .Select(x => ScriptToFlowElement(x))
            .Where(x => x != null)
            .OrderBy(x => x.Name); // can be null if failed to parse
        results.AddRange(scripts);

        return results?.ToArray() ?? new FlowElement[] { };
    }

    /// <summary>
    /// Converts a script into a flow element
    /// </summary>
    /// <param name="script"></param>
    /// <returns></returns>
    private FlowElement ScriptToFlowElement(Script script)
    {
        try
        {
            var sm = new ScriptParser().Parse(script?.Name, script?.Code);
            FlowElement ele = new FlowElement();
            ele.Name = script.Name;
            ele.Uid = $"Script:{script.Uid}:{script.Name}";
            ele.Icon = "fas fa-scroll";
            ele.Inputs = 1;
            ele.Description = sm.Description;
            ele.OutputLabels = sm.Outputs.Select(x => x.Description).ToList();
            int count = 0;
            IDictionary<string, object> model = new ExpandoObject()!;
            ele.Fields = sm.Parameters.Select(x =>
            {
                ElementField ef = new ElementField();
                ef.InputType = x.Type switch
                {
                    ScriptArgumentType.Bool => FormInputType.Switch,
                    ScriptArgumentType.Int => FormInputType.Int,
                    ScriptArgumentType.String => FormInputType.Text,
                    _ => throw new ArgumentOutOfRangeException()
                };
                ef.Name = x.Name;
                ef.Order = ++count;
                ef.Description = x.Description;
                model.Add(ef.Name, x.Type switch
                {
                    ScriptArgumentType.Bool => false,
                    ScriptArgumentType.Int => 0,
                    ScriptArgumentType.String => string.Empty,
                    _ => null
                });
                return ef;
            }).ToList();
            ele.Group = "Scripts";
            ele.Type = FlowElementType.Script;
            ele.Outputs = sm.Outputs.Count;
            ele.Model = model as ExpandoObject;
            return ele;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves a flow
    /// </summary>
    /// <param name="model">The flow being saved</param>
    /// <param name="uniqueName">Whether or not a new unique name should be generated if the name already exists</param>
    /// <returns>The saved flow</returns>
    [HttpPut]
    public async Task<Flow> Save([FromBody] Flow model, [FromQuery] bool uniqueName = false)
    {
        if (model == null)
            throw new Exception("No model");

        if (string.IsNullOrWhiteSpace(model.Name))
            throw new Exception("ErrorMessages.NameRequired");
        model.Name = model.Name.Trim();
        if (uniqueName == false)
        {
            bool inUse = await NameInUse(model.Uid, model.Name);
            if (inUse)
                throw new Exception("ErrorMessages.NameInUse");
        }
        else
        {
            model.Name = await GetNewUniqueName(model.Name);
        }

        if (model.Parts?.Any() != true)
            throw new Exception("Flow.ErrorMessages.NoParts");

        foreach (var p in model.Parts)
        {
            if (Guid.TryParse(p.Name, out Guid guid))
                p.Name = string.Empty; // fixes issue with Scripts being saved as the Guids
            if (string.IsNullOrEmpty(p.Name))
                continue;
            if (p.FlowElementUid.ToLower().EndsWith("." + p.Name.Replace(" ", "").ToLower()))
                p.Name = string.Empty; // fixes issue with flow part being named after the display
        }

        int inputNodes = model.Parts
            .Where(x => x.Type == FlowElementType.Input || x.Type == FlowElementType.Failure).Count();
        if (inputNodes == 0)
            throw new Exception("Flow.ErrorMessages.NoInput");
        else if (inputNodes > 1)
            throw new Exception("Flow.ErrorMessages.TooManyInputNodes");

        return await Update(model);
    }

    /// <summary>
    /// Rename a flow
    /// </summary>
    /// <param name="uid">The Flow UID</param>
    /// <param name="name">The new name</param>
    /// <returns>an awaited task</returns>
    [HttpPut("{uid}/rename")]
    public async Task Rename([FromRoute] Guid uid, [FromQuery] string name)
    {

        if (uid == Guid.Empty)
            return; // renaming a new flow


        var flow = await Get(uid);
        if (flow == null)
            throw new Exception("Flow not found");
        if (flow.Name == name)
            return; // name already is the requested name

        flow.Name = name;
        await base.Update(flow);

        // update any object references
        await new LibraryFileController().UpdateFlowName(flow.Uid, flow.Name);
        var libraries = new LibraryController().UpdateFlowName(flow.Uid, flow.Name);
    }

    /// <summary>
    /// Get variables for flow parts
    /// </summary>
    /// <param name="flowParts">The flow parts</param>
    /// <param name="partUid">The specific part UID</param>
    /// <param name="isNew">If the flow part is a new part</param>
    /// <returns>The available variables for the flow part</returns>
    [HttpPost("{uid}/variables")]
    public async Task<Dictionary<string, object>> GetVariables([FromBody] List<FlowPart> flowParts,
        [FromRoute(Name = "uid")] Guid partUid, [FromQuery] bool isNew = false)
    {
        var variables = new Dictionary<string, object>();
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        bool dir = flowParts?.Any(x => x.FlowElementUid.EndsWith("InputDirectory")) == true;

        if (dir)
        {
            variables.Add("folder.Name", "FolderName");
            variables.Add("folder.FullName", windows ? @"C:\Folder\SubFolder" : "/folder/subfolder");
            variables.Add("folder.Date", DateTime.Now);
            variables.Add("folder.Date.Day", DateTime.Now.Day);
            variables.Add("folder.Date.Month", DateTime.Now.Month);
            variables.Add("folder.Date.Year", DateTime.Now.Year);
            variables.Add("folder.OrigName", "FolderOriginalName");
            variables.Add("folder.OrigFullName",
                windows ? @"C:\OriginalFolder\SubFolder" : "/originalFolder/subfolder");
        }
        else
        {
            variables.Add("ext", ".mkv");
            variables.Add("file.Name", "Filename");
            variables.Add("file.Extension", ".mkv");
            variables.Add("file.Size", 1000);
            variables.Add("file.FullName",
                windows ? @"C:\Folder\temp\randomfile.ext" : "/media/temp/randomfile.ext");
            variables.Add("file.Orig.Extension", ".mkv");
            variables.Add("file.Orig.FileName", "OriginalFile");
            variables.Add("file.Orig.FullName",
                windows ? @"C:\Folder\files\filename.ext" : "/media/files/filename.ext");
            variables.Add("file.Orig.Size", 1000);

            variables.Add("file.Create", DateTime.Now);
            variables.Add("file.Create.Day", DateTime.Now.Day);
            variables.Add("file.Create.Month", DateTime.Now.Month);
            variables.Add("file.Create.Year", DateTime.Now.Year);
            variables.Add("file.Modified", DateTime.Now);
            variables.Add("file.Modified.Day", DateTime.Now.Day);
            variables.Add("file.Modified.Month", DateTime.Now.Month);
            variables.Add("file.Modified.Year", DateTime.Now.Year);

            variables.Add("folder.Name", "FolderName");
            variables.Add("folder.FullName", windows ? @"C:\Folder\SubFolder" : "/folder/subfolder");
            variables.Add("folder.Orig.Name", "FolderOriginalName");
            variables.Add("folder.Orig.FullName",
                windows ? @"C:\OriginalFolder\SubFolder" : "/originalFolder/subfolder");
        }

        //p.FlowElementUid == FileFlows.VideoNodes.DetectBlackBars
        var flowElements = await GetElements((FlowType)(-1));
        flowElements ??= new FlowElement[] { };
        var dictFlowElements = flowElements.ToDictionary(x => x.Uid, x => x);

        if (isNew)
        {
            // we add all variables on new, so they can hook up a connection easily
            foreach (var p in flowParts ?? new List<FlowPart>())
            {
                if (dictFlowElements.ContainsKey(p.FlowElementUid) == false)
                    continue;
                var partVariables = dictFlowElements[p.FlowElementUid].Variables ??
                                    new Dictionary<string, object>();
                foreach (var pv in partVariables)
                {
                    if (variables.ContainsKey(pv.Key) == false)
                        variables.Add(pv.Key, pv.Value);
                }
            }

            return variables;
        }

        // get the connected nodes to this part
        var part = flowParts?.Where(x => x.Uid == partUid)?.FirstOrDefault();
        if (part == null)
            return variables;

        List<FlowPart> checkedParts = new List<FlowPart>();

        var parentParts = FindParts(part, 0);
        if (parentParts.Any() == false)
            return variables;

        foreach (var p in parentParts)
        {
            if (dictFlowElements.ContainsKey(p.FlowElementUid) == false)
                continue;

            var partVariables = dictFlowElements[p.FlowElementUid].Variables ?? new Dictionary<string, object>();
            foreach (var pv in partVariables)
            {
                if (variables.ContainsKey(pv.Key) == false)
                    variables.Add(pv.Key, pv.Value);
            }
        }

        return variables;

        List<FlowPart> FindParts(FlowPart part, int depth)
        {
            List<FlowPart> results = new List<FlowPart>();
            if (depth > 30)
                return results; // prevent infinite recursion

            foreach (var p in flowParts ?? new List<FlowPart>())
            {
                if (checkedParts.Contains(p) || p == part)
                    continue;

                if (p.OutputConnections?.Any() != true)
                {
                    checkedParts.Add(p);
                    continue;
                }

                if (p.OutputConnections.Any(x => x.InputNode == part.Uid))
                {
                    results.Add(p);
                    if (checkedParts.Contains(p))
                        continue;
                    checkedParts.Add(p);
                    results.AddRange(FindParts(p, ++depth));
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Gets all the flow template files
    /// </summary>
    /// <returns>a array of all flow template files</returns>
    private FileInfo[] GetTemplateFiles() => new DirectoryInfo("Templates/FlowTemplates").GetFiles("*.json");

    /// <summary>
    /// Get flow templates
    /// </summary>
    /// <returns>A list of flow templates</returns>
    [HttpGet("templates")]
    public async Task<IDictionary<string, List<FlowTemplateModel>>> GetTemplates()
    {
        var elements = await GetElements((FlowType)(-1)); // special case to load all template typs
        var parts = elements.ToDictionary(x => x.Uid.Substring(x.Uid.LastIndexOf(".") + 1), x => x);

        SortedDictionary<string, List<FlowTemplateModel>> templates = new();
        string group = string.Empty;
        templates.Add(group, new List<FlowTemplateModel>());
        foreach (var tf in GetTemplateFiles())
        {
            try
            {
                string json = System.IO.File.ReadAllText(tf.FullName);
                json = TemplateHelper.ReplaceWindowsPathIfWindows(json);
                var jsTemplates = JsonSerializer.Deserialize<FlowTemplate[]>(json, new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                }) ?? new FlowTemplate[] { };
                foreach (var _jst in jsTemplates)
                {
                    try
                    {
                        var jstJson = JsonSerializer.Serialize(_jst);
                        List<TemplateField> fields = _jst.Fields ?? new List<TemplateField>();
                        // replace all the guids with unique guides
                        for (int i = 1; i < 50; i++)
                        {
                            Guid oldUid = new Guid("00000000-0000-0000-0000-0000000000" + (i < 10 ? "0" : "") + i);
                            Guid newUid = Guid.NewGuid();
                            foreach (var field in fields)
                            {
                                if (field.Uid == oldUid)
                                    field.Uid = newUid;
                            }

                            jstJson = jstJson.Replace(oldUid.ToString(), newUid.ToString());
                        }

                        var jst = JsonSerializer.Deserialize<FlowTemplate>(jstJson);

                        List<FlowPart> flowParts = new List<FlowPart>();
                        int y = DEFAULT_YPOS;
                        bool invalid = false;
                        foreach (var jsPart in jst.Parts)
                        {
                            if (jsPart.Node == null || parts.ContainsKey(jsPart.Node) == false)
                            {
                                invalid = true;
                                break;
                            }

                            var element = parts[jsPart.Node];

                            flowParts.Add(new FlowPart
                            {
                                yPos = jsPart.yPos ?? y,
                                xPos = jsPart.xPos ?? DEFAULT_XPOS,
                                FlowElementUid = element.Uid,
                                Outputs = jsPart.Outputs ?? element.Outputs,
                                Inputs = element.Inputs,
                                Type = element.Type,
                                Name = jsPart.Name ?? string.Empty,
                                Uid = jsPart.Uid,
                                Icon = element.Icon,
                                Model = jsPart.Model,
                                OutputConnections = jsPart.Connections?.Select(x => new FlowConnection
                                {
                                    Input = x.Input,
                                    Output = x.Output,
                                    InputNode = x.Node
                                }).ToList() ?? new List<FlowConnection>()
                            });
                            y += 150;
                        }

                        if (invalid)
                            continue;

                        if (templates.ContainsKey(_jst.Group ?? String.Empty) == false)
                            templates.Add(_jst.Group ?? String.Empty, new List<FlowTemplateModel>());

                        templates[_jst.Group ?? String.Empty].Add(new FlowTemplateModel
                        {
                            Fields = fields,
                            Order = _jst.Order,
                            Save = jst.Save,
                            Type = jst.Type,
                            Flow = new Flow
                            {
                                Name = jst.Name,
                                Template = jst.Name,
                                Enabled = true,
                                Description = jst.Description,
                                Parts = flowParts
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.ELog("Template: " + _jst.Name);
                        Logger.Instance.ELog("Error reading template: " + ex.Message + Environment.NewLine +
                                             ex.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.ELog("Error reading template: " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        foreach (var gn in templates.Keys.ToArray())
        {
            templates[gn] = templates[gn].OrderBy(x =>
            {
                if (x.Order == null)
                    return int.MaxValue;
                return x.Order;
            }).ThenBy(x => x.Flow.Name).ToList();
        }

        return templates;
    }
}