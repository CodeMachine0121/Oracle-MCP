# Oracle MCP 伺服器（可串流 HTTP）

以下說明以繁體中文提供，並保留英文版本於本文後半段。

此專案是一個以 C#/.NET 建立的 MCP（Model Context Protocol）伺服器，提供唯讀的 Oracle 資料庫工具，方便在支援 MCP 的 IDE 或代理（Agent）中查詢資料庫與探索結構。

本伺服器提供的工具（Tools）：
- `oracle_ping`：檢查與 Oracle 的連線是否正常
- `oracle_query`：執行唯讀 SQL（僅支援 `SELECT` 或 `WITH ... SELECT`），支援綁定參數
- `oracle_search_schema`：以關鍵字搜尋資料表/欄位名稱

---

## 快速開始（Quick Start）

### 1) 先決條件
- 目標執行環境需可取得 Oracle .NET 驅動程式：`Oracle.ManagedDataAccess`（本伺服器會在執行階段動態載入）
- 本專案會以 self-contained 方式發佈，目標機器不必安裝 .NET Runtime，但需針對目標平台分別建置

支援的 Runtime Identifier（預設）：
- `win-x64`, `win-arm64`, `osx-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`

如需更多平台，請在專案檔的 `<RuntimeIdentifiers />` 內增加對應 RID。

### 2) 設定環境變數
請在啟動 MCP 伺服器的進程上設定下列環境變數：
- `ORACLE_CONNECTION_STRING`（必要）：Oracle 連線字串（強烈建議使用唯讀帳號）。
  - 範例（EZCONNECT）：`User Id=readonly;Password=yourPwd;Data Source=HOSTNAME:1521/SERVICE_NAME`
- `ORACLE_COMMAND_TIMEOUT_SECONDS`（選用，預設 `30`）
- `ORACLE_DEFAULT_MAX_ROWS`（選用，預設 `200`）
- `ORACLE_MAX_MAX_ROWS`（選用，預設 `2000`）

啟動位址：預設監聽 `http://0.0.0.0:3001`，可用 `MCP_HTTP_URL` 覆蓋，例如：`http://localhost:3001`。

### 3) 本機執行（從原始碼啟動 HTTP 伺服器）
在專案根目錄執行：

```bash
MCP_HTTP_URL=http://localhost:3001 dotnet run --project Oracle-MCP/Oracle-MCP.csproj
```

### 4) 連線一個 MCP 客戶端
以 Codex CLI 為例：

```bash
codex --enable rmcp_client mcp add oracle --url http://localhost:3001
```

或在 IDE 中設定：
- VS Code：建立 `<工作區>/.vscode/mcp.json`
- Visual Studio：建立 `<方案目錄>/.mcp.json`

設定內容範例：

```json
{
  "servers": {
    "Oracle-MCP": {
      "type": "http",
      "url": "http://localhost:3001"
    }
  }
}
```

### 5) 在聊天/指令中使用工具（使用範例）
當客戶端已連線後，可在 Copilot Chat 或支援 MCP 的介面中下達指令，例如：
- `Ping the Oracle database`（測試連線）
- `Search schema for keyword USER`（以關鍵字搜尋結構）
- `Run: select * from SOME_TABLE where ID = :id with id=123`（使用綁定參數的唯讀查詢）

### 6) 發佈到 NuGet（選用）
1. 建置套件：`dotnet pack -c Release`
2. 發佈到 NuGet.org：
   ```bash
   dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json
   ```

### 7) 從 NuGet.org 使用（在 IDE 中安裝）
VS Code 與 Visual Studio 會使用 `dnx` 指令從 NuGet.org 下載並安裝 MCP 伺服器套件。
完成發佈後，依前述 IDE 設定檔將伺服器指向可連線的 HTTP URL 即可。

### 疑難排解（Troubleshooting）
- 連線錯誤：請先執行 `oracle_ping` 測試；確認 `ORACLE_CONNECTION_STRING` 正確，帳號具備唯讀權限，且網路/防火牆允許存取。
- 查詢逾時：調整 `ORACLE_COMMAND_TIMEOUT_SECONDS` 或縮小查詢範圍；必要時調整 `ORACLE_DEFAULT_MAX_ROWS`。
- 驅動載入問題：確認執行環境可取得 `Oracle.ManagedDataAccess`，並與目標平台相容。

---

# MCP Server (Streamable HTTP)

This README was created using the C# MCP server project template.
It demonstrates how you can easily create an MCP server using C# and publish it as a NuGet package.

This server provides read-only Oracle DB tools for agents:
- `oracle_ping`: verifies connectivity
- `oracle_query`: executes read-only SQL (SELECT / WITH ... SELECT) with bind parameters
- `oracle_search_schema`: searches tables/columns by keyword

## Configuration

Set the following environment variables for the MCP server process:
- `ORACLE_CONNECTION_STRING` (required): Oracle connection string (strongly recommended to use a read-only DB user)
- `ORACLE_COMMAND_TIMEOUT_SECONDS` (optional, default `30`)
- `ORACLE_DEFAULT_MAX_ROWS` (optional, default `200`)
- `ORACLE_MAX_MAX_ROWS` (optional, default `2000`)

Driver requirement:
- The server dynamically loads `Oracle.ManagedDataAccess` at runtime. Ensure the Oracle .NET driver is available in the runtime environment.

The MCP server is built as a self-contained application and does not require the .NET runtime to be installed on the target machine.
However, since it is self-contained, it must be built for each target platform separately.
By default, the template is configured to build for:
* `win-x64`
* `win-arm64`
* `osx-arm64`
* `linux-x64`
* `linux-arm64`
* `linux-musl-x64`

If your users require more platforms to be supported, update the list of runtime identifiers in the project's `<RuntimeIdentifiers />` element.

See [aka.ms/nuget/mcp/guide](https://aka.ms/nuget/mcp/guide) for the full guide.

Please note that this template is currently in an early preview stage. If you have feedback, please take a [brief survey](http://aka.ms/dotnet-mcp-template-survey).

## Checklist before publishing to NuGet.org

- Test the MCP server locally using the steps below.
- Update the package metadata in the .csproj file, in particular the `<PackageId>`.
- Update `.mcp/server.json` to declare your MCP server's inputs.
  - See [configuring inputs](https://aka.ms/nuget/mcp/guide/configuring-inputs) for more details.
- Pack the project using `dotnet pack`.

The `bin/Release` directory will contain the package file (.nupkg), which can be [published to NuGet.org](https://learn.microsoft.com/nuget/nuget-org/publish-a-package).

## Developing locally

To test this MCP server from source code (locally) without using a built MCP server package, you can run it as an HTTP server.

By default it listens on `http://0.0.0.0:3001`. You can override the binding URL via `MCP_HTTP_URL`, e.g. `http://localhost:3001`.

Run:

```bash
MCP_HTTP_URL=http://localhost:3001 dotnet run --project Oracle-MCP/Oracle-MCP.csproj
```

Then configure an MCP client (example: Codex CLI):

```bash
codex --enable rmcp_client mcp add oracle --url http://localhost:3001
```

## Testing the MCP Server

Once configured, you can ask Copilot Chat to ping or query the database, for example:
- `Ping the Oracle database`
- `Search schema for keyword USER`
- `Run: select * from SOME_TABLE where ID = :id with id=123`

## Publishing to NuGet.org

1. Run `dotnet pack -c Release` to create the NuGet package
2. Publish to NuGet.org with `dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json`

## Using the MCP Server from NuGet.org

Once the MCP server package is published to NuGet.org, you can configure it in your preferred IDE. Both VS Code and Visual Studio use the `dnx` command to download and install the MCP server package from NuGet.org.

- **VS Code**: Create a `<WORKSPACE DIRECTORY>/.vscode/mcp.json` file
- **Visual Studio**: Create a `<SOLUTION DIRECTORY>\.mcp.json` file

For both VS Code and Visual Studio, the configuration file uses the following server definition (note: ensure your deployment exposes an HTTP URL reachable by the client):

```json
{
  "servers": {
    "Oracle-MCP": {
      "type": "http",
      "url": "http://localhost:3001"
    }
  }
}
```

## More information

.NET MCP servers use the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) C# SDK. For more information about MCP:

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)

Refer to the VS Code or Visual Studio documentation for more information on configuring and using MCP servers:

- [Use MCP servers in VS Code (Preview)](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio (Preview)](https://learn.microsoft.com/visualstudio/ide/mcp-servers)
