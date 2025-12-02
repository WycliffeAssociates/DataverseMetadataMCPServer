# DataverseMetadataMCPServer

An MCP (Model Context Protocol) server that provides tools for querying Microsoft Dataverse/Power Platform metadata. This server allows AI assistants like GitHub Copilot to interact with Dataverse environments to retrieve entity and attribute metadata.

## Overview

This MCP server exposes Dataverse metadata through a standardized protocol, enabling AI-powered tools to:
- List all entities in a Dataverse environment
- Retrieve detailed metadata for specific entities including attributes, types, and option sets
- Access lookup relationships and picklist values

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
    "OptionSetValues": null
  },
  {
    "LogicalName": "name",
    "DisplayName": "Account Name",
    "AttributeType": "String",
    "LookupTargetEntity": null,
    "OptionSetValues": null
  },
  {
    "LogicalName": "primarycontactid",
    "DisplayName": "Primary Contact",
    "AttributeType": "Lookup",
    "LookupTargetEntity": "contact",
    "OptionSetValues": null
  },
  {
    "LogicalName": "accountcategorycode",
    "DisplayName": "Category",
    "AttributeType": "Picklist",
    "LookupTargetEntity": null,
    "OptionSetValues": {
      "1": "Preferred Customer",
      "2": "Standard"
    }
  }
]
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
