using System.ComponentModel;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
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
                attributeInfo.OptionSetValues = await GetPicklistOptionSetValuesAsync(picklistAttribute.OptionSet) ?? new Dictionary<int, string>();
            }
            if (item is MultiSelectPicklistAttributeMetadata multiSelectPicklistAttribute)
            {
                attributeInfo.OptionSetValues = await GetPicklistOptionSetValuesAsync(multiSelectPicklistAttribute.OptionSet) ?? new Dictionary<int, string>();
            }
            output.Add(attributeInfo);
        }

        return output;
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
    public Dictionary<int, string>? OptionSetValues { get; set; }
}
