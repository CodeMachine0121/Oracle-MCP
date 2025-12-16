# Oracle DB Searching MCP - 架構設計

## 1. 專案上下文
- 語言/框架：C# / .NET 8（ASP.NET Core Web App）
- 通訊協定：MCP（Model Context Protocol）/ HTTP transport
- 架構模式：Clean Architecture / Onion Architecture
  - **Tools Layer**：MCP 對外介面（使用 [McpServerTool] 屬性）
  - **Services Layer**：商業邏輯協調與編排
  - **Repositories Layer**：資料存取抽象（DatabaseInfo, SchemaSearch, DataMapper）
  - **Factories Layer**：物件建立（DbConnection）
  - **Utilities Layer**：橫切關注點（SQL Guard, Parameter Binding, Error Formatting）
  - **Models Layer**：資料模型（Requests, Results, Infrastructure）
- 命名慣例：PascalCase 類別/方法；檔案名稱與類別名稱一致；工具方法以 `oracle_*` 命名（對外）
- 依賴注入：使用 ASP.NET Core DI 容器管理所有服務生命週期
- 重要限制：
  - Log 走 stderr（專案既有設定）
  - 本功能以「查詢/搜尋」為主，預設禁止任何寫入/DDL/PLSQL

## 2. 功能概述
提供一個可供 agent 使用的 Oracle DB 查詢型 MCP server，包含：
- `oracle_ping`：驗證連線設定與資料庫可用性（選擇性回傳版本/識別資訊）
- `oracle_query`：執行唯讀 SQL（SELECT / WITH…SELECT），支援 bind 參數，並限制最大回傳列數
- `oracle_search_schema`：依關鍵字搜尋資料字典（表/欄位），協助 agent 快速定位可能的資料來源

## 3. 資料模型

### 3.1 Infrastructure Models（基礎設施模型）
- `OracleToolResponse<T>`（泛型回應封裝）
  - `ok`: bool（操作是否成功）
  - `result`: T?（成功時的結果）
  - `error`: OracleToolError?（失敗時的錯誤資訊）
  - 靜態方法：`Success(T result)`, `Fail(OracleToolError error)`

- `OracleToolError`
  - `message`: string（對 agent 友善的錯誤訊息）
  - `details`: string?（可選，除錯摘要；不包含敏感資訊）

- `OracleConnectionOptions`
  - `ConnectionString`: string（來源：環境變數 `ORACLE_CONNECTION_STRING`）
  - `CommandTimeoutSeconds`: int（來源：環境變數 `ORACLE_COMMAND_TIMEOUT_SECONDS`，預設 30）
  - `DefaultMaxRows`: int（來源：環境變數 `ORACLE_DEFAULT_MAX_ROWS`，預設 200）
  - `MaxMaxRows`: int（來源：環境變數 `ORACLE_MAX_MAX_ROWS`，預設 2000）
  - 靜態方法：`FromEnvironment()` → `OracleToolResponse<OracleConnectionOptions>`

### 3.2 Request Models（請求模型）
- `OraclePingRequest`（目前為空，保留擴充性）

- `OracleQueryRequest`
  - `Sql`: string（唯讀 SQL 語句）
  - `Parameters`: Dictionary<string, object?>?（bind 參數）
  - `MaxRows`: int?（最大回傳列數）

- `OracleSchemaSearchRequest`
  - `Keyword`: string（搜尋關鍵字）
  - `Owner`: string?（可選的 schema/owner 過濾）
  - `MaxHits`: int?（最大命中數）

### 3.3 Result Models（結果模型）
- `OraclePingResult`
  - `Ok`: bool（連線是否成功）
  - `DatabaseInfo`: string?（資料庫資訊，例如 DB_NAME）

- `OracleColumnInfo`
  - `Name`: string（欄位名稱）
  - `DbType`: string?（資料庫型別字串）

- `OracleQueryResult`
  - `Columns`: OracleColumnInfo[]（欄位資訊）
  - `Rows`: IReadOnlyDictionary<string, object?>[]（資料列）
  - `Truncated`: bool（是否因 max_rows 限制而截斷）

- `OracleSchemaHit`
  - `Owner`: string（schema 擁有者）
  - `TableName`: string（表名稱）
  - `ColumnName`: string?（欄位名稱，表層級搜尋時為 null）
  - `DataType`: string?（欄位型別，表層級搜尋時為 null）
  - `MatchType`: string（`"table"` 或 `"column"`）

- `OracleSchemaSearchResult`
  - `Hits`: OracleSchemaHit[]（搜尋結果）

## 4. 分層架構設計

### 4.1 Tools Layer（MCP 介面層）
**職責**：暴露 MCP 工具給 agent，處理參數轉換
- **OracleDbTools**
  - `OraclePing()` → `OracleToolResponse<OraclePingResult>`
  - `OracleQuery(sql, parameters?, max_rows?)` → `OracleToolResponse<OracleQueryResult>`
  - `OracleSearchSchema(keyword, owner?, max_hits?)` → `OracleToolResponse<OracleSchemaSearchResult>`
  - 依賴：`IOracleDbService`

### 4.2 Services Layer（服務協調層）
**職責**：編排業務邏輯，協調多個 repositories 和 factories

#### 4.2.1 IOracleDbService
- `PingAsync(cancellationToken)` → `OracleToolResponse<OraclePingResult>`
  - 檢查環境變數與 driver
  - 委派 `IOracleDbConnectionFactory` 建立連線
  - 委派 `IOracleDatabaseInfoRepository` 取得 DB 資訊

- `QueryAsync(request, cancellationToken)` → `OracleToolResponse<OracleQueryResult>`
  - 檢查環境變數
  - 使用 `OracleSqlGuard` 驗證 SQL 唯讀性
  - 委派 `IOracleDbConnectionFactory` 建立連線
  - 使用 `OracleCommandParameterBinder` 綁定參數
  - 委派 `IOracleDataMapper` 映射資料

- `SearchSchemaAsync(request, cancellationToken)` → `OracleToolResponse<OracleSchemaSearchResult>`
  - 檢查環境變數與關鍵字
  - 委派 `IOracleDbConnectionFactory` 建立連線
  - 委派 `IOracleSchemaSearcher` 搜尋 schema

#### 4.2.2 IOracleDataMapper
- `ReadColumns(reader)` → `IReadOnlyList<OracleColumnInfo>`
- `ReadRow(reader, columns)` → `IReadOnlyDictionary<string, object?>`
  - 將 Oracle 型別轉為 JSON-safe 型別（DateTime → ISO-8601, byte[] → base64）

### 4.3 Repositories Layer（資料存取層）
**職責**：封裝資料庫查詢邏輯

#### 4.3.1 IOracleDatabaseInfoRepository
- `TryGetDatabaseInfoAsync(connection, options, cancellationToken)` → `string?`
  - 查詢 `sys_context('userenv','db_name')`

#### 4.3.2 IOracleSchemaSearcher
- `ReadSchemaTableHitsAsync(connection, options, keyword, owner, maxHits, hits, cancellationToken)`
  - 查詢 `ALL_TABLES`，以 `table_name LIKE '%keyword%'` 搜尋
- `ReadSchemaColumnHitsAsync(connection, options, keyword, owner, maxHits, hits, cancellationToken)`
  - 查詢 `ALL_TAB_COLUMNS`，以 `column_name LIKE '%keyword%'` 或 `table_name LIKE '%keyword%'` 搜尋

### 4.4 Factories Layer（工廠層）
**職責**：建立與驗證資料庫連線物件

#### 4.4.1 IOracleDbConnectionFactory
- `Create(options)` → `OracleToolResponse<DbConnection>`
  - 動態載入 `Oracle.ManagedDataAccess.Client.OracleConnection`
  - 使用反射建立連線物件（避免硬綁定 Oracle 套件）

### 4.5 Utilities Layer（工具層）
**職責**：提供橫切關注點功能

#### 4.5.1 OracleSqlGuard（靜態類別）
- `IsReadonlySql(sql, out reason)` → `bool`
  - 驗證 SQL 必須以 `SELECT` 或 `WITH` 開頭
  - 偵測危險關鍵字（INSERT/UPDATE/DELETE/DROP/ALTER/...）
  - 偵測 `FOR UPDATE` 子句
  - 移除註解與字串常值後進行檢查
- `WrapWithRowNumLimit(sql)` → `string`
  - 包裝為 `select * from (原SQL) where rownum <= :mcp_max_rows`
- `ClampMaxRows(value, max)` → `int`
  - 限制 max_rows 範圍為 1 ~ max

#### 4.5.2 OracleCommandParameterBinder（靜態類別）
- `AddParameter(command, name, value)`
  - 建立 `DbParameter` 並加入 command
  - 處理 `:` 前綴與 null 值

#### 4.5.3 OracleErrorFormatter（靜態類別）
- `SanitizeExceptionMessage(ex)` → `string`
  - 清理例外訊息，避免洩漏連線字串

## 5. 架構決策

### 5.1 Clean Architecture 原則
- **依賴方向**：外層依賴內層（Tools → Services → Repositories/Factories）
- **關注點分離**：每層各司其職
  - Tools：MCP 介面轉換
  - Services：業務邏輯編排
  - Repositories：資料存取
  - Factories：物件建立
  - Utilities：橫切關注點
- **可測試性**：所有層都使用介面，便於單元測試與 mock

### 5.2 技術決策
- **HTTP Transport**：使用 ASP.NET Core Web App + HTTP transport（而非 stdio），支援標準 web hosting
- **依賴注入**：使用 ASP.NET Core 內建 DI 容器管理所有服務生命週期
- **唯讀保護**：多層防護
  - SQL Guard 驗證語法（拒絕 DML/DDL/PL/SQL）
  - 建議使用唯讀資料庫帳號
- **環境變數配置**：避免敏感資訊寫入 repo，符合 MCP server 配置慣例
- **動態 Driver 載入**：使用反射載入 `Oracle.ManagedDataAccess`，避免硬綁定，降低建置依賴
- **JSON-Safe 序列化**：`OracleDataMapper` 將所有型別轉為 JSON 友善格式
  - DateTime → ISO-8601 字串
  - byte[] → Base64 字串
  - 其他型別 → 適當的 primitive 或字串
- **統一錯誤處理**：`OracleToolResponse<T>` 封裝成功/失敗，agent 可一致解析
- **安全性**：`OracleErrorFormatter` 清理錯誤訊息，避免洩漏連線字串

## 6. 情境對應
| 情境 | 模型 | 方法 |
| --- | --- | --- |
| 成功連線並檢查資料庫可用性 | OraclePingResult | Ping |
| 未設定連線字串時回報可理解的錯誤 | OracleToolError(missing_connection_string) | Ping |
| 未安裝/無法載入 Oracle driver 時回報可理解的錯誤 | OracleToolError(missing_oracle_driver) | Ping |
| 執行 SELECT 查詢並以 max_rows 限制結果 | OracleQueryResult | Query |
| 支援 bind 參數避免字串拼接 | OracleQueryResult | Query |
| 拒絕非唯讀 SQL（DML/DDL/PLSQL） | OracleToolError(non_readonly_sql) | Query |
| schema 搜尋可找到符合關鍵字的表與欄位 | OracleSchemaSearchResult | SearchSchema |
| SQL 語法錯誤時回報可理解的錯誤 | OracleToolError(query_failed) | Query |

## 7. 檔案結構規劃
以 `Oracle-MCP/Oracle-MCP/` 為專案根目錄：
```
Oracle-MCP/Oracle-MCP/
├── Models/                                    # 資料模型層
│   ├── OracleConnectionOptions.cs            # 環境變數配置模型
│   ├── OracleToolResponse.cs                 # 統一回應封裝
│   ├── OracleToolError.cs                    # 錯誤模型
│   ├── OraclePingRequest.cs                  # Ping 請求
│   ├── OraclePingResult.cs                   # Ping 結果
│   ├── OracleQueryRequest.cs                 # Query 請求
│   ├── OracleQueryResult.cs                  # Query 結果
│   ├── OracleSchemaSearchRequest.cs          # Schema 搜尋請求
│   └── OracleSchemaSearchResult.cs           # Schema 搜尋結果
│
├── Tools/                                     # MCP 介面層
│   └── OracleDbTools.cs                      # MCP 工具定義 (oracle_ping/query/search_schema)
│
├── Services/                                  # 服務層
│   ├── Interfaces/
│   │   ├── IOracleDbService.cs               # 主服務介面
│   │   └── IOracleDataMapper.cs              # 資料映射介面
│   ├── OracleDbService.cs                    # 主服務實作（編排邏輯）
│   └── OracleDataMapper.cs                   # 資料映射實作（型別轉換）
│
├── Repositories/                              # 資料存取層
│   ├── Interfaces/
│   │   ├── IOracleDatabaseInfoRepository.cs  # DB 資訊查詢介面
│   │   └── IOracleSchemaSearcher.cs          # Schema 搜尋介面
│   ├── OracleDatabaseInfoRepository.cs       # DB 資訊查詢實作
│   └── OracleSchemaSearchRepository.cs       # Schema 搜尋實作
│
├── Factories/                                 # 工廠層
│   ├── Interfaces/
│   │   └── IOracleDbConnectionFactory.cs     # 連線工廠介面
│   └── OracleDbConnectionFactory.cs          # 連線工廠實作（動態載入 driver）
│
├── Utilities/                                 # 工具層
│   ├── OracleSqlGuard.cs                     # SQL 唯讀驗證
│   ├── OracleCommandParameterBinder.cs       # 參數綁定
│   └── OracleErrorFormatter.cs               # 錯誤訊息清理
│
├── Program.cs                                 # ASP.NET Core 啟動程式（DI 註冊 + MCP 配置）
├── Oracle-MCP.csproj                          # 專案檔
└── .mcp/server.json                           # MCP server 設定（環境變數宣告）
```

### 7.1 namespace 規劃
- `Oracle_MCP.Models` - 所有資料模型
- `Oracle_MCP.Tools` - MCP 工具
- `Oracle_MCP.Services` - 服務層（包含介面）
- `Oracle` - Repositories, Factories, Utilities（共用 namespace，便於內部引用）
