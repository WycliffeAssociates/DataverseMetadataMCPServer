using System.ComponentModel;
using System.Xml.Linq;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using ModelContextProtocol.Server;

namespace DataverseMetadataMCPServer.Tools;

public class DataverseMetadataTools
{
    private readonly IOrganizationServiceAsync _service;
    public DataverseMetadataTools(IOrganizationServiceAsync service)
    {
        _service = service;
    }
    
    [McpServerTool]
    [Description("Gets a list of all entities in the Dataverse environment with their display names.")]
    public async Task<Dictionary<string,string>> GetListOfEntitiesAsync()
    {
        var metadataRetriever = new RetrieveAllEntitiesRequest()
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = true,
        };
        var response = (RetrieveAllEntitiesResponse)await _service.ExecuteAsync(metadataRetriever);
        return response.EntityMetadata.ToDictionary(i => i.LogicalName , i => i.DisplayName?.UserLocalizedLabel?.Label ?? i.LogicalName);
    }
    [McpServerTool]
    [Description("Gets metadata for all attributes of a specified entity.")]
    public async Task<List<AttributeMetadataInfo>> GetEntityMetadataAsync([Description("The logical name of the entity")]string logicalEntityName)
    {
        var metadataRetriever = new RetrieveEntityRequest()
        {
            EntityFilters = EntityFilters.All,
            LogicalName = logicalEntityName,
            RetrieveAsIfPublished = true
        };
        var response = (RetrieveEntityResponse)await _service.ExecuteAsync(metadataRetriever);
        var entityMetadata = response.EntityMetadata;
        var output = await ConvertAttributeMetadataInfoAsync(entityMetadata);

        return output;
    }

    [McpServerTool]
    [Description("Gets the list of forms defined for an entity (table). Use this to discover which forms exist before retrieving the fields and event handlers on a specific form with GetFormDetailsAsync.")]
    public async Task<List<FormInfo>> GetEntityFormsAsync([Description("The logical name of the entity (table), e.g. \"account\"")] string logicalEntityName)
    {
        var query = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet("formid", "name", "objecttypecode", "type", "ismanaged"),
            Criteria = new FilterExpression()
        };
        query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, logicalEntityName);

        var response = await _service.RetrieveMultipleAsync(query);
        return response.Entities
            .Select(form => new FormInfo
            {
                FormId = form.Id.ToString(),
                Name = form.GetAttributeValue<string>("name"),
                EntityLogicalName = form.GetAttributeValue<string>("objecttypecode"),
                FormType = DescribeFormType(form.GetAttributeValue<OptionSetValue>("type")?.Value),
                IsManaged = form.GetAttributeValue<bool>("ismanaged")
            })
            .ToList();
    }

    [McpServerTool]
    [Description("Gets the fields, web resource libraries, and event handlers configured on a single form by parsing its form XML. The event handlers reference the web resource library (libraryName) and function (functionName) they call, and fields list the columns actually placed on the form. Use this to match the fields on a form and the JavaScript functions wired to events against the web resources that should contain them.")]
    public async Task<FormDetails> GetFormDetailsAsync([Description("The form id (GUID) returned by GetEntityFormsAsync")] string formId)
    {
        var form = await _service.RetrieveAsync(
            "systemform",
            Guid.Parse(formId),
            new ColumnSet("formid", "name", "objecttypecode", "type", "formxml"));

        var details = new FormDetails
        {
            FormId = form.Id.ToString(),
            Name = form.GetAttributeValue<string>("name"),
            EntityLogicalName = form.GetAttributeValue<string>("objecttypecode"),
            FormType = DescribeFormType(form.GetAttributeValue<OptionSetValue>("type")?.Value)
        };

        var formXml = form.GetAttributeValue<string>("formxml");
        if (string.IsNullOrEmpty(formXml))
        {
            return details;
        }

        var root = XDocument.Parse(formXml);

        // Web resource libraries referenced by the form (the JavaScript files it loads).
        details.FormLibraries = DescendantsCi(root, "Library")
            .Select(lib => AttributeCi(lib, "name"))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Fields actually placed on the form (controls bound to a column via datafieldname).
        details.Fields = DescendantsCi(root, "control")
            .Where(control => !string.IsNullOrEmpty(AttributeCi(control, "datafieldname")))
            .Select(control => new FormFieldInfo
            {
                DataFieldName = AttributeCi(control, "datafieldname"),
                ControlId = AttributeCi(control, "id"),
                ControlClassId = AttributeCi(control, "classid")
            })
            .ToList();

        // Event handlers wired to form and column events, with the web resource + function they call.
        foreach (var eventElement in DescendantsCi(root, "event"))
        {
            var eventName = AttributeCi(eventElement, "name");
            var attribute = AttributeCi(eventElement, "attribute");
            foreach (var handler in DescendantsCi(eventElement, "Handler"))
            {
                details.EventHandlers.Add(new FormEventHandlerInfo
                {
                    EventName = eventName,
                    Attribute = attribute,
                    FunctionName = AttributeCi(handler, "functionName"),
                    LibraryName = AttributeCi(handler, "libraryName"),
                    Enabled = !string.Equals(AttributeCi(handler, "enabled"), "false", StringComparison.OrdinalIgnoreCase),
                    PassExecutionContext = string.Equals(AttributeCi(handler, "passExecutionContext"), "true", StringComparison.OrdinalIgnoreCase),
                    Parameters = AttributeCi(handler, "parameters")
                });
            }
        }

        return details;
    }

    /// <summary>Returns descendant elements matching a local name case-insensitively (form XML element casing is inconsistent, e.g. lowercase "control"/"event" but PascalCase "Handler"/"Library").</summary>
    private static IEnumerable<XElement> DescendantsCi(XContainer root, string localName)
        => root.Descendants().Where(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns an attribute value matching a name case-insensitively, or null if absent.</summary>
    private static string? AttributeCi(XElement element, string name)
        => element.Attributes()
            .FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    /// <summary>Maps the systemform "type" option set value to a human-readable form type.</summary>
    private static string DescribeFormType(int? type) => type switch
    {
        0 => "Dashboard",
        1 => "AppointmentBook",
        2 => "Main",
        5 => "MobileExpress",
        6 => "QuickViewForm",
        7 => "QuickCreate",
        8 => "Dialog",
        9 => "TaskFlowForm",
        10 => "InteractionCentricDashboard",
        11 => "Card",
        12 => "MainInteractiveExperience",
        100 => "Other",
        101 => "MainBackup",
        102 => "AppointmentBookBackup",
        103 => "PowerBIDashboard",
        null => "Unknown",
        _ => $"Unknown ({type})"
    };

    private async Task<List<AttributeMetadataInfo>> ConvertAttributeMetadataInfoAsync(EntityMetadata entityMetadata)
    {
        var output = new List<AttributeMetadataInfo>();
        foreach (var item in entityMetadata.Attributes)
        {
            var attributeInfo = new AttributeMetadataInfo
            {
                LogicalName = item.LogicalName,
                DisplayName = item.DisplayName?.UserLocalizedLabel?.Label,
                AttributeType = item.AttributeType.ToString(),
            };
            if (item is LookupAttributeMetadata lookupAttribute)
            {
                attributeInfo.LookupTargetEntity = string.Join(",", lookupAttribute.Targets);
            }
            if (item is PicklistAttributeMetadata picklistAttribute)
            {
                SetOptionSetSource(attributeInfo, picklistAttribute.OptionSet);
                attributeInfo.OptionSetValues = await GetPicklistOptionSetValuesAsync(picklistAttribute.OptionSet) ?? new Dictionary<int, string>();
            }
            if (item is MultiSelectPicklistAttributeMetadata multiSelectPicklistAttribute)
            {
                SetOptionSetSource(attributeInfo, multiSelectPicklistAttribute.OptionSet);
                attributeInfo.OptionSetValues = await GetPicklistOptionSetValuesAsync(multiSelectPicklistAttribute.OptionSet) ?? new Dictionary<int, string>();
            }
            output.Add(attributeInfo);
        }

        return output;
    }

    /// <summary>
    ///  Records whether a picklist attribute uses a global option set, and if so which one
    /// </summary>
    private static void SetOptionSetSource(AttributeMetadataInfo attributeInfo, OptionSetMetadata? optionSet)
    {
        if (optionSet == null)
        {
            return;
        }

        attributeInfo.IsGlobalOptionSet = optionSet.IsGlobal == true;
        attributeInfo.GlobalOptionSetName = attributeInfo.IsGlobalOptionSet == true ? optionSet.Name : null;
    }

    /// <summary>
    ///  Gets the option set values for a picklist attribute
    /// </summary>
    /// <param name="pickListOptions">A picklist option to get data for</param>
    /// <returns>A dictionary of optionsetvalue to label</returns>
    /// <remarks>
    /// This method handles both global and local option sets
    /// </remarks>
    private async Task<Dictionary<int,string>?> GetPicklistOptionSetValuesAsync(OptionSetMetadata? pickListOptions)
    {
        
        if (pickListOptions == null)
        {
            return null;
        }

        if (pickListOptions.IsGlobal == false)
        {
            return CreateOptionSetDictionary(pickListOptions);
        }
        
        // This is a global option set so we need to retrieve it separately
        var retrieveReq = new RetrieveOptionSetRequest { Name = pickListOptions.Name };
        var retrieveResp = (RetrieveOptionSetResponse)await _service.ExecuteAsync(retrieveReq);
        var optionSet = retrieveResp.OptionSetMetadata as OptionSetMetadata;
        return CreateOptionSetDictionary(optionSet);
    }

    private static Dictionary<int, string>? CreateOptionSetDictionary(OptionSetMetadata? optionSet)
    {
        if (optionSet == null)
        {
            return null;
        }
        var dict = new Dictionary<int, string>();
        foreach (var option in optionSet.Options)
        {
            var label = option.Label?.UserLocalizedLabel?.Label;
            if (!string.IsNullOrEmpty(label))
            {
                dict[option.Value ?? 0] = label;
            }
        }
        return dict;
    }
}

public class AttributeMetadataInfo
{
    public string? LogicalName { get; set; }
    public string? DisplayName { get; set; }
    public string? AttributeType { get; set; }
    public string? LookupTargetEntity { get; set; }

    /// <summary>For picklist/multi-select columns, whether the options come from a global (shared) option set; null for other column types.</summary>
    public bool? IsGlobalOptionSet { get; set; }

    /// <summary>The name of the global option set the column uses; null when the option set is local or the column is not a picklist.</summary>
    public string? GlobalOptionSetName { get; set; }

    public Dictionary<int, string>? OptionSetValues { get; set; }
}

public class FormInfo
{
    public string? FormId { get; set; }
    public string? Name { get; set; }
    public string? EntityLogicalName { get; set; }
    public string? FormType { get; set; }
    public bool IsManaged { get; set; }
}

public class FormDetails
{
    public string? FormId { get; set; }
    public string? Name { get; set; }
    public string? EntityLogicalName { get; set; }
    public string? FormType { get; set; }

    /// <summary>Web resource (JavaScript) libraries the form loads.</summary>
    public List<string> FormLibraries { get; set; } = new();

    /// <summary>Columns bound to controls placed on the form.</summary>
    public List<FormFieldInfo> Fields { get; set; } = new();

    /// <summary>Functions wired to form and column events, with the web resource library they live in.</summary>
    public List<FormEventHandlerInfo> EventHandlers { get; set; } = new();
}

public class FormFieldInfo
{
    public string? DataFieldName { get; set; }
    public string? ControlId { get; set; }
    public string? ControlClassId { get; set; }
}

public class FormEventHandlerInfo
{
    /// <summary>The event the handler is attached to, e.g. onload, onsave, onchange.</summary>
    public string? EventName { get; set; }

    /// <summary>For column events (e.g. onchange) the column the event fires on; null for form-level events.</summary>
    public string? Attribute { get; set; }

    /// <summary>The function called when the event fires, e.g. "MyNamespace.onLoad".</summary>
    public string? FunctionName { get; set; }

    /// <summary>The web resource library the function is expected to live in, e.g. "new_myscript.js".</summary>
    public string? LibraryName { get; set; }

    public bool Enabled { get; set; }
    public bool PassExecutionContext { get; set; }
    public string? Parameters { get; set; }
}
