# DataverseMetadataMCPServer

An MCP (Model Context Protocol) server that provides tools for querying Microsoft Dataverse/Power Platform metadata. This server allows AI assistants like GitHub Copilot to interact with Dataverse environments to retrieve entity and attribute metadata.

## Overview

This MCP server exposes Dataverse metadata through a standardized protocol, enabling AI-powered tools to:
- List all entities in a Dataverse environment
- Retrieve detailed metadata for specific entities including attributes, types, and option sets
- Access lookup relationships and picklist values, including which picklists use a global option set
- List the forms on an entity and inspect the fields, web resource libraries, and event handlers configured on a form (useful for matching event handler functions to web resources)

## Prerequisites

- **.NET 8.0 SDK** or later (for building from source)
- **Dataverse environment** with appropriate access credentials
- **Dataverse connection string** - See [Microsoft documentation](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect) for connection string format

## Building the Project

### Build from Source

```bash
# Clone the repository
git clone https://github.com/WycliffeAssociates/DataverseMetadataMCPServer.git
cd DataverseMetadataMCPServer

# Build the project
dotnet build

# Build for release
dotnet build -c Release
```

### Create NuGet Package

```bash
# Pack the project (creates .nupkg file in bin/Release)
dotnet pack -c Release
```

The project is configured to build self-contained executables for multiple platforms:
- `win-x64` - Windows 64-bit
- `win-arm64` - Windows ARM64
- `osx-arm64` - macOS ARM64 (Apple Silicon)
- `linux-x64` - Linux 64-bit
- `linux-arm64` - Linux ARM64
- `linux-musl-x64` - Linux musl-based (Alpine, etc.)

## Running the Server

### Environment Variables

The server requires a Dataverse connection string to be set as an environment variable:

```bash
# Set the connection string environment variable
export DATAVERSE_CONNECTION_STRING="AuthType=OAuth;Username=youruser@yourdomain.com;Password=yourpassword;Url=https://yourorg.crm.dynamics.com;AppId=your-app-id-guid;RedirectUri=app://your-redirect-uri-guid;LoginPrompt=Auto"
```

**Connection String Components:**
- `AuthType` - Authentication type (OAuth, Office365, etc.)
- `Url` - Your Dataverse environment URL
- `Username` / `Password` - Credentials (if using Office365 auth)
- `AppId` - Azure AD application ID
- `RedirectUri` - OAuth redirect URI
- Additional parameters as needed for your authentication method

### Run from Source

```bash
# Run the server directly
dotnet run --project DataverseMetadataMCPServer/DataverseMetadataMCPServer.csproj
```

### Configure in VS Code

Create or update `.vscode/mcp.json` in your workspace:

```json
{
  "servers": {
    "DataverseMetadataMCPServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/DataverseMetadataMCPServer/DataverseMetadataMCPServer.csproj"
      ],
      "env": {
        "DATAVERSE_CONNECTION_STRING": "AuthType=OAuth;Username=youruser@yourdomain.com;..."
      }
    }
  }
}
```

### Configure in Visual Studio

Create `.mcp.json` in your solution directory:

```json
{
  "servers": {
    "DataverseMetadataMCPServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\DataverseMetadataMCPServer\\DataverseMetadataMCPServer.csproj"
      ],
      "env": {
        "DATAVERSE_CONNECTION_STRING": "AuthType=OAuth;Username=youruser@yourdomain.com;..."
      }
    }
  }
}
```

## Available Tools

The server provides the following MCP tools:

### 1. GetListOfEntitiesAsync

**Description:** Gets a list of all entities in the Dataverse environment with their display names.

**Parameters:** None

**Returns:** A dictionary mapping entity logical names to their display names.

**Example Usage:**
```
User: "Show me all the entities in the Dataverse environment"
AI: Uses GetListOfEntitiesAsync to retrieve and display the list
```

**Sample Output:**
```json
{
  "account": "Account",
  "contact": "Contact",
  "opportunity": "Opportunity",
  "lead": "Lead"
}
```

### 2. GetEntityMetadataAsync

**Description:** Gets detailed metadata for all attributes of a specified entity.

**Parameters:**
- `logicalEntityName` (string, required) - The logical name of the entity to retrieve metadata for

**Returns:** A list of `AttributeMetadataInfo` objects containing:
- `LogicalName` - The logical name of the attribute
- `DisplayName` - The user-friendly display name
- `AttributeType` - The data type of the attribute (String, Integer, Lookup, Picklist, etc.)
- `LookupTargetEntity` - For lookup fields, the target entity/entities
- `IsGlobalOptionSet` - For picklist/multi-select fields, `true` if the options come from a global (shared) option set, `false` for a local one; null for other field types
- `GlobalOptionSetName` - The name of the global option set the field uses; null when the option set is local or the field is not a picklist
- `OptionSetValues` - For picklist/multi-select fields, a dictionary of option values to labels

**Example Usage:**
```
User: "What are the attributes of the account entity?"
AI: Uses GetEntityMetadataAsync with logicalEntityName="account"
```

**Sample Output:**
```json
[
  {
    "LogicalName": "accountid",
    "DisplayName": "Account",
    "AttributeType": "Uniqueidentifier",
    "LookupTargetEntity": null,
    "IsGlobalOptionSet": null,
    "GlobalOptionSetName": null,
    "OptionSetValues": null
  },
  {
    "LogicalName": "name",
    "DisplayName": "Account Name",
    "AttributeType": "String",
    "LookupTargetEntity": null,
    "IsGlobalOptionSet": null,
    "GlobalOptionSetName": null,
    "OptionSetValues": null
  },
  {
    "LogicalName": "primarycontactid",
    "DisplayName": "Primary Contact",
    "AttributeType": "Lookup",
    "LookupTargetEntity": "contact",
    "IsGlobalOptionSet": null,
    "GlobalOptionSetName": null,
    "OptionSetValues": null
  },
  {
    "LogicalName": "accountcategorycode",
    "DisplayName": "Category",
    "AttributeType": "Picklist",
    "LookupTargetEntity": null,
    "IsGlobalOptionSet": false,
    "GlobalOptionSetName": null,
    "OptionSetValues": {
      "1": "Preferred Customer",
      "2": "Standard"
    }
  },
  {
    "LogicalName": "new_region",
    "DisplayName": "Region",
    "AttributeType": "Picklist",
    "LookupTargetEntity": null,
    "IsGlobalOptionSet": true,
    "GlobalOptionSetName": "new_region",
    "OptionSetValues": {
      "100000000": "North",
      "100000001": "South"
    }
  }
]
```

### 3. GetEntityFormsAsync

**Description:** Gets the list of forms defined for an entity (table). Use this to discover which forms exist before retrieving the fields and event handlers on a specific form.

**Parameters:**
- `logicalEntityName` (string, required) - The logical name of the entity (table), e.g. `account`

**Returns:** A list of `FormInfo` objects containing:
- `FormId` - The GUID of the form (pass this to `GetFormDetailsAsync`)
- `Name` - The form name
- `EntityLogicalName` - The entity the form belongs to
- `FormType` - The form type (Main, QuickCreate, QuickViewForm, Card, etc.)
- `IsManaged` - Whether the form comes from a managed solution

**Example Usage:**
```
User: "What forms does the account table have?"
AI: Uses GetEntityFormsAsync with logicalEntityName="account"
```

**Sample Output:**
```json
[
  {
    "FormId": "00000000-0000-0000-0000-000000000001",
    "Name": "Account",
    "EntityLogicalName": "account",
    "FormType": "Main",
    "IsManaged": true
  }
]
```

### 4. GetFormDetailsAsync

**Description:** Gets the fields, web resource libraries, and event handlers configured on a single form by parsing its form XML. Event handlers reference the web resource library (`LibraryName`) and function (`FunctionName`) they call, and fields list the columns actually placed on the form. Use this to match the fields on a form and the JavaScript functions wired to events against the web resources that should contain them.

**Parameters:**
- `formId` (string, required) - The form GUID returned by `GetEntityFormsAsync`

**Returns:** A `FormDetails` object containing:
- `FormId`, `Name`, `EntityLogicalName`, `FormType` - Form identity
- `FormLibraries` - The web resource (JavaScript) libraries the form loads
- `Fields` - The columns bound to controls placed on the form, each with `DataFieldName`, `ControlId`, and `ControlClassId`
- `EventHandlers` - The functions wired to form and column events, each with:
  - `EventName` - The event (e.g. `onload`, `onsave`, `onchange`)
  - `Attribute` - For column events (e.g. `onchange`), the column the event fires on; null for form-level events
  - `FunctionName` - The function called, e.g. `MyNamespace.onLoad`
  - `LibraryName` - The web resource the function is expected to live in, e.g. `new_myscript.js`
  - `Enabled`, `PassExecutionContext`, `Parameters`

**Example Usage:**
```
User: "What fields and event handlers are on the main account form?"
AI: Uses GetEntityFormsAsync to find the form id, then GetFormDetailsAsync with that formId
```

**Sample Output:**
```json
{
  "FormId": "00000000-0000-0000-0000-000000000001",
  "Name": "Account",
  "EntityLogicalName": "account",
  "FormType": "Main",
  "FormLibraries": ["new_account.js"],
  "Fields": [
    { "DataFieldName": "name", "ControlId": "name", "ControlClassId": "{...}" },
    { "DataFieldName": "primarycontactid", "ControlId": "primarycontactid", "ControlClassId": "{...}" }
  ],
  "EventHandlers": [
    {
      "EventName": "onload",
      "Attribute": null,
      "FunctionName": "Account.Form.onLoad",
      "LibraryName": "new_account.js",
      "Enabled": true,
      "PassExecutionContext": true,
      "Parameters": ""
    },
    {
      "EventName": "onchange",
      "Attribute": "primarycontactid",
      "FunctionName": "Account.Form.onPrimaryContactChange",
      "LibraryName": "new_account.js",
      "Enabled": true,
      "PassExecutionContext": true,
      "Parameters": ""
    }
  ]
}
```

## Publishing to NuGet.org

1. **Update package metadata** in `DataverseMetadataMCPServer.csproj`:
   - Set `<PackageId>` to a unique name
   - Update `<PackageVersion>`
   - Add `<Authors>`, `<Company>`, etc.

2. **Pack the project:**
   ```bash
   dotnet pack -c Release
   ```

3. **Publish to NuGet.org:**
   ```bash
   dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json
   ```

4. **Configure clients to use published package:**
   ```json
   {
     "servers": {
       "DataverseMetadataMCPServer": {
         "type": "stdio",
         "command": "dnx",
         "args": [
           "DataverseMetadataMCPServer",
           "--version",
           "0.1.0-beta",
           "--yes"
         ],
         "env": {
           "DATAVERSE_CONNECTION_STRING": "..."
         }
       }
     }
   }
   ```

## Architecture

The server is built using:
- **.NET 8.0** - Modern, cross-platform runtime
- **ModelContextProtocol.Server.Stdio** - MCP SDK for .NET
- **Microsoft.PowerPlatform.Dataverse.Client** - Official Dataverse client library
- **Microsoft.Extensions.Hosting** - For application hosting and DI

The server uses standard input/output (stdio) for communication, making it compatible with any MCP-compliant client.

## Troubleshooting

### Connection Issues

If you encounter connection errors:
1. Verify your connection string is correct
2. Ensure you have appropriate permissions in the Dataverse environment
3. Check that network access to the Dataverse URL is available
4. Verify the AppId and RedirectUri are registered in Azure AD (for OAuth)

### Missing Environment Variable

If the server fails to start with "DATAVERSE_CONNECTION_STRING environment variable is not set":
- Ensure the environment variable is set in your shell or IDE configuration
- The variable must be available to the process running the server

## Additional Resources

- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Microsoft Dataverse Documentation](https://learn.microsoft.com/power-apps/developer/data-platform/)
- [Dataverse Connection Strings](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect)
- [Use MCP servers in VS Code](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio](https://learn.microsoft.com/visualstudio/ide/mcp-servers)

## License

See [LICENSE](LICENSE) file for details.
