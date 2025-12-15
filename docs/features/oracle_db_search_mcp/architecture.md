# Oracle DB Searching MCP - 架構設計

## 1. 專案上下文
- 語言/框架：C# / .NET 8（Console App）
- 通訊協定：MCP（Model Context Protocol）/ stdio transport
- 架構模式：以「Tools（MCP 對外介面）」呼叫「Service（商業邏輯/資料存取）」；以「Models（資料模型）」承載輸入/輸出
- 命名慣例：PascalCase 類別/方法；檔案名稱與類別名稱一致；工具方法以 `oracle_*` 命名（對外）
- 重要限制：
  - stdout 需保留給 MCP 訊息；Log 走 stderr（專案既有設定）
  - 本功能以「查詢/搜尋」為主，預設禁止任何寫入/DDL/PLSQL

## 2. 功能概述
提供一個可供 agent 使用的 Oracle DB 查詢型 MCP server，包含：
- `oracle_ping`：驗證連線設定與資料庫可用性（選擇性回傳版本/識別資訊）
- `oracle_query`：執行唯讀 SQL（SELECT / WITH…SELECT），支援 bind 參數，並限制最大回傳列數
- `oracle_search_schema`：依關鍵字搜尋資料字典（表/欄位），協助 agent 快速定位可能的資料來源

## 3. 資料模型

### 3.1 列舉/常數
- `OracleMcpErrorCodes`（字串常數，便於 agent 穩定比對）
  - `missing_connection_string`
  - `missing_oracle_driver`
  - `non_readonly_sql`
  - `query_failed`

### 3.2 核心實體
- `OracleConnectionOptions`
  - `connectionString`: string（來源：環境變數 `ORACLE_CONNECTION_STRING`）
  - `commandTimeoutSeconds`: int（來源：環境變數 `ORACLE_COMMAND_TIMEOUT_SECONDS`，預設 30）
  - `defaultMaxRows`: int（來源：環境變數 `ORACLE_DEFAULT_MAX_ROWS`，預設 200）
  - `maxMaxRows`: int（來源：環境變數 `ORACLE_MAX_MAX_ROWS`，預設 2000；用於上限保護）

- `OraclePingResult`
  - `ok`: bool
  - `databaseInfo`: string?（可選，例如版本、service name；取不到則為空）

- `OracleColumnInfo`
  - `name`: string
  - `dbType`: string?（資料庫回報的型別字串）

- `OracleQueryResult`
  - `columns`: OracleColumnInfo[]
  - `rows`: object[]（每列以「欄位名 → 值」的 map/JSON object 表示）
  - `truncated`: bool（是否因 max_rows 被截斷/限制）

- `OracleSchemaHit`
  - `owner`: string
  - `tableName`: string
  - `columnName`: string?（表層級命中時為空）
  - `dataType`: string?（欄位命中時可填）
  - `matchType`: string（`table` / `column`）

- `OracleSchemaSearchResult`
  - `hits`: OracleSchemaHit[]

- `OracleToolError`
  - `code`: OracleMcpErrorCode
  - `message`: string（對 agent 友善，可直接採取行動）
  - `details`: string?（可選，提供除錯摘要；不得包含連線字串/密碼）

> 回傳格式建議：工具方法以「成功回傳 result；失敗拋出可被 MCP SDK 轉譯的例外」或「以統一結果物件封裝 ok/error」二擇一；本專案採用「統一結果物件」以利 agent 一致解析。

## 4. 服務介面

### 4.1 介面（語言無關描述）
- `Ping(options) -> OraclePingResult 或 OracleToolError`
  - 規則：
    - 必須先檢查 `ORACLE_CONNECTION_STRING` 是否存在
    - 必須確認 Oracle driver 可被載入
    - 以最小成本執行簡單查詢（例如 `select 1 from dual`）驗證可連線

- `Query(options, sql, parameters?, maxRows?) -> OracleQueryResult 或 OracleToolError`
  - 規則：
    - 僅允許唯讀 SQL：開頭必須是 `SELECT` 或 `WITH`（忽略註解/空白）
    - 若偵測到 DML/DDL/PLSQL 相關關鍵字，必須拒絕（回 `non_readonly_sql`）
    - `maxRows` 需套用上限保護（<= `maxMaxRows`），缺省使用 `defaultMaxRows`
    - bind 參數以 key/value map 傳入，透過 driver 參數化避免字串拼接
    - 結果需提供欄位資訊與列資料；資料型別需能安全序列化為 JSON（必要時轉字串/base64）

- `SearchSchema(options, keyword, owner?, maxHits?) -> OracleSchemaSearchResult 或 OracleToolError`
  - 規則：
    - `keyword` 不可為空；過短可直接回空或回錯（本專案採回空 hits）
    - 以 Oracle 資料字典 view 進行搜尋（例如 `ALL_TABLES`、`ALL_TAB_COLUMNS`）
    - 需限制回傳數量（例如預設 50，上限 200）

## 5. 架構決策
- 唯讀保護：以 SQL 字串檢查與工具層限制，降低 agent 誤執行寫入的風險（仍建議 DB 使用唯讀帳號）
- 連線設定以環境變數輸入：避免把敏感資訊寫入 repo；也符合 MCP server 配置的常見模式
- driver 載入策略：採「反射/動態載入」避免在此模板專案中硬綁 Oracle 套件，降低建置/發佈時的外部依賴；若環境已提供 `Oracle.ManagedDataAccess` 即可運作
- 結果序列化：以 JSON 友善型別為主（數字/字串/布林/ISO-8601 時間/base64），避免 MCP client 解析困難

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
以 `Oracle-MCP/` 專案目錄為根（同層已有 `Program.cs`）：
```
Oracle-MCP/
├── Models/
│   ├── OracleMcpErrorCodes.cs
│   ├── OracleToolError.cs
│   ├── OracleConnectionOptions.cs
│   ├── OraclePingResult.cs
│   ├── OracleQueryResult.cs
│   └── OracleSchemaSearchResult.cs
├── Services/
│   ├── IOracleDbService.cs
│   └── OracleDbService.cs
├── Tools/
│   └── OracleDbTools.cs
├── Program.cs
└── .mcp/server.json（宣告環境變數輸入）
```
